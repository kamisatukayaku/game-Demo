using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>Build 图鉴与成长线详情面板（竞技场入口 · Esc 关闭）。</summary>
  public sealed class BuildCodexDetailUI : MonoBehaviour
  {
    enum Tab { Growth, Archive }

    static BuildCodexDetailUI s_instance;
    static CanvasGroup s_lobbyRaycastRoot;

    public static void SetLobbyRaycastRoot(CanvasGroup root) => s_lobbyRaycastRoot = root;

    static readonly Color PanelBg = new(0.08f, 0.12f, 0.16f, 0.98f);
    static readonly Color Accent = new(0.46f, 0.78f, 0.92f, 1f);
    static readonly Color NormalBg = new(0.14f, 0.20f, 0.26f, 1f);
    static readonly Color SelectedBg = new(0.22f, 0.50f, 0.60f, 1f);
    static readonly Color DetailBg = new(0.06f, 0.09f, 0.12f, 0.92f);
    static readonly Color TextDim = new(0.65f, 0.75f, 0.82f, 1f);
    static readonly Color LockedBg = new(0.10f, 0.12f, 0.14f, 0.75f);
    const int CanvasSortOrder = 660;

    static readonly string[] CodexBuildIds =
    {
      ArenaBuildBootstrap.Unified,
      ArenaBuildBootstrap.Mage,
      ArenaBuildBootstrap.Shooter,
      ArenaBuildBootstrap.Contact
    };

    Font _font;
    GameObject _overlayRoot;
    GameObject _backdrop;
    GameObject _panel;
    RectTransform _listContent;
    ScrollRect _listScroll;
    Text _detailTitle;
    Text _detailSubtitle;
    Text _detailBody;
    Image _accentBar;
    Image _tabGrowthBg;
    Image _tabArchiveBg;
    Tab _tab = Tab.Growth;
    string _selectedId;

    public static bool IsOpen =>
      s_instance != null && s_instance._overlayRoot != null && s_instance._overlayRoot.activeSelf;

    public static void Open(Transform parent = null)
    {
      EnsureExists();
      s_instance.Show();
    }

    static void EnsureExists(Transform parent = null)
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_BuildCodexDetailUI");
      Object.DontDestroyOnLoad(go);
      go.AddComponent<BuildCodexDetailUI>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      Object.DontDestroyOnLoad(gameObject);
      _font = UiFontHelper.GetFont();
      BuildUI();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      if (!IsOpen)
        return;
      if (Input.GetKeyDown(KeyCode.Escape))
        Close();
    }

    void Show()
    {
      UiBootstrap.EnsureEventSystem();
      SelectDefault();
      SetLobbyBlocked(true);
      if (_overlayRoot != null)
        _overlayRoot.SetActive(true);
    }

    void Close()
    {
      SetLobbyBlocked(false);
      if (_overlayRoot != null)
        _overlayRoot.SetActive(false);
    }

    static void SetLobbyBlocked(bool blocked)
    {
      if (s_lobbyRaycastRoot == null)
        return;

      s_lobbyRaycastRoot.blocksRaycasts = !blocked;
      s_lobbyRaycastRoot.interactable = !blocked;
    }

    void SelectDefault()
    {
      _selectedId = _tab == Tab.Growth ? GrowthCatalog[0].id : CodexBuildIds[0];
      RebuildList();
      RefreshDetail();
    }

    void SwitchTab(Tab tab)
    {
      _tab = tab;
      RefreshTabVisuals();
      SelectDefault();
    }

    void RefreshTabVisuals()
    {
      if (_tabGrowthBg != null)
        _tabGrowthBg.color = _tab == Tab.Growth ? SelectedBg : NormalBg;
      if (_tabArchiveBg != null)
        _tabArchiveBg.color = _tab == Tab.Archive ? SelectedBg : NormalBg;
    }

    void RebuildList()
    {
      foreach (Transform child in _listContent)
        Destroy(child.gameObject);

      if (_tab == Tab.Growth)
      {
        foreach (var line in GrowthCatalog)
          AddListButton(line.id, line.name, line.color, line.id == _selectedId, () => SelectGrowth(line.id));
      }
      else
      {
        foreach (var buildId in CodexBuildIds)
        {
          var unlocked = ArenaMetaProgress.IsBuildUnlocked(buildId);
          var label = unlocked
            ? ArenaBuildBootstrap.GetDisplayName(buildId)
            : $"未解锁  ({ArenaBuildBootstrap.GetDisplayName(buildId)})";
          AddListButton(buildId, label, ArenaBuildBootstrap.GetIdentityColor(buildId), buildId == _selectedId,
            () => SelectBuild(buildId), unlocked);
        }
      }

      LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
      Canvas.ForceUpdateCanvases();
      _listScroll.verticalNormalizedPosition = 1f;
    }

    void SelectGrowth(string id)
    {
      _selectedId = id;
      RebuildList();
      RefreshDetail();
    }

    void SelectBuild(string id)
    {
      _selectedId = id;
      RebuildList();
      RefreshDetail();
    }

    void RefreshDetail()
    {
      if (_tab == Tab.Growth)
        ShowGrowthDetail(_selectedId);
      else
        ShowBuildDetail(_selectedId);
    }

    void ShowGrowthDetail(string id)
    {
      var line = FindGrowth(id);
      if (line == null)
      {
        ClearDetail("未找到成长线");
        return;
      }

      _accentBar.color = line.color;
      _detailTitle.text = line.name;
      _detailSubtitle.text = line.subtitle;
      _detailSubtitle.color = line.color;

      var sb = new StringBuilder();
      sb.AppendLine("【开局基线】");
      sb.AppendLine(line.baseline);
      sb.AppendLine();
      sb.AppendLine("【启源 · Foundation】");
      sb.AppendLine(line.origin);
      sb.AppendLine();
      sb.AppendLine("【深度成长】");
      foreach (var step in line.depthSteps)
        sb.AppendLine("· " + step);
      sb.AppendLine();
      sb.AppendLine("【前置规则】");
      sb.AppendLine(line.prerequisiteNote);
      _detailBody.text = sb.ToString();
    }

    void ShowBuildDetail(string buildId)
    {
      var unlocked = ArenaMetaProgress.IsBuildUnlocked(buildId);
      var runs = ArenaMetaSaveStore.GetBuildRunCount(buildId);
      var color = ArenaBuildBootstrap.GetIdentityColor(buildId);

      _accentBar.color = color;
      _detailTitle.text = unlocked
        ? ArenaBuildBootstrap.GetDisplayName(buildId)
        : "未解锁";
      _detailSubtitle.text = unlocked
        ? ArenaBuildBootstrap.GetTagline(buildId)
        : "完成一局对应流派/run 后解锁档案";
      _detailSubtitle.color = unlocked ? color : TextDim;

      var sb = new StringBuilder();
      if (unlocked)
      {
        sb.AppendLine($"累计局数：{runs}");
        sb.AppendLine($"主题武器：{DescribeThemeWeapon(ArenaBuildBootstrap.GetThemeForBuild(buildId))}");
        sb.AppendLine();
        sb.AppendLine(DescribeBuildRole(buildId));
        sb.AppendLine();
        sb.AppendLine("【里程碑】");
        foreach (var m in MilestoneLines(buildId))
          sb.AppendLine("· " + m);
        var capstoneId = buildId + "_capstone";
        if (ArenaMetaProgress.IsEvolutionUnlocked(capstoneId))
          sb.AppendLine().AppendLine("★ 终局进化已收录");
      }
      else
      {
        sb.AppendLine("该流派档案尚未解锁。");
        sb.AppendLine();
        sb.AppendLine("当前竞技场默认使用「自由构筑」开局；");
        sb.AppendLine("完成任意一局后会记录本局构筑并解锁对应档案。");
      }

      _detailBody.text = sb.ToString();
    }

    static string DescribeBuildRole(string buildId) => buildId switch
    {
      ArenaBuildBootstrap.Unified =>
        "统一基线仅含移动与冲刺。射击、环绕黑球、离体无人机、法术均通过升级启源解锁，可自由混搭。",
      ArenaBuildBootstrap.Shooter =>
        "远程弹幕压制：齐射、贯穿、爆破、闪电等装备链为核心。",
      ArenaBuildBootstrap.Contact =>
        "贴身星环与冲刺裁决：环绕黑球与冲刺近战链为核心。",
      _ => "引力井与潮汐脉冲双法术链，控场与区域压制为核心。"
    };

    static IEnumerable<string> MilestoneLines(string buildId)
    {
      if (buildId == ArenaBuildBootstrap.Unified)
      {
        yield return "L5  融合启源 — 已解锁机制线获得通用加成";
        yield return "L10 交叉共鸣 — 弹道、冲刺、星环协同";
        yield return "L15 自由冠顶 — 多系混搭巅峰加成";
        yield break;
      }

      if (buildId == ArenaBuildBootstrap.Shooter)
      {
        yield return "L5  弹幕织网 · L10 过载回路 · L15 弹幕冠顶";
        yield break;
      }

      if (buildId == ArenaBuildBootstrap.Contact)
      {
        yield return "L5  贴身领域 · L10 斩击共鸣 · L15 接触冠顶";
        yield break;
      }

      yield return "L5  奥术共振 · L10 引力织网 · L15 奇点觉醒";
    }

    static GrowthLineDef FindGrowth(string id)
    {
      foreach (var line in GrowthCatalog)
      {
        if (line.id == id)
          return line;
      }

      return null;
    }

    void ClearDetail(string message)
    {
      _detailTitle.text = "构筑图鉴";
      _detailSubtitle.text = string.Empty;
      _detailBody.text = message;
    }

    void AddListButton(string id, string label, Color accent, bool selected, System.Action onClick, bool enabled = true)
    {
      var bg = selected ? SelectedBg : enabled ? NormalBg : LockedBg;
      var btn = CreateButton(_listContent, "Item_" + id, label, bg, onClick);
      btn.interactable = enabled;
      var colors = btn.colors;
      colors.normalColor = bg;
      colors.highlightedColor = selected ? SelectedBg : new Color(bg.r + 0.06f, bg.g + 0.06f, bg.b + 0.06f, 1f);
      btn.colors = colors;

      var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
      stripe.transform.SetParent(btn.transform, false);
      var srt = stripe.GetComponent<RectTransform>();
      srt.anchorMin = new Vector2(0f, 0f);
      srt.anchorMax = new Vector2(0f, 1f);
      srt.pivot = new Vector2(0f, 0.5f);
      srt.sizeDelta = new Vector2(4f, 0f);
      srt.anchoredPosition = Vector2.zero;
      stripe.GetComponent<Image>().color = accent;
    }

    static string DescribeThemeWeapon(string theme) => theme switch
    {
      "warrior" => "战士",
      "ranged"  => "射手",
      "mage"    => "法师",
      "melee"   => "近战",
      "unified" => "自由构筑",
      _         => theme ?? "未知"
    };

    void BuildUI()
    {
      var canvasGo = new GameObject("BuildCodexCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.overrideSorting = true;
      canvas.sortingOrder = CanvasSortOrder;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, CanvasSortOrder);
      canvasGo.AddComponent<GraphicRaycaster>();

      _overlayRoot = canvasGo;
      _overlayRoot.SetActive(false);

      _backdrop = CreatePanel(canvasGo.transform, "Backdrop", new Color(0.02f, 0.05f, 0.08f, 0.94f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
      var backdropImage = _backdrop.GetComponent<Image>();
      backdropImage.raycastTarget = true;
      _backdrop.AddComponent<Button>().onClick.AddListener(Close);

      _panel = CreatePanel(canvasGo.transform, "BuildCodexPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 620f)).gameObject;
      _panel.transform.SetAsLastSibling();
      _panel.GetComponent<Image>().raycastTarget = true;

      CreateLabel(_panel.transform, "Title", "构筑图鉴", 30, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(400f, 40f));

      CreateLabel(_panel.transform, "Hint", "成长线 · 流派档案 · 按 Esc 返回", 14, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(520f, 22f)).color = TextDim;

      BuildTabs(_panel.transform);

      var content = CreatePanel(_panel.transform, "Content", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      content.anchorMin = Vector2.zero;
      content.anchorMax = Vector2.one;
      content.offsetMin = new Vector2(20f, 60f);
      content.offsetMax = new Vector2(-20f, -118f);
      content.GetComponent<Image>().raycastTarget = false;

      BuildListColumn(content);
      BuildDetailColumn(content);

      var backButton = CreateButton(_panel.transform, "BackButton", "返回", NormalBg, Close,
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(160f, 40f));
      backButton.transform.SetAsLastSibling();
      _panel.transform.Find("Tabs")?.SetAsLastSibling();
    }

    public static void HideIfOpen()
    {
      if (s_instance != null && IsOpen)
        s_instance.Close();
    }

    void BuildTabs(Transform parent)
    {
      var tabRow = CreatePanel(parent, "Tabs", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -78f), new Vector2(-40f, 36f));

      _tabGrowthBg = CreateButton(tabRow, "TabGrowth", "成长线", SelectedBg, () => SwitchTab(Tab.Growth),
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(120f, 32f)).GetComponent<Image>();

      _tabArchiveBg = CreateButton(tabRow, "TabArchive", "流派档案", NormalBg, () => SwitchTab(Tab.Archive),
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(120f, 32f)).GetComponent<Image>();
    }

    void BuildListColumn(Transform parent)
    {
      var listHost = CreatePanel(parent, "ListHost", NormalBg,
        new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
      listHost.anchorMin = new Vector2(0f, 0f);
      listHost.anchorMax = new Vector2(0f, 1f);
      listHost.pivot = new Vector2(0f, 0.5f);
      listHost.offsetMin = Vector2.zero;
      listHost.offsetMax = new Vector2(240f, 0f);

      var scrollGo = new GameObject("Scroll", typeof(RectTransform));
      scrollGo.transform.SetParent(listHost, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = Vector2.zero;
      scrollRt.anchorMax = Vector2.one;
      scrollRt.offsetMin = new Vector2(4f, 4f);
      scrollRt.offsetMax = new Vector2(-4f, -4f);

      _listScroll = scrollGo.AddComponent<ScrollRect>();
      _listScroll.horizontal = false;
      _listScroll.movementType = ScrollRect.MovementType.Clamped;

      var viewport = CreatePanel(scrollGo.transform, "Viewport", new Color(1f, 1f, 1f, 0.01f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      _listScroll.viewport = viewport;

      _listContent = CreatePanel(viewport, "Content", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      _listContent.anchorMin = new Vector2(0f, 1f);
      _listContent.anchorMax = new Vector2(1f, 1f);
      _listContent.pivot = new Vector2(0.5f, 1f);

      var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 4f;
      layout.padding = new RectOffset(4, 4, 4, 4);
      layout.childControlHeight = true;
      layout.childControlWidth = true;
      layout.childForceExpandHeight = false;
      layout.childForceExpandWidth = true;

      _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      _listScroll.content = _listContent;
    }

    void BuildDetailColumn(Transform parent)
    {
      var detailHost = CreatePanel(parent, "DetailHost", DetailBg,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      detailHost.anchorMin = new Vector2(0f, 0f);
      detailHost.anchorMax = new Vector2(1f, 1f);
      detailHost.offsetMin = new Vector2(252f, 0f);
      detailHost.offsetMax = Vector2.zero;

      _accentBar = CreatePanel(detailHost, "Accent", Accent,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(0f, 4f)).GetComponent<Image>();
      _accentBar.rectTransform.offsetMin = new Vector2(12f, -8f);
      _accentBar.rectTransform.offsetMax = new Vector2(-12f, -4f);

      _detailTitle = CreateLabel(detailHost, "DetailTitle", "", 24, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-24f, 32f));
      _detailTitle.alignment = TextAnchor.MiddleLeft;

      _detailSubtitle = CreateLabel(detailHost, "DetailSubtitle", "", 15, FontStyle.Italic,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -68f), new Vector2(-24f, 24f));
      _detailSubtitle.alignment = TextAnchor.MiddleLeft;

      var bodyScroll = new GameObject("BodyScroll", typeof(RectTransform), typeof(ScrollRect));
      bodyScroll.transform.SetParent(detailHost, false);
      var bodyRt = bodyScroll.GetComponent<RectTransform>();
      bodyRt.anchorMin = Vector2.zero;
      bodyRt.anchorMax = Vector2.one;
      bodyRt.offsetMin = new Vector2(16f, 12f);
      bodyRt.offsetMax = new Vector2(-16f, -88f);

      var viewport = CreatePanel(bodyScroll.transform, "BodyViewport", new Color(1f, 1f, 1f, 0.01f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

      var bodyContent = CreatePanel(viewport, "BodyContent", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      bodyContent.anchorMin = new Vector2(0f, 1f);
      bodyContent.anchorMax = new Vector2(1f, 1f);
      bodyContent.pivot = new Vector2(0.5f, 1f);
      bodyContent.GetComponent<Image>().raycastTarget = false;
      bodyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

      var scroll = bodyScroll.GetComponent<ScrollRect>();
      scroll.viewport = viewport;
      scroll.content = bodyContent;
      scroll.horizontal = false;
      scroll.movementType = ScrollRect.MovementType.Clamped;

      _detailBody = CreateLabel(bodyContent, "DetailBody", "", 15, FontStyle.Normal,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _detailBody.alignment = TextAnchor.UpperLeft;
      _detailBody.color = TextDim;
      _detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
      _detailBody.verticalOverflow = VerticalWrapMode.Overflow;
    }

    RectTransform CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform), typeof(Image));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = sizeDelta;
      go.GetComponent<Image>().color = color;
      return rt;
    }

    Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = sizeDelta;
      var label = go.AddComponent<Text>();
      label.font = _font;
      label.text = text;
      label.fontSize = size;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = Color.white;
      label.raycastTarget = false;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }

    Button CreateButton(Transform parent, string name, string label, Color bg, System.Action onClick,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = sizeDelta;
      var image = go.GetComponent<Image>();
      image.color = bg;
      var btn = go.GetComponent<Button>();
      btn.targetGraphic = image;
      if (onClick != null)
        btn.onClick.AddListener(() => onClick());

      var text = CreateLabel(go.transform, "Label", label, 16, FontStyle.Normal,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      text.raycastTarget = false;
      return btn;
    }

    Button CreateButton(Transform parent, string name, string label, Color bg, System.Action onClick)
    {
      var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
      go.transform.SetParent(parent, false);
      var le = go.GetComponent<LayoutElement>();
      le.minHeight = 36f;
      le.preferredHeight = 36f;
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0f, 1f);
      rt.anchorMax = new Vector2(1f, 1f);
      rt.sizeDelta = new Vector2(0f, 36f);
      var image = go.GetComponent<Image>();
      image.color = bg;
      var btn = go.GetComponent<Button>();
      btn.targetGraphic = image;
      if (onClick != null)
        btn.onClick.AddListener(() => onClick());

      var text = CreateLabel(go.transform, "Label", label, 15, FontStyle.Normal,
        Vector2.zero, Vector2.one, new Vector2(10f, 0f), Vector2.zero);
      text.alignment = TextAnchor.MiddleLeft;
      text.raycastTarget = false;
      return btn;
    }

    sealed class GrowthLineDef
    {
      public string id;
      public string name;
      public string subtitle;
      public Color color;
      public string baseline;
      public string origin;
      public string[] depthSteps;
      public string prerequisiteNote;
    }

    static readonly GrowthLineDef[] GrowthCatalog =
    {
      new()
      {
        id = "projectile",
        name = "射击",
        subtitle = "自动远程 · 弹道形态",
        color = new Color(1f, 0.72f, 0.28f, 1f),
        baseline = "开局无自动射击，须先选「射击启源」。",
        origin = "射击启源 — 解锁 projectile 标签与自动攻击",
        depthSteps = new[]
        {
          "双重弹道 → 散射协议 / 穿透弹体",
          "齐射 / 贯穿 / 爆破 / 闪电 深度链（需 projectile）",
          "弹道机制：贯穿、弱追踪、命中分裂"
        },
        prerequisiteNote = "所有射击强化需已解锁射击启源；numeric 池同样要求 projectile 标签。"
      },
      new()
      {
        id = "orbit",
        name = "星环",
        subtitle = "环绕黑球 · 逐颗获得",
        color = new Color(0.92f, 0.95f, 1f, 1f),
        baseline = "与离体无人机无关；黑球由 OrbitWeaponSystem 驱动。",
        origin = "星环启源 — +1 颗环绕黑球（warrior_weapon_count）",
        depthSteps = new[]
        {
          "星环扩展 — 每选一次 +1 黑球",
          "星环强化 — 伤害与体积",
          "卫星扩展 / 灵刃 等 warrior 深度链（需 orbit 标签）"
        },
        prerequisiteNote = "须先选星环启源；扩展与强化均需 orbit 标签。"
      },
      new()
      {
        id = "detached",
        name = "外置武器",
        subtitle = "自主 AI · 游走核心",
        color = new Color(0.55f, 0.82f, 1f, 1f),
        baseline = "新增外置核心，可进化为激光、飞弹、爆破、脉冲、回旋或轨迹。",
        origin = "外置武器启源 — +1 自主武器（detached_part_count）",
        depthSteps = new[]
        {
          "星环核心 +1 — 新增外置武器",
          "外置核心强化 → 六系进化（激光/飞弹/爆炸/脉冲/回旋/轨迹）",
          "外置武器数值：伤害、机动、活动范围"
        },
        prerequisiteNote = "所有外置武器强化需 detached 标签；进化需先拥有外置核心。"
      },
      new()
      {
        id = "dash",
        name = "冲刺近战",
        subtitle = "路径伤害 · DashMelee",
        color = new Color(0.95f, 0.45f, 0.38f, 1f),
        baseline = "冲刺可用但无伤害，直至选择冲击推进。",
        origin = "冲击推进 — 引入 dash_melee 机制",
        depthSteps = new[]
        {
          "动能尾流 — 路径留区域伤害",
          "DashMelee 五阶链（需 dash 标签）"
        },
        prerequisiteNote = "深度链需 dash 标签；由冲击推进授予。"
      },
      new()
      {
        id = "gravity",
        name = "引力法术",
        subtitle = "引力井 · 控场",
        color = new Color(0.45f, 0.65f, 1f, 1f),
        baseline = "开局无法术槽位。",
        origin = "引力启源 — 解锁引力井技能（skill_gravity_well_unlock）",
        depthSteps = new[]
        {
          "奇点投掷 → 弹道弯折 → 冲锋偏航 → 双重奇点 → 移动奇点",
          "五阶引力链（需 gravity 标签）"
        },
        prerequisiteNote = "引力强化不会在没有引力启源时出现。"
      },
      new()
      {
        id = "tidal",
        name = "潮汐法术",
        subtitle = "潮汐脉冲 · 击退",
        color = new Color(0.38f, 0.88f, 0.82f, 1f),
        baseline = "开局无法术槽位。",
        origin = "潮汐启源 — 解锁潮汐脉冲技能（skill_tidal_pulse_unlock）",
        depthSteps = new[]
        {
          "潮汐脉冲 → 逐浪推进 → 拒止潮界 → 永续潮涌 → 潮域共鸣",
          "五阶潮汐链（需 tidal 标签）"
        },
        prerequisiteNote = "潮汐强化不会在没有潮汐启源时出现。"
      }
    };
  }
}
