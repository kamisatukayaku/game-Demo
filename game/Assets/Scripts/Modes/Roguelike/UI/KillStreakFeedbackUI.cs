using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Combat;
using Game.Shared.Core;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  [DisallowMultipleComponent]
  public sealed class KillStreakFeedbackUI : MonoBehaviour
  {
    const int SortOrder = 325;

    static KillStreakFeedbackUI s_instance;
    static Sprite s_discSprite;
    static Sprite s_ringSprite;
    static Sprite s_vignetteSprite;

    /// <summary>S1: 连杀称号里程碑 — 10 / 25 / 50 / 100，每档仅在本连杀段首次达成时触发。</summary>
    [SerializeField] float streakTimeout = 3f;
    [SerializeField] StreakTier[] tiers =
    {
      new(10, "十连斩", 1, new Color(0.38f, 0.86f, 1f, 1f)),
      new(25, "廿五连诛", 2, new Color(0.65f, 0.48f, 1f, 1f)),
      new(50, "五十连屠", 3, new Color(1f, 0.28f, 0.18f, 1f)),
      new(100, "百斩封神", 4, new Color(1f, 0.95f, 0.48f, 1f))
    };

    const int BaseAnnounceFontSize = 56;
    const int BaseCounterFontSize = 28;

    CanvasGroup _announceGroup;
    RectTransform _announceRoot;
    RectTransform _burstRoot;
    RectTransform _counterRoot;
    Text _announceText;
    Text _countText;
    Image[] _rings;
    Image[] _fragments;
    EventListenerHandle _enemyKilledHandle;
    Image _edgeFlashImage;
    Coroutine _announceRoutine;
    Coroutine _counterRoutine;
    Coroutine _edgeFlashRoutine;
    int _streak;
    int _lastTierIndex = -1;
    float _timer;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_KillStreakFeedbackUI");
      go.AddComponent<KillStreakFeedbackUI>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildUI();
    }

    void OnEnable()
    {
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
      StreamModeSettings.Changed += ApplyStreamVisuals;
      ApplyStreamVisuals();
    }

    void OnDisable()
    {
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      StreamModeSettings.Changed -= ApplyStreamVisuals;
    }

    void ApplyStreamVisuals()
    {
      if (_announceText != null)
        _announceText.fontSize = Mathf.RoundToInt(BaseAnnounceFontSize * StreamModeSettings.KillStreakTitleScale);
      if (_countText != null)
        _countText.fontSize = Mathf.RoundToInt(BaseCounterFontSize * StreamModeSettings.KillStreakCounterScale);
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      if (_streak <= 0)
        return;

      _timer -= Time.deltaTime;
      if (_timer <= 0f)
        ResetStreak();
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (!CircleArenaController.IsActive || !IsPlayerAttribution(evt.Killer))
        return;

      _streak++;
      _timer = streakTimeout;
      RefreshCounter(true);

      var tierIndex = ResolveTierIndex(_streak);
      if (tierIndex >= 0 && tierIndex > _lastTierIndex)
      {
        _lastTierIndex = tierIndex;
        PlayAnnouncement(tiers[tierIndex]);
      }
    }

    void PlayAnnouncement(StreakTier tier)
    {
      if (_announceRoutine != null)
        StopCoroutine(_announceRoutine);

      _announceRoutine = StartCoroutine(AnnouncementRoutine(tier));
      PlayEdgeFlash(tier);
      CircleArenaController.PlayKillStreakPulse(Mathf.InverseLerp(1f, 4f, tier.intensity));
    }

    void PlayEdgeFlash(StreakTier tier)
    {
      if (_edgeFlashImage == null)
        return;

      if (_edgeFlashRoutine != null)
        StopCoroutine(_edgeFlashRoutine);

      _edgeFlashRoutine = StartCoroutine(EdgeFlashRoutine(tier));
    }

    IEnumerator EdgeFlashRoutine(StreakTier tier)
    {
      _edgeFlashImage.enabled = true;
      var peakAlpha = Mathf.Lerp(0.42f, 0.78f, tier.intensity / 4f);
      if (StreamModeSettings.Enabled)
        peakAlpha = Mathf.Min(1f, peakAlpha * 1.25f);
      const float duration = 0.38f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var envelope = t < 0.22f
          ? t / 0.22f
          : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 1f, t));
        _edgeFlashImage.color = WithAlpha(StreamModeSettings.BoostColor(tier.color), peakAlpha * envelope);
        yield return null;
      }

      _edgeFlashImage.enabled = false;
      _edgeFlashRoutine = null;
    }

    IEnumerator AnnouncementRoutine(StreakTier tier)
    {
      _announceText.text = tier.label;
      _announceText.color = StreamModeSettings.BoostColor(tier.color);
      _announceGroup.alpha = 0f;
      _announceRoot.localScale = Vector3.one * 0.5f;

      for (var i = 0; i < _rings.Length; i++)
      {
        _rings[i].enabled = true;
        _rings[i].color = WithAlpha(tier.color, 0f);
      }

      SpawnFragments(tier);

      const float duration = 0.92f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var maxPunch = StreamModeSettings.Enabled ? 1.48f : 1.3f;
        var punch = t < 0.42f
          ? Mathf.Lerp(0.5f, maxPunch, EaseOutBack(t / 0.42f))
          : Mathf.Lerp(maxPunch, 1f, 1f - Mathf.Pow(1f - Mathf.InverseLerp(0.42f, 0.7f, t), 2f));
        _announceRoot.localScale = Vector3.one * punch;
        _announceGroup.alpha = t < 0.24f ? t / 0.24f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));

        UpdateBurst(t, tier);
        yield return null;
      }

      _announceGroup.alpha = 0f;
      foreach (var ring in _rings)
        ring.enabled = false;
      foreach (var fragment in _fragments)
        fragment.enabled = false;
      _announceRoutine = null;
    }

    void UpdateBurst(float t, StreakTier tier)
    {
      for (var i = 0; i < _rings.Length; i++)
      {
        var local = Mathf.Clamp01(t * 1.25f - i * 0.12f);
        var size = Mathf.Lerp(80f, 360f + tier.intensity * 24f, 1f - Mathf.Pow(1f - local, 2.2f));
        var rt = (RectTransform)_rings[i].transform;
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.Euler(0f, 0f, t * (60f + tier.intensity * 18f) * (i % 2 == 0 ? 1f : -1f));
        var alpha = Mathf.Sin(local * Mathf.PI) * Mathf.Lerp(0.18f, 0.5f, tier.intensity / 4f);
        _rings[i].color = WithAlpha(i == 1 ? Color.white : tier.color, alpha);
      }

      var fragmentAlpha = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
      foreach (var fragment in _fragments)
      {
        if (!fragment.enabled)
          continue;

        var rt = (RectTransform)fragment.transform;
        var dir3 = rt.localRotation * Vector3.right;
        var dir = new Vector2(dir3.x, dir3.y);
        rt.anchoredPosition = dir * Mathf.Lerp(24f, 150f + tier.intensity * 14f, t);
        fragment.color = WithAlpha(fragment.color, fragmentAlpha * 0.45f);
      }
    }

    void SpawnFragments(StreakTier tier)
    {
      var count = Mathf.Clamp(4 + tier.intensity * 2, 6, _fragments.Length);
      for (var i = 0; i < _fragments.Length; i++)
      {
        var fragment = _fragments[i];
        var rt = (RectTransform)fragment.transform;
        var active = i < count;
        fragment.enabled = active;
        if (!active)
          continue;

        var angle = i * 360f / count + Random.Range(-10f, 10f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.one * Random.Range(6f, 14f);
        fragment.color = WithAlpha(Color.Lerp(tier.color, Color.white, Random.Range(0.1f, 0.55f)), 0.5f);
      }
    }

    void RefreshCounter(bool bump)
    {
      _counterRoot.gameObject.SetActive(_streak > 0);
      _countText.text = $"⚔ {_streak}";
      if (!bump)
        return;

      if (_counterRoutine != null)
        StopCoroutine(_counterRoutine);
      _counterRoutine = StartCoroutine(CounterBumpRoutine());
    }

    IEnumerator CounterBumpRoutine()
    {
      var elapsed = 0f;
      const float duration = 0.18f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
        _counterRoot.localScale = Vector3.one * scale;
        yield return null;
      }
      _counterRoot.localScale = Vector3.one;
      _counterRoutine = null;
    }

    void ResetStreak()
    {
      _streak = 0;
      _timer = 0f;
      _lastTierIndex = -1;
      RefreshCounter(false);
    }

    int ResolveTierIndex(int count)
    {
      var index = -1;
      for (var i = 0; i < tiers.Length; i++)
        if (count >= tiers[i].kills)
          index = i;
      return index;
    }

    void BuildUI()
    {
      EnsureSprites();

      var canvasGo = new GameObject("KillStreakCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      canvasGo.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      var edgeFlashRt = CreateRect(canvasGo.transform, "EdgeFlash", Vector2.zero, Vector2.zero, Vector2.zero);
      edgeFlashRt.anchorMin = Vector2.zero;
      edgeFlashRt.anchorMax = Vector2.one;
      edgeFlashRt.offsetMin = Vector2.zero;
      edgeFlashRt.offsetMax = Vector2.zero;
      _edgeFlashImage = edgeFlashRt.gameObject.AddComponent<Image>();
      _edgeFlashImage.sprite = EnsureVignetteSprite();
      _edgeFlashImage.raycastTarget = false;
      _edgeFlashImage.enabled = false;

      _announceRoot = CreateRect(canvasGo.transform, "Announcement", new Vector2(0.5f, 0.5f), new Vector2(0f, 174f), new Vector2(780f, 220f));
      _announceGroup = _announceRoot.gameObject.AddComponent<CanvasGroup>();
      _announceGroup.alpha = 0f;
      _burstRoot = CreateRect(_announceRoot, "Burst", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

      _rings = new Image[3];
      for (var i = 0; i < _rings.Length; i++)
      {
        var ringRt = CreateRect(_burstRoot, $"BurstRing_{i + 1}", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = i == 2 ? s_discSprite : s_ringSprite;
        ring.raycastTarget = false;
        ring.enabled = false;
        _rings[i] = ring;
      }

      _fragments = new Image[24];
      for (var i = 0; i < _fragments.Length; i++)
      {
        var rt = CreateRect(_burstRoot, $"Fragment_{i + 1}", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = s_discSprite;
        image.raycastTarget = false;
        image.enabled = false;
        _fragments[i] = image;
      }

      _announceText = CreateText(_announceRoot, "AnnouncementText", string.Empty, BaseAnnounceFontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
      _announceText.rectTransform.anchorMin = new Vector2(0f, 0f);
      _announceText.rectTransform.anchorMax = new Vector2(1f, 1f);
      _announceText.rectTransform.offsetMin = Vector2.zero;
      _announceText.rectTransform.offsetMax = Vector2.zero;

      _counterRoot = CreateRect(canvasGo.transform, "StreakCounter", new Vector2(1f, 0.5f), new Vector2(-92f, 82f), new Vector2(170f, 56f));
      var bg = _counterRoot.gameObject.AddComponent<Image>();
      bg.sprite = s_discSprite;
      bg.color = new Color(0.03f, 0.07f, 0.1f, 0.48f);
      bg.raycastTarget = false;

      _countText = CreateText(_counterRoot, "Count", "⚔ 0", BaseCounterFontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
      _countText.color = new Color(0.78f, 0.96f, 1f, 1f);
      _countText.rectTransform.anchorMin = Vector2.zero;
      _countText.rectTransform.anchorMax = Vector2.one;
      _countText.rectTransform.offsetMin = Vector2.zero;
      _countText.rectTransform.offsetMax = Vector2.zero;
      _counterRoot.gameObject.SetActive(false);
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchor;
      rt.anchorMax = anchor;
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = position;
      rt.sizeDelta = size;
      return rt;
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, TextAnchor align)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var label = go.AddComponent<Text>();
      label.text = text;
      label.alignment = align;
      label.raycastTarget = false;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }

    static bool IsPlayerAttribution(GameObject killer) =>
      PlayerCombatAttribution.IsPlayerOrOwned(killer);

    static float EaseOutBack(float t)
    {
      const float c1 = 1.70158f;
      const float c3 = c1 + 1f;
      return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static void EnsureSprites()
    {
      if (s_discSprite != null && s_ringSprite != null)
        return;

      s_discSprite = CreateDiscSprite(64, 0f);
      s_ringSprite = CreateDiscSprite(96, 0.68f);
    }

    static Sprite EnsureVignetteSprite()
    {
      if (s_vignetteSprite != null)
        return s_vignetteSprite;

      const int width = 256;
      const int height = 144;
      var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };
      var pixels = new Color[width * height];
      for (var y = 0; y < height; y++)
      {
        for (var x = 0; x < width; x++)
        {
          var nx = x / (width - 1f);
          var ny = y / (height - 1f);
          var edgeX = Mathf.Min(nx, 1f - nx) * 2f;
          var edgeY = Mathf.Min(ny, 1f - ny) * 2f;
          var edge = Mathf.Clamp01(Mathf.Min(edgeX, edgeY));
          var alpha = 1f - Mathf.SmoothStep(0f, 0.42f, edge);
          pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
        }
      }

      tex.SetPixels(pixels);
      tex.Apply();
      s_vignetteSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
      return s_vignetteSprite;
    }

    static Sprite CreateDiscSprite(int resolution, float innerCutout)
    {
      var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };
      var pixels = new Color[resolution * resolution];
      var center = new Vector2(resolution * 0.5f, resolution * 0.5f);
      var outer = resolution * 0.5f - 1f;
      var inner = outer * innerCutout;
      for (var y = 0; y < resolution; y++)
      {
        for (var x = 0; x < resolution; x++)
        {
          var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
          pixels[y * resolution + x] = dist <= outer && dist >= inner ? Color.white : Color.clear;
        }
      }
      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }

    [System.Serializable]
    public struct StreakTier
    {
      public int kills;
      public string label;
      public int intensity;
      public Color color;

      public StreakTier(int kills, string label, int intensity, Color color)
      {
        this.kills = kills;
        this.label = label;
        this.intensity = intensity;
        this.color = color;
      }
    }
  }
}
