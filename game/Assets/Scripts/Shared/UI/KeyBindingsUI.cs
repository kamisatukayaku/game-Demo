using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.UI
{
  /// <summary>键位自定义面板。默认 [K] 打开，开局界面也可进入。</summary>
  public class KeyBindingsUI : MonoBehaviour
  {
    static KeyBindingsUI s_instance;

    Canvas _canvas;
    GameObject _panel;
    RectTransform _panelRt;
    Font _font;
    readonly List<RowRefs> _rows = new();
    Text _hint;

    struct RowRefs
    {
      public GameInputBindings.InputAction Action;
      public Text KeyLabel;
    }

    public static bool IsOpen => s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_KeyBindingsUI");
      DontDestroyOnLoad(go);
      go.AddComponent<KeyBindingsUI>();
    }

    public static void Toggle()
    {
      EnsureExists();
      if (IsOpen) s_instance.Close();
      else s_instance.Open();
    }

    public static void OpenPanel()
    {
      EnsureExists();
      s_instance.Open();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      GameInputBindings.EnsureLoaded();
      _font = UiFontHelper.GetFont();
      BuildUI();
      Close();
    }

    void OnDestroy()
    {
      GameInputBindings.Changed -= RebuildOrRefresh;
      if (s_instance == this) s_instance = null;
    }

    void Update()
    {
      if (!IsOpen && GameInputBindings.WasPressed(GameInputBindings.InputAction.KeySettings))
        Open();

      if (IsOpen && Input.GetKeyDown(KeyCode.Escape) && !GameInputBindings.IsRebinding)
      {
        Close();
        return;
      }

      if (GameInputBindings.IsRebinding)
      {
        if (_hint != null)
          _hint.text = $"按下新按键绑定「{GameInputBindings.GetDisplayName(GameInputBindings.RebindTarget)}」（Esc 取消）";

        GameInputBindings.TryCompleteRebind();
        RefreshAll();
        return;
      }

      if (_hint != null && IsOpen)
        _hint.text = StreamModeSettings.Enabled
          ? "点击按键栏位可重新绑定；F8 关闭 Stream 模式；Esc 关闭"
          : "点击按键栏位可重新绑定；F8 开启 Stream 模式；Esc 关闭";
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("KeyBindingsCanvas");
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 900);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = new GameObject("Panel", typeof(RectTransform));
      _panel.transform.SetParent(canvasGo.transform, false);
      _panelRt = _panel.GetComponent<RectTransform>();
      _panelRt.anchorMin = _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
      _panelRt.sizeDelta = new Vector2(520f, 520f);
      var panelImg = _panel.AddComponent<Image>();
      panelImg.color = new Color(0.06f, 0.1f, 0.14f, 0.95f);

      _hint = CreateLabel(_panel.transform, "Hint", "", 16, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(480f, 28f));
      _hint.color = new Color(0.75f, 0.85f, 0.92f, 1f);

      CreateActionButton(_panel.transform, "Reset", "恢复默认", new Vector2(-90f, 16f), () =>
      {
        GameInputBindings.ResetToDefaults();
        RefreshAll();
      });

      CreateActionButton(_panel.transform, "Close", "关闭 (Esc)", new Vector2(90f, 16f), Close);

      GameInputBindings.Changed += RebuildOrRefresh;
      RebuildRows();
    }

    /// <summary>
    /// 当 Changed 事件触发时，若注册动作数量变化则重建行，否则仅刷新标签。
    /// </summary>
    void RebuildOrRefresh()
    {
      var registered = GameInputBindings.RegisteredActions;
      int expectedRows = registered.Count;
      // 排除 KeySettings（不在 UI 中显示）
      for (int i = 0; i < registered.Count; i++)
        if (registered[i].Id == "KeySettings") expectedRows--;

      if (_rows.Count != expectedRows)
        RebuildRows();
      else
        RefreshAll();
    }

    /// <summary>完全重建行列表（支持动态注册后的 UI 刷新）。</summary>
    void RebuildRows()
    {
      // 销毁旧行
      for (int i = _rows.Count - 1; i >= 0; i--)
      {
        var rowGo = _rows[i].KeyLabel?.transform?.parent?.gameObject;
        if (rowGo != null) Destroy(rowGo);
      }
      _rows.Clear();

      // 创建标题
      var existingTitle = _panel.transform.Find("Title");
      if (existingTitle != null) Destroy(existingTitle.gameObject);

      CreateLabel(_panel.transform, "Title", "键位设置", 28, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 40f));

      // 按注册顺序创建行（跳过 KeySettings）
      var registered = GameInputBindings.RegisteredActions;
      float y = -96f;
      int visibleCount = 0;

      for (int i = 0; i < registered.Count; i++)
      {
        var action = registered[i];
        if (action.Id == "KeySettings") continue;

        CreateRow(action, y);
        y -= 52f;
        visibleCount++;
      }

      // 动态调整面板高度
      float panelHeight = Mathf.Max(240f, 140f + visibleCount * 52f);
      _panelRt.sizeDelta = new Vector2(520f, panelHeight);

      RefreshAll();
    }

    void CreateRow(GameInputBindings.InputAction action, float y)
    {
      var row = new GameObject($"Row_{action.Id}", typeof(RectTransform));
      row.transform.SetParent(_panel.transform, false);
      var rt = row.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
      rt.anchoredPosition = new Vector2(0f, y);
      rt.sizeDelta = new Vector2(460f, 44f);

      var bg = row.AddComponent<Image>();
      bg.color = new Color(0.1f, 0.16f, 0.2f, 1f);

      CreateLabel(row.transform, "Name", GameInputBindings.GetDisplayName(action), 18, FontStyle.Normal,
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(220f, 32f))
        .alignment = TextAnchor.MiddleLeft;

      var btnGo = new GameObject("KeyButton", typeof(RectTransform));
      btnGo.transform.SetParent(row.transform, false);
      var btnRt = btnGo.GetComponent<RectTransform>();
      btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0.5f);
      btnRt.anchoredPosition = new Vector2(-84f, 0f);
      btnRt.sizeDelta = new Vector2(150f, 34f);
      var btnImg = btnGo.AddComponent<Image>();
      btnImg.color = new Color(0.16f, 0.34f, 0.44f, 1f);
      var btn = btnGo.AddComponent<Button>();
      btn.targetGraphic = btnImg;

      var keyLabel = CreateLabel(btnGo.transform, "Key", "", 16, FontStyle.Bold,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      keyLabel.alignment = TextAnchor.MiddleCenter;

      var captured = action;
      btn.onClick.AddListener(() =>
      {
        GameInputBindings.BeginRebind(captured);
        RefreshAll();
      });

      _rows.Add(new RowRefs { Action = action, KeyLabel = keyLabel });
    }

    void RefreshAll()
    {
      foreach (var row in _rows)
      {
        if (row.KeyLabel != null)
          row.KeyLabel.text = GameInputBindings.FormatKey(GameInputBindings.Get(row.Action));
      }
    }

    void Open()
    {
      if (_panel != null) _panel.SetActive(true);
      RefreshAll();
    }

    void Close()
    {
      GameInputBindings.CancelRebind();
      if (_panel != null) _panel.SetActive(false);
    }

    void CreateActionButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = new Vector2(160f, 40f);
      var img = go.AddComponent<Image>();
      img.color = new Color(0.18f, 0.42f, 0.52f, 1f);
      var btn = go.AddComponent<Button>();
      btn.targetGraphic = img;
      btn.onClick.AddListener(onClick);

      var text = CreateLabel(go.transform, "Label", label, 16, FontStyle.Normal,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      text.alignment = TextAnchor.MiddleCenter;
    }

    Text CreateLabel(Transform parent, string name, string textValue, int size, FontStyle style,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(anchorMin.x, 0.5f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;

      var text = go.AddComponent<Text>();
      text.font = _font;
      text.fontSize = size;
      text.fontStyle = style;
      text.text = textValue;
      text.color = Color.white;
      text.raycastTarget = false;
      return text;
    }
  }
}
