using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 事件 UI — F 键打开。靠近事件点时按 F 弹出事件选择面板。
  /// 监听 WorldEventBus.EventPresented 自动弹出。
  /// </summary>
  public class WorldEventUI : MonoBehaviour
  {
    static WorldEventUI s_instance;
    static readonly Color PanelBg = new(0.06f, 0.08f, 0.12f, 0.95f);
    static readonly Color Accent = new(0.36f, 0.72f, 0.82f, 1f);
    static readonly Color OptionBg = new(0.12f, 0.18f, 0.24f, 1f);
    static readonly Color OptionHover = new(0.20f, 0.28f, 0.36f, 1f);

    const float InteractRange = 10f;

    Font _font;
    GameObject _panel;
    Text _titleText;
    Text _descText;
    RectTransform _optionsContainer;
    WorldDatabase.EventDef _currentEvent;
    bool _visible;

    public static bool IsOpen => s_instance != null && s_instance._visible;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_WorldEventUI");
      DontDestroyOnLoad(go);
      go.AddComponent<WorldEventUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildCanvas();
      WorldEventBus.EventPresented += OnEventPresented;
      WorldEventBus.EventSuspended += OnEventSuspended;
      _panel.SetActive(false);
    }

    void OnDestroy()
    {
      WorldEventBus.EventPresented -= OnEventPresented;
      WorldEventBus.EventSuspended -= OnEventSuspended;
      if (s_instance == this) s_instance = null;
    }

    void Update()
    {
      // 事件进行中：禁止 Esc 关闭，必须走完流程或选择暂离
      if (_visible)
      {
        // 不监听 ClosePanel — 锁死事件流程
        return;
      }
      if (GameInputBindings.WasPressed(GameInputBindings.InputAction.Interact) && IsNearEventPoint())
        TryTriggerNearbyEvent();
    }

    bool IsNearEventPoint()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return false;
      var player = GetPlayer();
      if (player == null) return false;

      var wm = WorldManager.Instance;
      var mapMgr = wm?.MapManager;
      if (mapMgr == null) return false;

      var nearest = mapMgr.GetNearestMarkerOfType(MapMarker.MarkerType.EventPoint, player.position);
      if (nearest == null) return false;

      return Vector2.Distance(player.position, nearest.WorldPosition) <= InteractRange;
    }

    void TryTriggerNearbyEvent()
    {
      var wm = WorldManager.Instance;
      var eventMgr = wm?.GetSystem<EventManager>();
      if (eventMgr != null && !eventMgr.HasActiveEvent)
      {
        eventMgr.SelectAndTrigger("map_point");
      }
    }

    void OnEventPresented(string eventId)
    {
      var eventMgr = WorldManager.Instance?.GetSystem<EventManager>();
      if (eventMgr == null || !eventMgr.HasActiveEvent) return;
      _currentEvent = eventMgr.ActiveEvent;

      // 互斥锁
      if (!WorldUILock.TryAcquire("event")) return;

      // 事件开始 → 暂停游戏
      if (WorldManager.Instance != null) WorldManager.Instance.IsPaused = true;

      Show();
    }

    void OnEventSuspended(string eventId)
    {
      // 事件暂离 → 关闭UI，恢复游戏
      Close();
    }

    void Show()
    {
      if (_currentEvent == null) return;
      var eventMgr = WorldManager.Instance?.GetSystem<EventManager>();
      var ctx = eventMgr?.CurrentContext;
      if (ctx == null || ctx.Node == null) { Close(); return; }

      _visible = true;
      _panel.SetActive(true);
      _titleText.text = _currentEvent.display_name ?? _currentEvent.id;
      _descText.text = ctx.Node.description ?? "";

      // 清空选项
      for (int i = _optionsContainer.childCount - 1; i >= 0; i--)
        Destroy(_optionsContainer.GetChild(i).gameObject);

      // 渲染选项
      if (ctx.Options != null)
      {
        for (int i = 0; i < ctx.Options.Length; i++)
        {
          var opt = ctx.Options[i];
          var idx = i;
          var isSuspend = opt.is_suspend;
          CreateOptionButton(_optionsContainer, opt.text ?? $"选项 {i + 1}", i, () =>
          {
            var mgr = WorldManager.Instance?.GetSystem<EventManager>();
            if (mgr == null) return;

            if (isSuspend)
            {
              mgr.SuspendEvent(idx);
              return; // Close 由 OnEventSuspended 触发
            }

            mgr.ResolveCurrent(idx);

            // 检查是否还有后续节点
            var nextCtx = mgr.CurrentContext;
            if (nextCtx != null && nextCtx.HasOptions)
            {
              Show(); // 刷新显示下一节点
            }
            else
            {
              Close();
            }
          });
        }
      }
      else
      {
        // 无选项但也不是 end → 等待自动推进（已在 LoadNode 中处理完）
        Close();
      }
    }

    void Close()
    {
      _visible = false;
      _panel.SetActive(false);
      _currentEvent = null;
      WorldUILock.Release("event");
      // 恢复游戏（如果其他面板没有保持暂停）
      if (WorldManager.Instance != null && !WorldMapUI.IsOpen)
        WorldManager.Instance.IsPaused = false;
    }

    void BuildCanvas()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("EventUICanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = 100;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = CreatePanel(canvasGo.transform, "EventPanel", PanelBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        Vector2.zero, new Vector2(480, 320)).gameObject;

      _titleText = CreateLabel(_panel.transform, "Title", "", 24, FontStyle.Bold,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(440, 32));
      _titleText.alignment = TextAnchor.MiddleCenter;

      _descText = CreateLabel(_panel.transform, "Desc", "", 14, FontStyle.Normal,
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -52), new Vector2(-40, 60));
      _descText.alignment = TextAnchor.UpperLeft;
      _descText.color = new Color(0.85f, 0.9f, 0.95f, 1f);

      var optsHost = CreatePanel(_panel.transform, "OptionsHost", new Color(0.03f, 0.04f, 0.06f, 0.5f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      optsHost.offsetMin = new Vector2(20, 20);
      optsHost.offsetMax = new Vector2(-20, -120);

      var layout = optsHost.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 8);
      layout.childControlHeight = true; layout.childControlWidth = true;
      layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
      _optionsContainer = optsHost;
    }

    void CreateOptionButton(Transform parent, string label, int index, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, $"Option_{index}", OptionBg, new Vector2(0, 1), new Vector2(1, 1),
        Vector2.zero, new Vector2(0, 48));
      var btn = rt.gameObject.AddComponent<Button>();
      btn.targetGraphic = rt.GetComponent<Image>();

      var colors = btn.colors;
      colors.highlightedColor = OptionHover;
      colors.pressedColor = new Color(0.25f, 0.38f, 0.48f, 1f);
      btn.colors = colors;

      var text = CreateLabel(rt, "Label", label, 16, FontStyle.Normal,
        new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 0), new Vector2(-12, 0));
      text.alignment = TextAnchor.MiddleLeft;

      btn.onClick.AddListener(onClick);
    }

    RectTransform CreatePanel(Transform parent, string name, Color bg, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.pivot = aMin == aMax ? aMin : new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos; rt.sizeDelta = size;
      var img = go.AddComponent<Image>(); img.color = bg; img.raycastTarget = bg.a > 0.01f;
      return rt;
    }

    Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 wh)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.pivot = new Vector2(aMin.x, aMin.y == aMax.y ? 0.5f : 1f);
      rt.anchoredPosition = pos; rt.sizeDelta = wh;
      var label = go.AddComponent<Text>();
      label.font = _font; label.fontSize = size; label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
      label.text = text; label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      return label;
    }

    Transform GetPlayer()
    {
      var go = GameObject.FindWithTag("Player");
      if (go == null) go = GameObject.Find("Player");
      return go?.transform;
    }
  }
}
