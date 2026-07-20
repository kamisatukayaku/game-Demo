using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Core;
using Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
  /// <summary>
  /// Meta Progression 天赋树 UI。
  /// 使用 UiFontHelper.StyleText() 统一字体方案（含 Outline 描边），
  /// Canvas sortingOrder = 600 确保覆盖主菜单。
  /// Esc 关闭，支持拖拽平移、滚轮缩放、节点点击、悬停 Tooltip。
  /// </summary>
  public class MetaProgressionUI : MonoBehaviour
  {
    [Header("Node")]
    public float NodeWidth = 140f;
    public float NodeHeight = 56f;
    [Header("Spacing")]
    public float HorizontalSpacing = 200f;
    public float VerticalSpacing = 80f;
    [Header("Column Split")]
    public int MaxNodesPerColumn = 5;
    [Header("Line")]
    public float LineWidth = 2.5f;
    [Header("Padding")]
    public float ContentPadding = 60f;

    internal static readonly Color ColorLocked       = new(0.25f, 0.25f, 0.28f, 1f);
    internal static readonly Color ColorLockedBorder  = new(0.35f, 0.35f, 0.38f, 1f);
    internal static readonly Color ColorAvailable     = new(0.12f, 0.16f, 0.2f, 1f);
    internal static readonly Color ColorAvailableBorder = new(1f, 0.84f, 0.25f, 1f);
    internal static readonly Color ColorUnlocked      = new(0.18f, 0.55f, 0.35f, 1f);
    internal static readonly Color ColorUnlockedBorder = new(0.35f, 0.85f, 0.5f, 1f);
    static readonly Color ColorPanelBg       = new(0.03f, 0.04f, 0.06f, 0.95f);
    static readonly Color ColorLineLocked    = new(0.35f, 0.35f, 0.38f, 0.6f);
    static readonly Color ColorLineAvailable = new(1f, 0.84f, 0.25f, 0.8f);
    static readonly Color ColorLineUnlocked  = new(0.35f, 0.85f, 0.5f, 0.8f);
    internal static readonly Color ColorTextWhite     = new(0.9f, 0.92f, 0.95f, 1f);
    internal static readonly Color ColorTextDim       = new(0.6f, 0.65f, 0.7f, 1f);
    internal static readonly Color ColorTextUnlocked  = new(0.7f, 1f, 0.75f, 1f);
    internal static readonly Color ColorTextAvailable = new(1f, 0.9f, 0.4f, 1f);
    internal static readonly Color ColorTextGold      = new(1f, 0.84f, 0.25f, 1f);

    const int CanvasSortingOrder = 600; // 高于 StartGameCanvas (500)

    static MetaProgressionUI s_instance;
    public static bool IsOpen => s_instance != null && s_instance._canvasPanel != null && s_instance._canvasPanel.activeSelf;
    public static event Action<string> OnNodeClicked;

    public static void EnsureExists(MetaProgressionSystem system)
    {
      if (s_instance != null) { s_instance._system = system; return; }
      var go = new GameObject("_MetaProgressionUI"); DontDestroyOnLoad(go);
      var ui = go.AddComponent<MetaProgressionUI>(); ui._system = system; ui.Build();
    }

    public static void Open(MetaProgressionSystem system)
    {
      if (!WorldUILock.TryAcquire("meta_progression")) return;
      EnsureExists(system);
      if (s_instance._canvasPanel != null) s_instance._canvasPanel.SetActive(true);
      s_instance.CenterContent();
      s_instance.RefreshDisplay();
    }
    public static void Close()
    {
      if (s_instance != null && s_instance._canvasPanel != null)
      {
        s_instance._canvasPanel.SetActive(false);
        s_instance.SaveIfNeeded();
      }
      WorldUILock.Release("meta_progression");
    }

    MetaProgressionSystem _system;
    Font _font;
    GameObject _canvasPanel;
    RectTransform _viewport;
    RectTransform _content;
    Image _contentBg;
    Text _expPointsLabel; // 左上角探索点显示
    Dictionary<string, Vector2> _nodePositions = new();
    List<EdgeData> _edges = new();
    float _contentWidth, _contentHeight;
    readonly Dictionary<string, TalentNodeView> _nodeViews = new();
    readonly List<GameObject> _connectionObjects = new();
    TalentTooltip _tooltip;
    Vector2 _dragStartPos, _dragStartContentPos;
    float _zoom = 1f;
    const float ZoomMin = 0.25f, ZoomMax = 2.5f, ZoomStep = 0.08f;

    void Awake() { if (s_instance != null && s_instance != this) { Destroy(gameObject); return; } s_instance = this; }
    void OnDestroy() { if (s_instance == this) s_instance = null; }
    void Update() { HandleZoom(); if (Input.GetKeyDown(KeyCode.Escape)) Close(); }

    // ══════════════════════════════════════════════════════
    //  构建
    // ══════════════════════════════════════════════════════

    void Build()
    {
      if (_system == null) return;
      _system.Initialize();
      _font = UiFontHelper.GetFont();    // 与 StartGameUIShared 完全一致
      BuildCanvas();
      RebuildLayout();
      RenderNodes();
      RenderConnections();
      RefreshDisplay();
      CenterContent();
      _canvasPanel.SetActive(false);
    }

    public void RebuildLayout()
    {
      if (_system == null || _system.AllNodes == null || _system.AllNodes.Count == 0) return;
      var result = TalentTreeLayout.Compute(_system.AllNodes, NodeWidth, NodeHeight,
        HorizontalSpacing, VerticalSpacing, MaxNodesPerColumn, ContentPadding);
      _nodePositions = result.Positions; _edges = result.Edges;
      _contentWidth = result.TotalWidth; _contentHeight = result.TotalHeight;
      if (_content != null) _content.sizeDelta = new Vector2(_contentWidth, _contentHeight);
    }

    public void RefreshDisplay()
    {
      if (_system == null) return;
      // 探索点显示
      if (_expPointsLabel != null)
        _expPointsLabel.text = $"探索点: {_system.TotalBattleExp:F0}";
      foreach (var kv in _nodeViews)
      {
        var state = _system.GetNodeDisplayState(kv.Key);
        int level = _system.GetNodeLevel(kv.Key);
        var nodeDef = _system.AllNodes.TryGetValue(kv.Key, out var def) ? def : null;
        float cost = nodeDef != null ? _system.GetUnlockCost(kv.Key) : 0f;
        kv.Value.UpdateState(state, level, nodeDef?.max_level ?? 1, cost);
      }
      RefreshConnectionColors();
    }

    // ══════════════════════════════════════════════════════
    //  Canvas（sortingOrder = 600）
    // ══════════════════════════════════════════════════════

    void BuildCanvas()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("MetaProgressionCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, CanvasSortingOrder);
      canvasGo.AddComponent<GraphicRaycaster>();
      _canvasPanel = canvasGo;

      // 全屏背景
      var bgGo = new GameObject("Background", typeof(RectTransform));
      bgGo.transform.SetParent(canvasGo.transform, false);
      var bgRt = bgGo.GetComponent<RectTransform>();
      bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
      bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
      var bgImg = bgGo.AddComponent<Image>();
      bgImg.color = ColorPanelBg; bgImg.raycastTarget = true;

      // 左上角：探索点显示
      var expGo = new GameObject("ExpPoints", typeof(RectTransform));
      expGo.transform.SetParent(canvasGo.transform, false);
      var expRt = expGo.GetComponent<RectTransform>();
      expRt.anchorMin = new Vector2(0f, 1f); expRt.anchorMax = new Vector2(0f, 1f);
      expRt.pivot = new Vector2(0f, 1f);
      expRt.anchoredPosition = new Vector2(20f, -20f);
      expRt.sizeDelta = new Vector2(300f, 36f);
      _expPointsLabel = expGo.AddComponent<Text>();
      _expPointsLabel.font = _font;
      _expPointsLabel.fontSize = 22;
      _expPointsLabel.fontStyle = FontStyle.Bold;
      _expPointsLabel.alignment = TextAnchor.MiddleLeft;
      _expPointsLabel.color = new Color(1f, 0.84f, 0.25f, 1f);
      _expPointsLabel.text = "探索点: 0";
      _expPointsLabel.raycastTarget = false;

      // Viewport — 使用 RectMask2D 替代 Mask（Mask 的 stencil buffer 与动态字体不兼容）
      var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
      vpGo.transform.SetParent(canvasGo.transform, false);
      var vpRt = vpGo.GetComponent<RectTransform>();
      vpRt.anchorMin = new Vector2(0.02f, 0.02f);
      vpRt.anchorMax = new Vector2(0.98f, 0.98f);
      vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
      var vpImg = vpGo.GetComponent<Image>();
      vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;
      _viewport = vpRt;
      var dragHandler = vpGo.AddComponent<ViewportDragHandler>();
      dragHandler.OnDragEvent += OnViewportDrag;
      dragHandler.OnBeginDragEvent += OnViewportBeginDrag;

      // Content
      var ctGo = new GameObject("Content", typeof(RectTransform));
      ctGo.transform.SetParent(vpGo.transform, false);
      var ctRt = ctGo.GetComponent<RectTransform>();
      ctRt.pivot = new Vector2(0.5f, 0.5f);
      ctRt.anchorMin = new Vector2(0.5f, 0.5f);
      ctRt.anchorMax = new Vector2(0.5f, 0.5f);
      ctRt.anchoredPosition = Vector2.zero;
      ctRt.sizeDelta = new Vector2(2000f, 2000f);
      _content = ctRt;
      var ctBgImg = ctGo.AddComponent<Image>();
      ctBgImg.color = new Color(0f, 0f, 0f, 0f); ctBgImg.raycastTarget = true;
      _contentBg = ctBgImg;

      _tooltip = new TalentTooltip(canvasGo.transform, _font);
    }

    // ══════════════════════════════════════════════════════
    //  节点 / 连线
    // ══════════════════════════════════════════════════════

    void RenderNodes()
    {
      ClearNodes();
      if (_system == null || _system.AllNodes == null) return;
      foreach (var kv in _system.AllNodes)
      {
        var nodeId = kv.Key; var nodeDef = kv.Value;
        if (nodeDef == null || !_nodePositions.TryGetValue(nodeId, out var pos)) continue;
        var view = new TalentNodeView(_content, _font, nodeId, nodeDef.display_name,
          nodeDef.description, nodeDef.tree_type, nodeDef.max_level,
          NodeWidth, NodeHeight, pos);
        view.OnClick += id =>
        {
          var state = _system.GetNodeDisplayState(id);
          if (state == MetaProgressionSystem.NodeDisplayState.Available)
          {
            if (_system.UnlockNode(id))
            {
              SaveIfNeeded();
              RefreshDisplay();
            }
          }
          // 始终显示 Tooltip 作为反馈（锁定/已解锁/可解锁都有信息）
          var nodeView = _nodeViews.TryGetValue(id, out var v) ? v : null;
          if (nodeView != null)
            ShowTooltip(id, nodeView.RectTransform);
          OnNodeClicked?.Invoke(id);
        };
        view.OnHoverEnter += id => ShowTooltip(id, view.RectTransform);
        view.OnHoverExit += id => HideTooltip();
        _nodeViews[nodeId] = view;
      }
    }

    void ClearNodes() { foreach (var kv in _nodeViews) kv.Value.Destroy(); _nodeViews.Clear(); }

    void RenderConnections()
    {
      ClearConnections();
      foreach (var edge in _edges)
      {
        if (!_nodePositions.TryGetValue(edge.FromId, out var fromPos)) continue;
        if (!_nodePositions.TryGetValue(edge.ToId, out var toPos)) continue;
        var go = new GameObject($"Line_{edge.FromId}_to_{edge.ToId}", typeof(RectTransform));
        go.transform.SetParent(_content, false); go.transform.SetAsFirstSibling();
        new ConnectionLine(go, fromPos, toPos, NodeWidth, NodeHeight, LineWidth);
        _connectionObjects.Add(go);
      }
    }

    void ClearConnections() { foreach (var go in _connectionObjects) Destroy(go); _connectionObjects.Clear(); }

    void RefreshConnectionColors()
    {
      if (_system == null) return;
      for (int i = 0; i < Mathf.Min(_connectionObjects.Count, _edges.Count); i++)
      {
        var edge = _edges[i];
        var c = _system.GetNodeDisplayState(edge.ToId) switch
        {
          MetaProgressionSystem.NodeDisplayState.Unlocked => ColorLineUnlocked,
          MetaProgressionSystem.NodeDisplayState.Available => ColorLineAvailable,
          _ => ColorLineLocked
        };
        var images = _connectionObjects[i].GetComponentsInChildren<Image>();
        foreach (var img in images) img.color = c;
      }
    }

    // ══════════════════════════════════════════════════════
    //  Tooltip / 交互
    // ══════════════════════════════════════════════════════

    void CenterContent()
    {
      if (_content == null) return;
      _content.anchoredPosition = Vector2.zero;
      _zoom = 1f; _content.localScale = Vector3.one;
    }

    /// <summary>持久化保存 MetaProgressionSystem 数据到 PlayerPrefs。</summary>
    void SaveIfNeeded()
    {
      _system?.SavePersistentData();
    }

    void ShowTooltip(string nodeId, RectTransform nodeRt)
    {
      if (_system == null || _tooltip == null) return;
      if (!_system.AllNodes.TryGetValue(nodeId, out var def)) return;
      var state = _system.GetNodeDisplayState(nodeId);
      int level = _system.GetNodeLevel(nodeId);
      float cost = _system.GetUnlockCost(nodeId);
      float totalExp = _system.TotalBattleExp;
      string stateText = state switch
      {
        MetaProgressionSystem.NodeDisplayState.Unlocked  => $"已解锁 Lv.{level}/{def.max_level}",
        MetaProgressionSystem.NodeDisplayState.Available => $"可解锁 消耗 {cost:F0}",
        _ => "前置未满足"
      };
      var screenPos = RectTransformUtility.WorldToScreenPoint(null, nodeRt.position);
      _tooltip.Show(def.display_name, def.description, cost, stateText, totalExp, state, screenPos, NodeWidth);
    }

    void HideTooltip() { _tooltip?.Hide(); }

    void HandleZoom()
    {
      if (_content == null) return;
      var scroll = Input.GetAxis("Mouse ScrollWheel");
      if (Mathf.Approximately(scroll, 0f)) return;
      if (!RectTransformUtility.RectangleContainsScreenPoint(_viewport, Input.mousePosition)) return;
      RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, Input.mousePosition, null, out var mouseInViewport);
      float prevZoom = _zoom;
      _zoom = Mathf.Clamp(_zoom + scroll * ZoomStep * 4f, ZoomMin, ZoomMax);
      if (!Mathf.Approximately(prevZoom, _zoom))
      {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, Input.mousePosition, null, out var prevMouseInContent);
        _content.localScale = new Vector3(_zoom, _zoom, 1f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, Input.mousePosition, null, out var newMouseInContent);
        _content.anchoredPosition += newMouseInContent - prevMouseInContent;
      }
    }

    void OnViewportBeginDrag(Vector2 screenPos) { _dragStartPos = screenPos; _dragStartContentPos = _content.anchoredPosition; }
    void OnViewportDrag(Vector2 screenPos) { _content.anchoredPosition = _dragStartContentPos + (screenPos - _dragStartPos); }
  }

  // ════════════════════════════════════════════════════════
  //  样式辅助（统一使用 UiFontHelper.StyleText）
  // ════════════════════════════════════════════════════════

  /// <summary>天赋树布局计算。</summary>
  static class TalentTreeLayout
  {
    public struct LayoutResult { public Dictionary<string, Vector2> Positions; public List<EdgeData> Edges; public float TotalWidth; public float TotalHeight; }

    public static LayoutResult Compute(IReadOnlyDictionary<string, MetaProgressionSystem.MetaNodeDef> allNodes,
      float nodeWidth, float nodeHeight, float hSpacing, float vSpacing, int maxNodesPerColumn, float padding)
    {
      var nodeIds = new List<string>(allNodes.Keys);
      var children = new Dictionary<string, List<string>>();
      var parents = new Dictionary<string, List<string>>();
      foreach (var kv in allNodes)
      {
        var id = kv.Key; var def = kv.Value;
        if (!children.ContainsKey(id)) children[id] = new List<string>();
        if (!parents.ContainsKey(id)) parents[id] = new List<string>();
        if (def.prerequisites != null)
        {
          foreach (var prereq in def.prerequisites)
          {
            if (!children.ContainsKey(prereq)) children[prereq] = new List<string>();
            children[prereq].Add(id); parents[id].Add(prereq);
          }
        }
      }
      var layers = ComputeLayers(nodeIds, parents);
      var layerGroups = new SortedDictionary<int, List<string>>();
      foreach (var kv in layers)
      {
        if (!layerGroups.ContainsKey(kv.Value)) layerGroups[kv.Value] = new List<string>();
        layerGroups[kv.Value].Add(kv.Key);
      }
      foreach (var layer in layerGroups.Values) layer.Sort();
      var positions = new Dictionary<string, Vector2>();
      float currentX = padding + nodeWidth * 0.5f;
      foreach (int layerIdx in layerGroups.Keys)
      {
        var layerNodes = layerGroups[layerIdx];
        int nodeCount = layerNodes.Count;
        int colCount = Mathf.CeilToInt((float)nodeCount / maxNodesPerColumn);
        for (int col = 0; col < colCount; col++)
        {
          float cx = currentX + col * (nodeWidth + hSpacing);
          int startIdx = col * maxNodesPerColumn;
          int count = Mathf.Min(maxNodesPerColumn, nodeCount - startIdx);
          float fullColumnHeight = maxNodesPerColumn * (nodeHeight + vSpacing) - vSpacing;
          float actualColumnHeight = count * (nodeHeight + vSpacing) - vSpacing;
          float yOffset = padding + nodeHeight * 0.5f + (fullColumnHeight - actualColumnHeight) * 0.5f;
          for (int row = 0; row < count; row++)
          {
            string nodeId = layerNodes[startIdx + row];
            float cy = yOffset + row * (nodeHeight + vSpacing);
            positions[nodeId] = new Vector2(cx, cy);
          }
        }
        currentX += colCount * (nodeWidth + hSpacing);
      }
      float maxY = padding + nodeHeight;
      foreach (var pos in positions.Values) maxY = Mathf.Max(maxY, pos.y + nodeHeight * 0.5f);
      var edges = new List<EdgeData>();
      foreach (var kv in allNodes)
      {
        var def = kv.Value;
        if (def.prerequisites == null) continue;
        foreach (var prereq in def.prerequisites)
          if (positions.ContainsKey(prereq) && positions.ContainsKey(kv.Key))
            edges.Add(new EdgeData { FromId = prereq, ToId = kv.Key });
      }
      return new LayoutResult
      {
        Positions = positions, Edges = edges,
        TotalWidth = currentX - nodeWidth * 0.5f + padding,
        TotalHeight = maxY + padding
      };
    }

    static Dictionary<string, int> ComputeLayers(List<string> nodeIds, Dictionary<string, List<string>> parents)
    {
      var layers = new Dictionary<string, int>();
      var remaining = new HashSet<string>(nodeIds);
      foreach (var id in nodeIds)
      {
        if (!parents.TryGetValue(id, out var pList) || pList.Count == 0)
        { layers[id] = 0; remaining.Remove(id); }
      }
      while (remaining.Count > 0)
      {
        var toRemove = new List<string>();
        foreach (var id in remaining)
        {
          if (parents.TryGetValue(id, out var pList) && pList.All(p => layers.ContainsKey(p)))
          {
            int maxLayer = 0;
            foreach (var p in pList) maxLayer = Mathf.Max(maxLayer, layers[p]);
            layers[id] = maxLayer + 1; toRemove.Add(id);
          }
        }
        if (toRemove.Count == 0) break;
        foreach (var id in toRemove) remaining.Remove(id);
      }
      foreach (var id in remaining) layers[id] = 0;
      return layers;
    }
  }

  struct EdgeData { public string FromId; public string ToId; }

  // ════════════════════════════════════════════════════════
  //  天赋节点 UI
  // ════════════════════════════════════════════════════════

  sealed class TalentNodeView
  {
    public event Action<string> OnClick, OnHoverEnter, OnHoverExit;
    public RectTransform RectTransform => _rect;
    readonly string _nodeId;
    readonly RectTransform _rect;
    readonly Image _bgImage, _borderImage;
    readonly Text _nameLabel, _levelLabel, _costLabel;
    readonly Button _button;
    MetaProgressionSystem.NodeDisplayState _currentState;
    int _currentLevel, _maxLevel;
    float _currentCost;

    public TalentNodeView(Transform parent, Font font, string nodeId, string displayName,
      string description, string treeType, int maxLevel,
      float nodeWidth, float nodeHeight, Vector2 layoutPos)
    {
      _nodeId = nodeId; _maxLevel = maxLevel;

      var go = new GameObject($"Node_{displayName}", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      _rect = go.GetComponent<RectTransform>();
      _rect.pivot = new Vector2(0.5f, 0.5f);
      _rect.anchoredPosition = new Vector2(layoutPos.x, -layoutPos.y);
      _rect.sizeDelta = new Vector2(nodeWidth, nodeHeight);

      // Border
      var borderGo = new GameObject("Border", typeof(RectTransform));
      borderGo.transform.SetParent(go.transform, false);
      var borderRt = borderGo.GetComponent<RectTransform>();
      borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
      borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;
      _borderImage = borderGo.AddComponent<Image>(); _borderImage.raycastTarget = false;

      // Background
      var bgGo = new GameObject("Background", typeof(RectTransform));
      bgGo.transform.SetParent(go.transform, false);
      var bgRt = bgGo.GetComponent<RectTransform>();
      bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
      bgRt.offsetMin = new Vector2(3f, 3f);
      bgRt.offsetMax = new Vector2(-3f, -3f);
      _bgImage = bgGo.AddComponent<Image>(); _bgImage.raycastTarget = false;

      // 根节点上的透明 Image 作为按钮的射线目标（Unity Button 要求 hierarchy 中有 raycastTarget=true 的 Graphic）
      var hitImg = go.AddComponent<Image>();
      hitImg.color = new Color(0f, 0f, 0f, 0f);
      hitImg.raycastTarget = true;

      // Type bar
      var typeBarGo = new GameObject("TypeBar", typeof(RectTransform));
      typeBarGo.transform.SetParent(go.transform, false);
      var typeBarRt = typeBarGo.GetComponent<RectTransform>();
      typeBarRt.anchorMin = new Vector2(0f, 0f); typeBarRt.anchorMax = new Vector2(0f, 1f);
      typeBarRt.pivot = new Vector2(0f, 0.5f);
      typeBarRt.anchoredPosition = new Vector2(4f, 0f); typeBarRt.sizeDelta = new Vector2(4f, 0f);
      var typeBarImg = typeBarGo.AddComponent<Image>();
      typeBarImg.raycastTarget = false;
      typeBarImg.color = GetTypeColor(treeType);

      // Name — 完全对齐 StartGameUIShared.CreateLabel 的方案
      var nameGo = new GameObject("Name", typeof(RectTransform));
      nameGo.transform.SetParent(go.transform, false);
      var nameRt = nameGo.GetComponent<RectTransform>();
      nameRt.anchorMin = new Vector2(0f, 0.55f); nameRt.anchorMax = new Vector2(1f, 1f);
      nameRt.offsetMin = new Vector2(10f, 0f); nameRt.offsetMax = new Vector2(-6f, -2f);
      _nameLabel = nameGo.AddComponent<Text>();
      _nameLabel.font = font;
      _nameLabel.fontSize = 14;
      _nameLabel.fontStyle = FontStyle.Bold;
      _nameLabel.alignment = TextAnchor.MiddleLeft;
      _nameLabel.color = MetaProgressionUI.ColorTextWhite;
      _nameLabel.text = displayName;
      _nameLabel.raycastTarget = false;
      _nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
      _nameLabel.verticalOverflow = VerticalWrapMode.Truncate;

      // Level
      var lvlGo = new GameObject("Level", typeof(RectTransform));
      lvlGo.transform.SetParent(go.transform, false);
      var lvlRt = lvlGo.GetComponent<RectTransform>();
      lvlRt.anchorMin = new Vector2(0f, 0f); lvlRt.anchorMax = new Vector2(1f, 0.45f);
      lvlRt.offsetMin = new Vector2(10f, 1f); lvlRt.offsetMax = new Vector2(-6f, -1f);
      _levelLabel = lvlGo.AddComponent<Text>();
      _levelLabel.font = font;
      _levelLabel.fontSize = 11;
      _levelLabel.alignment = TextAnchor.MiddleLeft;
      _levelLabel.color = MetaProgressionUI.ColorTextDim;
      _levelLabel.text = $"Lv.0/{maxLevel}";
      _levelLabel.raycastTarget = false;

      // Cost
      var costGo = new GameObject("Cost", typeof(RectTransform));
      costGo.transform.SetParent(go.transform, false);
      var costRt = costGo.GetComponent<RectTransform>();
      costRt.anchorMin = new Vector2(1f, 0f); costRt.anchorMax = new Vector2(1f, 0f);
      costRt.pivot = new Vector2(1f, 0f);
      costRt.anchoredPosition = new Vector2(-4f, 3f); costRt.sizeDelta = new Vector2(50f, 16f);
      _costLabel = costGo.AddComponent<Text>();
      _costLabel.font = font;
      _costLabel.fontSize = 11;
      _costLabel.alignment = TextAnchor.MiddleRight;
      _costLabel.color = MetaProgressionUI.ColorTextDim;
      _costLabel.text = "";
      _costLabel.raycastTarget = false;

      _button = go.AddComponent<Button>();
      _button.targetGraphic = _bgImage;
      _button.onClick.AddListener(() => OnClick?.Invoke(_nodeId));

      var hoverDetector = go.AddComponent<HoverDetector>();
      hoverDetector.OnEnter += () => OnHoverEnter?.Invoke(_nodeId);
      hoverDetector.OnExit += () => OnHoverExit?.Invoke(_nodeId);
    }

    public void UpdateState(MetaProgressionSystem.NodeDisplayState state, int level, int maxLevel, float cost)
    {
      _currentState = state; _currentLevel = level; _maxLevel = maxLevel; _currentCost = cost;
      switch (state)
      {
        case MetaProgressionSystem.NodeDisplayState.Unlocked:
          _bgImage.color = MetaProgressionUI.ColorUnlocked;
          _borderImage.color = MetaProgressionUI.ColorUnlockedBorder;
          _nameLabel.color = MetaProgressionUI.ColorTextUnlocked;
          _levelLabel.color = MetaProgressionUI.ColorTextUnlocked;
          _levelLabel.text = $"Lv.{level}/{maxLevel}";
          _costLabel.text = "";
          _button.interactable = true;
          break;
        case MetaProgressionSystem.NodeDisplayState.Available:
          _bgImage.color = MetaProgressionUI.ColorAvailable;
          _borderImage.color = MetaProgressionUI.ColorAvailableBorder;
          _nameLabel.color = MetaProgressionUI.ColorTextWhite;
          _levelLabel.color = MetaProgressionUI.ColorTextAvailable;
          _levelLabel.text = $"Lv.{level}/{maxLevel}";
          _costLabel.color = MetaProgressionUI.ColorTextGold;
          _costLabel.text = $"{cost:F0}";
          _button.interactable = true;
          break;
        default: // Locked — 按钮可点击以显示提示信息
          _bgImage.color = MetaProgressionUI.ColorLocked;
          _borderImage.color = MetaProgressionUI.ColorLockedBorder;
          _nameLabel.color = MetaProgressionUI.ColorTextDim;
          _levelLabel.color = MetaProgressionUI.ColorTextDim;
          _levelLabel.text = $"Lv.{level}/{maxLevel}";
          _costLabel.text = "";
          _button.interactable = true;
          break;
      }
    }

    public void Destroy() { if (_rect != null && _rect.gameObject != null) UnityEngine.Object.Destroy(_rect.gameObject); }

    static Color GetTypeColor(string treeType) => treeType switch
    {
      "attribute" => new Color(1f, 0.4f, 0.3f, 1f),
      "world"     => new Color(0.3f, 0.6f, 1f, 1f),
      "event"     => new Color(0.9f, 0.55f, 0.2f, 1f),
      "loot"      => new Color(0.85f, 0.75f, 0.3f, 1f),
      _           => new Color(0.5f, 0.5f, 0.5f, 1f)
    };
  }

  // ════════════════════════════════════════════════════════
  //  连线
  // ════════════════════════════════════════════════════════

  sealed class ConnectionLine
  {
    public ConnectionLine(GameObject container, Vector2 fromCenter, Vector2 toCenter,
      float nodeWidth, float nodeHeight, float lineWidth)
    {
      Vector2 p0 = new(fromCenter.x + nodeWidth * 0.5f, fromCenter.y);
      Vector2 p2 = new(toCenter.x - nodeWidth * 0.5f, toCenter.y);
      float midX = (p0.x + p2.x) * 0.5f;
      if (Mathf.Abs(p0.y - p2.y) < lineWidth * 2f)
        CreateSegment(container.transform, p0, p2, lineWidth);
      else
      {
        CreateSegment(container.transform, p0, new Vector2(midX, p0.y), lineWidth);
        CreateSegment(container.transform, new Vector2(midX, p0.y), new Vector2(midX, p2.y), lineWidth);
        CreateSegment(container.transform, new Vector2(midX, p2.y), p2, lineWidth);
      }
    }

    static void CreateSegment(Transform parent, Vector2 a, Vector2 b, float lineWidth)
    {
      Vector2 delta = b - a; float length = delta.magnitude;
      if (length < 0.5f) return;
      Vector2 center = (a + b) * 0.5f;
      float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
      var go = new GameObject("Segment", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = new Vector2(center.x, -center.y);
      rt.sizeDelta = new Vector2(length, lineWidth);
      rt.localRotation = Quaternion.Euler(0f, 0f, angle);
      var img = go.AddComponent<Image>(); img.raycastTarget = false;
    }
  }

  // ════════════════════════════════════════════════════════
  //  Tooltip
  // ════════════════════════════════════════════════════════

  sealed class TalentTooltip
  {
    readonly GameObject _root;
    readonly RectTransform _rect;
    readonly Text _nameLabel, _descLabel, _stateLabel, _expLabel;

    public TalentTooltip(Transform canvasParent, Font font)
    {
      _root = new GameObject("TalentTooltip", typeof(RectTransform));
      _root.transform.SetParent(canvasParent, false);
      _root.SetActive(false);
      _rect = _root.GetComponent<RectTransform>();
      _rect.anchorMin = new Vector2(0.5f, 0.5f);
      _rect.anchorMax = new Vector2(0.5f, 0.5f);
      _rect.pivot = new Vector2(0f, 1f);
      _rect.sizeDelta = new Vector2(260f, 130f);

      // 背景
      var bgGo = new GameObject("Background", typeof(RectTransform));
      bgGo.transform.SetParent(_root.transform, false);
      var bgRt = bgGo.GetComponent<RectTransform>();
      bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
      bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
      var bgImage = bgGo.AddComponent<Image>();
      bgImage.color = new Color(0.08f, 0.1f, 0.14f, 0.95f); bgImage.raycastTarget = false;

      // Name
      _nameLabel = CreateText(_root.transform, font, "Name", 16, FontStyle.Bold, TextAnchor.UpperLeft,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -6f), new Vector2(-10f, -28f));

      // Description
      _descLabel = CreateText(_root.transform, font, "Description", 12, FontStyle.Normal, TextAnchor.UpperLeft,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -32f), new Vector2(-10f, -62f));

      // State
      _stateLabel = CreateText(_root.transform, font, "State", 12, FontStyle.Normal, TextAnchor.LowerLeft,
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 24f));

      // Exp
      _expLabel = CreateText(_root.transform, font, "Exp", 11, FontStyle.Normal, TextAnchor.LowerRight,
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 24f));
    }

    public void Show(string name, string description, float cost, string stateText,
      float totalExp, MetaProgressionSystem.NodeDisplayState state, Vector2 screenPos, float nodeWidth)
    {
      _nameLabel.text = name;
      _descLabel.text = description.Length > 80 ? description[..77] + "..." : description;
      _stateLabel.text = stateText;
      _stateLabel.color = state switch
      {
        MetaProgressionSystem.NodeDisplayState.Unlocked  => new Color(0.7f, 1f, 0.75f, 1f),
        MetaProgressionSystem.NodeDisplayState.Available => new Color(1f, 0.9f, 0.4f, 1f),
        _ => new Color(0.6f, 0.65f, 0.7f, 1f)
      };
      _expLabel.text = $"经验: {totalExp:F0}";
      _expLabel.color = new Color(0.6f, 0.65f, 0.7f, 1f);

      float tooltipX = screenPos.x + nodeWidth * 0.5f + 16f;
      float tooltipY = screenPos.y;
      if (tooltipX + 270f > Screen.width) tooltipX = screenPos.x - 270f;
      if (tooltipY - 140f < 0f) tooltipY = 140f;
      _rect.anchoredPosition = new Vector2(tooltipX - Screen.width * 0.5f, tooltipY - Screen.height * 0.5f);
      _root.SetActive(true);
    }

    public void Hide() { _root.SetActive(false); }

    static Text CreateText(Transform parent, Font font, string name, int fontSize, FontStyle style,
      TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 offsetMin, Vector2 offsetMax)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
      rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
      var text = go.AddComponent<Text>();
      text.font = font;
      text.fontSize = fontSize;
      text.fontStyle = style;
      text.alignment = alignment;
      text.color = Color.white;
      text.text = "";
      text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Truncate;
      return text;
    }
  }

  // ════════════════════════════════════════════════════════
  //  事件工具
  // ════════════════════════════════════════════════════════

  sealed class HoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    public Action OnEnter, OnExit;
    public void OnPointerEnter(PointerEventData e) => OnEnter?.Invoke();
    public void OnPointerExit(PointerEventData e)  => OnExit?.Invoke();
  }

  sealed class ViewportDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
  {
    public event Action<Vector2> OnBeginDragEvent, OnDragEvent;
    public event Action OnEndDragEvent;
    public void OnBeginDrag(PointerEventData e) => OnBeginDragEvent?.Invoke(e.position);
    public void OnDrag(PointerEventData e)      => OnDragEvent?.Invoke(e.position);
    public void OnEndDrag(PointerEventData e)   => OnEndDragEvent?.Invoke();
  }
}
