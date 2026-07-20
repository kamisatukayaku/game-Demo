using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;
using UnityEngine;

using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>
  /// 成长预览面板：展示所有单条升级属性的初始状态与获取后效果对比，支持动画过渡?
  /// 直接调用 LevelUpChoiceDatabase / BuildStatRepository / ThemeState 等实际游戏实现?
  /// </summary>
  public class GrowthPreviewUI : MonoBehaviour
  {
    static GrowthPreviewUI s_instance;

    static readonly Color PanelBg     = new(0.08f, 0.12f, 0.16f, 0.98f);
    static readonly Color Accent      = new(0.46f, 0.78f, 0.92f, 1f);
    static readonly Color NormalBg    = new(0.14f, 0.20f, 0.26f, 1f);
    static readonly Color SelectedBg  = new(0.22f, 0.50f, 0.60f, 1f);
    static readonly Color DetailBg    = new(0.06f, 0.09f, 0.12f, 0.92f);
    static readonly Color TextDim     = new(0.65f, 0.75f, 0.82f, 1f);
    static readonly Color RoutePlayer    = new(0.35f, 0.58f, 0.78f, 1f);
    static readonly Color RouteEquipment = new(0.82f, 0.55f, 0.28f, 1f);
    static readonly Color RouteSkill     = new(0.72f, 0.38f, 0.82f, 1f);
    static readonly Color PositiveGreen  = new(0.35f, 0.82f, 0.42f, 1f);

    const int PreviewCanvasSortOrder = 650;

    Font _font;
    GameObject _panel;
    RectTransform _listContent;
    ScrollRect _listScroll;
    Text _detailTitle;
    Text _detailRoute;
    Text _detailDesc;
    RectTransform _statContainer;
    Text _statHeader;

    // 当前流派（由 RoguelikeGameMode 传入）
    string _weaponTheme = "ranged";
    string _selectedId;

    // 所有升级条目（按路线分组）
    readonly List<UpgradeEntry> _allUpgrades = new();
    // BuildStatRepository 快照（模拟前后恢复用?
    readonly Dictionary<string, float> _repoSnapshot = new();
    // 当前选中升级?before/after 对比数据
    readonly List<StatComparison> _comparisons = new();
    Coroutine _animRoutine;

    // ==================== 公开 API ====================

    public static bool IsOpen =>
      s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;

    /// <summary>打开面板，weaponTheme 决定模拟时使用的流派基础种子?/summary>
    public static void Open(Transform parent, string weaponTheme)
    {
      EnsureExists(parent);
      s_instance._weaponTheme = string.IsNullOrEmpty(weaponTheme) ? "ranged" : weaponTheme;
      s_instance.Show();
    }

    static void EnsureExists(Transform parent)
    {
      if (s_instance != null) return;
      var go = new GameObject("_GrowthPreviewUI");
      go.transform.SetParent(parent, false);
      go.AddComponent<GrowthPreviewUI>();
    }

    // ==================== 生命周期 ====================

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildUI();
      Close();
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
      RestoreRepository();
    }

    void Update()
    {
      if (!IsOpen) return;
      if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ==================== 显示/隐藏 ====================

    void Show()
    {
      UiBootstrap.EnsureEventSystem();
      SaveRepository();
      LoadUpgrades();
      RebuildList();

      if (_allUpgrades.Count > 0)
        SelectUpgrade(_allUpgrades[0]);
      else
        ClearDetail();
      _panel.SetActive(true);
    }

    void Close()
    {
      if (_panel != null) _panel.SetActive(false);
      RestoreRepository();
      StopAnim();
    }

    // ==================== 数据加载 ====================

    void LoadUpgrades()
    {
      _allUpgrades.Clear();

      // 直接读取原始升级数据（来?data/roguelike/upgrades/*.json），未经过滤
      var allByRoute = LevelUpChoiceDatabase.GetAllUpgradesForClass(_weaponTheme);

      var routeOrder = new Dictionary<string, int>
      {
        { "equipment", 0 }, { "skill", 1 }, { "player", 2 }
      };

      foreach (var kv in allByRoute)
      {
        var route = kv.Key;
        foreach (var def in kv.Value)
        {
          if (def == null || string.IsNullOrEmpty(def.id)) continue;
          if (def.modifiers == null || def.modifiers.Length == 0) continue;

          if (route == "equipment" && !string.IsNullOrEmpty(def.weapon_theme)
              && def.weapon_theme != _weaponTheme)
            continue;

          if (!MatchesCurrentClass(def)) continue;

          if (EvolutionBuildGatesDatabase.IsUpgradeBlocked(def, ArenaBuildBootstrap.SelectedBuildId, null))
            continue;

          _allUpgrades.Add(new UpgradeEntry { Def = def, Route = route });
        }
      }

      // 按路??名称排序
      _allUpgrades.Sort((a, b) =>
      {
        var ra = routeOrder.TryGetValue(a.Route, out var oa) ? oa : 99;
        var rb = routeOrder.TryGetValue(b.Route, out var ob) ? ob : 99;
        if (ra != rb) return ra.CompareTo(rb);
        return string.CompareOrdinal(a.Def.display_name, b.Def.display_name);
      });
    }

    /// <summary>
    /// 检查升级是否匹配当前选中的职业?
    /// player 路线（通用属性）始终可见；其他路线检?classes 字段?
    /// </summary>
    bool MatchesCurrentClass(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def?.classes == null || def.classes.Length == 0)
        return true; // ?classes 字段则默认可觀"

      foreach (var c in def.classes)
      {
        if (c == "all" || c == _weaponTheme)
          return true;
      }

      return false;
    }

    // ==================== 模拟引擎（核心） ====================

    /// <summary>保存当前 BuildStatRepository 状态?/summary>
    void SaveRepository()
    {
      _repoSnapshot.Clear();
      foreach (var kv in BuildStatRepository.Stats)
        _repoSnapshot[kv.Key] = kv.Value;
    }

    /// <summary>恢复保存?BuildStatRepository 状态?/summary>
    void RestoreRepository()
    {
      if (_repoSnapshot.Count == 0) return;
      BuildStatRepository.Clear();
      foreach (var kv in _repoSnapshot)
        BuildStatRepository.SetStat(kv.Key, kv.Value);
    }

    /// <summary>
    /// 模拟一条升级：清空 repo ?播种流派基础 ?依次应用属??记录前后值?
    /// 调用实际?ThemeState.SeedThemeBaseStats() ?BuildProgressionState.ApplyModifiers 等价逻辑?
    /// </summary>
    List<StatComparison> SimulateUpgrade(LevelUpChoiceDatabase.UpgradeDef def)
    {
      var result = new List<StatComparison>();
      if (def?.modifiers == null) return result;

      // 1. 清空 repo + 播种流派基础属性（Before 状态）
      BuildStatRepository.Clear();
      ThemeState.Reset(_weaponTheme);
      ThemeState.SeedThemeBaseStats();

      // 2. 记录 Before 值"
      var beforeValues = new Dictionary<string, float>();
      foreach (var mod in def.modifiers)
      {
        if (mod == null || string.IsNullOrEmpty(mod.stat)) continue;
        if (!beforeValues.ContainsKey(mod.stat))
          beforeValues[mod.stat] = BuildStatRepository.GetStat(mod.stat);
      }

      // 3. 应用升级属性（After 状态）?等价?BuildProgressionState.ApplyModifiers
      foreach (var mod in def.modifiers)
      {
        if (mod == null || string.IsNullOrEmpty(mod.stat)) continue;
        var cur = BuildStatRepository.GetStat(mod.stat);
        if (mod.op == "mul")
          BuildStatRepository.SetStat(mod.stat, cur * (1f + mod.value));
        else
          BuildStatRepository.SetStat(mod.stat, cur + mod.value);
      }

      // 4. 记录 After 值"
      foreach (var kv in beforeValues)
      {
        var after = BuildStatRepository.GetStat(kv.Key);
        var before = kv.Value;
        result.Add(new StatComparison
        {
          StatKey    = kv.Key,
          Before     = before,
          After      = after,
          RawDelta   = after - before,
          Op         = ResolveOpForStat(def.modifiers, kv.Key)
        });
      }

      // 5. 恢复原始 repo
      RestoreRepository();
      return result;
    }

    static string ResolveOpForStat(LevelUpChoiceDatabase.StatModifier[] mods, string statKey)
    {
      foreach (var m in mods)
        if (m != null && m.stat == statKey) return m.op ?? "add";
      return "add";
    }

    static string ExtractEvolutionId(string upgradeId)
    {
      if (string.IsNullOrEmpty(upgradeId) || !upgradeId.StartsWith("evo_"))
        return null;

      var parts = upgradeId.Split('_');
      return parts.Length >= 2 ? parts[1] : null;
    }

    // ==================== UI 构建 ====================

    void BuildUI()
    {
      var canvasGo = new GameObject("GrowthPreviewCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = PreviewCanvasSortOrder;
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, PreviewCanvasSortOrder);
      canvasGo.AddComponent<GraphicRaycaster>();

      // 主面杀"
      _panel = CreatePanel(canvasGo.transform, "GrowthPreviewPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 660f)).gameObject;

      // 标题
      CreateLabel(_panel.transform, "Title", "成长预览", 30, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(400f, 40f));

      CreateLabel(_panel.transform, "Hint", "查看所有升级属性对各项数值的影响 · 选择左侧条目查看前后对比 · Esc 返回",
        13, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(600f, 22f))
        .color = TextDim;

      // 内容区域
      var content = CreatePanel(_panel.transform, "Content", Color.clear,
        new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      content.anchorMin = new Vector2(0f, 0f);
      content.anchorMax = new Vector2(1f, 1f);
      content.offsetMin = new Vector2(16f, 56f);
      content.offsetMax = new Vector2(-16f, -72f);

      BuildListColumn(content);
      BuildDetailColumn(content);

      // 返回按钮
      CreateButton(_panel.transform, "BackButton", "返回",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(180f, 42f),
        NormalBg, Close);
    }

    void BuildListColumn(Transform parent)
    {
      var listHost = CreatePanel(parent, "ListHost", NormalBg,
        new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 0f));
      listHost.offsetMin = new Vector2(0f, 0f);
      listHost.offsetMax = new Vector2(260f, 0f);

      var scrollGo = new GameObject("Scroll", typeof(RectTransform));
      scrollGo.transform.SetParent(listHost, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = Vector2.zero;
      scrollRt.anchorMax = Vector2.one;
      scrollRt.offsetMin = new Vector2(4f, 4f);
      scrollRt.offsetMax = new Vector2(-4f, -4f);

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false;
      scroll.movementType = ScrollRect.MovementType.Clamped;

      var viewport = CreatePanel(scrollGo.transform, "Viewport", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      scroll.viewport = viewport;

      var scContent = CreatePanel(viewport, "Content", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f));
      scContent.anchorMin = new Vector2(0f, 1f);
      scContent.anchorMax = new Vector2(1f, 1f);
      scContent.pivot = new Vector2(0.5f, 1f);

      var layout = scContent.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 3f;
      layout.padding = new RectOffset(4, 4, 4, 4);
      layout.childControlHeight = true;
      layout.childControlWidth = true;
      layout.childForceExpandHeight = false;
      layout.childForceExpandWidth = true;

      scContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      scroll.content = scContent;
      _listContent = scContent;
      _listScroll = scroll;
    }

    void BuildDetailColumn(Transform parent)
    {
      var detailHost = CreatePanel(parent, "DetailHost", DetailBg,
        new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      detailHost.offsetMin = new Vector2(270f, 0f);
      detailHost.offsetMax = new Vector2(0f, 0f);

      _detailTitle = CreateLabel(detailHost, "DetailTitle", "", 22, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -16f), new Vector2(-32f, 28f));
      _detailTitle.alignment = TextAnchor.MiddleLeft;

      _detailRoute = CreateLabel(detailHost, "DetailRoute", "", 14, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -46f), new Vector2(-32f, 20f));
      _detailRoute.alignment = TextAnchor.MiddleLeft;

      _detailDesc = CreateLabel(detailHost, "DetailDesc", "", 14, FontStyle.Normal,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -72f), new Vector2(-32f, 40f));
      _detailDesc.alignment = TextAnchor.UpperLeft;
      _detailDesc.color = new Color(0.88f, 0.92f, 0.95f, 1f);

      _statHeader = CreateLabel(detailHost, "StatHeader", "选择左侧升级条目查看属性对比", 15, FontStyle.Normal,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -120f), new Vector2(-32f, 20f));
      _statHeader.alignment = TextAnchor.MiddleLeft;
      _statHeader.color = TextDim;

      // 属性对比容器（可滚动）
      var statScrollGo = new GameObject("StatScroll", typeof(RectTransform));
      statScrollGo.transform.SetParent(detailHost, false);
      var statScrollRt = statScrollGo.GetComponent<RectTransform>();
      statScrollRt.anchorMin = new Vector2(0f, 0f);
      statScrollRt.anchorMax = new Vector2(1f, 1f);
      statScrollRt.offsetMin = new Vector2(16f, 16f);
      statScrollRt.offsetMax = new Vector2(-16f, -148f);

      var statScroll = statScrollGo.AddComponent<ScrollRect>();
      statScroll.horizontal = false;
      statScroll.movementType = ScrollRect.MovementType.Clamped;

      var statViewport = CreatePanel(statScrollGo.transform, "StatViewport", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      statScroll.viewport = statViewport;

      _statContainer = CreatePanel(statViewport, "StatContent", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f));
      _statContainer.anchorMin = new Vector2(0f, 1f);
      _statContainer.anchorMax = new Vector2(1f, 1f);
      _statContainer.pivot = new Vector2(0.5f, 1f);

      var statLayout = _statContainer.gameObject.AddComponent<VerticalLayoutGroup>();
      statLayout.spacing = 8f;
      statLayout.padding = new RectOffset(4, 4, 4, 4);
      statLayout.childControlHeight = true;
      statLayout.childControlWidth = true;
      statLayout.childForceExpandHeight = false;
      statLayout.childForceExpandWidth = true;

      _statContainer.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      statScroll.content = _statContainer;
    }

    // ==================== 列表逻辑 ====================

    void RebuildList()
    {
      if (_listContent == null) return;

      for (var i = _listContent.childCount - 1; i >= 0; i--)
        Destroy(_listContent.GetChild(i).gameObject);

      string currentRoute = null;
      foreach (var entry in _allUpgrades)
      {
        // 路线分组标题
        if (entry.Route != currentRoute)
        {
          currentRoute = entry.Route;
          var header = CreateLabel(_listContent, $"RouteHeader_{currentRoute}",
            $"{GetRouteDisplay(currentRoute)}", 15, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 32f));
          header.alignment = TextAnchor.MiddleLeft;
          header.color = GetRouteColor(currentRoute);

          var headerLayout = header.gameObject.AddComponent<LayoutElement>();
          headerLayout.preferredHeight = 32f;
        }

        var captured = entry;
        var label = $"  {entry.Def.display_name}";
        if (entry.Def.tier > 0)
          label += $" <size=11>(T{entry.Def.tier})</size>";

        var btn = CreateButton(_listContent, $"Upg_{entry.Def.id}", label,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 44f),
          NormalBg, () => SelectUpgrade(captured));

        var btnLayout = btn.gameObject.AddComponent<LayoutElement>();
        btnLayout.preferredHeight = 44f;

        var tag = btn.gameObject.AddComponent<UpgradeListTag>();
        tag.UpgradeId = entry.Def.id;
      }

      var pad = new GameObject("BottomPad", typeof(RectTransform));
      pad.transform.SetParent(_listContent, false);
      pad.AddComponent<LayoutElement>().preferredHeight = 12f;
    }

    void SelectUpgrade(UpgradeEntry entry)
    {
      _selectedId = entry.Def.id;
      RefreshListSelection();

      // 执行真实模拟
      _comparisons.Clear();
      _comparisons.AddRange(SimulateUpgrade(entry.Def));

      // 更新详情
      if (_detailTitle != null)
        _detailTitle.text = entry.Def.display_name;

      if (_detailDesc != null)
      {
        var desc = entry.Def.description ?? "";
        _detailDesc.text = desc;
      }

      if (_detailRoute != null)
      {
        var routeLine = $"{GetRouteDisplay(entry.Route)} · Tier {entry.Def.tier}";
        var evolutionId = !string.IsNullOrEmpty(entry.Def.mechanic_id)
          ? entry.Def.mechanic_id
          : ExtractEvolutionId(entry.Def.id);
        var verb = EvolutionFantasyDatabase.GetBehaviorVerb(evolutionId);
        if (!string.IsNullOrEmpty(verb))
          routeLine += $" · {verb}";
        _detailRoute.text = routeLine;
        _detailRoute.color = GetRouteColor(entry.Route);
      }

      if (_statHeader != null)
        _statHeader.text = _comparisons.Count > 0
          ? $"属性影响（{_comparisons.Count} 项）"
          : "该升级无直接属性影响";

      BuildComparisonRows();
      _animRoutine = StartCoroutine(AnimateComparisons());
    }

    void RefreshListSelection()
    {
      if (_listContent == null) return;
      foreach (var tag in _listContent.GetComponentsInChildren<UpgradeListTag>(true))
      {
        var img = tag.GetComponent<Image>();
        if (img != null)
          img.color = tag.UpgradeId == _selectedId ? SelectedBg : NormalBg;
      }
    }

    void ClearDetail()
    {
      if (_detailTitle != null) _detailTitle.text = "暂无数据";
      if (_detailRoute != null) _detailRoute.text = "";
      if (_detailDesc != null) _detailDesc.text = "";
      if (_statHeader != null) _statHeader.text = "选择左侧升级条目查看属性对比";
      ClearComparisonRows();
    }

    // ==================== 属性对?UI ====================

    void ClearComparisonRows()
    {
      StopAnim();
      if (_statContainer == null) return;
      for (var i = _statContainer.childCount - 1; i >= 0; i--)
        Destroy(_statContainer.GetChild(i).gameObject);
    }

    void BuildComparisonRows()
    {
      ClearComparisonRows();
      _statContainer.gameObject.SetActive(false);
      if (_statContainer == null || _comparisons.Count == 0) return;

      foreach (var cmp in _comparisons)
      {
        var row = CreatePanel(_statContainer, $"Stat_{cmp.StatKey}",
          new Color(0f, 0f, 0f, 0.12f),
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
          Vector2.zero, new Vector2(0f, 52f));
        var rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 52f;

        // 属性名
        var nameLabel = CreateLabel(row, "StatName", GetStatDisplayName(cmp.StatKey), 14, FontStyle.Bold,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(170f, 22f));
        nameLabel.alignment = TextAnchor.MiddleLeft;

        // 操作类型标签
        var opLabel = CreateLabel(row, "OpLabel", cmp.Op == "mul" ? "×" : "+", 11, FontStyle.Normal,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -16f), new Vector2(32f, 14f));
        opLabel.alignment = TextAnchor.MiddleLeft;
        opLabel.color = TextDim;

        // 初始值"
        var beforeText = CreateLabel(row, "BeforeVal",
          $"初始: {FormatValue(cmp.Before)}", 13, FontStyle.Normal,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(184f, 6f), new Vector2(140f, 18f));
        beforeText.alignment = TextAnchor.MiddleLeft;
        beforeText.color = new Color(0.72f, 0.78f, 0.84f, 1f);

        // 升级后值"
        var afterText = CreateLabel(row, "AfterVal",
          $"获取后 {FormatValue(cmp.After)}", 14, FontStyle.Bold,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(184f, -12f), new Vector2(140f, 20f));
        afterText.alignment = TextAnchor.MiddleLeft;
        afterText.color = Color.white;

        // 差值"
        var delta = cmp.After - cmp.Before;
        var deltaStr = delta >= 0.001f ? $"+{FormatValue(delta)}"
          : (delta <= -0.001f ? FormatValue(delta) : "0");
        var deltaText = CreateLabel(row, "Delta", deltaStr, 15, FontStyle.Bold,
          new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(100f, 26f));
        deltaText.alignment = TextAnchor.MiddleRight;
        deltaText.color = Mathf.Abs(delta) < 0.001f ? TextDim
          : (delta > 0f ? PositiveGreen : new Color(0.95f, 0.30f, 0.30f, 1f));
      }

      _statContainer.gameObject.SetActive(true);
    }

    IEnumerator AnimateComparisons()
    {
      if (_statContainer == null) yield break;

      var rows = _statContainer.GetComponentsInChildren<LayoutElement>(true);
      if (rows == null || rows.Length == 0) yield break;

      const float duration = 0.25f;
      float elapsed = 0f;

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        if (_statContainer == null) yield break;

        float t = Mathf.Clamp01(elapsed / duration);
        float alpha = t;

        foreach (var le in rows)
        {
          if (le == null) continue;
          var rt = le.GetComponent<RectTransform>();
          if (rt == null) continue;
          var img = le.GetComponent<Image>();
          if (img != null)
          {
            var c = img.color;
            c.a = 0.12f * alpha;
            img.color = c;
          }
        }
        yield return null;
      }
    }

    void StopAnim()
    {
      if (_animRoutine != null)
      {
        StopCoroutine(_animRoutine);
        _animRoutine = null;
      }
    }

    // ==================== 格式化工?====================

    static string FormatValue(float v)
    {
      if (Mathf.Abs(v) >= 100f)  return v.ToString("0");
      if (Mathf.Abs(v) >= 10f)   return v.ToString("0.#");
      if (Mathf.Abs(v) >= 1f)    return v.ToString("0.##");
      if (Mathf.Abs(v) >= 0.01f) return v.ToString("0.###");
      if (v == 0f)               return "0";
      return v.ToString("0.####");
    }

    static string GetRouteDisplay(string route) => route switch
    {
      "equipment" => "武装",
      "skill"     => "技能",
      "player"    => "属性",
      _           => route ?? ""
    };

    static Color GetRouteColor(string route) => route switch
    {
      "equipment" => RouteEquipment,
      "skill"     => RouteSkill,
      "player"    => RoutePlayer,
      _           => TextDim
    };

    /// <summary>?stat key 转为中文显示名?/summary>
    static string GetStatDisplayName(string key)
    {
      if (string.IsNullOrEmpty(key)) return key;

      return key switch
      {
        // 武器
        "weapon_damage_mult"       => "武器伤害倍率",
        "weapon_attack_speed_mult" => "武器攻速倍率",
        "weapon_range_add"         => "武器范围加成",
        "weapon_flat_add"          => "武器基础伤害",
        "weapon_extra_projectile"  => "额外弹体",

        // 技能"
        "skill_damage_mult"        => "技能伤害倍率",
        "skill_cooldown_reduce"    => "技能冷却缩减",
        "skill_range_mult"         => "技能范围倍率",
        "skill_extra_projectile"   => "技能额外弹体",
        "skill_crit_chance"        => "技能暴击率",
        "skill_crit_damage"        => "技能暴击伤害",
        "skill_slow_chance"        => "技能减速概率",
        "skill_slow_amount"        => "技能减速量",
        "skill_burn_dps"           => "技能燃烧伤害",
        "skill_burn_duration"      => "技能燃烧持续",
        "skill_chain_count"        => "技能弹射次数",
        "skill_pierce"             => "技能穿透",
        "skill_explosion_radius"   => "技能爆炸半径",
        "skill_explosion_ratio"    => "技能爆炸比例",
        "skill_echo"               => "技能回响",
        "skill_echo_guarantee"     => "技能回响保底",
        "skill_vacuum"             => "技能真空",
        "skill_vacuum_duration"    => "真空持续时间",
        "skill_vacuum_strength"    => "真空强度",
        "skill_vacuum_split"       => "真空分裂",
        "skill_vacuum_overlap_amp" => "真空重叠加成",
        "skill_vacuum_trail"       => "真空轨迹",
        "skill_vacuum_ramp_damage" => "真空递增伤害",
        "skill_mirror_cast"        => "技能镜像施法",
        "skill_split_on_hit"       => "技能命中分裂",
        "skill_homing"             => "技能追踪",
        "skill_homing_turn"        => "技能追踪转角",
        "skill_volley_on_cast"     => "技能齐射",
        "skill_cd_reset_chance"    => "技能CD重置概率",
        "skill_time_stop_chance"   => "技能时停概率",
        "skill_time_rewind"        => "技能时间回溯",
        "skill_element_burst"      => "元素爆发",
        "skill_burst_radius_bonus" => "爆发半径加成",
        "skill_element_melt"       => "元素融化",
        "skill_element_overload"   => "元素超载",
        "skill_time_dilation_field"=> "时间膨胀领域",
        "skill_pct_hp_damage"      => "百分比血量伤害",
        "skill_vulnerable_bonus"   => "易伤加成",
        "skill_collapse_explosion" => "坍缩爆炸",
        "skill_zone_collapse"      => "区域坍缩",
        "skill_collapse_radius_bonus"=>"坍缩半径加成",
        "skill_pulse_damage"       => "脉冲突击伤害",
        "skill_chain_damage_ratio" => "弹射伤害比例",

        // 玩家属性
        "max_hp_mult"              => "最大生命倍率",
        "max_hp_flat"              => "最大生命",
        "move_speed_mult"          => "移速倍率",
        "hp_regen"                 => "生命回复",
        "damage_reduction"         => "伤害减免",
        "all_resist"               => "全抗性",
        "overheal_shield"          => "过量治疗护盾",
        "knockback_resist"         => "击退抗性",
        "exp_gain_mult"            => "经验获取倍率",
        "all_damage_mult"          => "全伤害倍率",
        "crit_chance"              => "暴击率",
        "crit_damage_mult"         => "暴击伤害倍率",
        "lifesteal"                => "生命偷取",
        "heal_on_hit_pct"          => "命中治疗",
        "heal_on_kill_pct"         => "击杀治疗",
        "elite_damage_mult"        => "精英伤害倍率",
        "boss_damage_mult"         => "Boss伤害倍率",
        "long_range_damage_mult"   => "远程伤害倍率",
        "slow_target_damage_mult"  => "减速目标伤害倍率",

        // 弹体
        "projectile_pierce"        => "弹体穿透",
        "pierce_no_falloff"        => "穿透无衰减",
        "projectile_explosion_radius"=>"弹体爆炸半径",
        "projectile_explosion_ratio" =>"弹体爆炸比例",
        "explosion_damage_mult"    => "爆炸伤害倍率",
        "projectile_chain_count"   => "弹体弹射次数",
        "projectile_chain_damage_ratio"=>"弹射伤害比例",
        "projectile_chain_jump_range"=>"弹射跳跃距离",
        "projectile_weak_homing"   => "弱追踪",
        "projectile_homing_turn_add"=>"追踪转向加成",
        "projectile_side_shed"     => "侧向散弹",
        "projectile_trail_spray"   => "轨迹喷洒",
        "projectile_split_on_hit"  => "命中分裂",
        "projectile_heavy_shot"    => "重型射击",
        "projectile_speed_mult"    => "弹速倍率",
        "projectile_slow_chance"   => "弹体减速概率",
        "projectile_slow_amount"   => "弹体减速量",
        "projectile_burn_dps"      => "弹体燃烧伤害",
        "projectile_burn_duration" => "弹体燃烧持续",
        "explosion_vacuum"         => "爆炸真空",

        // 近战
        "melee_explosion_radius"   => "近战爆炸半径",
        "melee_knockback_chance"   => "近战击退概率",
        "melee_slow_chance"        => "近战减速概率",
        "melee_slow_amount"        => "近战减速量",
        "melee_bleed_dps"          => "近战流血伤害",
        "melee_bleed_duration"     => "近战流血持续",
        "melee_burn_dps"           => "近战燃烧伤害",
        "melee_burn_duration"      => "近战燃烧持续",

        // 连杀/低血/移?暴击/幸运
        "kill_streak_crit_stacks"  => "连杀暴击层数",
        "kill_streak_crit_bonus"   => "连杀暴击加成",
        "kill_streak_damage_stacks"=> "连杀伤害层数",
        "kill_streak_damage_bonus" => "连杀伤害加成",
        "kill_streak_attack_speed" => "连杀攻速",
        "kill_move_speed_bonus"    => "击杀移速加成",
        "missing_hp_damage_mult"   => "低血伤害倍率",
        "missing_hp_damage_reduction"=>"低血伤害减免",
        "missing_hp_attack_speed"  => "低血攻速",
        "missing_hp_lifesteal"     => "低血偷取",
        "move_speed_damage_ratio"  => "移速转伤害",
        "move_speed_crit_ratio"    => "移速转暴击",
        "crit_attack_speed_bonus"  => "暴击攻速加成",
        "lucky_double_proc"        => "幸运双倍触发",
        "lucky_kill_xp_bonus"      => "幸运击杀经验",
        "lucky_drop_bonus"         => "幸运掉落加成",
        "projectile_homing"        => "弹体追踪",

        _ => key
      };
    }

    // ==================== UI 工坊 ====================

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
      return label;
    }

    Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 anchoredPos, Vector2 sizeDelta, Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, name, bg, anchorMin, anchorMax, anchoredPos, sizeDelta);
      var btn = rt.gameObject.AddComponent<Button>();
      btn.targetGraphic = rt.GetComponent<Image>();

      var colors = btn.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
      colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
      colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
      btn.colors = colors;

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(rt, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = new Vector2(8f, 4f);
      textRt.offsetMax = new Vector2(-8f, -4f);

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 14;
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

    // ==================== 内部类型 ====================

    class UpgradeEntry
    {
      public LevelUpChoiceDatabase.UpgradeDef Def;
      public string Route;
    }

    struct StatComparison
    {
      public string StatKey;
      public float Before;
      public float After;
      public float RawDelta;
      public string Op;
    }

    sealed class UpgradeListTag : MonoBehaviour
    {
      public string UpgradeId;
    }


  }
}
