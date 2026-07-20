using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Combat;
using Game.Shared.Core;
using static Game.Modes.Roguelike.UI.RunTimelineRecorder;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>S2: HP&lt;15% 濒死 — 前 5s 全屏闪红，之后四周呼吸式边缘微红 + 心跳 + 音频 duck；脱离后「死里逃生」字幕。</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaMomentUI : MonoBehaviour
  {
    const int SortOrder = 324;
    const float NearDeathThreshold = 0.15f;
    const float NearDeathFlashDuration = 5f;
    const float NearDeathPhaseBlend = 0.45f;
    const float DuckVolume = 0.52f;
    const float HeartbeatInterval = 0.78f;
    static readonly Color NearDeathRed = new(0.92f, 0.08f, 0.12f, 1f);

    static ArenaMomentUI s_instance;
    static Sprite s_vignetteSprite;
    static Sprite s_edgeVignetteSprite;
    static AudioClip s_heartbeatClip;

    Image _vignetteImage;
    Image _flashImage;
    CanvasGroup _escapeGroup;
    Text _escapeText;
    AudioSource _heartbeatSource;
    Health _playerHealth;
    float _refFindTimer;
    bool _inNearDeath;
    bool _wasNearDeath;
    float _heartbeatTimer;
    float _listenerBaseVolume = 1f;
    float _vignettePulse;
    float _nearDeathElapsed;
    Coroutine _escapeRoutine;
    Coroutine _bossSubtitleRoutine;
    Coroutine _flashRoutine;
    EventListenerHandle _bossPhaseHandle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaMomentUI");
      go.AddComponent<ArenaMomentUI>();
    }

    public static void ShowBanner(string text, Color color)
    {
      EnsureExists();
      if (s_instance == null || string.IsNullOrEmpty(text))
        return;

      if (s_instance._escapeRoutine != null)
        s_instance.StopCoroutine(s_instance._escapeRoutine);

      s_instance._escapeRoutine = s_instance.StartCoroutine(s_instance.MomentBannerRoutine(text, color));
    }

    public static void PlaySignatureFlash(Color peakColor, float duration = 0.55f)
    {
      EnsureExists();
      if (s_instance == null)
        return;

      if (s_instance._flashRoutine != null)
        s_instance.StopCoroutine(s_instance._flashRoutine);

      s_instance._flashRoutine = s_instance.StartCoroutine(s_instance.SignatureFlashRoutine(peakColor, duration));
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
      _listenerBaseVolume = AudioListener.volume;
    }

    void OnDestroy()
    {
      if (_inNearDeath)
        RestoreAudio();

      if (s_instance == this)
        s_instance = null;
    }

    void OnEnable()
    {
      _bossPhaseHandle = GameEventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
    }

    void OnDisable()
    {
      if (_bossPhaseHandle.Valid)
        GameEventBus.Unsubscribe(_bossPhaseHandle);
    }

    void OnBossPhaseChanged(BossPhaseChangedEvent evt)
    {
      if (string.IsNullOrEmpty(evt.Subtitle))
        return;

      if (_bossSubtitleRoutine != null)
        StopCoroutine(_bossSubtitleRoutine);

      _bossSubtitleRoutine = StartCoroutine(BossSubtitleRoutine(evt.Subtitle));
    }

    IEnumerator BossSubtitleRoutine(string subtitle)
    {
      PlaySignatureFlash(new Color(1f, 0.82f, 0.35f, 0.88f), 0.45f);
      CombatTimePause.PushPause();
      yield return new WaitForSecondsRealtime(0.2f);
      CombatTimePause.PopPause();

      _escapeText.text = subtitle;
      _escapeText.color = new Color(1f, 0.82f, 0.35f, 1f);
      _escapeGroup.alpha = 0f;

      const float duration = 1.1f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        _escapeGroup.alpha = t < 0.15f ? t / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
        yield return null;
      }

      _escapeGroup.alpha = 0f;
      _bossSubtitleRoutine = null;
    }

    void Update()
    {
      if (!CircleArenaController.IsActive)
        return;

      if (_playerHealth == null || _playerHealth.IsDead)
      {
        FindPlayerHealth();
        if (_playerHealth == null)
          return;
      }

      if (_playerHealth.IsDead)
      {
        ForceClearNearDeath();
        return;
      }

      var hpRatio = _playerHealth.HpPercent;
      _wasNearDeath = _inNearDeath;
      _inNearDeath = hpRatio > 0f && hpRatio < NearDeathThreshold;

      if (_inNearDeath)
      {
        EnterNearDeathIfNeeded();
        TickNearDeath();
      }
      else
      {
        if (_wasNearDeath)
          ShowEscapeBanner();

        ExitNearDeath();
      }
    }

    void FindPlayerHealth()
    {
      _refFindTimer -= Time.deltaTime;
      if (_refFindTimer > 0f)
        return;

      _refFindTimer = 0.75f;
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      _playerHealth = player != null ? player.GetComponent<Health>() : null;
    }

    void EnterNearDeathIfNeeded()
    {
      if (_wasNearDeath)
        return;

      RunTimelineRecorder.Record("濒死", "HP 跌入危险区");

      if (_flashRoutine != null)
      {
        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
      }

      _listenerBaseVolume = AudioListener.volume > 0.01f ? AudioListener.volume : 1f;
      AudioListener.volume = _listenerBaseVolume * DuckVolume;
      _heartbeatTimer = 0f;
      _nearDeathElapsed = 0f;
      _vignettePulse = 0f;
      _flashImage.enabled = true;
      _vignetteImage.enabled = false;
    }

    void TickNearDeath()
    {
      _nearDeathElapsed += Time.unscaledDeltaTime;
      _vignettePulse += Time.unscaledDeltaTime;

      var urgency = 1f - Mathf.Clamp01(_playerHealth.HpPercent / NearDeathThreshold);
      var flashWeight = 1f;
      var edgeWeight = 0f;
      if (_nearDeathElapsed >= NearDeathFlashDuration)
      {
        flashWeight = 0f;
        edgeWeight = 1f;
      }
      else if (_nearDeathElapsed >= NearDeathFlashDuration - NearDeathPhaseBlend)
      {
        var blendT = Mathf.InverseLerp(
          NearDeathFlashDuration - NearDeathPhaseBlend,
          NearDeathFlashDuration,
          _nearDeathElapsed);
        flashWeight = 1f - blendT;
        edgeWeight = blendT;
      }

      if (flashWeight > 0.001f)
      {
        var flashPulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(_vignettePulse * 3.4f)), 1.35f);
        var flashAlpha = Mathf.Lerp(0.18f, 0.58f, urgency) * flashPulse * flashWeight;
        _flashImage.enabled = true;
        _flashImage.color = WithAlpha(NearDeathRed, flashAlpha);
      }
      else
      {
        _flashImage.color = WithAlpha(NearDeathRed, 0f);
        _flashImage.enabled = false;
      }

      if (edgeWeight > 0.001f)
      {
        var edgePulse = 0.5f + Mathf.Sin(_vignettePulse * 1.85f) * 0.5f;
        var edgeAlpha = Mathf.Lerp(0.05f, 0.16f, urgency) * edgePulse * edgeWeight;
        _vignetteImage.sprite = EnsureEdgeVignetteSprite();
        _vignetteImage.enabled = true;
        _vignetteImage.color = WithAlpha(NearDeathRed, edgeAlpha);
      }
      else
      {
        _vignetteImage.enabled = false;
      }

      _heartbeatTimer -= Time.unscaledDeltaTime;
      if (_heartbeatTimer <= 0f)
      {
        _heartbeatTimer = HeartbeatInterval;
        PlayHeartbeat();
      }
    }

    void ExitNearDeath()
    {
      if (_inNearDeath)
        return;

      ForceClearNearDeath();
    }

    void ForceClearNearDeath()
    {
      _inNearDeath = false;
      _nearDeathElapsed = 0f;
      if (_vignetteImage != null)
        _vignetteImage.enabled = false;
      if (_flashImage != null)
      {
        _flashImage.color = WithAlpha(NearDeathRed, 0f);
        _flashImage.enabled = false;
      }
      RestoreAudio();
    }

    void RestoreAudio()
    {
      AudioListener.volume = _listenerBaseVolume > 0.01f ? _listenerBaseVolume : 1f;
    }

    void PlayHeartbeat()
    {
      if (_heartbeatSource == null)
        return;

      _heartbeatSource.pitch = Random.Range(0.92f, 1.02f);
      _heartbeatSource.PlayOneShot(EnsureHeartbeatClip(), 0.55f);
    }

    IEnumerator MomentBannerRoutine(string text, Color color)
    {
      PlaySignatureFlash(new Color(color.r, color.g, color.b, 0.75f), 0.42f);
      _escapeText.text = text;
      _escapeText.color = color;
      _escapeGroup.alpha = 0f;

      const float duration = 1.35f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var scale = t < 0.2f ? Mathf.Lerp(0.72f, 1.08f, t / 0.2f) : 1f;
        _escapeText.rectTransform.localScale = Vector3.one * scale;
        _escapeGroup.alpha = t < 0.18f
          ? t / 0.18f
          : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
        yield return null;
      }

      _escapeText.rectTransform.localScale = Vector3.one;
      _escapeGroup.alpha = 0f;
      _escapeRoutine = null;
    }

    IEnumerator SignatureFlashRoutine(Color peakColor, float duration)
    {
      if (_flashImage == null)
      {
        _flashRoutine = null;
        yield break;
      }

      var elapsed = 0f;
      var peak = new Color(peakColor.r, peakColor.g, peakColor.b, peakColor.a);
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = elapsed / duration;
        float alpha;
        if (t < 0.12f)
          alpha = Mathf.Lerp(0f, peak.a, t / 0.12f);
        else if (t < 0.2f)
          alpha = peak.a;
        else
          alpha = Mathf.Lerp(peak.a, 0f, (t - 0.2f) / 0.8f);

        _flashImage.color = new Color(peak.r, peak.g, peak.b, alpha);
        yield return null;
      }

      _flashImage.color = new Color(peak.r, peak.g, peak.b, 0f);
      _flashRoutine = null;
    }

    void ShowEscapeBanner()
    {
      if (IsDecisionWaveGateMet())
        TryRecordOnce(MomentKind.NearDeathEscape, "决定瞬间", "濒死存活");

      if (_escapeRoutine != null)
        StopCoroutine(_escapeRoutine);

      _escapeRoutine = StartCoroutine(EscapeBannerRoutine());
    }

    static bool IsDecisionWaveGateMet()
    {
      var director = WaveDirector.Instance;
      return director != null && director.CurrentWave >= 8;
    }

    IEnumerator EscapeBannerRoutine()
    {
      _escapeText.text = "死里逃生";
      _escapeGroup.alpha = 0f;
      _escapeText.color = new Color(0.55f, 1f, 0.82f, 1f);

      const float duration = 1.35f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        _escapeGroup.alpha = t < 0.18f
          ? t / 0.18f
          : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
        yield return null;
      }

      _escapeGroup.alpha = 0f;
      _escapeRoutine = null;
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("ArenaMomentCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      canvasGo.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      var vignetteRt = CreateStretchRect(canvasGo.transform, "NearDeathVignette");
      _vignetteImage = vignetteRt.gameObject.AddComponent<Image>();
      _vignetteImage.sprite = EnsureVignetteSprite();
      _vignetteImage.raycastTarget = false;
      _vignetteImage.enabled = false;

      var flashRt = CreateStretchRect(canvasGo.transform, "SignatureFlash");
      _flashImage = flashRt.gameObject.AddComponent<Image>();
      _flashImage.sprite = UiSolidSprite.White;
      _flashImage.raycastTarget = false;
      _flashImage.color = new Color(1f, 1f, 1f, 0f);

      var escapeRt = CreateRect(canvasGo.transform, "EscapeBanner", new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(520f, 72f));
      _escapeGroup = escapeRt.gameObject.AddComponent<CanvasGroup>();
      _escapeGroup.alpha = 0f;
      _escapeText = CreateText(escapeRt, "EscapeText", "死里逃生", 42, FontStyle.Bold, TextAnchor.MiddleCenter);
      _escapeText.rectTransform.anchorMin = Vector2.zero;
      _escapeText.rectTransform.anchorMax = Vector2.one;
      _escapeText.rectTransform.offsetMin = Vector2.zero;
      _escapeText.rectTransform.offsetMax = Vector2.zero;

      _heartbeatSource = gameObject.AddComponent<AudioSource>();
      _heartbeatSource.playOnAwake = false;
      _heartbeatSource.loop = false;
      _heartbeatSource.spatialBlend = 0f;
      _heartbeatSource.volume = 1f;
    }

    static RectTransform CreateStretchRect(Transform parent, string name)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      return rt;
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

    static Sprite EnsureVignetteSprite() => EnsureVignetteSprite(ref s_vignetteSprite, 0.38f);

    static Sprite EnsureEdgeVignetteSprite() => EnsureVignetteSprite(ref s_edgeVignetteSprite, 0.14f);

    static Sprite EnsureVignetteSprite(ref Sprite cache, float edgeFalloff)
    {
      if (cache != null)
        return cache;

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
          var alpha = 1f - Mathf.SmoothStep(0f, edgeFalloff, edge);
          pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
        }
      }

      tex.SetPixels(pixels);
      tex.Apply();
      cache = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
      return cache;
    }

    static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    static AudioClip EnsureHeartbeatClip()
    {
      if (s_heartbeatClip != null)
        return s_heartbeatClip;

      const int sampleRate = 44100;
      const float duration = 0.22f;
      var sampleCount = Mathf.CeilToInt(sampleRate * duration);
      var samples = new float[sampleCount];
      for (var i = 0; i < sampleCount; i++)
      {
        var t = i / (float)sampleRate;
        var env = Mathf.Exp(-t * 18f);
        var low = Mathf.Sin(t * Mathf.PI * 2f * 58f);
        var thump = Mathf.Sin(t * Mathf.PI * 2f * 110f) * 0.35f;
        samples[i] = (low * 0.75f + thump) * env * 0.65f;
      }

      s_heartbeatClip = AudioClip.Create("NearDeathHeartbeat", sampleCount, 1, sampleRate, false);
      s_heartbeatClip.SetData(samples, 0);
      return s_heartbeatClip;
    }
  }
}
