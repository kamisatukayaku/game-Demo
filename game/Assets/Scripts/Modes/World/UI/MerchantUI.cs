using System.Collections.Generic;
using System.Text;
using Game.Shared.Core;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Health;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 商人商店 UI — Interact 键打开。
  ///
  /// 品类概率制刷新：武器/道具和饰品按品质分类，Buff/HP/XP 独立。
  /// 每类有独立刷新概率、购买上限。仅显示满足世界等级的商品。
  /// 购买后执行实际效果：加经验、回血、加Buff、物品入背包。
  /// </summary>
  public class MerchantUI : MonoBehaviour
  {
    static MerchantUI s_instance;
    static readonly Color PanelBg     = new(0.06f, 0.08f, 0.12f, 0.95f);
    static readonly Color Accent      = new(1f, 0.84f, 0.25f, 1f);
    static readonly Color RowBg       = new(0.12f, 0.18f, 0.24f, 1f);
    static readonly Color Unaffordable = new(0.3f, 0.3f, 0.3f, 1f);
    static readonly Color HealColor   = new(0.3f, 1f, 0.3f, 1f);
    static readonly Color ExpColor    = new(0.5f, 0.8f, 1f, 1f);

    const float InteractRange = 10f;

    Font _font;
    GameObject _panel;
    Text _titleText, _goldBalanceText;
    RectTransform _listContent;
    MapMarker _currentMerchant;
    bool _visible;

    // 运行时生成的商品池 + 购买追踪
    readonly List<ShopItem> _shopItems = new();
    readonly Dictionary<string, int> _purchased = new();

    struct ShopItem
    {
      public string Category;        // weapon_common / buff / heal / exp / accessory_rare ...
      public string DisplayName;
      public string Description;
      public int Price;
      public string DetailId;        // item_id 或 buff_id
      public int BatchCount;
      public Color NameColor;
    }

    public static bool IsOpen => s_instance != null && s_instance._visible;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_MerchantUI");
      DontDestroyOnLoad(go);
      go.AddComponent<MerchantUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildCanvas();
      _panel.SetActive(false);
    }

    void OnDestroy() { if (s_instance == this) s_instance = null; }

    void Update()
    {
      if (_visible && GameInputBindings.WasPressed(WorldInputKeys.ClosePanel))
        Close();
      if (!_visible && !WorldUILock.IsLocked && GameInputBindings.WasPressed(GameInputBindings.InputAction.Interact))
        TryOpen();
    }

    // ══════════════════════════════════════════════════════
    //  打开/关闭
    // ══════════════════════════════════════════════════════

    void TryOpen()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;
      var wm = WorldManager.Instance;
      var mapMgr = wm?.GetSystem<WorldMapManager>();
      if (mapMgr == null) return;
      var player = GetPlayer();
      if (player == null) return;

      var markers = mapMgr.GetMarkersByType(MapMarker.MarkerType.Merchant);
      for (int i = 0; i < markers.Count; i++)
      {
        var m = markers[i];
        if (m.State == MapMarker.DiscoveryState.Destroyed) continue;
        if (Vector2.Distance(player.position, m.WorldPosition) <= InteractRange)
        {
          _currentMerchant = m;
          Open();
          return;
        }
      }
    }

    void Open()
    {
      if (_currentMerchant == null) return;
      if (!WorldUILock.TryAcquire("merchant")) return;
      _visible = true;
      _panel.SetActive(true);
      RollShopItems();
      Refresh();
    }

    void Close()
    {
      _visible = false;
      _panel.SetActive(false);
      _currentMerchant = null;
      _shopItems.Clear();
      _purchased.Clear();
      WorldUILock.Release("merchant");
    }

    // ══════════════════════════════════════════════════════
    //  品类概率刷新
    // ══════════════════════════════════════════════════════

    void RollShopItems()
    {
      _shopItems.Clear();
      _purchased.Clear();

      var shopDef = WorldDatabase.GetShop(_currentMerchant.SubTypeId);
      if (shopDef?.categories == null) return;

      int worldLevel = WorldRuntimeContext.WorldLevel;
      if (worldLevel < shopDef.min_world_level) return;

      int total = shopDef.total_items;
      for (int i = 0; i < total; i++)
      {
        // 按概率抽取品类
        float roll = Random.value;
        float cumulative = 0f;
        WorldDatabase.ShopCategoryDef pickedCat = null;
        foreach (var cat in shopDef.categories)
        {
          cumulative += cat.probability;
          if (roll <= cumulative && cat.max_purchases > 0 && cat.max_count > 0)
          { pickedCat = cat; break; }
        }
        if (pickedCat == null) continue;

        // 根据品类生成具体商品
        var item = RollCategoryItem(pickedCat, worldLevel);
        if (item != null) _shopItems.Add(item.Value);
      }
    }

    ShopItem? RollCategoryItem(WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      switch (cat.category)
      {
        case "weapon_common":   return RollWeaponItem("common", cat, worldLevel);
        case "weapon_uncommon": return RollWeaponItem("uncommon", cat, worldLevel);
        case "accessory_common":  return RollAccessoryItem("common", cat, worldLevel);
        case "accessory_uncommon": return RollAccessoryItem("uncommon", cat, worldLevel);
        case "accessory_rare":    return RollAccessoryItem("rare", cat, worldLevel);
        case "accessory_epic":    return RollAccessoryItem("epic", cat, worldLevel);
        case "buff":  return RollBuffItem(cat, worldLevel);
        case "heal":  return RollHealItem(cat, worldLevel);
        case "exp":   return RollExpItem(cat, worldLevel);
      }
      return null;
    }

    ShopItem? RollWeaponItem(string quality, WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      var candidates = new List<WorldDatabase.ItemDef>();
      foreach (var kv in WorldDatabase.Items)
      {
        var def = kv.Value;
        if (def.IsWeapon && def.quality == quality && def.shop_price > 0)
          candidates.Add(def);
      }
      if (candidates.Count == 0) return null;
      var picked = candidates[Random.Range(0, candidates.Count)];
      var color = WorldDatabase.QualityColor(picked.ParsedQuality);
      return new ShopItem
      {
        Category = cat.category, DisplayName = picked.display_name,
        Description = picked.description, Price = picked.shop_price,
        DetailId = picked.item_id, BatchCount = picked.shop_batch_count > 0 ? picked.shop_batch_count : 1,
        NameColor = color
      };
    }

    ShopItem? RollAccessoryItem(string quality, WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      var candidates = new List<WorldDatabase.ItemDef>();
      foreach (var kv in WorldDatabase.Items)
      {
        var def = kv.Value;
        if (def.IsAccessory && def.quality == quality && def.shop_price > 0)
          candidates.Add(def);
      }
      if (candidates.Count == 0) return null;
      var picked = candidates[Random.Range(0, candidates.Count)];
      var color = WorldDatabase.QualityColor(picked.ParsedQuality);
      return new ShopItem
      {
        Category = cat.category, DisplayName = picked.display_name,
        Description = picked.description, Price = picked.shop_price,
        DetailId = picked.item_id, BatchCount = Mathf.Max(1, picked.shop_batch_count),
        NameColor = color
      };
    }

    ShopItem? RollBuffItem(WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      var candidates = new List<WorldDatabase.ShopBuffDef>();
      foreach (var kv in WorldDatabase.ShopBuffs)
      {
        if (kv.Value.min_world_level <= worldLevel)
          candidates.Add(kv.Value);
      }
      if (candidates.Count == 0) return null;
      var picked = candidates[Random.Range(0, candidates.Count)];
      return new ShopItem
      {
        Category = "buff", DisplayName = picked.display_name,
        Description = picked.description, Price = picked.price,
        DetailId = picked.buff_id, BatchCount = 1,
        NameColor = new Color(0.6f, 1f, 0.6f, 1f) // 淡绿色
      };
    }

    ShopItem? RollHealItem(WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      float[] healPcts = { 0.25f, 0.50f, 1.00f };
      int[] prices     = { 30, 60, 120 };
      string[] names   = { "小型治疗", "中型治疗", "完全治疗" };

      int idx = worldLevel >= 5 ? 2 : worldLevel >= 3 ? 1 : 0;
      return new ShopItem
      {
        Category = "heal", DisplayName = names[idx],
        Description = $"回复 {healPcts[idx] * 100:F0}% 最大生命值。",
        Price = prices[idx], DetailId = $"{healPcts[idx]}", BatchCount = 1,
        NameColor = HealColor
      };
    }

    ShopItem? RollExpItem(WorldDatabase.ShopCategoryDef cat, int worldLevel)
    {
      int baseAmount = 20 + worldLevel * 15;
      return new ShopItem
      {
        Category = "exp", DisplayName = "经验之书",
        Description = $"获得 {baseAmount} 经验值。",
        Price = baseAmount / 2, DetailId = $"{baseAmount}", BatchCount = 1,
        NameColor = ExpColor
      };
    }

    // ══════════════════════════════════════════════════════
    //  渲染
    // ══════════════════════════════════════════════════════

    void Refresh()
    {
      var shopDef = WorldDatabase.GetShop(_currentMerchant.SubTypeId);
      _titleText.text = shopDef?.display_name ?? _currentMerchant.DisplayName ?? "商人";

      var wallet = WorldManager.Instance?.GetSystem<GoldWallet>();
      int balance = wallet?.Balance ?? 0;
      _goldBalanceText.text = $"余额: {balance} G";

      for (int i = _listContent.childCount - 1; i >= 0; i--)
        Destroy(_listContent.GetChild(i).gameObject);

      if (_shopItems.Count == 0)
      {
        var empty = CreateLabel(_listContent, "Empty", "当前没有可购买的商品", 14, FontStyle.Italic,
          new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(300, 30));
        empty.alignment = TextAnchor.MiddleCenter;
        empty.color = new Color(0.6f, 0.7f, 0.75f, 1f);
        return;
      }

      for (int idx = 0; idx < _shopItems.Count; idx++)
        CreateShopItemRow(_listContent, idx, balance);
    }

    void CreateShopItemRow(Transform parent, int idx, int balance)
    {
      var item = _shopItems[idx];
      var key = $"{item.Category}_{item.DetailId}";
      int bought = _purchased.TryGetValue(key, out var v) ? v : 0;
      var shopDef = WorldDatabase.GetShop(_currentMerchant.SubTypeId);
      var catDef = FindCategory(shopDef, item.Category);
      int maxBuy = catDef?.max_purchases ?? 999;
      bool soldOut = bought >= maxBuy;
      bool canAfford = !soldOut && balance >= item.Price;

      var rt = CreatePanel(parent, $"Item_{idx}", RowBg,
        new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 64));

      // 名称（品质色）
      var nameLabel = CreateLabel(rt, "Name", item.DisplayName, 16, FontStyle.Bold,
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -6), new Vector2(-110, 22));
      nameLabel.alignment = TextAnchor.UpperLeft;
      nameLabel.color = item.NameColor;

      // 描述
      string descText = item.Description;
      if (item.BatchCount > 1) descText += $"  (x{item.BatchCount})";
      var descLabel = CreateLabel(rt, "Desc", descText, 11, FontStyle.Normal,
        new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 28), new Vector2(-110, -16));
      descLabel.alignment = TextAnchor.LowerLeft;
      descLabel.color = new Color(0.65f, 0.75f, 0.82f, 1f);

      // 购买按钮
      string btnText = soldOut ? "售罄" : $"{item.Price} G";
      var btnColor = soldOut ? Unaffordable : (canAfford ? Accent : Unaffordable);
      CreateButton(rt, "BuyBtn", btnText, btnColor, canAfford, () =>
      {
        BuyItem(idx, key, item.Price);
      });
    }

    void BuyItem(int idx, string key, int price)
    {
      var wallet = WorldManager.Instance?.GetSystem<GoldWallet>();
      if (wallet == null || !wallet.SpendGold(price)) return;

      _purchased[key] = (_purchased.TryGetValue(key, out var v) ? v : 0) + 1;
      ExecuteEffect(_shopItems[idx]);
      Refresh();
    }

    // ══════════════════════════════════════════════════════
    //  效果执行
    // ══════════════════════════════════════════════════════

    void ExecuteEffect(ShopItem item)
    {
      var player = GameObject.FindGameObjectWithTag("Player");
      if (player == null) return;

      switch (item.Category)
      {
        case string s when s.StartsWith("weapon") || s.StartsWith("accessory"):
        {
          var inv = WorldManager.Instance?.Inventory;
          inv?.AddItem(item.DetailId, item.BatchCount);
          Debug.Log($"[MerchantUI] Added {item.BatchCount}x {item.DetailId} to inventory.");
          break;
        }
        case "buff":
        {
          var buffDef = WorldDatabase.GetShopBuff(item.DetailId);
          if (buffDef == null) break;
          var container = player.GetComponent<BuffContainer>();
          if (container != null)
          {
            container.ApplyBuff(buffDef.buff_id, new BuffContainer.BuffApplyContext
            {
              sourceEntity = player,
              durationOverride = buffDef.duration
            });
          }
          Debug.Log($"[MerchantUI] Applied buff '{buffDef.buff_id}' for {buffDef.duration}s.");
          break;
        }
        case "heal":
        {
          if (float.TryParse(item.DetailId, out float pct))
          {
            var health = player.GetComponent<Health>();
            if (health != null)
            {
              float amount = health.MaxHp * pct;
              health.Heal(amount);
              Debug.Log($"[MerchantUI] Healed player for {amount:F0} HP ({pct*100:F0}%).");
            }
          }
          break;
        }
        case "exp":
        {
          if (int.TryParse(item.DetailId, out int xp))
          {
            var playerLvl = WorldManager.Instance?.GetSystem<PlayerLevelSystem>();
            playerLvl?.AddXp(xp);
            Debug.Log($"[MerchantUI] Added {xp} XP.");
          }
          break;
        }
      }
    }

    static WorldDatabase.ShopCategoryDef FindCategory(WorldDatabase.ShopDef shop, string category)
    {
      if (shop?.categories == null) return null;
      foreach (var c in shop.categories)
        if (c.category == category) return c;
      return null;
    }

    // ══════════════════════════════════════════════════════
    //  Canvas
    // ══════════════════════════════════════════════════════

    void BuildCanvas()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("MerchantUICanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = 100;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();

      _panel = CreatePanel(canvasGo.transform, "MerchantPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 440)).gameObject;

      _titleText = CreateLabel(_panel.transform, "Title", "商人", 24, FontStyle.Bold,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(440, 32));
      _titleText.alignment = TextAnchor.MiddleCenter;

      _goldBalanceText = CreateLabel(_panel.transform, "Balance", "余额: 0 G", 14, FontStyle.Bold,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(200, 22));
      _goldBalanceText.alignment = TextAnchor.MiddleCenter;
      _goldBalanceText.color = Accent;

      var scrollGo = new GameObject("Scroll", typeof(RectTransform));
      scrollGo.transform.SetParent(_panel.transform, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1);
      scrollRt.offsetMin = new Vector2(12, 12); scrollRt.offsetMax = new Vector2(-12, -68);

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
      var viewport = CreatePanel(scrollGo.transform, "Viewport", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      scroll.viewport = viewport;

      var content = CreatePanel(viewport, "Content", Color.clear, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
      content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
      var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 6; layout.padding = new RectOffset(0, 0, 4, 4);
      layout.childControlHeight = true; layout.childControlWidth = true;
      layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
      content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      scroll.content = content;
      _listContent = content;
    }

    void CreateButton(Transform parent, string name, string label, Color bg, bool interactable, UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
      rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-12, -6); rt.sizeDelta = new Vector2(80, 28);
      var img = go.AddComponent<Image>(); img.color = bg;
      var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = interactable; btn.onClick.AddListener(onClick);
      var text = CreateLabel(rt, "T", label, 12, FontStyle.Bold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      text.color = interactable ? Color.black : new Color(0.6f, 0.6f, 0.6f, 1f);
    }

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

    Transform GetPlayer()
    {
      var go = GameObject.FindWithTag("Player");
      if (go == null) go = GameObject.Find("Player");
      return go?.transform;
    }

    static class Mathf
    {
      public static int Max(int a, int b) => a > b ? a : b;
    }
  }
}
