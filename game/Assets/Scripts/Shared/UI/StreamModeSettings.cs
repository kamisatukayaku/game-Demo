using System;

using UnityEngine;

using UnityEngine.UI;



using Game.Shared.Combat;

using Game.Shared.Core;



namespace Game.Shared.UI

{

  /// <summary>A18: 可选 Stream 模式 — 高对比 palette、大号伤害字、连杀横幅增强。</summary>

  [DisallowMultipleComponent]

  public sealed class StreamModeSettings : MonoBehaviour

  {

    const string PrefKey = "roguelike_stream_mode_v1";

    const int SortOrder = 860;



    static StreamModeSettings s_instance;



    CanvasGroup _badgeGroup;

    Text _badgeText;



    public static bool Enabled { get; private set; }



    public static float DamageFontScale => Enabled ? 1.55f : 1f;

    public static float KillStreakTitleScale => Enabled ? 1.38f : 1f;

    public static float KillStreakCounterScale => Enabled ? 1.32f : 1f;



    public static event Action Changed;



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_StreamModeSettings");

      DontDestroyOnLoad(go);

      go.AddComponent<StreamModeSettings>();

    }



    public static void SetEnabled(bool enabled)

    {

      EnsureExists();

      if (Enabled == enabled)

        return;



      Enabled = enabled;

      PlayerPrefs.SetInt(PrefKey, Enabled ? 1 : 0);

      PlayerPrefs.Save();

      s_instance.RefreshBadge();

      Changed?.Invoke();

    }



    public static void Toggle() => SetEnabled(!Enabled);



    public static Color HighContrastDamageColor(DamageNumberStyle style) =>

      style switch

      {

        DamageNumberStyle.Crit => new Color(1f, 0.92f, 0.08f, 1f),

        DamageNumberStyle.Eco => new Color(0.12f, 1f, 0.55f, 1f),

        DamageNumberStyle.Tech => new Color(0.15f, 0.95f, 1f, 1f),

        DamageNumberStyle.Player => new Color(1f, 0.38f, 0.18f, 1f),

        _ => Color.white

      };



    public static Color BoostColor(Color color)

    {

      if (!Enabled)

        return color;



      var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));

      if (max < 0.01f)

        return color;



      var scale = 1f / max;

      return new Color(

        Mathf.Clamp01(color.r * scale),

        Mathf.Clamp01(color.g * scale),

        Mathf.Clamp01(color.b * scale),

        Mathf.Clamp(color.a, 0.85f, 1f));

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

      Enabled = PlayerPrefs.GetInt(PrefKey, 0) == 1;

      BuildBadge();

      RefreshBadge();

    }



    void OnDestroy()

    {

      if (s_instance == this)

        s_instance = null;

    }



    void Update()

    {

      if (Input.GetKeyDown(KeyCode.F8))

        Toggle();

    }



    void BuildBadge()

    {

      var canvasGo = new GameObject("StreamModeBadgeCanvas");

      canvasGo.transform.SetParent(transform, false);

      var canvas = canvasGo.AddComponent<Canvas>();

      var scaler = canvasGo.AddComponent<CanvasScaler>();

      canvasGo.AddComponent<GraphicRaycaster>();

      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);



      var rtGo = new GameObject("Badge", typeof(RectTransform));

      rtGo.transform.SetParent(canvasGo.transform, false);

      var rt = rtGo.GetComponent<RectTransform>();

      rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);

      rt.pivot = new Vector2(0f, 1f);

      rt.anchoredPosition = new Vector2(16f, -16f);

      rt.sizeDelta = new Vector2(168f, 34f);



      var bg = rtGo.AddComponent<Image>();

      bg.color = new Color(0.04f, 0.06f, 0.1f, 0.82f);

      bg.raycastTarget = false;



      _badgeGroup = rtGo.AddComponent<CanvasGroup>();

      _badgeText = CreateLabel(rt, "Label", "STREAM", 15, FontStyle.Bold);

      _badgeText.alignment = TextAnchor.MiddleCenter;

      _badgeText.color = new Color(1f, 0.88f, 0.22f, 1f);

      _badgeText.rectTransform.anchorMin = Vector2.zero;

      _badgeText.rectTransform.anchorMax = Vector2.one;

      _badgeText.rectTransform.offsetMin = Vector2.zero;

      _badgeText.rectTransform.offsetMax = Vector2.zero;

    }



    void RefreshBadge()

    {

      if (_badgeGroup == null)

        return;



      _badgeGroup.alpha = Enabled ? 1f : 0f;

      if (_badgeText != null)

        _badgeText.text = Enabled ? "STREAM 模式" : "STREAM";

    }



    static Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style)

    {

      var go = new GameObject(name, typeof(RectTransform));

      go.transform.SetParent(parent, false);

      var label = go.AddComponent<Text>();

      label.text = text;

      label.font = UiFontHelper.GetFont();

      label.fontSize = size;

      label.fontStyle = style;

      label.raycastTarget = false;

      return label;

    }

  }

}


