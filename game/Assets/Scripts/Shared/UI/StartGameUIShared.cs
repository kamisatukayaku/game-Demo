using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Shared.Core;

namespace Game.Shared.UI
{
  public enum MenuVisualStage
  {
    MainUniverse,
    ModeSelect,
    ArchetypeTemple,
    ArenaEntry,
    Exploration
  }

  public class StartGameUIShared : MonoBehaviour
  {
    public static readonly List<GameModeDescriptor> RegisteredModes = new();

    public static readonly Color PanelBg = new(0.035f, 0.07f, 0.11f, 0.88f);
    public static readonly Color Accent = new(0.3f, 0.9f, 1f, 1f);
    public static readonly Color NormalBg = new(0.075f, 0.13f, 0.18f, 0.86f);

    protected enum Screen { Main, Settings, ModeSelect, ModeContent }

    static StartGameUIShared s_instance;

    protected Screen _screen = Screen.Main;
    protected Font _font;
    protected Canvas _canvas;
    protected RectTransform _canvasRoot;
    protected RectTransform _mainPanel;
    protected RectTransform _settingsPanel;
    protected Slider _volumeSlider;

    RectTransform _modeSelectPanel;
    RectTransform _modeContentContainer;
    RectTransform _loadingOverlay;
    Image _loadingFade;
    Image _loadingCore;
    readonly List<Image> _loadingShapes = new();
    GameModeDescriptor _selectedMode;
    GeometricMenuBackground _background;
    bool _transitioning;

    enum LoadingTransitionKind { System, Gameplay }

    protected virtual void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
    }

    protected virtual void OnDestroy()
    {
      _selectedMode?.TeardownModeUI();
      _selectedMode = null;
      if (s_instance == this) s_instance = null;
    }

    protected virtual void Start() => Build();

    protected virtual void Build()
    {
      GameAudioSettings.EnsureLoaded();
      _font = UiFontHelper.GetFont();

      var canvasGo = new GameObject("StartGameCanvas");
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();
      _canvasRoot = canvasGo.GetComponent<RectTransform>();

      _background = GeometricMenuBackground.Create(_canvasRoot);
      BuildMainPanel(canvasGo.transform);
      BuildSettingsPanel(canvasGo.transform);
      BuildModeSelectPanel(canvasGo.transform);
      BuildLoadingOverlay(canvasGo.transform);
      ShowScreen(Screen.Main);
    }

    public void SetVisualStage(MenuVisualStage stage) => _background?.SetStage(stage);
    public Font GetFont() => _font;

    public void NavigateBackToModeSelect()
    {
      _selectedMode?.TeardownModeUI();
      _selectedMode = null;
      if (_modeContentContainer != null)
      {
        Destroy(_modeContentContainer.gameObject);
        _modeContentContainer = null;
      }
      ShowScreen(Screen.ModeSelect);
    }

    public void PlayGameplayLoadingTransition(Action onComplete)
    {
      if (_transitioning) return;
      StartCoroutine(LoadingTransitionRoutine(LoadingTransitionKind.Gameplay, onComplete));
    }

    protected void BuildMainPanel(Transform parent)
    {
      _mainPanel = CreatePanel(parent, "MainPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _mainPanel.offsetMin = Vector2.zero;
      _mainPanel.offsetMax = Vector2.zero;

      var title = CreateLabel(_mainPanel, "Title", "几何核心", 42, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -94f), new Vector2(520f, 58f));
      title.color = new Color(0.86f, 0.98f, 1f, 1f);
      title.alignment = TextAnchor.MiddleLeft;

      var sub = CreateLabel(_mainPanel, "Subtitle", "极简几何 Roguelike", 17, FontStyle.Normal,
        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -142f), new Vector2(420f, 28f));
      sub.color = new Color(0.58f, 0.72f, 0.8f, 1f);
      sub.alignment = TextAnchor.MiddleLeft;

      var core = CreatePanel(_mainPanel, "UniverseCoreFrame", Color.clear,
        new Vector2(0.65f, 0.5f), new Vector2(0.65f, 0.5f), new Vector2(160f, 24f), new Vector2(330f, 330f));
      GeometricCoreVisual.Attach(core.gameObject, new Color(0.22f, 0.48f, 0.62f, 1f), GeometricCoreVisual.Style.HexCore);

      CreateMainMenuButton("StartButton", "开始游戏", 4f, () => ShowScreen(Screen.ModeSelect), true);
      CreateMainMenuButton("SettingsButton", "设置", -76f, () => ShowScreen(Screen.Settings), false);
      CreateMainMenuButton("ExitButton", "退出游戏", -156f, QuitGame, false);
    }

    void CreateMainMenuButton(string name, string label, float y, UnityEngine.Events.UnityAction onClick, bool primary)
    {
      var color = primary
        ? new Color(0.08f, 0.18f, 0.24f, 0.88f)
        : new Color(0.045f, 0.08f, 0.105f, 0.72f);
      var btn = CreateButton(_mainPanel, name, label,
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, y), new Vector2(primary ? 330f : 300f, 54f),
        color, onClick);
      var text = btn.GetComponentInChildren<Text>();
      if (text != null)
      {
        text.fontSize = primary ? 20 : 18;
        text.alignment = TextAnchor.MiddleLeft;
        text.rectTransform.offsetMin = new Vector2(34f, 6f);
        text.rectTransform.offsetMax = new Vector2(-14f, -6f);
      }
      MinimalMenuButtonCue.Attach(btn.gameObject, primary ? Accent : new Color(0.62f, 0.82f, 0.92f, 1f));
    }

    protected void BuildSettingsPanel(Transform parent)
    {
      _settingsPanel = CreatePanel(parent, "SettingsPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 430f));
      GeometricPanelFrame.Attach(_settingsPanel.gameObject, new Color(0.34f, 0.9f, 1f, 1f));

      CreateLabel(_settingsPanel, "Title", "系统设置", 32, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(520f, 46f));

      var volumeLabel = CreateLabel(_settingsPanel, "VolumeLabel", "主音量", 20, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -104f), new Vector2(180f, 30f));
      volumeLabel.alignment = TextAnchor.MiddleLeft;

      var sliderRow = CreatePanel(_settingsPanel, "VolumeRow", NormalBg,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -144f), new Vector2(-96f, 44f));

      var sliderGo = new GameObject("VolumeSlider", typeof(RectTransform));
      sliderGo.transform.SetParent(sliderRow, false);
      var sliderRt = sliderGo.GetComponent<RectTransform>();
      sliderRt.anchorMin = new Vector2(0f, 0.5f);
      sliderRt.anchorMax = new Vector2(1f, 0.5f);
      sliderRt.pivot = new Vector2(0.5f, 0.5f);
      sliderRt.anchoredPosition = Vector2.zero;
      sliderRt.sizeDelta = new Vector2(-24f, 24f);

      _volumeSlider = sliderGo.AddComponent<Slider>();
      _volumeSlider.minValue = 0f;
      _volumeSlider.maxValue = 1f;
      _volumeSlider.value = GameAudioSettings.MasterVolume;
      _volumeSlider.onValueChanged.AddListener(v => GameAudioSettings.MasterVolume = v);

      var bg = CreatePanel(sliderGo.transform, "Background", new Color(0.04f, 0.08f, 0.11f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
      var fillArea = CreatePanel(sliderGo.transform, "Fill Area", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      fillArea.offsetMin = new Vector2(8f, 8f); fillArea.offsetMax = new Vector2(-8f, -8f);
      var fill = CreatePanel(fillArea, "Fill", Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      var handle = CreatePanel(sliderGo.transform, "Handle", Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
      handle.GetComponent<Image>().raycastTarget = true;
      _volumeSlider.fillRect = fill;
      _volumeSlider.handleRect = handle;
      _volumeSlider.targetGraphic = handle.GetComponent<Image>();

      CreateButton(_settingsPanel, "KeySettingsButton", "键位设置",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(240f, 48f),
        NormalBg, () => KeyBindingsUI.OpenPanel());

      CreateButton(_settingsPanel, "ResetTutorialButton", "重置新手引导",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(240f, 48f),
        NormalBg, RoguelikeTutorialResetBridge.TryResetAll);

      CreateButton(_settingsPanel, "BackButton", "返回",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(180f, 44f),
        NormalBg, () => ShowScreen(Screen.Main));
    }

    void BuildModeSelectPanel(Transform parent)
    {
      _modeSelectPanel = CreatePanel(parent, "ModeSelectPanel", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _modeSelectPanel.offsetMin = Vector2.zero;
      _modeSelectPanel.offsetMax = Vector2.zero;

      CreateLabel(_modeSelectPanel, "Title", "选择游戏模式", 38, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(760f, 54f));
      var sub = CreateLabel(_modeSelectPanel, "Subtitle", "选择本局进入的世界层级", 19, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(640f, 30f));
      sub.color = new Color(0.68f, 0.84f, 0.92f, 1f);

      if (RegisteredModes.Count == 0)
      {
        CreateLabel(_modeSelectPanel, "EmptyHint", "暂无可用模式", 18, FontStyle.Normal,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 80f));
      }
      else
      {
        var count = RegisteredModes.Count;
        var spacing = 380f;
        var startX = -(count - 1) * spacing * 0.5f;
        for (var i = 0; i < count; i++)
        {
          var mode = RegisteredModes[i];
          var pos = new Vector2(startX + i * spacing, -18f);
          var card = CreateModeCard(_modeSelectPanel, mode, pos, () => SelectMode(mode));
          StrategicMapNode.Attach(card.gameObject, mode.ThemeColor, mode.ModeId == "arena");
        }
      }

      CreateButton(_modeSelectPanel, "BackFromMode", "返回主菜单",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(210f, 46f),
        NormalBg, () => ShowScreen(Screen.Main));
    }

    Button CreateModeCard(RectTransform parent, GameModeDescriptor mode, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
      var bg = new Color(mode.ThemeColor.r * 0.28f, mode.ThemeColor.g * 0.28f, mode.ThemeColor.b * 0.28f, 0.36f);
      var btn = CreateButton(parent, $"ModeBtn_{mode.ModeId}", $"{mode.DisplayName}\n<size=15>{mode.Description}</size>",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(310f, 265f), bg, onClick);
      var label = btn.GetComponentInChildren<Text>();
      if (label != null)
      {
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        label.rectTransform.sizeDelta = new Vector2(-42f, 110f);
        label.fontSize = 20;
      }
      var hover = btn.GetComponent<EnergyButtonHover>();
      if (hover != null) Destroy(hover);
      return btn;
    }

    void SelectMode(GameModeDescriptor descriptor)
    {
      if (descriptor == null) return;
      if (_transitioning) return;
      StartCoroutine(LoadingTransitionRoutine(LoadingTransitionKind.System, () => OpenModeContent(descriptor)));
    }

    void OpenModeContent(GameModeDescriptor descriptor)
    {
      if (descriptor == null) return;
      if (_selectedMode != null && _selectedMode != descriptor)
        _selectedMode.TeardownModeUI();

      _selectedMode = descriptor;
      if (_modeContentContainer != null)
        Destroy(_modeContentContainer.gameObject);

      var containerGo = new GameObject($"ModeContent_{descriptor.ModeId}", typeof(RectTransform));
      containerGo.transform.SetParent(_canvasRoot, false);
      _modeContentContainer = containerGo.GetComponent<RectTransform>();
      _modeContentContainer.anchorMin = Vector2.zero;
      _modeContentContainer.anchorMax = Vector2.one;
      _modeContentContainer.offsetMin = Vector2.zero;
      _modeContentContainer.offsetMax = Vector2.zero;

      SetVisualStage(descriptor.ModeId == "arena" ? MenuVisualStage.ArchetypeTemple : MenuVisualStage.Exploration);
      descriptor.BuildModeUI(_modeContentContainer, this);
      ShowScreen(Screen.ModeContent);
    }

    void SelectModeById(string modeId)
    {
      foreach (var mode in RegisteredModes)
      {
        if (mode != null && mode.ModeId == modeId)
        {
          SelectMode(mode);
          return;
        }
      }

      ShowScreen(Screen.ModeSelect);
    }

    static void QuitGame()
    {
      Application.Quit();
    }

    protected virtual void ShowScreen(Screen screen)
    {
      _screen = screen;
      if (screen == Screen.Main) SetVisualStage(MenuVisualStage.MainUniverse);
      else if (screen == Screen.ModeSelect) SetVisualStage(MenuVisualStage.ModeSelect);

      if (_mainPanel != null) _mainPanel.gameObject.SetActive(screen == Screen.Main);
      if (_settingsPanel != null) _settingsPanel.gameObject.SetActive(screen == Screen.Settings);
      if (_modeSelectPanel != null) _modeSelectPanel.gameObject.SetActive(screen == Screen.ModeSelect);
      if (_modeContentContainer != null) _modeContentContainer.gameObject.SetActive(screen == Screen.ModeContent);
    }

    void BuildLoadingOverlay(Transform parent)
    {
      _loadingOverlay = CreatePanel(parent, "LayeredTransitionOverlay", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _loadingOverlay.offsetMin = Vector2.zero;
      _loadingOverlay.offsetMax = Vector2.zero;
      _loadingOverlay.gameObject.SetActive(false);
      _loadingOverlay.SetAsLastSibling();

      _loadingFade = CreatePanel(_loadingOverlay, "FadeField", new Color(0.005f, 0.012f, 0.02f, 0f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();

      _loadingCore = CreatePanel(_loadingOverlay, "TransitionCore", Color.clear,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f)).GetComponent<Image>();
      _loadingCore.sprite = GeometricMenuBackground.HexSprite;
      _loadingCore.raycastTarget = false;

      for (var i = 0; i < 18; i++)
      {
        var sprite = i % 3 == 0 ? GeometricMenuBackground.HexSprite : i % 3 == 1 ? GeometricMenuBackground.DiamondSprite : GeometricMenuBackground.RingSprite;
        var img = CreatePanel(_loadingOverlay, $"TransitionShape_{i + 1}", Color.clear,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 18f).GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        _loadingShapes.Add(img);
      }
    }

    IEnumerator LoadingTransitionRoutine(LoadingTransitionKind kind, Action onComplete)
    {
      _transitioning = true;
      if (_loadingOverlay == null)
        BuildLoadingOverlay(_canvasRoot);
      _loadingOverlay.SetAsLastSibling();
      _loadingOverlay.gameObject.SetActive(true);
      SetLoadingInteractable(false);

      var duration = kind == LoadingTransitionKind.Gameplay ? 1.45f : 1.25f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        UpdateLoadingVisual(Mathf.Clamp01(elapsed / duration), kind);
        yield return null;
      }

      onComplete?.Invoke();

      if (kind == LoadingTransitionKind.Gameplay)
      {
        _loadingOverlay.gameObject.SetActive(false);
        SetLoadingInteractable(true);
        _transitioning = false;
        yield break;
      }

      var fadeOut = 0f;
      while (fadeOut < 0.28f)
      {
        fadeOut += Time.unscaledDeltaTime;
        var t = 1f - Mathf.Clamp01(fadeOut / 0.28f);
        ApplyLoadingAlpha(t);
        yield return null;
      }

      _loadingOverlay.gameObject.SetActive(false);
      SetLoadingInteractable(true);
      _transitioning = false;
    }

    void UpdateLoadingVisual(float t, LoadingTransitionKind kind)
    {
      var eased = kind == LoadingTransitionKind.Gameplay ? EaseInOutCubic(t) : EaseOutCubic(t);
      var alpha = t < 0.5f ? Mathf.Lerp(0f, 0.94f, t * 2f) : 0.94f;
      if (_loadingFade != null)
      {
        var baseColor = kind == LoadingTransitionKind.Gameplay
          ? new Color(0.004f, 0.018f, 0.034f, alpha)
          : new Color(0.006f, 0.014f, 0.024f, alpha * 0.9f);
        _loadingFade.color = baseColor;
      }

      var accent = kind == LoadingTransitionKind.Gameplay
        ? new Color(0.32f, 0.9f, 1f, 1f)
        : new Color(0.62f, 0.78f, 0.86f, 1f);

      if (_loadingCore != null)
      {
        var coreScale = kind == LoadingTransitionKind.Gameplay
          ? Mathf.Lerp(0.72f, 4.8f, eased)
          : Mathf.Lerp(1.2f, 0.65f, Mathf.Sin(t * Mathf.PI));
        _loadingCore.rectTransform.localScale = Vector3.one * coreScale;
        _loadingCore.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (kind == LoadingTransitionKind.Gameplay ? 160f : 42f) * t);
        _loadingCore.color = WithAlpha(accent, kind == LoadingTransitionKind.Gameplay ? 0.24f + (1f - t) * 0.28f : 0.22f + Mathf.Sin(t * Mathf.PI) * 0.2f);
      }

      for (var i = 0; i < _loadingShapes.Count; i++)
      {
        var img = _loadingShapes[i];
        if (img == null) continue;
        var angle = i * (360f / Mathf.Max(1, _loadingShapes.Count)) + t * (kind == LoadingTransitionKind.Gameplay ? 72f : 18f);
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        var from = dir * (kind == LoadingTransitionKind.Gameplay ? 48f : 420f);
        var to = dir * (kind == LoadingTransitionKind.Gameplay ? 860f : 92f);
        var pos = kind == LoadingTransitionKind.Gameplay
          ? Vector2.Lerp(from, to, eased)
          : Vector2.Lerp(from, to, Mathf.Sin(t * Mathf.PI * 0.5f));
        pos += new Vector2(Mathf.Sin(t * 5f + i), Mathf.Cos(t * 4f + i * 0.3f)) * (kind == LoadingTransitionKind.Gameplay ? 28f : 10f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = Vector2.one * (kind == LoadingTransitionKind.Gameplay ? Mathf.Lerp(12f, 52f, t) : Mathf.Lerp(22f, 10f, t));
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle + t * 90f);
        img.color = WithAlpha(accent, kind == LoadingTransitionKind.Gameplay ? Mathf.Lerp(0.42f, 0f, t) : 0.22f * Mathf.Sin(t * Mathf.PI));
      }
    }

    void ApplyLoadingAlpha(float alpha)
    {
      if (_loadingFade != null)
      {
        var color = _loadingFade.color;
        color.a *= alpha;
        _loadingFade.color = color;
      }
      if (_loadingCore != null)
      {
        var color = _loadingCore.color;
        color.a *= alpha;
        _loadingCore.color = color;
      }
      foreach (var img in _loadingShapes)
      {
        if (img == null) continue;
        var color = img.color;
        color.a *= alpha;
        img.color = color;
      }
    }

    void SetLoadingInteractable(bool interactable)
    {
      if (_canvas == null) return;
      var raycaster = _canvas.GetComponent<GraphicRaycaster>();
      if (raycaster != null) raycaster.enabled = interactable;
      if (_loadingFade != null) _loadingFade.raycastTarget = !interactable;
    }

    static float EaseOutCubic(float t)
    {
      t = Mathf.Clamp01(t);
      return 1f - Mathf.Pow(1f - t, 3f);
    }

    static float EaseInOutCubic(float t)
    {
      t = Mathf.Clamp01(t);
      return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = Mathf.Clamp01(alpha);
      return color;
    }

    public static RectTransform CreatePanel(Transform parent, string name, Color bg,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;
      var img = go.AddComponent<Image>();
      img.color = bg;
      img.raycastTarget = bg.a > 0.01f;
      return rt;
    }

    public Text CreateLabel(RectTransform parent, string name, string text, int fontSize,
      FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(anchorMin.x, anchorMin.y == anchorMax.y ? 0.5f : 1f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;
      var label = go.AddComponent<Text>();
      label.font = _font;
      label.fontSize = fontSize;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = Color.white;
      label.text = text;
      label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      label.verticalOverflow = VerticalWrapMode.Truncate;
      label.supportRichText = true;
      var outline = label.gameObject.AddComponent<Outline>();
      outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
      outline.effectDistance = new Vector2(1.2f, -1.2f);
      return label;
    }

    public Button CreateButton(RectTransform parent, string name, string label,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta,
      Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, name, bg, anchorMin, anchorMax, anchoredPos, sizeDelta);
      GeometricPanelFrame.Attach(rt.gameObject, new Color(0.42f, 0.9f, 1f, 1f));
      var btn = rt.gameObject.AddComponent<Button>();
      btn.targetGraphic = rt.GetComponent<Image>();
      var colors = btn.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
      colors.pressedColor = new Color(0.82f, 0.92f, 0.98f, 1f);
      colors.disabledColor = new Color(0.45f, 0.5f, 0.55f, 0.5f);
      btn.colors = colors;

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(rt, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = new Vector2(14f, 8f);
      textRt.offsetMax = new Vector2(-14f, -8f);

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 17;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      text.text = label;
      text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Overflow;
      text.supportRichText = true;
      UiFontHelper.StyleText(text, 17);

      btn.onClick.AddListener(onClick);
      rt.gameObject.AddComponent<EnergyButtonHover>();
      return btn;
    }
  }

  public sealed class EnergyButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    Vector3 _baseScale = Vector3.one;
    bool _hovered;
    float _phase;

    void Awake() => _baseScale = transform.localScale;

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      var target = _hovered ? 1.055f : 1f;
      var pulse = _hovered ? Mathf.Sin(_phase * 8f) * 0.012f : 0f;
      transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * (target + pulse), Time.unscaledDeltaTime * 10f);
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;
  }

  public sealed class MinimalMenuButtonCue : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    Image _image;
    Image _indicator;
    Color _accent;
    Color _baseColor;
    Vector3 _baseScale = Vector3.one;
    bool _hovered;

    public static void Attach(GameObject target, Color accent)
    {
      var cue = target.GetComponent<MinimalMenuButtonCue>();
      if (cue == null) cue = target.AddComponent<MinimalMenuButtonCue>();
      cue.Configure(accent);
    }

    void Configure(Color accent)
    {
      _accent = accent;
      _image = GetComponent<Image>();
      if (_image != null)
        _baseColor = _image.color;
      _baseScale = transform.localScale;
      if (_indicator == null)
        _indicator = CreateIndicator();
    }

    void Update()
    {
      var t = Time.unscaledDeltaTime * 10f;
      transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * (_hovered ? 1.025f : 1f), t);
      if (_image != null)
      {
        var target = _hovered
          ? Color.Lerp(_baseColor, WithAlpha(_accent, Mathf.Max(_baseColor.a, 0.86f)), 0.28f)
          : _baseColor;
        _image.color = Color.Lerp(_image.color, target, t);
      }

      if (_indicator != null)
      {
        var rt = _indicator.rectTransform;
        rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, new Vector2(_hovered ? 4f : 0f, 34f), t);
        _indicator.color = Color.Lerp(_indicator.color, WithAlpha(_accent, _hovered ? 0.86f : 0f), t);
      }
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    Image CreateIndicator()
    {
      var go = new GameObject("HoverIndicator", typeof(RectTransform));
      go.transform.SetParent(transform, false);
      go.transform.SetAsLastSibling();
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0f, 0.5f);
      rt.anchorMax = new Vector2(0f, 0.5f);
      rt.pivot = new Vector2(0f, 0.5f);
      rt.anchoredPosition = new Vector2(12f, 0f);
      rt.sizeDelta = new Vector2(0f, 34f);
      var img = go.AddComponent<Image>();
      img.color = Color.clear;
      img.raycastTarget = false;
      return img;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  public sealed class EnergyMenuNode : MonoBehaviour
  {
    Image _image;
    Image _ring;
    Color _accent;
    bool _primary;
    float _phase;

    public static void Attach(GameObject target, Color accent, bool primary)
    {
      var node = target.GetComponent<EnergyMenuNode>();
      if (node == null) node = target.AddComponent<EnergyMenuNode>();
      node._accent = accent;
      node._primary = primary;
      node._image = target.GetComponent<Image>();
      if (node._ring == null)
        node._ring = node.CreateOverlay("HoverEnergyRing", GeometricMenuBackground.RingSprite);
    }

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      if (_image != null)
      {
        var a = _primary ? 0.36f : 0.22f;
        _image.color = Color.Lerp(_image.color, WithAlpha(_accent, a + Mathf.Sin(_phase * 1.8f) * 0.06f), Time.unscaledDeltaTime * 1.8f);
      }
      if (_ring != null)
      {
        _ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _phase * (_primary ? 24f : 12f));
        _ring.color = WithAlpha(_accent, (_primary ? 0.4f : 0.24f) + Mathf.Sin(_phase * 2.4f) * 0.08f);
      }
    }

    Image CreateOverlay(string name, Sprite sprite)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(transform, false);
      go.transform.SetAsFirstSibling();
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = new Vector2(-10f, -10f);
      rt.offsetMax = new Vector2(10f, 10f);
      var img = go.AddComponent<Image>();
      img.sprite = sprite;
      img.raycastTarget = false;
      return img;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  public sealed class StrategicMapNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    Image _image;
    Image _core;
    Image _ring;
    Color _accent;
    Color _baseColor;
    Vector3 _baseScale = Vector3.one;
    bool _hovered;
    bool _primary;
    float _phase;

    public static void Attach(GameObject target, Color accent, bool primary)
    {
      var node = target.GetComponent<StrategicMapNode>();
      if (node == null) node = target.AddComponent<StrategicMapNode>();
      node.Configure(accent, primary);
    }

    void Configure(Color accent, bool primary)
    {
      _accent = accent;
      _primary = primary;
      _image = GetComponent<Image>();
      if (_image != null) _baseColor = _image.color;
      _baseScale = transform.localScale;
      if (_ring == null) _ring = CreateOverlay("NavigationRing", GeometricMenuBackground.RingSprite, new Vector2(96f, 96f), new Vector2(0f, 36f));
      if (_core == null) _core = CreateOverlay("NavigationCore", _primary ? GeometricMenuBackground.HexSprite : GeometricMenuBackground.DiamondSprite, new Vector2(54f, 54f), new Vector2(0f, 36f));
    }

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      var t = Time.unscaledDeltaTime * 7f;
      transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * (_hovered ? 1.035f : 1f), t);
      if (_image != null)
      {
        var target = _hovered
          ? Color.Lerp(_baseColor, WithAlpha(_accent, 0.52f), 0.42f)
          : _baseColor;
        _image.color = Color.Lerp(_image.color, target, t);
      }

      var breath = 0.5f + Mathf.Sin(_phase * 1.05f) * 0.5f;
      if (_ring != null)
      {
        _ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _phase * (_primary ? 2.1f : -1.4f));
        _ring.color = Color.Lerp(_ring.color, WithAlpha(_accent, (_hovered ? 0.28f : 0.12f) + breath * 0.035f), t);
      }

      if (_core != null)
      {
        _core.rectTransform.localScale = Vector3.one * (1f + breath * (_hovered ? 0.035f : 0.018f));
        _core.color = Color.Lerp(_core.color, WithAlpha(_accent, (_hovered ? 0.5f : 0.28f) + breath * 0.045f), t);
      }
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    Image CreateOverlay(string name, Sprite sprite, Vector2 size, Vector2 pos)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(transform, false);
      go.transform.SetAsFirstSibling();
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0.5f);
      rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      var img = go.AddComponent<Image>();
      img.sprite = sprite;
      img.raycastTarget = false;
      return img;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  public sealed class GeometricPanelFrame : MonoBehaviour
  {
    Image _frame;
    Color _accent;
    float _phase;

    public static void Attach(GameObject target, Color accent)
    {
      var frame = target.GetComponent<GeometricPanelFrame>();
      if (frame == null) frame = target.AddComponent<GeometricPanelFrame>();
      frame._accent = accent;
      if (frame._frame == null)
        frame._frame = frame.CreateFrame();
    }

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      if (_frame != null)
        _frame.color = WithAlpha(_accent, 0.12f + Mathf.Sin(_phase * 1.7f) * 0.035f);
    }

    Image CreateFrame()
    {
      var go = new GameObject("GeometricFrame", typeof(RectTransform));
      go.transform.SetParent(transform, false);
      go.transform.SetAsFirstSibling();
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = new Vector2(-3f, -3f);
      rt.offsetMax = new Vector2(3f, 3f);
      var img = go.AddComponent<Image>();
      img.sprite = GeometricMenuBackground.FrameSprite;
      img.type = Image.Type.Sliced;
      img.raycastTarget = false;
      return img;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  public sealed class GeometricCoreVisual : MonoBehaviour
  {
    public enum Style { HexCore, ArenaPortal, ExploreShard, Warrior, Mage, Ranged }

    readonly List<Image> _layers = new();
    Color _accent;
    Style _style;
    bool _subtle;
    float _phase;

    public static void Attach(GameObject target, Color accent, Style style)
    {
      var visual = target.GetComponent<GeometricCoreVisual>();
      if (visual == null) visual = target.AddComponent<GeometricCoreVisual>();
      visual.Configure(accent, style);
    }

    void Configure(Color accent, Style style)
    {
      _accent = accent;
      _style = style;
      _subtle = name.Contains("UniverseCore");
      if (_layers.Count > 0) return;

      _layers.Add(CreateLayer("OuterFrame", GeometricMenuBackground.HexSprite, new Vector2(0.92f, 0.92f)));
      _layers.Add(CreateLayer("MiddleRing", GeometricMenuBackground.RingSprite, new Vector2(0.68f, 0.68f)));
      _layers.Add(CreateLayer("Core", style == Style.ExploreShard ? GeometricMenuBackground.DiamondSprite : GeometricMenuBackground.HexSprite, new Vector2(0.34f, 0.34f)));
    }

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      for (var i = 0; i < _layers.Count; i++)
      {
        var layer = _layers[i];
        if (layer == null) continue;
        var dir = i % 2 == 0 ? 1f : -1f;
        var speedScale = _subtle ? 0.12f : 1f;
        layer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _phase * dir * (8f + i * 13f) * speedScale);
        var rawPulse = 0.5f - Mathf.Cos(_phase * (_subtle ? 0.18f : 1.1f) + i * 0.7f) * 0.5f;
        var pulse = Mathf.SmoothStep(0f, 1f, rawPulse);
        var alphaScale = _subtle ? 0.28f : 1f;
        layer.color = i switch
        {
          0 => WithAlpha(_accent, (0.18f + pulse * 0.035f) * alphaScale),
          1 => WithAlpha(Color.white, (0.09f + pulse * 0.035f) * alphaScale),
          _ => WithAlpha(_accent, (0.34f + pulse * 0.07f) * alphaScale)
        };
      }
    }

    Image CreateLayer(string name, Sprite sprite, Vector2 scale)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(transform, false);
      go.transform.SetAsFirstSibling();
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f - scale.x * 0.5f, 0.5f - scale.y * 0.5f);
      rt.anchorMax = new Vector2(0.5f + scale.x * 0.5f, 0.5f + scale.y * 0.5f);
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      var img = go.AddComponent<Image>();
      img.sprite = sprite;
      img.raycastTarget = false;
      return img;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  public sealed class GeometricMenuBackground : MonoBehaviour
  {
    static Sprite s_discSprite;
    static Sprite s_ringSprite;
    static Sprite s_hexSprite;
    static Sprite s_diamondSprite;
    static Sprite s_frameSprite;
    static Sprite s_gradientSprite;
    static Sprite s_softNebulaSprite;

    readonly List<Image> _rings = new();
    readonly List<Image> _particles = new();
    readonly List<Image> _shards = new();
    readonly List<Image> _mapRoutes = new();
    readonly List<Image> _mapNodes = new();
    Image _gradient;
    Image _nebula;
    Image _scanLine;
    MenuVisualStage _stage = MenuVisualStage.MainUniverse;
    float _phase;

    public static Sprite RingSprite { get { EnsureSprites(); return s_ringSprite; } }
    public static Sprite HexSprite { get { EnsureSprites(); return s_hexSprite; } }
    public static Sprite DiamondSprite { get { EnsureSprites(); return s_diamondSprite; } }
    public static Sprite FrameSprite { get { EnsureSprites(); return s_frameSprite; } }

    public static GeometricMenuBackground Create(RectTransform parent)
    {
      EnsureSprites();
      var go = new GameObject("GeometricUniverseBackground", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      go.transform.SetAsFirstSibling();
      var bg = go.AddComponent<GeometricMenuBackground>();
      bg.Build();
      bg.SetStage(MenuVisualStage.MainUniverse);
      return bg;
    }

    public void SetStage(MenuVisualStage stage) => _stage = stage;

    void Build()
    {
      _gradient = CreateImage("GradientField", s_gradientSprite, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2400f, 1500f));
      _nebula = CreateImage("NebulaMist", s_softNebulaSprite, new Vector2(0.5f, 0.5f), new Vector2(220f, 40f), new Vector2(1180f, 760f));

      for (var i = 0; i < 5; i++)
      {
        var size = 520f + i * 230f;
        var ring = CreateImage($"AmbientRing_{i + 1}", s_ringSprite, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        _rings.Add(ring);
      }

      for (var i = 0; i < 24; i++)
      {
        var pos = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(360f, 980f);
        var particle = CreateImage($"Particle_{i + 1}", s_discSprite, new Vector2(0.5f, 0.5f), pos, Vector2.one * UnityEngine.Random.Range(3f, 8f));
        _particles.Add(particle);
      }

      for (var i = 0; i < 12; i++)
      {
        var pos = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(420f, 980f) + UnityEngine.Random.insideUnitCircle * 120f;
        var sprite = i % 3 == 0 ? s_hexSprite : i % 3 == 1 ? s_diamondSprite : s_ringSprite;
        var shard = CreateImage($"FloatingGeometry_{i + 1}", sprite, new Vector2(0.5f, 0.5f), pos, Vector2.one * UnityEngine.Random.Range(18f, 54f));
        _shards.Add(shard);
      }

      BuildNavigationMapLayer();
    }

    void Update()
    {
      _phase += Time.unscaledDeltaTime;
      var palette = ResolvePalette(_stage);
      var mainStage = _stage == MenuVisualStage.MainUniverse;
      var navigationStage = _stage == MenuVisualStage.ModeSelect;
      if (_gradient != null)
        _gradient.color = Color.Lerp(_gradient.color, palette.Gradient, Time.unscaledDeltaTime * 1.6f);
      if (_nebula != null)
      {
        _nebula.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _phase * (navigationStage ? 0.05f : 0.025f));
        _nebula.color = Color.Lerp(_nebula.color, WithAlpha(palette.Nebula, palette.Nebula.a), Time.unscaledDeltaTime * 0.65f);
      }

      for (var i = 0; i < _rings.Count; i++)
      {
        _rings[i].gameObject.SetActive(navigationStage ? i < 1 : (!mainStage || i < 1));
        var rt = _rings[i].rectTransform;
        var speed = palette.RingMotion * (i % 2 == 0 ? 1f : -0.72f) * (0.55f + i * 0.12f);
        rt.localRotation = Quaternion.Euler(0f, 0f, _phase * speed);
        var scalePulse = 1f + Mathf.Sin(_phase * 0.34f + i) * 0.015f;
        rt.localScale = Vector3.one * scalePulse;
        _rings[i].color = WithAlpha(palette.Accent, palette.RingAlpha * (0.35f + i * 0.08f));
      }

      for (var i = 0; i < _particles.Count; i++)
      {
        var img = _particles[i];
        img.gameObject.SetActive(navigationStage ? i < 14 : (!mainStage || i < 10));
        var rt = img.rectTransform;
        rt.anchoredPosition += new Vector2(Mathf.Sin(_phase * 0.32f + i), Mathf.Cos(_phase * 0.27f + i * 0.7f)) * (Time.unscaledDeltaTime * palette.ParticleDrift);
        var particleAlpha = navigationStage ? 0.045f + Mathf.Sin(_phase * 0.52f + i) * 0.015f : 0.08f + Mathf.Sin(_phase * 0.9f + i) * 0.035f;
        img.color = WithAlpha(palette.Particle, particleAlpha);
      }

      for (var i = 0; i < _shards.Count; i++)
      {
        var img = _shards[i];
        img.gameObject.SetActive(navigationStage ? false : (!mainStage || i < 3));
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _phase * (i % 2 == 0 ? 5f : -4f) + i * 17f);
        img.color = WithAlpha(palette.Accent, palette.ShardAlpha * (0.55f + Mathf.Sin(_phase * 0.52f + i) * 0.22f));
      }

      UpdateNavigationMapLayer(navigationStage, palette);
    }

    void BuildNavigationMapLayer()
    {
      var nodes = new[]
      {
        new Vector2(-560f, 90f),
        new Vector2(-330f, -10f),
        new Vector2(-80f, 86f),
        new Vector2(170f, -38f),
        new Vector2(440f, 72f),
        new Vector2(-420f, -205f),
        new Vector2(-120f, -168f),
        new Vector2(230f, -218f),
        new Vector2(535f, -120f)
      };

      var edges = new[]
      {
        new Vector2Int(0, 1),
        new Vector2Int(1, 2),
        new Vector2Int(2, 3),
        new Vector2Int(3, 4),
        new Vector2Int(1, 5),
        new Vector2Int(5, 6),
        new Vector2Int(6, 7),
        new Vector2Int(7, 8),
        new Vector2Int(3, 7)
      };

      for (var i = 0; i < edges.Length; i++)
      {
        var route = CreateImage($"StarRoute_{i + 1}", null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        PositionRoute(route.rectTransform, nodes[edges[i].x], nodes[edges[i].y], 1.35f);
        _mapRoutes.Add(route);
      }

      for (var i = 0; i < nodes.Length; i++)
      {
        var node = CreateImage($"StarMapNode_{i + 1}", i % 3 == 0 ? s_diamondSprite : s_discSprite, new Vector2(0.5f, 0.5f), nodes[i], Vector2.one * (i % 3 == 0 ? 12f : 8f));
        _mapNodes.Add(node);
      }

      _scanLine = CreateImage("NavigationScanLine", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 1.2f));
    }

    void UpdateNavigationMapLayer(bool active, Palette palette)
    {
      for (var i = 0; i < _mapRoutes.Count; i++)
      {
        var route = _mapRoutes[i];
        route.gameObject.SetActive(active);
        if (!active) continue;
        route.color = WithAlpha(palette.Accent, 0.035f + Mathf.Sin(_phase * 0.32f + i * 0.65f) * 0.012f);
      }

      for (var i = 0; i < _mapNodes.Count; i++)
      {
        var node = _mapNodes[i];
        node.gameObject.SetActive(active);
        if (!active) continue;
        var pulse = 0.5f + Mathf.Sin(_phase * 0.55f + i * 0.9f) * 0.5f;
        node.rectTransform.localScale = Vector3.one * (1f + pulse * 0.08f);
        node.color = WithAlpha(palette.Particle, 0.09f + pulse * 0.035f);
      }

      if (_scanLine != null)
      {
        _scanLine.gameObject.SetActive(active);
        if (active)
        {
          _scanLine.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(_phase * 0.18f) * 230f);
          _scanLine.color = WithAlpha(palette.Accent, 0.024f);
        }
      }
    }

    static void PositionRoute(RectTransform rt, Vector2 from, Vector2 to, float width)
    {
      var delta = to - from;
      rt.anchorMin = new Vector2(0.5f, 0.5f);
      rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = (from + to) * 0.5f;
      rt.sizeDelta = new Vector2(delta.magnitude, width);
      rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    Palette ResolvePalette(MenuVisualStage stage)
    {
      return stage switch
      {
        MenuVisualStage.MainUniverse => new Palette(
          new Color(0.045f, 0.085f, 0.13f, 1f), new Color(0.18f, 0.32f, 0.42f, 0.055f), new Color(0.38f, 0.58f, 0.68f, 1f), new Color(0.68f, 0.82f, 0.9f, 1f), 0.45f, 0.012f, 0.035f, 1.35f),
        MenuVisualStage.ModeSelect => new Palette(
          new Color(0.035f, 0.065f, 0.095f, 1f), new Color(0.16f, 0.24f, 0.32f, 0.052f), new Color(0.36f, 0.58f, 0.68f, 1f), new Color(0.7f, 0.84f, 0.9f, 1f), 0.28f, 0.01f, 0.018f, 0.8f),
        MenuVisualStage.ArchetypeTemple => new Palette(
          new Color(0.04f, 0.075f, 0.14f, 1f), new Color(0.28f, 0.24f, 0.58f, 0.18f), new Color(0.58f, 0.72f, 1f, 1f), new Color(0.82f, 0.9f, 1f, 1f), 1.5f, 0.035f, 0.12f, 4f),
        MenuVisualStage.ArenaEntry => new Palette(
          new Color(0.02f, 0.13f, 0.18f, 1f), new Color(0.04f, 0.42f, 0.72f, 0.18f), new Color(0.34f, 0.95f, 1f, 1f), new Color(0.8f, 1f, 1f, 1f), 3.5f, 0.06f, 0.18f, 6f),
        _ => new Palette(
          new Color(0.03f, 0.12f, 0.08f, 1f), new Color(0.24f, 0.54f, 0.32f, 0.15f), new Color(0.48f, 0.92f, 0.62f, 1f), new Color(0.82f, 1f, 0.88f, 1f), 1.3f, 0.028f, 0.11f, 4f)
      };
    }

    Image CreateImage(string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(transform, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchor;
      rt.anchorMax = anchor;
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      var image = go.AddComponent<Image>();
      image.sprite = sprite;
      image.raycastTarget = false;
      return image;
    }

    static void EnsureSprites()
    {
      if (s_discSprite != null) return;
      s_discSprite = CreateDiscSprite(192, 0f);
      s_ringSprite = CreateDiscSprite(256, 0.74f);
      s_hexSprite = CreatePolygonSprite(256, 6);
      s_diamondSprite = CreatePolygonSprite(256, 4, 45f);
      s_frameSprite = CreateFrameSprite(32);
      s_gradientSprite = CreateGradientSprite(64);
      s_softNebulaSprite = CreateSoftNebulaSprite(512);
    }

    static Sprite CreateDiscSprite(int resolution, float innerCutout)
    {
      var tex = NewTexture(resolution);
      var pixels = new Color[resolution * resolution];
      var center = new Vector2(resolution * 0.5f, resolution * 0.5f);
      var outer = resolution * 0.5f - 1f;
      var inner = outer * innerCutout;
      for (var y = 0; y < resolution; y++)
      for (var x = 0; x < resolution; x++)
      {
        var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
        var outerAlpha = Mathf.Clamp01((outer - dist) / 2.5f);
        var innerAlpha = inner <= 0f ? 1f : Mathf.Clamp01((dist - inner) / 2.5f);
        pixels[y * resolution + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(outerAlpha, innerAlpha)));
      }
      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreatePolygonSprite(int resolution, int sides, float rotationDeg = 0f)
    {
      var tex = NewTexture(resolution);
      var pixels = new Color[resolution * resolution];
      var center = new Vector2(resolution * 0.5f, resolution * 0.5f);
      var radius = resolution * 0.43f;
      for (var y = 0; y < resolution; y++)
      for (var x = 0; x < resolution; x++)
      {
        var p = new Vector2(x + 0.5f, y + 0.5f) - center;
        var dist = p.magnitude;
        var angle = Mathf.Atan2(p.y, p.x) + rotationDeg * Mathf.Deg2Rad;
        var sector = Mathf.PI * 2f / sides;
        var local = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f);
        var edge = Mathf.Cos(sector * 0.5f) / Mathf.Max(0.001f, Mathf.Cos(local)) * radius;
        var outerAlpha = Mathf.Clamp01((edge - dist) / 2.8f);
        var innerAlpha = Mathf.Clamp01((dist - edge * 0.74f) / 2.8f);
        pixels[y * resolution + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(outerAlpha, innerAlpha)));
      }
      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreateFrameSprite(int resolution)
    {
      var tex = NewTexture(resolution);
      var pixels = new Color[resolution * resolution];
      for (var y = 0; y < resolution; y++)
      for (var x = 0; x < resolution; x++)
      {
        var edge = x < 2 || y < 2 || x >= resolution - 2 || y >= resolution - 2;
        pixels[y * resolution + x] = edge ? Color.white : Color.clear;
      }
      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 8f, 0, SpriteMeshType.FullRect, new Vector4(4f, 4f, 4f, 4f));
    }

    static Sprite CreateGradientSprite(int resolution)
    {
      var tex = NewTexture(resolution);
      var pixels = new Color[resolution * resolution];
      for (var y = 0; y < resolution; y++)
      for (var x = 0; x < resolution; x++)
      {
        var u = x / (float)(resolution - 1);
        var v = y / (float)(resolution - 1);
        var baseColor = Color.Lerp(new Color(0.02f, 0.06f, 0.18f, 1f), new Color(0.05f, 0.18f, 0.32f, 1f), v);
        var purple = new Color(0.13f, 0.08f, 0.32f, 1f);
        pixels[y * resolution + x] = Color.Lerp(baseColor, purple, Mathf.Clamp01(u * 0.65f + (1f - v) * 0.35f));
      }
      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreateSoftNebulaSprite(int resolution)
    {
      var tex = NewTexture(resolution);
      var pixels = new Color[resolution * resolution];
      var center = new Vector2(resolution * 0.5f, resolution * 0.5f);
      var radius = resolution * 0.5f;

      for (var y = 0; y < resolution; y++)
      for (var x = 0; x < resolution; x++)
      {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        var d = Vector2.Distance(p, center) / radius;
        var core = Mathf.Exp(-d * d * 2.15f);
        var edge = 1f - Mathf.SmoothStep(0.72f, 1f, d);
        var alpha = Mathf.Clamp01(core * edge);
        pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
      }

      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewTexture(int resolution) => new(resolution, resolution, TextureFormat.RGBA32, false)
    {
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp
    };

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }

    readonly struct Palette
    {
      public readonly Color Gradient;
      public readonly Color Nebula;
      public readonly Color Accent;
      public readonly Color Particle;
      public readonly float RingMotion;
      public readonly float RingAlpha;
      public readonly float ShardAlpha;
      public readonly float ParticleDrift;

      public Palette(Color gradient, Color nebula, Color accent, Color particle, float ringMotion, float ringAlpha, float shardAlpha, float particleDrift)
      {
        Gradient = gradient;
        Nebula = nebula;
        Accent = accent;
        Particle = particle;
        RingMotion = ringMotion;
        RingAlpha = ringAlpha;
        ShardAlpha = shardAlpha;
        ParticleDrift = particleDrift;
      }
    }
  }
}
