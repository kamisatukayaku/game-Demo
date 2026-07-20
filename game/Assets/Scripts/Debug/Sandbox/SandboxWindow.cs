using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;

namespace Game.DevTools.Sandbox
{
  /// <summary>
  /// Build Sandbox UI：JSON → Build → Runtime → Combat → VFX 验证工具。
  /// </summary>
  public class SandboxWindow : MonoBehaviour
  {
    static SandboxWindow s_instance;

    static readonly Color PanelBg = new(0.06f, 0.09f, 0.12f, 0.98f);
    static readonly Color ColumnBg = new(0.10f, 0.14f, 0.18f, 1f);
    static readonly Color Accent = new(0.42f, 0.72f, 0.88f, 1f);
    static readonly Color PickedBg = new(0.18f, 0.38f, 0.32f, 1f);
    static readonly Color TextDim = new(0.62f, 0.72f, 0.78f, 1f);

    static readonly string[] ClassIds = { "warrior", "ranged", "mage" };

    const int CanvasSortOrder = 700;

    Font _font;
    GameObject _panel;
    SandboxController _controller;
    RawImage _worldView;
    RectTransform _worldViewRect;
    Text _autoCombatLabel;

    SandboxBuildTreeView _buildTree;
    SandboxBuildDescriptionPanel _buildDescPanel;
    SandboxClassDescriptionPanel _classDescPanel;
    SandboxRuntimeStatsView _runtimeStatsView;
    SandboxDpsView _dpsView;
    RuntimeEventLogView _eventLog;
    SandboxSkillBarView _skillBar;
    SandboxDetachedWeaponRegressionPanel _detachedVfxPanel;
    SandboxRangedRegressionPanel _rangedRegressionPanel;
    GameObject _rangedRegressionHost;
    RectTransform _skillBarHost;

    RectTransform _buildContent;
    Text _currentArchetypeLabel;
    string _weaponTheme = "ranged";

    public static bool IsOpen =>
      s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;

    public static void Open(Transform parent, string weaponTheme) =>
      Open(parent, weaponTheme, null);

    public static void Open(Transform parent, string weaponTheme, SandboxBootstrap bootstrap)
    {
      EnsureExists(parent);
      s_instance.Show(weaponTheme, bootstrap?.CombatArena);
    }

    static void EnsureExists(Transform parent)
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_CombatSandbox");
      go.transform.SetParent(parent, false);
      s_instance = go.AddComponent<SandboxWindow>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      _font = UiFontHelper.GetFont();
      _controller = gameObject.AddComponent<SandboxController>();
      BuildUI();
      Close();
    }

    void OnDestroy()
    {
      _eventLog?.Dispose();
      _dpsView?.Dispose();
      _controller?.Dispose();
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      if (!IsOpen)
        return;

      if (Input.GetKeyDown(KeyCode.Escape))
        Close();

      _runtimeStatsView?.Refresh();
      _buildDescPanel?.Refresh();
      _classDescPanel?.Refresh();
      _dpsView?.Refresh();

      var cam = _controller?.Scene?.CameraController;
      if (cam?.Texture != null && _worldView != null && _worldView.texture != cam.Texture)
        _worldView.texture = cam.Texture;

      if (_worldViewRect != null && cam != null &&
          RectTransformUtility.RectangleContainsScreenPoint(_worldViewRect, Input.mousePosition))
      {
        var scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
          cam.AdjustZoom(scroll);
      }

      if (_autoCombatLabel != null && _controller?.AutoCombat != null)
        _autoCombatLabel.text = _controller.AutoCombat.AutoCombatEnabled ? "Auto: ON" : "Auto: OFF";
    }

    void RefreshBuildPanels()
    {
      _buildDescPanel?.SetTheme(_weaponTheme);
      _buildDescPanel?.Refresh();
      _classDescPanel?.SetTheme(_weaponTheme);
      _classDescPanel?.Refresh();
      _runtimeStatsView?.Refresh();
      _dpsView?.Refresh();
    }

    void Show(string weaponTheme, Transform combatArena)
    {
      UiBootstrap.EnsureEventSystem();
      _weaponTheme = string.IsNullOrEmpty(weaponTheme) ? "ranged" : weaponTheme;

      _controller.Initialize(_weaponTheme, combatArena);
      _buildTree.Load(_weaponTheme);
      RebuildBuildTree();
      RefreshClassButtons();
      RefreshBuildPanels();
      RefreshRangedRegressionPanel();

      _controller.SpawnDefaultTargets();
      _controller.SetAutoCombat(true);
      RefreshSkillBar();

      if (_controller.Scene?.CameraController?.Texture != null)
        _worldView.texture = _controller.Scene.CameraController.Texture;

      _panel.SetActive(true);
    }

    void Close()
    {
      if (_panel != null)
        _panel.SetActive(false);
      _controller?.Dispose();
    }

    void SelectClass(string theme)
    {
      _weaponTheme = theme;
      _controller.SetWeaponTheme(theme);
      _buildTree.Load(theme);
      RebuildBuildTree();
      RefreshClassButtons();
      RefreshBuildPanels();
      RefreshSkillBar();
      RefreshRangedRegressionPanel();
      _controller.SpawnDefaultTargets();
    }

    void RefreshSkillBar() => _skillBar?.Refresh(_weaponTheme);

    readonly List<Button> _classButtons = new();

    void RefreshClassButtons()
    {
      WeaponThemeDatabase.EnsureLoaded();

      for (var i = 0; i < _classButtons.Count && i < ClassIds.Length; i++)
      {
        var btn = _classButtons[i];
        if (btn == null)
          continue;

        var themeId = ClassIds[i];
        var theme = WeaponThemeDatabase.Get(themeId);
        var label = theme?.display_name ?? themeId;

        var text = btn.GetComponentInChildren<Text>();
        if (text != null)
          text.text = label;

        var img = btn.GetComponent<Image>();
        if (img != null)
          img.color = themeId == _weaponTheme ? PickedBg : new Color(0.22f, 0.30f, 0.38f, 1f);
      }

      // 更新 Current Archetype 标签
      if (_currentArchetypeLabel != null)
      {
        var activeTheme = WeaponThemeDatabase.Get(_weaponTheme);
        _currentArchetypeLabel.text = $"Current Archetype: {activeTheme?.display_name ?? _weaponTheme}";
      }
    }

    void RebuildBuildTree()
    {
      _buildTree.Rebuild(
        (label, parent, y) =>
        {
          var t = CreateLabel(parent, $"Route_{label}", label, 12, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, y), new Vector2(-8f, 18f));
          t.alignment = TextAnchor.MiddleLeft;
          t.color = TextDim;
        },
        (label, parent, y, picked, onClick) =>
        {
          CreateButton(parent, label, label,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(-8f, 26f),
            picked ? PickedBg : new Color(0.16f, 0.22f, 0.30f, 1f),
            () => onClick?.Invoke());
        });
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("CombatSandboxCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = CanvasSortOrder;
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, CanvasSortOrder);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = CreatePanel(canvasGo.transform, "CombatSandboxPanel", PanelBg,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
      StretchFull(_panel.GetComponent<RectTransform>());

      CreateLabel(_panel.transform, "Title", "Build Sandbox", 26, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(480f, 36f));

      CreateLabel(_panel.transform, "Hint",
        "Build 验证：JSON → Runtime → Combat  |  滚轮缩放镜头  |  Esc 关闭",
        11, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(720f, 18f))
        .color = TextDim;

      var statsText = CreateLabel(_panel.transform, "RuntimeStats", "-", 11, FontStyle.Normal,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(300f, -58f), new Vector2(-90f, 18f));
      statsText.alignment = TextAnchor.MiddleLeft;
      statsText.color = TextDim;
      statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
      _runtimeStatsView = new SandboxRuntimeStatsView(statsText);

      var body = CreatePanel(_panel.transform, "Body", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      StretchFull(body, 8f, 112f, -8f, -78f);

      BuildLeftColumn(body);
      BuildCenterColumn(body);
      BuildRightColumn(body);
      BuildBottomLog(_panel.transform);
      BuildTopControls(_panel.transform);

      CreateButton(_panel.transform, "CloseBtn", "Close",
        new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(72f, 28f),
        ColumnBg, Close);
    }

    void BuildLeftColumn(RectTransform body)
    {
      var left = CreatePanel(body, "LeftColumn", ColumnBg,
        new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(280f, 0f));
      left.offsetMin = Vector2.zero;
      left.offsetMax = new Vector2(280f, 0f);

      CreateLabel(left, "BuildHeader", "Build 树（点击切换）", 13, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, 20f))
        .alignment = TextAnchor.MiddleLeft;

      _buildContent = BuildScrollArea(left, "BuildScroll",
        new Vector2(0f, 0f), new Vector2(1f, 1f),
        new Vector2(4f, 204f), new Vector2(-4f, -26f));
      _buildTree = new SandboxBuildTreeView(_buildContent, def =>
      {
        _controller.ApplyUpgrade(def);
        RebuildBuildTree();
        RefreshBuildPanels();
      });

      var rangedScroll = BuildScrollArea(left, "RangedRegressionScroll",
        new Vector2(0f, 0f), new Vector2(1f, 0f),
        new Vector2(4f, 4f), new Vector2(-4f, 196f));
      rangedScroll.sizeDelta = new Vector2(0f, 620f);
      _rangedRegressionHost = rangedScroll.parent.gameObject;
      _rangedRegressionPanel = new SandboxRangedRegressionPanel(_font, () => _controller?.Scene);
      _rangedRegressionPanel.Build(rangedScroll);
      RefreshRangedRegressionPanel();
    }

    void RefreshRangedRegressionPanel()
    {
      var show = _weaponTheme == "ranged";
      if (_rangedRegressionHost != null)
        _rangedRegressionHost.SetActive(show);
      _rangedRegressionPanel?.SetVisible(show);
    }

    void BuildCenterColumn(RectTransform body)
    {
      const float spawnBarHeight = 48f;

      var center = CreatePanel(body, "CenterColumn", new Color(0.04f, 0.06f, 0.08f, 1f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      center.offsetMin = new Vector2(288f, 0f);
      center.offsetMax = new Vector2(-288f, 0f);

      CreateLabel(center, "ArenaLabel", "Build 验证场（实时特效）", 13, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(280f, 20f))
        .color = TextDim;

      var viewHost = CreatePanel(center, "ViewHost", Color.black,
        new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      viewHost.offsetMin = new Vector2(4f, spawnBarHeight + 8f);
      viewHost.offsetMax = new Vector2(-4f, -28f);
      _worldViewRect = viewHost;

      var rawGo = new GameObject("LiveCombatView", typeof(RectTransform));
      rawGo.transform.SetParent(viewHost, false);
      StretchFull(rawGo.GetComponent<RectTransform>());
      _worldView = rawGo.AddComponent<RawImage>();

      var spawnBar = CreatePanel(center, "SpawnBar", ColumnBg,
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, spawnBarHeight));
      spawnBar.anchorMin = new Vector2(0f, 0f);
      spawnBar.anchorMax = new Vector2(1f, 0f);
      spawnBar.pivot = new Vector2(0.5f, 0f);

      var x = 6f;
      x = AddBarButton(spawnBar.transform, "Dummy", x, SpawnDummy);
      x = AddBarButton(spawnBar.transform, "Elite", x, SpawnElite);
      x = AddBarButton(spawnBar.transform, "Boss", x, SpawnBoss);
      x = AddBarButton(spawnBar.transform, "W.B1", x, SpawnWildBoss1);
      x = AddBarButton(spawnBar.transform, "W.B2", x, SpawnWildBoss2);
      x = AddBarButton(spawnBar.transform, "W.B3", x, SpawnWildBoss3);
      x = AddBarButton(spawnBar.transform, "F.Boss", x, SpawnFinalBoss);
      x = AddBarButton(spawnBar.transform, "Swarm", x, () => _controller?.Scene?.Spawner?.SpawnSwarm());
      x = AddBarButton(spawnBar.transform, "Clear", x, () => _controller?.Scene?.Spawner?.ClearAll());
      x += 4f;  // 分隔间距

      // ── 职业切换按钮 ──
      for (var i = 0; i < ClassIds.Length; i++)
      {
        var id = ClassIds[i];
        var capturedId = id;
        var theme = WeaponThemeDatabase.Get(id);
        var label = theme?.display_name ?? id;
        var btn = CreateButton(spawnBar.transform, $"Class_{id}", label,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(68f, 32f),
          new Color(0.20f, 0.30f, 0.38f, 1f), () => SelectClass(capturedId));
        _classButtons.Add(btn);
        x += 72f + 4f;
      }

      var autoBtn = CreateButton(spawnBar.transform, "AutoToggle", "Auto: ON",
        new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(72f, 32f),
        new Color(0.24f, 0.42f, 0.34f, 1f),
        () =>
        {
          var ac = _controller?.AutoCombat;
          if (ac == null)
            return;
          ac.AutoCombatEnabled = !ac.AutoCombatEnabled;
        });
      _autoCombatLabel = autoBtn.GetComponentInChildren<Text>();
    }

    void SpawnDummy()
    {
      var spawner = _controller?.Scene?.Spawner;
      if (spawner == null)
        return;
      spawner.SpawnDummy(Vector2.right * 4f);
    }

    void SpawnElite()
    {
      var spawner = _controller?.Scene?.Spawner;
      if (spawner == null)
        return;
      spawner.SpawnElite(Vector2.right * 5f);
    }

    void SpawnBoss()
    {
      var spawner = _controller?.Scene?.Spawner;
      if (spawner == null)
        return;
      spawner.SpawnBoss(Vector2.right * 6f);
    }

    void SpawnWildBoss1() => _controller?.Scene?.Spawner?.Spawn("wild_boss_hex_king", Vector2.right * 7f);
    void SpawnWildBoss2() => _controller?.Scene?.Spawner?.Spawn("wild_boss_pent_colossus", Vector2.right * 8f);
    void SpawnFinalBoss() => _controller?.Scene?.Spawner?.Spawn("final_boss_prism_nexus", Vector2.right * 9f);
    void SpawnWildBoss3() => _controller?.Scene?.Spawner?.Spawn("wild_boss_star_hive", Vector2.right * 7f);

    float AddBarButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
      CreateButton(parent, $"Spawn_{label}", label,
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(68f, 32f),
        new Color(0.20f, 0.30f, 0.38f, 1f), action);
      return x + 72f;
    }

    void BuildRightColumn(RectTransform body)
    {
      const float rightWidth = 280f;
      const float rightTopInset = 28f;

      // 右侧面板：锚定右上角，pivot(1,1)，带安全边距
      var right = CreatePanel(body, "RightColumn", ColumnBg,
        new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(rightWidth, 0f));
      right.pivot = new Vector2(1f, 1f);
      right.anchoredPosition = new Vector2(-SandboxSafeArea.RightMargin, -SandboxSafeArea.TopMargin);
      right.anchorMin = new Vector2(1f, 0f);
      right.anchorMax = new Vector2(1f, 1f);
      right.sizeDelta = new Vector2(rightWidth, -(SandboxSafeArea.TopMargin + rightTopInset));
      // 确保面板背景不阻挡点击
      var rightImg = right.GetComponent<Image>();
      if (rightImg != null) rightImg.raycastTarget = false;

      var classDescLabel = CreateLabel(right, "ClassDescHeader", "职业说明", 12, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, 18f));
      classDescLabel.alignment = TextAnchor.MiddleLeft;

      var classDescText = CreateScrollText(right, "ClassDesc", new Vector2(0f, 0.64f), new Vector2(1f, 1f));
      var classDescRt = classDescText.transform.parent.GetComponent<RectTransform>();
      classDescRt.offsetMin = new Vector2(4f, 4f);
      classDescRt.offsetMax = new Vector2(-4f, -20f);
      classDescText.fontSize = 11;
      _classDescPanel = new SandboxClassDescriptionPanel(classDescText);

      var dpsHost = CreatePanel(right, "DpsHost", new Color(0.06f, 0.08f, 0.10f, 0.55f),
        new Vector2(0f, 0.54f), new Vector2(1f, 0.68f), Vector2.zero, Vector2.zero);
      dpsHost.offsetMin = new Vector2(4f, 4f);
      dpsHost.offsetMax = new Vector2(-4f, -4f);
      var dpsHostImg = dpsHost.GetComponent<Image>();
      if (dpsHostImg != null) dpsHostImg.raycastTarget = false;
      CreateLabel(dpsHost, "DpsHeader", "DPS 统计", 12, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -2f), new Vector2(-4f, 16f))
        .alignment = TextAnchor.MiddleLeft;
      var dpsTextGo = new GameObject("DpsText", typeof(RectTransform));
      dpsTextGo.transform.SetParent(dpsHost, false);
      StretchFull(dpsTextGo.GetComponent<RectTransform>(), 4f, 4f, 4f, 18f);
      var dpsText = dpsTextGo.AddComponent<Text>();
      dpsText.font = _font;
      dpsText.fontSize = 11;
      dpsText.alignment = TextAnchor.UpperLeft;
      dpsText.color = Color.white;
      dpsText.horizontalOverflow = HorizontalWrapMode.Wrap;
      dpsText.verticalOverflow = VerticalWrapMode.Overflow;
      dpsText.raycastTarget = false;
      _dpsView = new SandboxDpsView(dpsText);

      _skillBarHost = CreatePanel(right, "SkillBarHost", Color.clear,
        new Vector2(0f, 0f), new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
      _skillBarHost.offsetMin = new Vector2(4f, 4f);
      _skillBarHost.offsetMax = new Vector2(-4f, -4f);

      var detachedHost = CreatePanel(right, "DetachedVfxHost", new Color(0.06f, 0.08f, 0.10f, 0.55f),
        new Vector2(0f, 0.38f), new Vector2(1f, 0.53f), Vector2.zero, Vector2.zero);
      detachedHost.offsetMin = new Vector2(4f, 4f);
      detachedHost.offsetMax = new Vector2(-4f, -4f);
      _detachedVfxPanel = new SandboxDetachedWeaponRegressionPanel(_font, () => _controller?.Scene);
      _detachedVfxPanel.Build(detachedHost);

      _skillBar = new SandboxSkillBarView(
        _font,
        _skillBarHost,
        index =>
        {
          _controller?.CastSkillSlot(index);
          _skillBar?.RefreshActiveStates();
        },
        index => _controller != null && _controller.IsSkillSlotActive(index),
        () =>
        {
          if (_controller?.AutoCombat == null)
            return;
          _controller.SetAutoCastSkills(!_controller.IsAutoCastSkills);
          _skillBar?.RefreshActiveStates();
        },
        () => _controller != null && _controller.IsAutoCastSkills,
        () => _controller?.ResetSkillCooldowns());
    }

    void BuildBottomLog(Transform panel)
    {
      const float bottomHeight = 104f;

      var buildHost = CreatePanel(panel, "BuildDescHost", ColumnBg,
        new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(280f, bottomHeight));
      buildHost.anchorMin = Vector2.zero;
      buildHost.anchorMax = new Vector2(0f, 0f);
      buildHost.pivot = new Vector2(0f, 0f);
      buildHost.offsetMin = new Vector2(8f, 8f);
      buildHost.offsetMax = new Vector2(288f, bottomHeight);

      CreateLabel(buildHost, "BuildDescHeader", "当前 Build", 13, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -2f), new Vector2(-8f, 18f))
        .alignment = TextAnchor.MiddleLeft;

      // Current Archetype 调试标签
      var archLabel = CreateLabel(buildHost, "CurrentArchetype", "Current Archetype: Ranged", 10, FontStyle.Normal,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -18f), new Vector2(-8f, 14f));
      archLabel.alignment = TextAnchor.MiddleLeft;
      archLabel.color = Accent;
      _currentArchetypeLabel = archLabel;

      var buildText = CreateScrollText(buildHost, "BuildDesc", Vector2.zero, Vector2.one);
      buildText.fontSize = 10;
      buildText.color = TextDim;
      _buildDescPanel = new SandboxBuildDescriptionPanel(buildText);

      var logHost = CreatePanel(panel, "LogHost", ColumnBg,
        new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, bottomHeight));
      logHost.anchorMin = Vector2.zero;
      logHost.anchorMax = new Vector2(1f, 0f);
      logHost.pivot = new Vector2(0.5f, 0f);
      logHost.offsetMin = new Vector2(296f, 8f);
      logHost.offsetMax = new Vector2(-8f, bottomHeight);

      CreateLabel(logHost, "LogHeader", "事件日志", 13, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -2f), new Vector2(-8f, 18f))
        .alignment = TextAnchor.MiddleLeft;

      var logText = CreateScrollText(logHost, "LogText", Vector2.zero, Vector2.one);
      logText.fontSize = 10;
      logText.color = TextDim;
      _eventLog = new RuntimeEventLogView(logText);
    }

    void BuildTopControls(Transform panel)
    {
      var bar = CreatePanel(panel, "ControlBar", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, 32f));
      bar.anchorMin = new Vector2(0f, 1f);
      bar.anchorMax = new Vector2(1f, 1f);
      bar.pivot = new Vector2(0.5f, 1f);

      var x = 300f;
      x = AddCtrl(bar.transform, "Reset All", x, () =>
      {
        _controller.ResetSandbox();
        RebuildBuildTree();
        RefreshBuildPanels();
        _eventLog?.Refresh();
      });
      x = AddCtrl(bar.transform, "Reset Build", x, () =>
      {
        _controller.ResetContext();
        RebuildBuildTree();
        RefreshBuildPanels();
      });
      x = AddCtrl(bar.transform, "Reset DPS", x, () => _controller.ResetDps());
      x = AddCtrl(bar.transform, "Clear Events", x, () => { _controller.ClearEvents(); _eventLog?.Refresh(); });
    }

    float AddCtrl(Transform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
      CreateButton(parent, $"Ctrl_{label}", label,
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(100f, 26f),
        new Color(0.22f, 0.34f, 0.44f, 1f), action);
      return x + 106f;
    }

    static void StretchFull(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = new Vector2(left, bottom);
      rt.offsetMax = new Vector2(-right, -top);
    }

    RectTransform BuildScrollArea(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 offsetMin, Vector2 offsetMax)
    {
      var scrollGo = new GameObject(name, typeof(RectTransform));
      scrollGo.transform.SetParent(parent, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = anchorMin;
      scrollRt.anchorMax = anchorMax;
      scrollRt.offsetMin = offsetMin;
      scrollRt.offsetMax = offsetMax;

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false;
      scroll.vertical = true;

      var viewport = CreatePanel(scrollGo.transform, "Viewport", new Color(0.06f, 0.08f, 0.10f, 0.5f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      StretchFull(viewport);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

      var content = CreatePanel(viewport, "Content", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 400f));
      content.pivot = new Vector2(0.5f, 1f);
      scroll.viewport = viewport;
      scroll.content = content;
      return content;
    }

    Text CreateScrollText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
      var host = CreatePanel(parent, name + "Host", new Color(0.06f, 0.08f, 0.10f, 0.55f),
        anchorMin, anchorMax, Vector2.zero, Vector2.zero);
      host.offsetMin = new Vector2(4f, 4f);
      host.offsetMax = new Vector2(-4f, -4f);

      var textGo = new GameObject("Text", typeof(RectTransform));
      textGo.transform.SetParent(host, false);
      StretchFull(textGo.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 10;
      text.alignment = TextAnchor.UpperLeft;
      text.color = Color.white;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Overflow;
      text.raycastTarget = false;
      text.text = "-";
      return text;
    }

    RectTransform CreatePanel(Transform parent, string name, Color bg,
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

      if (bg.a > 0.001f)
      {
        var img = go.AddComponent<Image>();
        img.color = bg;
        img.raycastTarget = bg.a > 0.01f;
      }

      return rt;
    }

    Text CreateLabel(Transform parent, string name, string text, int fontSize, FontStyle style,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0.5f, 0.5f);
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
      return label;
    }

    Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 anchoredPos, Vector2 sizeDelta, Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, name, bg, anchorMin, anchorMax, anchoredPos, sizeDelta);
      var btn = rt.gameObject.AddComponent<Button>();
      btn.targetGraphic = rt.GetComponent<Image>();

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(rt, false);
      StretchFull(textGo.GetComponent<RectTransform>(), 4f, 2f, 4f, 2f);

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 10;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      text.text = label;
      text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Truncate;

      if (onClick != null)
        btn.onClick.AddListener(onClick);

      return btn;
    }
  }
}
