using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 物品栏 UI — 左侧纵向 9 个槽位。
  /// 显示绑定物品的图标/名称/数量，高亮当前选中的槽位。
  /// 按下 1-9 在未开背包时选中槽位（高亮），按下使用键消耗物品。
  /// </summary>
  public class ItemSlotBar : MonoBehaviour
  {
    static ItemSlotBar s_instance;

    Font _font;
    GameObject _panel;
    SlotRow[] _slots = new SlotRow[9];

    struct SlotRow
    {
      public GameObject Root;
      public Image Bg;
      public Text Label;
      public Text CountText;
    }

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_ItemSlotBar");
      DontDestroyOnLoad(go);
      go.AddComponent<ItemSlotBar>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildUI();
      WorldRuntimeContext.ItemSlotsChanged += Refresh;
      Refresh();
    }

    void OnDestroy()
    {
      WorldRuntimeContext.ItemSlotsChanged -= Refresh;
      if (s_instance == this) s_instance = null;
    }

    void Update()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      // 按键 1-9 切换选中槽位（背包关闭时）
      if (!InventoryUI.IsOpen)
      {
        for (int i = 0; i < 9; i++)
        {
          if (GameInputBindings.WasPressed(WorldInputKeys.SelectSlots[i]))
          {
            WorldRuntimeContext.SelectedItemSlot =
              WorldRuntimeContext.SelectedItemSlot == i ? -1 : i;
            Refresh();
          }
        }
      }
    }

    void Refresh()
    {
      var slots = WorldRuntimeContext.ItemSlots;
      int selected = WorldRuntimeContext.SelectedItemSlot;
      var inv = WorldManager.Instance?.Inventory;

      for (int i = 0; i < 9; i++)
      {
        var itemId = slots[i];
        bool hasItem = !string.IsNullOrEmpty(itemId);
        var def = hasItem ? WorldDatabase.GetItem(itemId) : null;
        int count = hasItem && inv != null ? inv.GetItemCount(itemId) : 0;

        var s = _slots[i];
        s.Label.text = hasItem ? (def?.display_name ?? itemId) : (i + 1).ToString();
        s.CountText.text = hasItem && count > 0 ? $"x{count}" : "";
        s.Label.color = hasItem ? WorldDatabase.QualityColor(def?.ParsedQuality ?? WorldDatabase.ItemQuality.Common) : new Color(0.4f, 0.4f, 0.4f);

        bool isSelected = i == selected && hasItem;
        s.Bg.color = isSelected
          ? new Color(0.3f, 0.5f, 0.7f, 1f)
          : new Color(0.08f, 0.10f, 0.14f, 0.9f);
      }
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("ItemSlotCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 90;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = new GameObject("Panel", typeof(RectTransform));
      _panel.transform.SetParent(canvasGo.transform, false);
      var panelRt = _panel.GetComponent<RectTransform>();
      panelRt.anchorMin = new Vector2(0, 0.5f); panelRt.anchorMax = new Vector2(0, 0.5f);
      panelRt.pivot = new Vector2(0, 0.5f);
      panelRt.anchoredPosition = new Vector2(8, 0);
      panelRt.sizeDelta = new Vector2(56, 480);

      for (int i = 0; i < 9; i++)
      {
        float y = 228 - i * 54;
        var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
        slotGo.transform.SetParent(_panel.transform, false);
        var rt = slotGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(46, 46);

        var bg = slotGo.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);

        var label = CreateLabel(slotGo.transform, $"Lbl{i}", (i + 1).ToString(), 12, FontStyle.Bold,
          Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        var countText = CreateLabel(slotGo.transform, $"Cnt{i}", "", 10, FontStyle.Normal,
          new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(40, 14));
        countText.alignment = TextAnchor.MiddleCenter;

        _slots[i] = new SlotRow { Root = slotGo, Bg = bg, Label = label, CountText = countText };
      }
    }

    Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 wh)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.anchoredPosition = pos; rt.sizeDelta = wh;
      var label = go.AddComponent<Text>();
      label.font = _font; label.fontSize = size; label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
      label.text = text; label.raycastTarget = false;
      return label;
    }
  }
}
