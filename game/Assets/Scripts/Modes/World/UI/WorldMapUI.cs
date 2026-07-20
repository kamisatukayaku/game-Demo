using System.Collections.Generic;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.World
{
  /// <summary>
  /// POI 地图 UI — M 键打开的 World 模式全局地图。
  /// 使用 Resources/Sprites/Icons/POI/ 下的素材渲染图标。
  /// 打开地图时游戏暂停。
  /// </summary>
  public class WorldMapUI : MonoBehaviour
  {
    static WorldMapUI s_instance;
    static readonly Color PanelBg = new(0.04f, 0.05f, 0.08f, 0.95f);
    static readonly Color DestroyedTint = new(0.4f, 0.4f, 0.4f, 0.5f);
    static readonly Color HiddenTint = new(0.3f, 0.3f, 0.35f, 0.2f);

    const float MapScale = 0.6f;
    const float IconSize = 24f;
    const float ZoomMin = 0.3f;
    const float ZoomMax = 3f;

    Font _font;
    GameObject _panel;
    RectTransform _viewport;
    RectTransform _content;
    float _zoom = 1f;
    Vector2 _dragStartPos;
    Vector2 _dragStartContentPos;

    // POI 图标精灵
    Sprite _sprCamp;
    Sprite _sprWildBoss;
    Sprite _sprFinalBoss;
    Sprite _sprShop;
    Sprite _sprEvent;

    readonly List<MarkerIcon> _icons = new();
    GameObject _tooltipGo;
    Text _tooltipText;
    bool _tooltipVisible;

    public static bool IsOpen => s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_WorldMapUI");
      DontDestroyOnLoad(go);
      go.AddComponent<WorldMapUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      LoadSprites();
      BuildUI();
      _panel.SetActive(false);
    }

    void LoadSprites()
    {
      const string path = "Sprites/Icons/POI/";
      _sprCamp      = Resources.Load<Sprite>(path + "camp");
      _sprWildBoss  = Resources.Load<Sprite>(path + "wild_boss");
      _sprFinalBoss = Resources.Load<Sprite>(path + "final_boss");
      _sprShop      = Resources.Load<Sprite>(path + "shop");
      _sprEvent     = Resources.Load<Sprite>(path + "incident");
    }

    void OnDestroy() { if (s_instance == this) s_instance = null; }
    void Update() { if (GameInputBindings.WasPressed(WorldInputKeys.OpenMap)) Toggle(); if (IsOpen) HandleMapInput(); }

    void Toggle()
    {
      if (_panel.activeSelf)
      {
        _panel.SetActive(false);
        WorldUILock.Release("map");
        if (WorldManager.Instance != null) WorldManager.Instance.IsPaused = false;
        return;
      }
      if (!WorldUILock.TryAcquire("map")) return;
      _panel.SetActive(true);
      if (WorldManager.Instance != null) WorldManager.Instance.IsPaused = true;
      BuildMarkers();
    }

    void BuildUI()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("WorldMapCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = 50;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 510);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = new GameObject("MapPanel");
      _panel.transform.SetParent(canvasGo.transform, false);
      var prt = _panel.AddComponent<RectTransform>();
      prt.anchorMin = Vector2.zero;
      prt.anchorMax = Vector2.one;
      prt.offsetMin = new Vector2(40, 40);
      prt.offsetMax = new Vector2(-40, -40);
      var pImg = _panel.AddComponent<Image>();
      pImg.color = PanelBg;
      pImg.raycastTarget = true;

      // Title
      var title = MakeLabel(_panel.transform, "Title", "世界地图 [M] 关闭", 18, FontStyle.Bold,
        TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 0.95f, 1f),
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(0, -8));

      // Viewport
      var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
      vpGo.transform.SetParent(_panel.transform, false);
      _viewport = vpGo.GetComponent<RectTransform>();
      _viewport.anchorMin = new Vector2(0, 0);
      _viewport.anchorMax = new Vector2(1, 1);
      _viewport.offsetMin = new Vector2(8, 8);
      _viewport.offsetMax = new Vector2(-8, -40);
      var vpImg = vpGo.GetComponent<Image>();
      vpImg.color = new Color(0f, 0f, 0f, 0f);
      vpImg.raycastTarget = true;
      vpGo.AddComponent<ViewportDragHandler>().OnDragEvent += OnDrag;
      vpGo.AddComponent<ViewportDragHandler>().OnBeginDragEvent += OnBeginDrag;

      // Content
      var ctGo = new GameObject("Content", typeof(RectTransform));
      ctGo.transform.SetParent(vpGo.transform, false);
      _content = ctGo.GetComponent<RectTransform>();
      _content.anchorMin = new Vector2(0.5f, 0.5f);
      _content.anchorMax = new Vector2(0.5f, 0.5f);
      _content.pivot = new Vector2(0.5f, 0.5f);
      _content.anchoredPosition = Vector2.zero;
      _content.sizeDelta = new Vector2(2000, 2000);

      // Tooltip
      _tooltipGo = new GameObject("Tooltip", typeof(RectTransform));
      _tooltipGo.transform.SetParent(canvasGo.transform, false);
      _tooltipGo.SetActive(false);
      var ttRt = _tooltipGo.GetComponent<RectTransform>();
      ttRt.pivot = new Vector2(0, 1);
      ttRt.sizeDelta = new Vector2(200, 80);
      var ttImg = _tooltipGo.AddComponent<Image>();
      ttImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
      _tooltipText = MakeLabelFloating(_tooltipGo.transform, "", 13);
    }

    void BuildMarkers()
    {
      foreach (var icon in _icons) Destroy(icon.Root);
      _icons.Clear();

      var wm = WorldManager.Instance;
      if (wm?.MapManager == null) return;

      var markers = wm.MapManager.GetAllMarkers();
      foreach (var m in markers)
      {
        var sprite = GetMarkerSprite(m.Type);
        var tint = GetMarkerStateTint(m.State);
        var icon = CreateMarkerIcon(_content, m.WorldPosition, sprite, tint, m);
        _icons.Add(icon);
      }
    }

    MarkerIcon CreateMarkerIcon(Transform parent, Vector2 worldPos, Sprite sprite, Color tint, MapMarker marker)
    {
      var go = new GameObject($"Marker_{marker.MarkerId}", typeof(RectTransform));
      go.transform.SetParent(parent, false);

      var rt = go.GetComponent<RectTransform>();
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = WorldToUI(worldPos);
      rt.sizeDelta = new Vector2(IconSize, IconSize);

      var img = go.AddComponent<Image>();
      img.sprite = sprite;
      img.color = tint;
      img.raycastTarget = true;
      img.preserveAspect = true;

      var hover = go.AddComponent<MapMarkerHover>();
      hover.Marker = marker;
      hover.Parent = this;

      return new MarkerIcon { Root = go, Rect = rt, Marker = marker };
    }

    Vector2 WorldToUI(Vector2 worldPos)
    {
      return worldPos * (MapScale * _zoom);
    }

    Sprite GetMarkerSprite(MapMarker.MarkerType type)
    {
      return type switch
      {
        MapMarker.MarkerType.Camp => _sprCamp,
        MapMarker.MarkerType.WildBoss => _sprWildBoss,
        MapMarker.MarkerType.FinalBoss => _sprFinalBoss,
        MapMarker.MarkerType.Merchant => _sprShop,
        MapMarker.MarkerType.EventPoint => _sprEvent,
        _ => _sprEvent
      };
    }

    Color GetMarkerStateTint(MapMarker.DiscoveryState state)
    {
      return state switch
      {
        MapMarker.DiscoveryState.Hidden => HiddenTint,
        MapMarker.DiscoveryState.Destroyed => DestroyedTint,
        _ => Color.white
      };
    }

    void HandleMapInput()
    {
      // Zoom
      var scroll = Input.GetAxis("Mouse ScrollWheel");
      if (!Mathf.Approximately(scroll, 0f))
      {
        _zoom = Mathf.Clamp(_zoom + scroll * 0.15f, ZoomMin, ZoomMax);
        _content.localScale = new Vector3(_zoom, _zoom, 1f);
        RefreshIconPositions();
      }

      // Close tooltip when not hovering
      if (_tooltipVisible && MapMarkerHover.CurrentHover == null)
      {
        _tooltipGo.SetActive(false);
        _tooltipVisible = false;
      }
    }

    void OnBeginDrag(Vector2 pos)
    {
      _dragStartPos = pos;
      _dragStartContentPos = _content.anchoredPosition;
    }

    void OnDrag(Vector2 pos)
    {
      _content.anchoredPosition = _dragStartContentPos + (pos - _dragStartPos);
    }

    void RefreshIconPositions()
    {
      foreach (var icon in _icons)
        icon.Rect.anchoredPosition = WorldToUI(icon.Marker.WorldPosition);
    }

    public void ShowTooltip(MapMarker marker, Vector3 worldPosition)
    {
      _tooltipVisible = true;
      var screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
      var ttRt = _tooltipGo.GetComponent<RectTransform>();
      ttRt.position = screenPos + new Vector2(16, 8);

      // Hidden 状态：仅显示"未探索区域"
      if (marker.State == MapMarker.DiscoveryState.Hidden)
      {
        _tooltipText.text = "???\n未探索区域";
        _tooltipGo.SetActive(true);
        return;
      }

      var typeName = marker.Type switch
      {
        MapMarker.MarkerType.Camp => "营地",
        MapMarker.MarkerType.WildBoss => "野外Boss巢穴",
        MapMarker.MarkerType.FinalBoss => "最终Boss巢穴",
        MapMarker.MarkerType.Merchant => "商店",
        MapMarker.MarkerType.EventPoint => "事件",
        _ => "?"
      };

      var stateLabel = marker.State switch
      {
        MapMarker.DiscoveryState.Destroyed => " [已摧毁]",
        MapMarker.DiscoveryState.Visited => " [已访问]",
        _ => ""
      };

      // Visited+ 状态展示编码详情
      string encodedDetail = "";
      if (marker.State >= MapMarker.DiscoveryState.Visited)
      {
        var wm = WorldManager.Instance;
        WorldCampData? campData = null;
        float bossHp = -1f, bossMaxHp = -1f;

        if (marker.Type == MapMarker.MarkerType.Camp || marker.Type == MapMarker.MarkerType.FinalBoss)
        {
          campData = wm?.MapManager?.GetCampInfoForMarker(marker);
        }

        // 野外Boss/Boss巢穴：查询实时血量
        if (marker.Type == MapMarker.MarkerType.WildBoss || marker.Type == MapMarker.MarkerType.FinalBoss)
        {
          var bossGo = FindBossByMarker(marker);
          if (bossGo != null)
          {
            var health = bossGo.GetComponent<Health>();
            if (health != null)
            {
              bossHp = health.CurrentHp;
              bossMaxHp = health.MaxHp;
            }
          }
        }

        encodedDetail = "\n" + marker.GetEncodedInfo(campData, bossHp, bossMaxHp);
      }

      _tooltipText.text = $"{marker.DisplayName}\n类型: {typeName}{stateLabel}\n坐标: ({marker.WorldPosition.x:F0}, {marker.WorldPosition.y:F0}){encodedDetail}";
      _tooltipGo.SetActive(true);
    }

    /// <summary>通过 MarkerId 查找对应的 Boss GameObject（匹配名称格式）。</summary>
    static GameObject FindBossByMarker(MapMarker marker)
    {
      var candidates = GameObject.FindObjectsOfType<GameObject>();
      foreach (var go in candidates)
      {
        if (go == null) continue;
        // Boss巢穴 boss 格式: [Camp]_wild_boss_xxx_boss
        if (go.name.Contains(marker.SubTypeId ?? "") && go.name.Contains("_boss"))
          return go;
        // 野外Boss 格式: [Wild]_wild_boss_xxx_0
        if (go.name.Contains(marker.SubTypeId ?? ""))
          return go;
      }
      return null;
    }

    public void HideTooltip()
    {
      _tooltipVisible = false;
      _tooltipGo.SetActive(false);
    }

    Text MakeLabel(Transform parent, string name, string text, int size, FontStyle style,
      TextAnchor align, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.offsetMin = oMin; rt.offsetMax = oMax;
      var label = go.AddComponent<Text>();
      label.font = _font; label.fontSize = size; label.fontStyle = style;
      label.alignment = align; label.color = color; label.text = text; label.raycastTarget = false;
      return label;
    }

    Text MakeLabelFloating(Transform parent, string text, int size)
    {
      var go = new GameObject("Text", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
      rt.offsetMin = new Vector2(8, 8); rt.offsetMax = new Vector2(-8, -8);
      var label = go.AddComponent<Text>();
      label.font = _font; label.fontSize = size; label.alignment = TextAnchor.UpperLeft;
      label.color = new Color(0.85f, 0.9f, 0.95f, 1f); label.text = text; label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      return label;
    }

    struct MarkerIcon
    {
      public GameObject Root;
      public RectTransform Rect;
      public MapMarker Marker;
    }

    sealed class MapMarkerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
      public MapMarker Marker;
      public WorldMapUI Parent;
      public static MapMarkerHover CurrentHover;

      public void OnPointerEnter(PointerEventData e)
      {
        CurrentHover = this;
        Parent.ShowTooltip(Marker, transform.position);
      }

      public void OnPointerExit(PointerEventData e)
      {
        CurrentHover = null;
        Parent.HideTooltip();
      }
    }

    sealed class ViewportDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
      public System.Action<Vector2> OnBeginDragEvent;
      public System.Action<Vector2> OnDragEvent;
      public void OnBeginDrag(PointerEventData e) => OnBeginDragEvent?.Invoke(e.position);
      public void OnDrag(PointerEventData e) => OnDragEvent?.Invoke(e.position);
    }
  }
}
