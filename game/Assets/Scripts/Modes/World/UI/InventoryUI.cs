using System.Collections.Generic;
using System.Text;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 背包 UI — Tab 键打开，只读查看。
  ///
  /// 功能：
  ///   - Tab 键切换显示/隐藏
  ///   - Esc 键关闭
  ///   - 两个标签页：武器道具 / 饰品
  ///   - 悬停物品显示详情 Tooltip（道具效果参数、饰品词条）
  ///   - 只读，无丢弃/排序等交互按钮
  /// </summary>
  public class InventoryUI : MonoBehaviour
  {
    static InventoryUI s_instance;
    static readonly Color PanelBg = new(0.06f, 0.08f, 0.12f, 0.95f);
    static readonly Color Accent = new(0.45f, 0.75f, 1f, 1f);
    static readonly Color TabActiveBg = new(0.12f, 0.22f, 0.35f, 1f);
    static readonly Color TabInactiveBg = new(0.10f, 0.14f, 0.20f, 1f);
    static readonly Color RowBg = new(0.12f, 0.18f, 0.24f, 1f);
    static readonly Color WeaponColor = new(1f, 0.75f, 0.35f, 1f);
    static readonly Color AccessoryColor = new(0.55f, 0.85f, 0.55f, 1f);
    static readonly Color TooltipBg = new(0.04f, 0.06f, 0.10f, 0.97f);
    static readonly Color DescColor = new(0.70f, 0.78f, 0.85f, 1f);

    Font _font;
    GameObject _panel;
    Text _titleText;
    Text _tabWeaponLabel;
    Text _tabAccessoryLabel;
    RectTransform _listContent;
    ScrollRect _scroll;
    GameObject _tooltipPanel;
    Text _tooltipText;
    bool _visible;
    bool _showAccessories; // false = weapons, true = accessories
    string _hoveredItemId;  // 当前悬停的物品 ID

    public static bool IsOpen => s_instance != null && s_instance._visible;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_InventoryUI");
      DontDestroyOnLoad(go);
      go.AddComponent<InventoryUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildCanvas();
      _panel.SetActive(false);

      // 订阅背包变化事件，自动刷新
      WorldRuntimeContext.InventoryChanged += OnInventoryChanged;
    }

    void OnDestroy()
    {
      WorldRuntimeContext.InventoryChanged -= OnInventoryChanged;
      if (s_instance == this) s_instance = null;
    }

    void OnInventoryChanged()
    {
      if (_visible) Refresh();
    }

    void Update()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      if (GameInputBindings.WasPressed(GameInputBindings.InputAction.Inventory))
      {
        if (_visible) Close();
        else Open();
      }

      if (_visible && GameInputBindings.WasPressed(WorldInputKeys.ClosePanel))
        Close();

      // 物品栏绑定（在背包打开时按下 1-9 将悬停物品绑定到槽位）
      if (_visible && !string.IsNullOrEmpty(_hoveredItemId))
      {
        for (int i = 0; i < 9; i++)
        {
          if (GameInputBindings.WasPressed(WorldInputKeys.SelectSlots[i]))
          {
            WorldRuntimeContext.BindItemToSlot(i, _hoveredItemId);
            break;
          }
        }
      }

      // 检查鼠标是否移出面板，隐藏 Tooltip
      if (_visible && _tooltipPanel.activeSelf)
      {
        if (!RectTransformUtility.RectangleContainsScreenPoint(
          _panel.GetComponent<RectTransform>(), Input.mousePosition, null))
        {
          _hoveredItemId = null;
          _tooltipPanel.SetActive(false);
        }
      }
    }

    void Open()
    {
      if (!WorldUILock.TryAcquire("inventory")) return;
      _visible = true;
      _panel.SetActive(true);
      Refresh();
    }

    void Close()
    {
      _visible = false;
      _panel.SetActive(false);
      _tooltipPanel.SetActive(false);
      WorldUILock.Release("inventory");
    }

    void Refresh()
    {
      _titleText.text = "背包";

      // 更新 Tab 标签颜色
      _tabWeaponLabel.color = _showAccessories ? Color.white : Accent;
      _tabAccessoryLabel.color = _showAccessories ? Accent : Color.white;

      // 清空列表
      for (int i = _listContent.childCount - 1; i >= 0; i--)
        Destroy(_listContent.GetChild(i).gameObject);

      var wm = WorldManager.Instance;
      var inv = wm?.Inventory;
      if (inv == null) return;

      if (!_showAccessories)
      {
        // 武器/道具列表
        var weapons = inv.Weapons;
        if (weapons != null)
        {
          foreach (var kv in weapons)
            CreateItemRow(_listContent, kv.Key, kv.Value, false);
        }
      }
      else
      {
        // 饰品列表（每个独立显示，聚合同种数量）
        var accessories = inv.Accessories;
        if (accessories != null)
        {
          var grouped = new Dictionary<string, int>();
          foreach (var id in accessories)
          {
            if (grouped.ContainsKey(id)) grouped[id]++;
            else grouped[id] = 1;
          }
          foreach (var kv in grouped)
            CreateItemRow(_listContent, kv.Key, kv.Value, true);
        }
      }

      // 空背包提示
      if (_listContent.childCount == 0)
      {
        var emptyLabel = CreateLabel(_listContent, "Empty", "背包是空的", 14, FontStyle.Italic,
          new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(300, 30));
        emptyLabel.color = DescColor;
        emptyLabel.alignment = TextAnchor.MiddleCenter;
      }
    }

    void CreateItemRow(Transform parent, string itemId, int count, bool isAccessory)
    {
      var def = WorldDatabase.GetItem(itemId);
      var displayName = def?.display_name ?? itemId;
      var desc = def?.description ?? "";
      var quality = def?.ParsedQuality ?? WorldDatabase.ItemQuality.Common;
      var itemColor = WorldDatabase.QualityColor(quality);

      var rt = CreatePanel(parent, $"Row_{itemId}", RowBg,
        new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 54));

      // 左侧色块图标（品质色）
      var iconRt = CreatePanel(rt, "Icon", itemColor,
        new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(8, 36));

      // 名称（品质色）
      var nameLabel = CreateLabel(rt, "Name", displayName, 15, FontStyle.Bold,
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -6), new Vector2(-80, 24));
      nameLabel.alignment = TextAnchor.UpperLeft;
      nameLabel.color = itemColor;

      // 描述（截断）
      var shortDesc = desc.Length > 40 ? desc.Substring(0, 40) + "..." : desc;
      var descLabel = CreateLabel(rt, "Desc", shortDesc, 11, FontStyle.Normal,
        new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 24), new Vector2(-80, -12));
      descLabel.alignment = TextAnchor.LowerLeft;
      descLabel.color = DescColor;

      // 数量（右上角）
      var countLabel = CreateLabel(rt, "Count", $"x{count}", 14, FontStyle.Bold,
        new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -6), new Vector2(60, 22));
      countLabel.alignment = TextAnchor.UpperRight;
      countLabel.color = new Color(0.75f, 0.82f, 0.88f, 1f);

      // 悬停触发器 — Tooltip
      var hoverTrigger = rt.gameObject.AddComponent<HoverTrigger>();
      hoverTrigger.Init(() => ShowTooltip(itemId, isAccessory), () => HideTooltip());
    }

    void ShowTooltip(string itemId, bool isAccessory)
    {
      _hoveredItemId = itemId;
      var def = WorldDatabase.GetItem(itemId);
      if (def == null) return;

      var sb = new StringBuilder();

      // 标题
      var qualityStr = FormatQuality(def.ParsedQuality);
      sb.AppendLine($"<b>{def.display_name}</b>  {qualityStr}");
      sb.AppendLine();

      // 描述
      if (!string.IsNullOrEmpty(def.description))
      {
        sb.AppendLine(def.description);
        sb.AppendLine();
      }

      if (isAccessory)
      {
        // 饰品词条
        var affixes = def.GetAffixes();
        if (affixes.Count > 0)
        {
          sb.AppendLine("<b>饰品词条：</b>");
          foreach (var kv in affixes)
            sb.AppendLine($"  {FormatAffixKey(kv.Key)}{FormatAffixValue(kv.Key, kv.Value)}");
        }
      }
      else
      {
        // 道具效果
        if (!string.IsNullOrEmpty(def.effect_type))
        {
          sb.AppendLine($"<b>效果：</b>{FormatEffectType(def.effect_type)}");
        }
        var effectParams = def.GetEffectParams();
        if (effectParams.Count > 0)
        {
          sb.AppendLine("<b>参数：</b>");
          foreach (var kv in effectParams)
            sb.AppendLine($"  {FormatParamKey(kv.Key)}: {FormatParamValue(kv.Value)}");
        }
      }

      _tooltipText.text = sb.ToString().TrimEnd();
      _tooltipPanel.SetActive(true);

      // 定位 Tooltip 在鼠标右侧
      var mousePos = Input.mousePosition;
      var tooltipRt = _tooltipPanel.GetComponent<RectTransform>();
      // 计算 Tooltip 尺寸
      var textGenSettings = _tooltipText.GetGenerationSettings(_tooltipText.rectTransform.rect.size);
      var preferredHeight = _tooltipText.cachedTextGenerator.GetPreferredHeight(
        _tooltipText.text, textGenSettings);
      var canvas = _tooltipPanel.GetComponentInParent<Canvas>();
      var scaleFactor = canvas?.scaleFactor ?? 1f;

      // 将屏幕坐标转为 Canvas 局部坐标
      var panelRt = _panel.GetComponent<RectTransform>();
      Vector2 localPoint;
      if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
        panelRt, mousePos, null, out localPoint))
      {
        float tooltipW = 260f;
        float tooltipH = Mathf.Max(preferredHeight + 30f, 60f);
        tooltipRt.sizeDelta = new Vector2(tooltipW, tooltipH);

        // 放在鼠标右侧下方
        float offsetX = localPoint.x + 20f;
        float offsetY = localPoint.y - 10f;

        // 边界检查，防止超出面板
        var panelSize = panelRt.rect;
        if (offsetX + tooltipW > panelSize.xMax - 10f)
          offsetX = localPoint.x - tooltipW - 20f;
        if (offsetY - tooltipH < panelSize.yMin + 10f)
          offsetY = localPoint.y + tooltipH + 10f;

        tooltipRt.anchoredPosition = new Vector2(offsetX, offsetY);
      }
    }

    void HideTooltip()
    {
      _tooltipPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  Tab 按钮回调
    // ══════════════════════════════════════════════════════

    void SwitchToWeapons()
    {
      _showAccessories = false;
      Refresh();
    }

    void SwitchToAccessories()
    {
      _showAccessories = true;
      Refresh();
    }

    // ══════════════════════════════════════════════════════
    //  格式化辅助
    // ══════════════════════════════════════════════════════

    static string FormatQuality(WorldDatabase.ItemQuality q)
    {
      switch (q)
      {
        case WorldDatabase.ItemQuality.Uncommon: return "<color=#1EFF00>[精良]</color>";
        case WorldDatabase.ItemQuality.Rare: return "<color=#4040FF>[稀有]</color>";
        case WorldDatabase.ItemQuality.Epic: return "<color=#C040F0>[史诗]</color>";
        case WorldDatabase.ItemQuality.Legendary: return "<color=#FF8000>[传说]</color>";
        default: return "<color=#CCCCCC>[普通]</color>";
      }
    }

    static string FormatEffectType(string type)
    {
      switch (type)
      {
        case "area_damage": return "范围伤害";
        case "heal": return "回复生命";
        case "chain_lightning": return "连锁闪电";
        case "area_slow": return "范围减速";
        default: return type;
      }
    }

    static string FormatParamKey(string key)
    {
      switch (key)
      {
        case "damage": return "伤害";
        case "radius": return "半径";
        case "heal_amount": return "回复量";
        case "bounce_count": return "弹跳次数";
        case "bounce_radius": return "弹跳半径";
        case "slow_percent": return "减速比例";
        case "duration": return "持续时间";
        default: return key;
      }
    }

    static string FormatParamValue(float value)
    {
      // 小于等于 1 的值显示为百分比（仅当看起来像比例时）
      if (value <= 1f && value > 0f)
        return (value * 100f).ToString("F0") + "%";
      if (value == (int)value)
        return ((int)value).ToString();
      return value.ToString("F1");
    }

    static string FormatAffixKey(string key)
    {
      switch (key)
      {
        case "attack_mult": return "攻击力";
        case "max_hp": return "最大生命值";
        case "hp_regen": return "生命回复";
        case "move_speed_mult": return "移动速度";
        case "dodge_chance": return "闪避率";
        case "thorns_damage": return "荆棘伤害";
        case "defense": return "防御力";
        case "crit_chance": return "暴击率";
        case "crit_damage_mult": return "暴击伤害";
        default: return key;
      }
    }

    static string FormatAffixValue(string key, float value)
    {
      // 比率类以百分比显示
      if (key.EndsWith("_mult") || key.EndsWith("_chance"))
        return $" +{(value * 100f).ToString("F0")}%";
      if (value == (int)value)
        return $" +{(int)value}";
      return $" +{value:F1}";
    }

    // ══════════════════════════════════════════════════════
    //  Canvas 构建
    // ══════════════════════════════════════════════════════

    void BuildCanvas()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("InventoryUICanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = 105;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();

      // 主面板
      _panel = CreatePanel(canvasGo.transform, "InventoryPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 460)).gameObject;

      // 标题
      _titleText = CreateLabel(_panel.transform, "Title", "背包", 24, FontStyle.Bold,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(200, 32));
      _titleText.alignment = TextAnchor.MiddleCenter;

      // ── Tab 栏 ──
      BuildTabBar();

      // ── Scroll View ──
      BuildScrollView();

      // ── Tooltip ──
      BuildTooltip();
    }

    void BuildTabBar()
    {
      var tabBarGo = new GameObject("TabBar", typeof(RectTransform));
      tabBarGo.transform.SetParent(_panel.transform, false);
      var tabBarRt = tabBarGo.GetComponent<RectTransform>();
      tabBarRt.anchorMin = new Vector2(0, 1); tabBarRt.anchorMax = new Vector2(1, 1);
      tabBarRt.pivot = new Vector2(0.5f, 1);
      tabBarRt.anchoredPosition = new Vector2(0, -48);
      tabBarRt.sizeDelta = new Vector2(0, 36);

      // 武器 Tab
      var weaponTabGo = new GameObject("WeaponTab", typeof(RectTransform));
      weaponTabGo.transform.SetParent(tabBarRt, false);
      var weaponTabRt = weaponTabGo.GetComponent<RectTransform>();
      weaponTabRt.anchorMin = new Vector2(0, 0); weaponTabRt.anchorMax = new Vector2(0.5f, 1);
      weaponTabRt.offsetMin = new Vector2(12, 0); weaponTabRt.offsetMax = new Vector2(-6, 0);
      var weaponTabImg = weaponTabGo.AddComponent<Image>();
      weaponTabImg.color = TabActiveBg;
      var weaponTabBtn = weaponTabGo.AddComponent<Button>();
      weaponTabBtn.onClick.AddListener(SwitchToWeapons);
      _tabWeaponLabel = CreateLabel(weaponTabRt, "Label", "武器道具", 15, FontStyle.Bold,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _tabWeaponLabel.color = Accent;

      // 饰品 Tab
      var accTabGo = new GameObject("AccessoryTab", typeof(RectTransform));
      accTabGo.transform.SetParent(tabBarRt, false);
      var accTabRt = accTabGo.GetComponent<RectTransform>();
      accTabRt.anchorMin = new Vector2(0.5f, 0); accTabRt.anchorMax = new Vector2(1, 1);
      accTabRt.offsetMin = new Vector2(6, 0); accTabRt.offsetMax = new Vector2(-12, 0);
      var accTabImg = accTabGo.AddComponent<Image>();
      accTabImg.color = TabInactiveBg;
      var accTabBtn = accTabGo.AddComponent<Button>();
      accTabBtn.onClick.AddListener(SwitchToAccessories);
      _tabAccessoryLabel = CreateLabel(accTabRt, "Label", "饰品", 15, FontStyle.Bold,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _tabAccessoryLabel.color = Color.white;

      // Tab 点击时更新颜色
      weaponTabBtn.onClick.AddListener(() =>
      {
        weaponTabImg.color = TabActiveBg;
        accTabImg.color = TabInactiveBg;
      });
      accTabBtn.onClick.AddListener(() =>
      {
        weaponTabImg.color = TabInactiveBg;
        accTabImg.color = TabActiveBg;
      });
    }

    void BuildScrollView()
    {
      var scrollGo = new GameObject("Scroll", typeof(RectTransform));
      scrollGo.transform.SetParent(_panel.transform, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1);
      scrollRt.offsetMin = new Vector2(12, 12); scrollRt.offsetMax = new Vector2(-12, -92);

      _scroll = scrollGo.AddComponent<ScrollRect>();
      _scroll.horizontal = false;
      _scroll.movementType = ScrollRect.MovementType.Clamped;

      var viewport = CreatePanel(scrollGo.transform, "Viewport", Color.clear,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      _scroll.viewport = viewport;

      var content = CreatePanel(viewport, "Content", Color.clear,
        new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 0));
      content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
      content.pivot = new Vector2(0.5f, 1);

      var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 5; layout.padding = new RectOffset(0, 0, 2, 2);
      layout.childControlHeight = true; layout.childControlWidth = true;
      layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
      content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      _scroll.content = content;
      _listContent = content;
    }

    void BuildTooltip()
    {
      _tooltipPanel = CreatePanel(_panel.transform, "Tooltip", TooltipBg,
        new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(260, 100)).gameObject;
      _tooltipPanel.SetActive(false);

      _tooltipText = CreateLabel(_tooltipPanel.transform, "Text", "", 12, FontStyle.Normal,
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -8), new Vector2(244, -8));
      _tooltipText.alignment = TextAnchor.UpperLeft;
      _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
      _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
      _tooltipText.supportRichText = true;
    }

    // ══════════════════════════════════════════════════════
    //  UI 工具方法
    // ══════════════════════════════════════════════════════

    RectTransform CreatePanel(Transform parent, string name, Color bg, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.pivot = aMin == aMax ? aMin : new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos; rt.sizeDelta = size;
      if (bg.a > 0.001f) { var img = go.AddComponent<Image>(); img.color = bg; img.raycastTarget = bg.a > 0.01f; }
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
  }

  /// <summary>
  /// 悬停触发器：鼠标进入 → ShowTooltip，鼠标离开 → HideTooltip。
  /// 用于背包物品行的 Tooltip 显示。
  /// </summary>
  public class HoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    System.Action _onEnter;
    System.Action _onExit;

    public void Init(System.Action onEnter, System.Action onExit)
    {
      _onEnter = onEnter;
      _onExit = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
      _onEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      _onExit?.Invoke();
    }
  }
}
