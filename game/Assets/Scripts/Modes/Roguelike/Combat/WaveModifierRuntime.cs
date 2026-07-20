using UnityEngine;

using UnityEngine.UI;



using Game.Shared.Core;



namespace Game.Modes.Roguelike.Combat

{

  /// <summary>A10/A11: 波次 Modifier 运行时 — 暗夜视野缩小、狂潮刷怪/经验加成视觉提示。</summary>

  [DisallowMultipleComponent]

  public sealed class WaveModifierRuntime : MonoBehaviour

  {

    const int SortOrder = 280;

    const float NightVisionRadius = 0.18f;
    const int MaskRevision = 2;



    public const float FrenzySpawnIntervalMultiplier = 0.55f;

    public const float FrenzyXpMultiplier = 1.5f;



    static WaveModifierRuntime s_instance;

    static Sprite s_maskSprite;



    Image _nightOverlay;

    Image _frenzyOverlay;

    CanvasGroup _nightGroup;

    CanvasGroup _frenzyGroup;

    float _nightTargetAlpha;

    float _frenzyPulse;



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_WaveModifierRuntime");

      go.AddComponent<WaveModifierRuntime>();

    }



    static bool s_bossRushNight;
    static bool s_bossRushFrenzy;

    public static void SetBossRushPresentation(bool night, bool frenzy)
    {
      s_bossRushNight = night;
      s_bossRushFrenzy = frenzy;
      if (s_instance == null)
        return;

      s_instance.SetNightActive(night);
      s_instance.SetFrenzyActive(frenzy);
    }

    public static float GetXpMultiplier()

    {

      if (!IsFrenzyActive())

        return 1f;

      return FrenzyXpMultiplier;

    }



    static bool IsFrenzyActive()

    {

      var dir = WaveDirector.Instance;

      if (dir == null || dir.CurrentModifierId != "frenzy")

        return false;



      var phase = dir.CurrentPhase;

      return phase == WaveDirector.Phase.WaveCountdown || phase == WaveDirector.Phase.WaveActive;

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

      BuildOverlay();

      WaveDirector.PhaseChanged += OnPhaseChanged;

    }



    void OnDestroy()

    {

      WaveDirector.PhaseChanged -= OnPhaseChanged;

      if (s_instance == this)

        s_instance = null;

    }



    void OnPhaseChanged(WaveDirector.Phase phase, int wave)

    {

      if (phase == WaveDirector.Phase.WaveCountdown || phase == WaveDirector.Phase.WaveActive)

      {

        var dir = WaveDirector.Instance;

        SetNightActive(dir != null && dir.CurrentModifierId == "night");

        SetFrenzyActive(dir != null && dir.CurrentModifierId == "frenzy");

      }

      else

      {

        SetNightActive(false);

        SetFrenzyActive(false);

      }

    }



    void Update()
    {
      if (Game.Shared.Runtime.GameSessionConfig.IsBossRush)
      {
        SetNightActive(s_bossRushNight);
        SetFrenzyActive(s_bossRushFrenzy);
      }

      if (_nightGroup != null)

        _nightGroup.alpha = Mathf.Lerp(_nightGroup.alpha, _nightTargetAlpha, Time.unscaledDeltaTime * 6f);



      if (_frenzyGroup == null || !_frenzyOverlay.enabled)

        return;



      _frenzyPulse += Time.unscaledDeltaTime * 2.4f;

      var pulse = 0.22f + Mathf.Sin(_frenzyPulse * Mathf.PI * 2f) * 0.08f;

      _frenzyGroup.alpha = pulse;

    }



    void SetNightActive(bool active)

    {

      _nightTargetAlpha = active ? 1f : 0f;

      if (_nightOverlay != null)

        _nightOverlay.enabled = active;

    }



    void SetFrenzyActive(bool active)

    {

      if (_frenzyOverlay == null)

        return;



      _frenzyOverlay.enabled = active;

      if (!active && _frenzyGroup != null)

        _frenzyGroup.alpha = 0f;

    }



    void BuildOverlay()

    {

      var canvasGo = new GameObject("WaveModifierCanvas");

      canvasGo.transform.SetParent(transform, false);

      var canvas = canvasGo.AddComponent<Canvas>();

      var scaler = canvasGo.AddComponent<CanvasScaler>();

      canvasGo.AddComponent<GraphicRaycaster>();

      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);



      BuildNightOverlay(canvasGo.transform);

      BuildFrenzyOverlay(canvasGo.transform);

    }



    void BuildNightOverlay(Transform parent)

    {

      var rtGo = new GameObject("NightOverlay", typeof(RectTransform));

      rtGo.transform.SetParent(parent, false);

      var rt = rtGo.GetComponent<RectTransform>();

      rt.anchorMin = Vector2.zero;

      rt.anchorMax = Vector2.one;

      rt.offsetMin = Vector2.zero;

      rt.offsetMax = Vector2.zero;



      _nightOverlay = rtGo.AddComponent<Image>();

      _nightOverlay.sprite = EnsureMaskSprite();

      _nightOverlay.raycastTarget = false;

      _nightOverlay.enabled = false;

      _nightGroup = rtGo.AddComponent<CanvasGroup>();

      _nightGroup.alpha = 0f;

      _nightGroup.blocksRaycasts = false;

    }



    void BuildFrenzyOverlay(Transform parent)

    {

      var rtGo = new GameObject("FrenzyOverlay", typeof(RectTransform));

      rtGo.transform.SetParent(parent, false);

      var rt = rtGo.GetComponent<RectTransform>();

      rt.anchorMin = Vector2.zero;

      rt.anchorMax = Vector2.one;

      rt.offsetMin = Vector2.zero;

      rt.offsetMax = Vector2.zero;



      _frenzyOverlay = rtGo.AddComponent<Image>();

      _frenzyOverlay.sprite = EnsureFrenzyEdgeSprite();

      _frenzyOverlay.raycastTarget = false;

      _frenzyOverlay.enabled = false;

      _frenzyGroup = rtGo.AddComponent<CanvasGroup>();

      _frenzyGroup.alpha = 0f;

      _frenzyGroup.blocksRaycasts = false;

    }



    static Sprite s_frenzySprite;



    static Sprite EnsureFrenzyEdgeSprite()

    {

      if (s_frenzySprite != null)

        return s_frenzySprite;



      const int size = 128;

      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)

      {

        filterMode = FilterMode.Bilinear,

        wrapMode = TextureWrapMode.Clamp

      };

      var center = new Vector2(size * 0.5f, size * 0.5f);

      var inner = size * 0.32f;

      var outer = size * 0.5f;

      for (var y = 0; y < size; y++)

      {

        for (var x = 0; x < size; x++)

        {

          var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

          var t = Mathf.Clamp01((dist - inner) / (outer - inner));

          var alpha = Mathf.SmoothStep(0f, 1f, t) * (1f - Mathf.SmoothStep(0.72f, 1f, t));

          tex.SetPixel(x, y, new Color(1f, 0.35f, 0.12f, alpha * 0.55f));

        }

      }

      tex.Apply();

      s_frenzySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);

      return s_frenzySprite;

    }



    static int s_maskRevisionApplied = -1;



    static Sprite EnsureMaskSprite()

    {

      if (s_maskSprite != null && s_maskRevisionApplied == MaskRevision)

        return s_maskSprite;



      s_maskSprite = null;

      s_maskRevisionApplied = MaskRevision;



      const int size = 128;

      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)

      {

        filterMode = FilterMode.Bilinear,

        wrapMode = TextureWrapMode.Clamp

      };

      var center = new Vector2(size * 0.5f, size * 0.5f);

      var clearRadius = size * NightVisionRadius;

      var fadeEnd = size * 0.42f;

      for (var y = 0; y < size; y++)

      {

        for (var x = 0; x < size; x++)

        {

          var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

          var t = Mathf.Clamp01((dist - clearRadius) / Mathf.Max(1f, fadeEnd - clearRadius));

          var alpha = t * t;

          tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));

        }

      }

      tex.Apply();

      s_maskSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);

      return s_maskSprite;

    }

  }

}


