using UnityEngine.UI;
using System.Collections;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using UnityEngine;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Modes.Roguelike.Gameplay.Player;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Player;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>
  /// 玩家 HUD：左上角爱心生命 + 等级/经验?
  /// </summary>
  public class PlayerHUD : MonoBehaviour
  {
    [Header("Layout")]
    [SerializeField] float marginLeft = 20f;
    [SerializeField] float marginTop = 20f;
    [SerializeField] float heartSize = 34f;
    [SerializeField] float heartSpacing = 6f;
    [SerializeField] int heartsPerRow = 10;
    [SerializeField] float hpPerHeart = 10f;
    [SerializeField] float xpBarWidth = 280f;
    [SerializeField] float xpBarHeight = 12f;
    [SerializeField] float xpFontSize = 18f;
    [SerializeField] float sectionGap = 10f;
    [SerializeField] float dashBarWidth = 170f;
    [SerializeField] float dashBarHeight = 10f;

    [Header("Colors")]
    [SerializeField] Color heartFullColor = new(0.95f, 0.22f, 0.32f, 1f);
    [SerializeField] Color heartPartialColor = new(0.95f, 0.55f, 0.58f, 1f);
    [SerializeField] Color heartEmptyColor = new(0.35f, 0.38f, 0.42f, 0.55f);
    [SerializeField] Color xpBarBgColor = new(0.08f, 0.1f, 0.14f, 0.75f);
    [SerializeField] Color xpBarFillColor = new(0.35f, 0.82f, 0.98f, 0.95f);
    [SerializeField] Color xpColor = new(0.75f, 0.92f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] bool debugLog;

    static PlayerHUD s_instance;
    public static PlayerHUD Instance => s_instance;
    public static bool Exists => s_instance != null;

    RectTransform _heartsRoot;
    Text[] _heartLabels;
    int _heartCount;
    Text _xpText;
    RectTransform _xpFillRt;
    Image _xpFillImage;
    Text _dashText;
    RectTransform _dashFillRt;
    Image _dashFillImage;
    Image _dashBgImage;
    RectTransform _overloadFillRt;
    Image _overloadFillImage;
    Image _overloadBgImage;
    Text _overloadText;
    Text _synergyText;
    Image _synergyPulseImage;
    float _xpFillTarget;
    float _xpFillDisplayed;
    Health _playerHealth;
    PlayerAutoAttack _playerAttack;
    float _lastMaxHp = -1f;
    Coroutine _synergyRoutine;
    float _synergyPulse;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_PlayerHUD");
      DontDestroyOnLoad(go);
      go.AddComponent<PlayerHUD>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      CreateCanvas();
      BuildHeartsDisplay();
      BuildXpDisplay();
      BuildDashDisplay();
      BuildOverloadDisplay();
      BuildSynergyDisplay();

      if (debugLog) Debug.Log("[PlayerHUD] Created.");

      ExperienceSystem.XpChanged += OnXpChanged;
      RunBuildState.SynergyTriggered += OnSynergyTriggered;
    }

    void Start() => FindPlayerReferences();

    void Update()
    {
      if (_playerHealth == null) { FindPlayerReferences(); return; }
      UpdateHearts();
      UpdateXpBarAnimated();
      UpdateDashDisplay();
      UpdateOverloadDisplay();
      TickSynergyPulse();
    }

    void OnDestroy()
    {
      ExperienceSystem.XpChanged -= OnXpChanged;
      RunBuildState.SynergyTriggered -= OnSynergyTriggered;
      if (s_instance == this) s_instance = null;
    }

    void CreateCanvas()
    {
      var go = new GameObject("PlayerHUDCanvas");
      go.transform.SetParent(transform);

      var canvas = go.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 300;

      var scaler = go.AddComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920, 1080);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;

      go.AddComponent<GraphicRaycaster>();
    }

    void BuildHeartsDisplay()
    {
      var rootGo = new GameObject("HeartsRoot", typeof(RectTransform));
      rootGo.transform.SetParent(transform.GetChild(0), false);
      _heartsRoot = rootGo.GetComponent<RectTransform>();
      _heartsRoot.anchorMin = new Vector2(0f, 1f);
      _heartsRoot.anchorMax = new Vector2(0f, 1f);
      _heartsRoot.pivot = new Vector2(0f, 1f);
      _heartsRoot.anchoredPosition = new Vector2(marginLeft, -marginTop);
      _heartsRoot.sizeDelta = new Vector2(600f, heartSize + 4f);
    }

    void EnsureHeartPool(int count)
    {
      count = Mathf.Max(1, count);
      if (_heartLabels != null && _heartCount == count)
        return;

      if (_heartLabels != null)
      {
        foreach (var label in _heartLabels)
        {
          if (label != null)
            Destroy(label.gameObject);
        }
      }

      _heartCount = count;
      _heartLabels = new Text[count];
      var font = UiFontHelper.GetFont();
      var rowWidth = heartsPerRow * (heartSize + heartSpacing);

      for (int i = 0; i < count; i++)
      {
        var row = i / heartsPerRow;
        var col = i % heartsPerRow;

        var heartGo = new GameObject($"Heart_{i}", typeof(RectTransform));
        heartGo.transform.SetParent(_heartsRoot, false);
        var rt = heartGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(col * (heartSize + heartSpacing), -row * (heartSize + heartSpacing));
        rt.sizeDelta = new Vector2(heartSize, heartSize);

        var text = heartGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = Mathf.RoundToInt(heartSize * 0.82f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "";
        text.raycastTarget = false;
        _heartLabels[i] = text;
      }

      var rows = Mathf.CeilToInt(count / heartsPerRow);
      _heartsRoot.sizeDelta = new Vector2(rowWidth, rows * (heartSize + heartSpacing));
    }

    void UpdateHearts()
    {
      var maxHp = _playerHealth.MaxHp;
      var currentHp = _playerHealth.CurrentHp;
      var heartCount = Mathf.Max(1, Mathf.CeilToInt(maxHp / hpPerHeart));
      EnsureHeartPool(heartCount);

      if (Mathf.Abs(maxHp - _lastMaxHp) > 0.01f)
        _lastMaxHp = maxHp;

      for (int i = 0; i < heartCount; i++)
      {
        var threshold = (i + 1) * hpPerHeart;
        var prevThreshold = i * hpPerHeart;
        var label = _heartLabels[i];

        if (currentHp >= threshold)
        {
          label.text = "♥";
          label.color = heartFullColor;
        }
        else if (currentHp > prevThreshold + 0.01f)
        {
          label.text = "♥";
          label.color = heartPartialColor;
        }
        else
        {
          label.text = "♥";
          label.color = heartEmptyColor;
        }
      }
    }

    float HeartsBlockHeight()
    {
      if (_heartsRoot == null)
        return heartSize;

      return _heartsRoot.sizeDelta.y;
    }

    void BuildXpDisplay()
    {
      var blockTop = marginTop + HeartsBlockHeight() + sectionGap;
      var font = UiFontHelper.GetFont();

      var barBgGo = new GameObject("XpBarBG", typeof(RectTransform));
      barBgGo.transform.SetParent(transform.GetChild(0), false);
      var barBgRt = barBgGo.GetComponent<RectTransform>();
      barBgRt.anchorMin = new Vector2(0f, 1f);
      barBgRt.anchorMax = new Vector2(0f, 1f);
      barBgRt.pivot = new Vector2(0f, 1f);
      barBgRt.anchoredPosition = new Vector2(marginLeft, -blockTop);
      barBgRt.sizeDelta = new Vector2(xpBarWidth, xpBarHeight);
      var barBgImg = barBgGo.AddComponent<Image>();
      barBgImg.color = xpBarBgColor;
      barBgImg.raycastTarget = false;

      var fillGo = new GameObject("XpBarFill", typeof(RectTransform));
      fillGo.transform.SetParent(barBgGo.transform, false);
      _xpFillRt = fillGo.GetComponent<RectTransform>();
      _xpFillRt.anchorMin = new Vector2(0f, 0f);
      _xpFillRt.anchorMax = new Vector2(0f, 1f);
      _xpFillRt.pivot = new Vector2(0f, 0.5f);
      _xpFillRt.anchoredPosition = Vector2.zero;
      _xpFillRt.sizeDelta = new Vector2(0f, 0f);
      _xpFillImage = fillGo.AddComponent<Image>();
      _xpFillImage.color = xpBarFillColor;
      _xpFillImage.raycastTarget = false;

      var xpGo = new GameObject("XpText", typeof(RectTransform));
      xpGo.transform.SetParent(transform.GetChild(0), false);
      var rt = xpGo.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0f, 1f);
      rt.anchorMax = new Vector2(0f, 1f);
      rt.pivot = new Vector2(0f, 1f);
      rt.anchoredPosition = new Vector2(marginLeft, -(blockTop + xpBarHeight + 4f));
      rt.sizeDelta = new Vector2(xpBarWidth + 80f, 24f);

      _xpText = xpGo.AddComponent<Text>();
      _xpText.font = font;
      _xpText.fontSize = Mathf.RoundToInt(xpFontSize);
      _xpText.alignment = TextAnchor.MiddleLeft;
      _xpText.color = xpColor;
      _xpText.raycastTarget = false;

      _xpFillDisplayed = ExperienceSystem.GetLevelProgress01();
      _xpFillTarget = _xpFillDisplayed;
      RefreshXpText(ExperienceSystem.TotalXp, ExperienceSystem.Level);
      ApplyXpFillImmediate(_xpFillDisplayed);
    }

    void UpdateXpBarAnimated()
    {
      _xpFillTarget = ExperienceSystem.GetLevelProgress01();
      _xpFillDisplayed = Mathf.Lerp(_xpFillDisplayed, _xpFillTarget, Time.deltaTime * 10f);
      ApplyXpFillImmediate(_xpFillDisplayed);
    }

    void ApplyXpFillImmediate(float pct)
    {
      if (_xpFillRt == null)
        return;

      _xpFillRt.sizeDelta = new Vector2(xpBarWidth * Mathf.Clamp01(pct), 0f);
    }

    void OnXpChanged(int totalXp, int level)
    {
      var progress = ExperienceSystem.GetLevelProgress01();
      if (progress < _xpFillDisplayed - 0.05f)
        _xpFillDisplayed = 0f;

      _xpFillTarget = progress;
      RefreshXpText(totalXp, level);
    }

    void RefreshXpText(int totalXp, int level)
    {
      if (_xpText == null)
        return;

      var into = ExperienceSystem.XpIntoCurrentLevel();
      var need = ExperienceSystem.XpNeededForNextLevel();
      _xpText.text = $"Lv.{level}  {into}/{need} XP";
    }

    float XpBlockHeight() => xpBarHeight + 28f;

    void BuildDashDisplay()
    {
      var blockTop = marginTop + HeartsBlockHeight() + sectionGap + XpBlockHeight() + sectionGap;
      var font = UiFontHelper.GetFont();

      var labelGo = new GameObject("DashText", typeof(RectTransform));
      labelGo.transform.SetParent(transform.GetChild(0), false);
      var labelRt = labelGo.GetComponent<RectTransform>();
      labelRt.anchorMin = new Vector2(0f, 1f);
      labelRt.anchorMax = new Vector2(0f, 1f);
      labelRt.pivot = new Vector2(0f, 1f);
      labelRt.anchoredPosition = new Vector2(marginLeft, -blockTop);
      labelRt.sizeDelta = new Vector2(dashBarWidth + 80f, 22f);
      _dashText = labelGo.AddComponent<Text>();
      _dashText.font = font;
      _dashText.fontSize = 15;
      _dashText.alignment = TextAnchor.MiddleLeft;
      _dashText.color = new Color(0.82f, 0.94f, 1f, 0.9f);
      _dashText.raycastTarget = false;

      var bgGo = new GameObject("DashBarBG", typeof(RectTransform));
      bgGo.transform.SetParent(transform.GetChild(0), false);
      var bgRt = bgGo.GetComponent<RectTransform>();
      bgRt.anchorMin = new Vector2(0f, 1f);
      bgRt.anchorMax = new Vector2(0f, 1f);
      bgRt.pivot = new Vector2(0f, 1f);
      bgRt.anchoredPosition = new Vector2(marginLeft, -(blockTop + 24f));
      bgRt.sizeDelta = new Vector2(dashBarWidth, dashBarHeight);
      _dashBgImage = bgGo.AddComponent<Image>();
      _dashBgImage.color = new Color(0.06f, 0.08f, 0.1f, 0.78f);
      _dashBgImage.raycastTarget = false;

      var fillGo = new GameObject("DashBarFill", typeof(RectTransform));
      fillGo.transform.SetParent(bgGo.transform, false);
      _dashFillRt = fillGo.GetComponent<RectTransform>();
      _dashFillRt.anchorMin = new Vector2(0f, 0f);
      _dashFillRt.anchorMax = new Vector2(0f, 1f);
      _dashFillRt.pivot = new Vector2(0f, 0.5f);
      _dashFillRt.anchoredPosition = Vector2.zero;
      _dashFillRt.sizeDelta = new Vector2(dashBarWidth, 0f);
      _dashFillImage = fillGo.AddComponent<Image>();
      _dashFillImage.color = new Color(0.48f, 0.95f, 1f, 0.95f);
      _dashFillImage.raycastTarget = false;
    }

    void UpdateDashDisplay()
    {
      if (_dashText == null || _dashFillRt == null)
        return;

      var showDash = PlayerDashController.HasDashCombat
        || string.Equals(RunBuildState.WeaponTheme, UnifiedBuildBootstrap.WeaponTheme, System.StringComparison.OrdinalIgnoreCase)
        || string.Equals(RunBuildState.WeaponTheme, "warrior", System.StringComparison.OrdinalIgnoreCase);
      _dashText.gameObject.SetActive(showDash);
      _dashBgImage?.gameObject.SetActive(showDash);
      if (!showDash)
        return;

      var ready = PlayerDashController.IsReady;
      var remaining = PlayerDashController.CooldownRemaining;
      var pursuit = PlayerDashController.IsPursuitReady;
      var pctReady = pursuit ? 1f : 1f - PlayerDashController.Cooldown01;
      _dashFillRt.sizeDelta = new Vector2(dashBarWidth * Mathf.Clamp01(pctReady), 0f);

      var isDashStrike = PlayerDashController.HasDashCombat;
      if (pursuit)
        _dashText.text = isDashStrike ? "左Shift  追击斩" : "左Shift  追击";
      else
        _dashText.text = ready
          ? (isDashStrike ? "左Shift  冲刺斩" : "左Shift  冲刺")
          : (isDashStrike ? $"左Shift  冲刺斩  {remaining:0.0}秒" : $"左Shift  冲刺  {remaining:0.0}秒");
      _dashText.color = ready
        ? new Color(0.92f, 1f, 1f, 1f)
        : new Color(0.62f, 0.78f, 0.86f, 0.82f);
      if (_dashFillImage != null)
        _dashFillImage.color = ready
          ? new Color(0.72f, 1f, 0.95f, 1f)
          : new Color(0.28f, 0.62f, 0.76f, 0.78f);
      if (_dashBgImage != null)
        _dashBgImage.color = ready
          ? new Color(0.06f, 0.13f, 0.14f, 0.82f)
          : new Color(0.05f, 0.06f, 0.08f, 0.75f);
    }

    void BuildOverloadDisplay()
    {
      var blockTop = marginTop + HeartsBlockHeight() + sectionGap + XpBlockHeight() + sectionGap + 35f + sectionGap;
      var font = UiFontHelper.GetFont();

      var labelGo = new GameObject("OverloadText", typeof(RectTransform));
      labelGo.transform.SetParent(transform.GetChild(0), false);
      var labelRt = labelGo.GetComponent<RectTransform>();
      labelRt.anchorMin = new Vector2(0f, 1f);
      labelRt.anchorMax = new Vector2(0f, 1f);
      labelRt.pivot = new Vector2(0f, 1f);
      labelRt.anchoredPosition = new Vector2(marginLeft, -blockTop);
      labelRt.sizeDelta = new Vector2(dashBarWidth + 80f, 22f);
      _overloadText = labelGo.AddComponent<Text>();
      _overloadText.font = font;
      _overloadText.fontSize = 14;
      _overloadText.alignment = TextAnchor.MiddleLeft;
      _overloadText.color = new Color(1f, 0.72f, 0.28f, 0.9f);
      _overloadText.raycastTarget = false;

      var bgGo = new GameObject("OverloadBarBG", typeof(RectTransform));
      bgGo.transform.SetParent(transform.GetChild(0), false);
      var bgRt = bgGo.GetComponent<RectTransform>();
      bgRt.anchorMin = new Vector2(0f, 1f);
      bgRt.anchorMax = new Vector2(0f, 1f);
      bgRt.pivot = new Vector2(0f, 1f);
      bgRt.anchoredPosition = new Vector2(marginLeft, -(blockTop + 22f));
      bgRt.sizeDelta = new Vector2(dashBarWidth, dashBarHeight);
      _overloadBgImage = bgGo.AddComponent<Image>();
      _overloadBgImage.color = new Color(0.08f, 0.06f, 0.04f, 0.78f);
      _overloadBgImage.raycastTarget = false;

      var fillGo = new GameObject("OverloadBarFill", typeof(RectTransform));
      fillGo.transform.SetParent(bgGo.transform, false);
      _overloadFillRt = fillGo.GetComponent<RectTransform>();
      _overloadFillRt.anchorMin = new Vector2(0f, 0f);
      _overloadFillRt.anchorMax = new Vector2(0f, 1f);
      _overloadFillRt.pivot = new Vector2(0f, 0.5f);
      _overloadFillRt.anchoredPosition = Vector2.zero;
      _overloadFillRt.sizeDelta = new Vector2(0f, 0f);
      _overloadFillImage = fillGo.AddComponent<Image>();
      _overloadFillImage.color = new Color(1f, 0.62f, 0.15f, 0.95f);
      _overloadFillImage.raycastTarget = false;
    }

    void UpdateOverloadDisplay()
    {
      var show = RangedOverloadController.IsActive;
      if (_overloadText != null)
        _overloadText.gameObject.SetActive(show);
      if (_overloadBgImage != null)
        _overloadBgImage.gameObject.SetActive(show);
      if (!show || _overloadFillRt == null)
        return;

      var pct = RangedOverloadController.Overload01;
      _overloadFillRt.sizeDelta = new Vector2(dashBarWidth * Mathf.Clamp01(pct), 0f);
      _overloadText.text = pct >= 0.99f ? "过载 · 扇形爆发" : $"过载 {Mathf.RoundToInt(pct * 100f)}%";
      if (_overloadFillImage != null)
        _overloadFillImage.color = pct >= 0.99f
          ? new Color(1f, 0.85f, 0.25f, 1f)
          : new Color(1f, 0.55f, 0.12f, 0.95f);
    }

    void BuildSynergyDisplay()
    {
      var font = UiFontHelper.GetFont();
      var blockTop = marginTop + HeartsBlockHeight() + sectionGap + XpBlockHeight() + sectionGap;

      var labelGo = new GameObject("SynergyText", typeof(RectTransform));
      labelGo.transform.SetParent(transform.GetChild(0), false);
      var labelRt = labelGo.GetComponent<RectTransform>();
      labelRt.anchorMin = new Vector2(0f, 1f);
      labelRt.anchorMax = new Vector2(0f, 1f);
      labelRt.pivot = new Vector2(0f, 1f);
      labelRt.anchoredPosition = new Vector2(marginLeft + xpBarWidth + 12f, -blockTop);
      labelRt.sizeDelta = new Vector2(220f, 28f);
      _synergyText = labelGo.AddComponent<Text>();
      _synergyText.font = font;
      _synergyText.fontSize = 16;
      _synergyText.fontStyle = FontStyle.Bold;
      _synergyText.alignment = TextAnchor.MiddleLeft;
      _synergyText.color = new Color(0.55f, 1f, 0.82f, 0f);
      _synergyText.raycastTarget = false;

      var pulseGo = new GameObject("SynergyPulse", typeof(RectTransform));
      pulseGo.transform.SetParent(transform.GetChild(0), false);
      var pulseRt = pulseGo.GetComponent<RectTransform>();
      pulseRt.anchorMin = new Vector2(0f, 1f);
      pulseRt.anchorMax = new Vector2(0f, 1f);
      pulseRt.pivot = new Vector2(0f, 1f);
      pulseRt.anchoredPosition = new Vector2(marginLeft - 4f, -(blockTop - 2f));
      pulseRt.sizeDelta = new Vector2(xpBarWidth + 8f, xpBarHeight + 8f);
      _synergyPulseImage = pulseGo.AddComponent<Image>();
      _synergyPulseImage.color = new Color(0.35f, 0.95f, 0.72f, 0f);
      _synergyPulseImage.raycastTarget = false;
    }

    void OnSynergyTriggered(string tag)
    {
      if (_synergyText == null)
        return;

      if (_synergyRoutine != null)
        StopCoroutine(_synergyRoutine);

      _synergyText.text = BuildTagSynergy.FormatSynergyLabel(tag);
      _synergyPulse = 1f;
      _synergyRoutine = StartCoroutine(SynergyFadeRoutine());
    }

    IEnumerator SynergyFadeRoutine()
    {
      const float duration = 1.8f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var alpha = t < 0.12f ? t / 0.12f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
        if (_synergyText != null)
          _synergyText.color = new Color(0.55f, 1f, 0.82f, alpha);
        yield return null;
      }

      if (_synergyText != null)
        _synergyText.color = new Color(0.55f, 1f, 0.82f, 0f);
      _synergyRoutine = null;
    }

    void TickSynergyPulse()
    {
      if (_synergyPulseImage == null || _synergyPulse <= 0f)
        return;

      _synergyPulse -= Time.deltaTime * 2.4f;
      var pulse = Mathf.Clamp01(_synergyPulse);
      _synergyPulseImage.color = new Color(0.35f, 0.95f, 0.72f, pulse * 0.42f);
    }

    float _refFindTimer;
    const float RefFindInterval = 1f;

    void FindPlayerReferences()
    {
      _refFindTimer -= Time.deltaTime;
      if (_refFindTimer > 0f) return;
      _refFindTimer = RefFindInterval;

      var playerGo = GameObject.FindWithTag("Player");
      if (playerGo == null) playerGo = GameObject.Find("Player");
      if (playerGo == null) return;

      _playerHealth = playerGo.GetComponent<Health>();
      _playerAttack = playerGo.GetComponent<PlayerAutoAttack>();

      if (debugLog && _playerHealth != null)
        Debug.Log($"[PlayerHUD] Found '{playerGo.name}' HP={_playerHealth.MaxHp}");
    }
  }
}
