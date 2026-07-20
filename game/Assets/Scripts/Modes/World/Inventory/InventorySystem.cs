using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式背包系统。
  ///
  /// 管理两列表：
  ///   - 武器/道具列表（可堆叠，按 itemId 聚合数量）
  ///   - 饰品列表（不堆叠，每个饰品独立存储）
  ///
  /// 均无容量上限。
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期。
  ///
  /// 同步：
  ///   - 背包变化时自动同步到 WorldRuntimeContext
  ///
  /// 使用方式：
  ///   var inv = WorldManager.Instance.GetSystem&lt;InventorySystem&gt;();
  ///   inv.AddItem("bomb", 3);
  ///   inv.OnInventoryChanged += () => RefreshUI();
  /// </summary>
  public class InventorySystem : IWorldSystem
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态
    // ══════════════════════════════════════════════════════

    /// <summary>武器/道具槽位（itemId → 堆叠数量），只读快照。</summary>
    public IReadOnlyDictionary<string, int> Weapons => _weapons;

    /// <summary>饰品 ID 列表（每个条目独立），只读快照。</summary>
    public IReadOnlyList<string> Accessories => _accessories;

    /// <summary>背包变化事件（添加/移除/清空均触发）。</summary>
    public event Action OnInventoryChanged;

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    readonly Dictionary<string, int> _weapons = new();
    readonly List<string> _accessories = new();
    bool _initialized;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;
      _weapons.Clear();
      _accessories.Clear();
      _initialized = true;

      // 发放开局饰品（3选1结果）
      var startingAccessory = Game.Shared.Runtime.GameSessionConfig.ConsumeWorldStartingAccessory();
      if (!string.IsNullOrEmpty(startingAccessory))
      {
        AddItem(startingAccessory, 1);
        if (DebugLog)
          Debug.Log($"[InventorySystem] Granted starting accessory: {startingAccessory}");
      }

      SyncToContext();

      if (DebugLog)
        Debug.Log("[InventorySystem] Initialized. Empty backpack.");
    }

    public void Tick(float deltaTime)
    {
      // 背包是事件驱动，无需每帧逻辑
    }

    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      _initialized = false;
      if (DebugLog)
        Debug.Log($"[InventorySystem] Shut down. Weapons={_weapons.Count}, Accessories={_accessories.Count}");
    }

    // ══════════════════════════════════════════════════════
    //  API — 添加物品
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 添加物品到背包。
    /// 武器/道具：按 itemId 堆叠计数。
    /// 饰品：直接追加到列表末尾（不堆叠）。
    /// </summary>
    /// <param name="itemId">物品 ID（需在 inventory_items.json 中定义）</param>
    /// <param name="count">数量（默认 1，饰品忽略此参数）</param>
    public void AddItem(string itemId, int count = 1)
    {
      if (!_initialized || string.IsNullOrEmpty(itemId) || count <= 0) return;

      var def = WorldDatabase.GetItem(itemId);
      if (def == null)
      {
        Debug.LogWarning($"[InventorySystem] Unknown item '{itemId}', cannot add.");
        return;
      }

      if (def.IsWeapon)
      {
        if (_weapons.TryGetValue(itemId, out var existing))
          _weapons[itemId] = existing + count;
        else
          _weapons[itemId] = count;

        if (DebugLog)
          Debug.Log($"[InventorySystem] +{count}x {itemId} (weapon, total={_weapons[itemId]})");
      }
      else if (def.IsAccessory)
      {
        // 饰品不堆叠，每个 count 追加 count 个条目
        for (int i = 0; i < count; i++)
          _accessories.Add(itemId);

        // 饰品词条同步到属性系统
        SyncAccessoryAffixes();

        if (DebugLog)
          Debug.Log($"[InventorySystem] +{count}x {itemId} (accessory)");
      }

      FireChanged();
    }

    // ══════════════════════════════════════════════════════
    //  API — 移除物品
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 从背包移除物品。
    /// 武器/道具：减少堆叠计数，数量归零时移除槽位。
    /// 饰品：移除指定 ID 的第一个匹配条目。
    /// </summary>
    /// <param name="itemId">物品 ID</param>
    /// <param name="count">移除数量（默认 1）</param>
    /// <returns>是否移除成功（数量不足或物品不存在时返回 false）</returns>
    public bool RemoveItem(string itemId, int count = 1)
    {
      if (!_initialized || string.IsNullOrEmpty(itemId) || count <= 0) return false;

      var def = WorldDatabase.GetItem(itemId);
      if (def == null) return false;

      if (def.IsWeapon)
      {
        if (!_weapons.TryGetValue(itemId, out var existing) || existing < count)
          return false;

        var newCount = existing - count;
        if (newCount <= 0)
          _weapons.Remove(itemId);
        else
          _weapons[itemId] = newCount;

        if (DebugLog)
          Debug.Log($"[InventorySystem] -{count}x {itemId} (weapon, remaining={newCount})");

        FireChanged();
        return true;
      }
      else if (def.IsAccessory)
      {
        int removed = 0;
        for (int i = _accessories.Count - 1; i >= 0; i--)
        {
          if (_accessories[i] == itemId)
          {
            _accessories.RemoveAt(i);
            removed++;
            if (removed >= count) break;
          }
        }

        if (removed == 0) return false;

        // 饰品词条同步到属性系统
        SyncAccessoryAffixes();

        if (DebugLog)
          Debug.Log($"[InventorySystem] -{removed}x {itemId} (accessory)");

        FireChanged();
        return true;
      }

      return false;
    }

    // ══════════════════════════════════════════════════════
    //  API — 查询
    // ══════════════════════════════════════════════════════

    /// <summary>查询某个物品在背包中的数量。</summary>
    public int GetItemCount(string itemId)
    {
      if (!_initialized || string.IsNullOrEmpty(itemId)) return 0;

      if (_weapons.TryGetValue(itemId, out var count))
        return count;

      // 检查饰品列表
      int accCount = 0;
      foreach (var id in _accessories)
        if (id == itemId) accCount++;
      return accCount;
    }

    /// <summary>背包中是否包含指定物品。</summary>
    public bool HasItem(string itemId)
    {
      return GetItemCount(itemId) > 0;
    }

    /// <summary>武器/道具槽位总数（不同 itemId 的数量）。</summary>
    public int WeaponSlotCount => _weapons.Count;

    /// <summary>饰品总数。</summary>
    public int AccessoryCount => _accessories.Count;

    // ══════════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 遍历所有已装备的饰品，将其词条聚合为修饰符列表，
    /// 同步到 AttributeManager（来源 ID = "accessories"）。
    ///
    /// 每当饰品添加/移除时自动调用。
    /// 将所有饰品词条以 Add 操作写入属性表——
    /// 词条的语义（flat vs multiplier）由属性定义本身的 base 值决定。
    /// 例如 "attack_mult":0.10 加到 base=1.0 的属性上，结果 1.10 = 正常 +10% 倍率。
    /// </summary>
    void SyncAccessoryAffixes()
    {
      var attrMgr = WorldManager.Instance?.Attributes;
      if (attrMgr == null) return;

      var mods = new List<AttributeManager.ModifierEntry>();

      foreach (var accId in _accessories)
      {
        var def = WorldDatabase.GetItem(accId);
        if (def == null || !def.IsAccessory || def.affixes == null) continue;

        foreach (var affix in def.affixes)
        {
          if (affix == null || string.IsNullOrEmpty(affix.key)) continue;
          // 饰品词条统一使用 Add 操作，语义由属性 base 值自然处理：
          //   base=1.0 的属性（倍率类）→ add 自动表现为百分比叠加
          //   base=0.0 的属性（绝对值类）→ add 直接累加
          mods.Add(new AttributeManager.ModifierEntry(
            affix.key, AttributeManager.ModifierOp.Add, affix.value));
        }
      }

      attrMgr.ApplyModifiers("accessories", mods);

      if (DebugLog)
        Debug.Log($"[InventorySystem] Synced {mods.Count} accessory affixes to AttributeManager.");
    }

    void FireChanged()
    {
      OnInventoryChanged?.Invoke();
      SyncToContext();
    }

    void SyncToContext()
    {
      WorldRuntimeContext.SyncInventory(_weapons, _accessories);
    }
  }
}
