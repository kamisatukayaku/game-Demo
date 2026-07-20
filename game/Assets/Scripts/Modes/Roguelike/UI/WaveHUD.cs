using UnityEngine.UI;
using UnityEngine;

using Game.Modes.Roguelike.Combat;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>
  /// 波次状态 HUD。显示当前波次、阶段、倒计时、剩余怪物数。
  /// S3: 波次开场 3-2-1 大字倒计时 + 升调音效。
  /// </summary>
  public class WaveHUD : MonoBehaviour
  {
    [Header("Debug")]
    [SerializeField] bool debugLog;

    Canvas _canvas;
    Text _waveText;
    Text _phaseText;
    Text _enemyCountText;
    GameObject _panel;
    GameObject _modifierPanel;
    Text _modifierTitle;
    Text _modifierDesc;
    Image _modifierAccent;
    CanvasGroup _countdownGroup;
    Text _countdownText;
    AudioSource _countdownAudio;
    int _lastCountdownStep = -1;
    static AudioClip s_countdownBeep;

    static WaveHUD s_instance;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_WaveHUD");
      go.AddComponent<WaveHUD>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      CreateUI();
      _countdownAudio = gameObject.AddComponent<AudioSource>();
      _countdownAudio.playOnAwake = false;
      _countdownAudio.spatialBlend = 0f;
      _countdownAudio.volume = 0.72f;
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
    }

    void Update()
    {
      var director = WaveDirector.Instance;
      if (director == null) return;

      _waveText.text = $"第 {director.CurrentWave} / {director.TotalWaves} 波";
      UpdateModifierCard(director);

      switch (director.CurrentPhase)
      {
        case WaveDirector.Phase.PreGame:
          _phaseText.text = "准备中...";
          _enemyCountText.text = "";
          HideCountdown();
          break;

        case WaveDirector.Phase.BuildPhase:
          var remaining = director.IsBossPrepActive
            ? director.BossPrepRemaining
            : director.BuildPhaseRemaining;
          var isMiniBossPrep = director.IsBossPrepActive && director.IsMiniBossWave(director.CurrentWave);
          _phaseText.text = isMiniBossPrep
            ? $"小 Boss 整备 | {remaining:F0} 秒"
            : director.IsBossPrepActive
              ? $"Boss 整备 | {remaining:F0} 秒"
              : $"整备中 | {remaining:F0} 秒";
          _phaseText.color = director.IsBossPrepActive
            ? isMiniBossPrep
              ? new Color(0.85f, 0.55f, 1f, 1f)
              : new Color(1f, 0.72f, 0.28f, 1f)
            : new Color(0.4f, 1f, 0.5f, 1f);
          if (CircleArenaController.ShouldShowShrinkPreview(director.CurrentWave))
          {
            var upcoming = CircleArenaController.GetUpcomingShrinkWave(director.CurrentWave);
            var nextR = CircleArenaController.PreviewNextRadius(upcoming);
            _enemyCountText.text = $"缩圈预告 第 {upcoming} 波 → 半径 {nextR:F0}";
          }
          else
            _enemyCountText.text = "拾取场地经验球";
          HideCountdown();
          break;

        case WaveDirector.Phase.WaveCountdown:
          _phaseText.text = "即将进攻";
          _enemyCountText.text = "";
          UpdateWaveCountdown(director);
          break;

        case WaveDirector.Phase.WaveActive:
          _phaseText.text = "怪物进攻中！";
          _phaseText.color = new Color(1f, 0.4f, 0.35f, 1f);
          if (HuntContractRuntime.IsTracking)
          {
            _enemyCountText.text =
              $"猎杀契约 {HuntContractRuntime.CurrentKills}/{HuntContractRuntime.KillTarget} | {HuntContractRuntime.TimeRemaining:F0}s";
            _enemyCountText.color = new Color(1f, 0.82f, 0.35f, 1f);
          }
          else
          {
            _enemyCountText.text = $"剩余: {director.EnemiesRemaining}";
            _enemyCountText.color = Color.white;
          }
          HideCountdown();
          break;

        case WaveDirector.Phase.AllWavesComplete:
          _phaseText.text = "胜利";
          _phaseText.color = new Color(1f, 0.9f, 0.3f, 1f);
          _enemyCountText.text = "所有波次已完成";
          HideCountdown();
          break;
      }
    }

    void UpdateModifierCard(WaveDirector director)
    {
      if (_modifierPanel == null)
        return;

      var show = director.CurrentPhase == WaveDirector.Phase.WaveCountdown
        || director.CurrentPhase == WaveDirector.Phase.WaveActive;
      _modifierPanel.SetActive(show);
      if (!show)
        return;

      var modifier = director.CurrentModifier;
      if (modifier == null)
        return;

      var accent = WaveModifierDatabase.ParseColor(modifier.color, new Color(0.55f, 0.78f, 1f, 1f));
      _modifierTitle.text = modifier.display_name;
      _modifierDesc.text = modifier.description;
      _modifierAccent.color = accent;
      _modifierTitle.color = Color.Lerp(accent, Color.white, 0.25f);
    }

    void UpdateWaveCountdown(WaveDirector director)
    {
      var step = director.WaveCountdownDisplay;
      var wave = director.CurrentWave;
      var warn = wave >= 12;
      var caution = !warn && wave >= 8;

      _countdownGroup.alpha = 1f;
      _countdownText.text = step.ToString();
      _countdownText.color = warn
        ? new Color(1f, 0.22f, 0.18f, 1f)
        : caution
          ? new Color(1f, 0.72f, 0.18f, 1f)
          : new Color(0.55f, 0.92f, 1f, 1f);
      _phaseText.color = _countdownText.color;

      if (step != _lastCountdownStep)
      {
        _lastCountdownStep = step;
        PlayCountdownBeep(step);
      }
    }

    void HideCountdown()
    {
      _lastCountdownStep = -1;
      if (_countdownGroup != null)
        _countdownGroup.alpha = 0f;
    }

    void PlayCountdownBeep(int step)
    {
      if (_countdownAudio == null)
        return;

      _countdownAudio.pitch = 0.82f + (4 - step) * 0.18f;
      _countdownAudio.PlayOneShot(EnsureCountdownBeep(), 0.65f);
    }

    static AudioClip EnsureCountdownBeep()
    {
      if (s_countdownBeep != null)
        return s_countdownBeep;

      const int sampleRate = 44100;
      const float duration = 0.12f;
      var count = Mathf.CeilToInt(sampleRate * duration);
      var samples = new float[count];
      for (var i = 0; i < count; i++)
      {
        var t = i / (float)sampleRate;
        var env = Mathf.Exp(-t * 22f);
        samples[i] = Mathf.Sin(t * Mathf.PI * 2f * 880f) * env * 0.55f;
      }

      s_countdownBeep = AudioClip.Create("WaveCountdownBeep", count, 1, sampleRate, false);
      s_countdownBeep.SetData(samples, 0);
      return s_countdownBeep;
    }

    void CreateUI()
    {
      var canvasGo = new GameObject("WaveHUDCanvas");
      canvasGo.transform.SetParent(transform);
      _canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 310);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = new GameObject("WavePanel");
      _panel.transform.SetParent(canvasGo.transform, false);
      var panelRt = _panel.AddComponent<RectTransform>();
      panelRt.anchorMin = new Vector2(0.5f, 1f);
      panelRt.anchorMax = new Vector2(0.5f, 1f);
      panelRt.pivot = new Vector2(0.5f, 1f);
      panelRt.anchoredPosition = new Vector2(0, -5);
      panelRt.sizeDelta = new Vector2(300, 70);

      var panelBg = _panel.AddComponent<Image>();
      panelBg.color = new Color(0.05f, 0.05f, 0.08f, 0.7f);

      var vert = _panel.AddComponent<VerticalLayoutGroup>();
      vert.spacing = 2;
      vert.padding = new RectOffset(10, 10, 6, 6);
      vert.childAlignment = TextAnchor.MiddleCenter;
      vert.childControlWidth = true;
      vert.childControlHeight = false;

      _waveText = CreateText("WaveText", "第 1 / 20 波", 20, Color.white);
      _waveText.transform.SetParent(_panel.transform, false);

      _phaseText = CreateText("PhaseText", "准备中...", 16, new Color(0.8f, 0.8f, 0.8f, 1f));
      _phaseText.transform.SetParent(_panel.transform, false);

      _enemyCountText = CreateText("EnemyCount", "", 13, new Color(0.6f, 0.6f, 0.6f, 1f));
      _enemyCountText.transform.SetParent(_panel.transform, false);


      _modifierPanel = new GameObject("ModifierCard");
      _modifierPanel.transform.SetParent(canvasGo.transform, false);
      var modRt = _modifierPanel.AddComponent<RectTransform>();
      modRt.anchorMin = new Vector2(1f, 1f);
      modRt.anchorMax = new Vector2(1f, 1f);
      modRt.pivot = new Vector2(1f, 1f);
      modRt.anchoredPosition = new Vector2(-16f, -88f);
      modRt.sizeDelta = new Vector2(220f, 72f);
      var modBg = _modifierPanel.AddComponent<Image>();
      modBg.color = new Color(0.04f, 0.06f, 0.09f, 0.82f);
      modBg.raycastTarget = false;

      var accentGo = new GameObject("Accent", typeof(RectTransform));
      accentGo.transform.SetParent(_modifierPanel.transform, false);
      var accentRt = accentGo.GetComponent<RectTransform>();
      accentRt.anchorMin = new Vector2(0f, 1f);
      accentRt.anchorMax = new Vector2(1f, 1f);
      accentRt.pivot = new Vector2(0.5f, 1f);
      accentRt.anchoredPosition = Vector2.zero;
      accentRt.sizeDelta = new Vector2(0f, 4f);
      _modifierAccent = accentGo.AddComponent<Image>();
      _modifierAccent.raycastTarget = false;

      _modifierTitle = CreateText("ModifierTitle", "标准波", 16, Color.white);
      _modifierTitle.alignment = TextAnchor.MiddleLeft;
      _modifierTitle.transform.SetParent(_modifierPanel.transform, false);
      var titleRt = _modifierTitle.rectTransform;
      titleRt.anchorMin = new Vector2(0f, 1f);
      titleRt.anchorMax = new Vector2(1f, 1f);
      titleRt.pivot = new Vector2(0f, 1f);
      titleRt.anchoredPosition = new Vector2(10f, -10f);
      titleRt.sizeDelta = new Vector2(-16f, 22f);

      _modifierDesc = CreateText("ModifierDesc", "", 11, new Color(0.7f, 0.78f, 0.86f, 0.92f));
      _modifierDesc.alignment = TextAnchor.UpperLeft;
      _modifierDesc.transform.SetParent(_modifierPanel.transform, false);
      var descRt = _modifierDesc.rectTransform;
      descRt.anchorMin = new Vector2(0f, 0f);
      descRt.anchorMax = new Vector2(1f, 1f);
      descRt.offsetMin = new Vector2(10f, 8f);
      descRt.offsetMax = new Vector2(-8f, -32f);
      _modifierPanel.SetActive(false);

      var countdownGo = new GameObject("WaveCountdown", typeof(RectTransform));
      countdownGo.transform.SetParent(canvasGo.transform, false);
      var countdownRt = countdownGo.GetComponent<RectTransform>();
      countdownRt.anchorMin = new Vector2(0.5f, 0.5f);
      countdownRt.anchorMax = new Vector2(0.5f, 0.5f);
      countdownRt.pivot = new Vector2(0.5f, 0.5f);
      countdownRt.anchoredPosition = new Vector2(0f, 48f);
      countdownRt.sizeDelta = new Vector2(240f, 240f);
      _countdownGroup = countdownGo.AddComponent<CanvasGroup>();
      _countdownGroup.alpha = 0f;
      _countdownText = CreateText("CountdownNumber", "3", 120, Color.white);
      _countdownText.transform.SetParent(countdownGo.transform, false);
      var numberRt = _countdownText.rectTransform;
      numberRt.anchorMin = Vector2.zero;
      numberRt.anchorMax = Vector2.one;
      numberRt.offsetMin = Vector2.zero;
      numberRt.offsetMax = Vector2.zero;
      _countdownText.fontStyle = FontStyle.Bold;
    }

    Text CreateText(string name, string text, int fontSize, Color color)
    {
      var go = new GameObject(name);
      var rt = go.AddComponent<RectTransform>();
      rt.sizeDelta = new Vector2(280, fontSize + 6);

      var label = go.AddComponent<Text>();
      label.alignment = TextAnchor.MiddleCenter;
      label.color = color;
      label.text = text;
      UiFontHelper.StyleText(label, fontSize);

      return label;
    }
  }
}
