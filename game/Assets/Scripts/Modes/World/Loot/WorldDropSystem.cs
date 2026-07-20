using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.Database;

namespace Game.World
{
  /// <summary>
  /// World 模式怪物掉落系统。
  ///
  /// 职责：
  ///   1. 监听 CombatEventBus.OnKill → 查询 monster_drops.json
  ///   2. EXP 条目：直接加到 PlayerLevelSystem（不生成拾取物）
  ///   3. Gold 条目：生成金币拾取物（金色圆点）
  ///   4. Item 条目：独立概率判定 → 加权抽取物品 → 生成物品拾取物
  ///
  /// 拾取物生命周期：
  ///   - DropPickup 自动检测玩家距离 → 淡出消失
  ///   - 拾取时回调 OnGoldPickup / OnItemPickup → 更新钱包/背包
  ///   - 超时（60s）自动销毁
  ///
  /// 设计要点：
  ///   - 金币改为掉落物模式（不再由 GoldRewardService 直接加到钱包）
  ///   - 掉落表 key 以 enemies.json 中的 loot_table_id 为索引
  ///   - 系统关闭时清理所有活跃拾取物
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期。
  /// </summary>
  public class WorldDropSystem : IWorldSystem, System.IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    readonly List<DropPickup> _activePickups = new();
    readonly HashSet<string> _monsterIdsWithoutDropTable = new(); // 缓存无配表的怪物，减少日志
    GameObject _pickupContainer;
    bool _initialized;
    bool _paused;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      CombatEventBus.OnKill += HandleCombatKill;

      // 创建拾取物容器（保持场景整洁）
      _pickupContainer = new GameObject("DropPickups");
      GameObject.DontDestroyOnLoad(_pickupContainer);

      _initialized = true;

      if (DebugLog)
        Debug.Log("[WorldDropSystem] Initialized. Listening for kills.");
    }

    public void Tick(float deltaTime)
    {
      // 清理已销毁的拾取物引用
      if (_activePickups.Count > 0)
        _activePickups.RemoveAll(p => p == null);

      // 完全事件驱动，无需每帧逻辑
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      CombatEventBus.OnKill -= HandleCombatKill;

      // 清理所有活跃拾取物
      foreach (var pickup in _activePickups)
      {
        if (pickup != null)
          GameObject.Destroy(pickup.gameObject);
      }
      _activePickups.Clear();

      if (_pickupContainer != null)
        GameObject.Destroy(_pickupContainer);

      _monsterIdsWithoutDropTable.Clear();
      _initialized = false;

      if (DebugLog)
        Debug.Log("[WorldDropSystem] Shut down. All pickups cleaned up.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  击杀处理
    // ══════════════════════════════════════════════════════

    void HandleCombatKill(CombatEventBus.KillArgs args)
    {
      if (!_initialized || _paused) return;
      if (args.IsPlayer) return;

      // 判断是否为玩家击杀
      var killer = args.Killer;
      if (killer == null) return;

      var isPlayerKill = killer.CompareTag("Player") || killer.name == "Player";
      if (!isPlayerKill)
      {
        var name = killer.name;
        if (!name.StartsWith("Player", System.StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("Proj_Player", System.StringComparison.OrdinalIgnoreCase))
          return;
      }

      // 获取死亡位置
      var position = args.Victim != null
        ? args.Victim.transform.position
        : Vector3.zero;

      var victimId = args.VictimId ?? "";

      // 通过怪物 ArchetypeId 查找掉落表
      var dropTable = ResolveDropTable(victimId);
      if (dropTable == null || dropTable.drops == null) return;

      // 处理每条掉落条目
      foreach (var entry in dropTable.drops)
      {
        if (entry == null || string.IsNullOrEmpty(entry.type)) continue;
        ProcessDropEntry(entry, position, victimId);
      }
    }

    /// <summary>
    /// 解析怪物掉落表。
    ///   1. 优先按精确怪物 ID 查 monster_drops.json（用于特殊 Boss）
    ///   2. 回退到 EnemyDatabase 获取 loot_table_id，再查 monster_drops.json
    ///   3. 敌人定义中无 loot_table_id 则返回 null
    /// </summary>
    WorldDatabase.MonsterDropDef ResolveDropTable(string victimId)
    {
      if (string.IsNullOrEmpty(victimId)) return null;

      // 1. 精确怪物 ID 查找（如 Boss 专属掉落）
      var def = WorldDatabase.GetMonsterDrop(victimId);
      if (def != null) return def;

      // 2. 通过 EnemyDatabase 获取 loot_table_id
      var enemyDef = EnemyDatabase.Get(victimId);
      if (enemyDef == null || string.IsNullOrEmpty(enemyDef.loot_table_id))
      {
        LogMissingDropTable(victimId);
        return null;
      }

      // 3. 用 loot_table_id 查掉落表
      def = WorldDatabase.GetMonsterDrop(enemyDef.loot_table_id);
      if (def == null)
        LogMissingDropTable(enemyDef.loot_table_id);

      return def;
    }

    void LogMissingDropTable(string id)
    {
      if (_monsterIdsWithoutDropTable.Contains(id)) return;
      _monsterIdsWithoutDropTable.Add(id);
      if (DebugLog)
        Debug.Log($"[WorldDropSystem] No drop table for '{id}'.");
    }

    /// <summary>
    /// 处理单条掉落条目。
    /// </summary>
    void ProcessDropEntry(WorldDatabase.DropEntryDef entry, Vector3 position, string victimId)
    {
      switch (entry.type)
      {
        case "exp":
          ProcessExpDrop(entry, victimId);
          break;

        case "gold":
          ProcessGoldDrop(entry, position, victimId);
          break;

        case "item":
          ProcessItemDrop(entry, position, victimId);
          break;

        default:
          if (DebugLog)
            Debug.LogWarning($"[WorldDropSystem] Unknown drop type '{entry.type}' for '{victimId}'.");
          break;
      }
    }

    // ══════════════════════════════════════════════════════
    //  EXP 掉落 — 直接加到玩家经验
    // ══════════════════════════════════════════════════════

    void ProcessExpDrop(WorldDatabase.DropEntryDef entry, string victimId)
    {
      var amount = Random.Range(entry.min, entry.max + 1);

      // 通过 PlayerLevelSystem 增加经验
      var playerLevelSys = WorldManager.Instance?.GetSystem<PlayerLevelSystem>();
      if (playerLevelSys != null)
      {
        playerLevelSys.AddXp(amount);

        if (DebugLog)
          Debug.Log($"[WorldDropSystem] '{victimId}' dropped +{amount} XP.");
      }
    }

    // ══════════════════════════════════════════════════════
    //  Gold 掉落 — 生成金币拾取物
    // ══════════════════════════════════════════════════════

    void ProcessGoldDrop(WorldDatabase.DropEntryDef entry, Vector3 position, string victimId)
    {
      var amount = Random.Range(entry.min, entry.max + 1);
      if (amount <= 0) return;

      var pickup = CreatePickup(position);
      pickup.SetupGold(amount);
      pickup.OnPickup = OnGoldPickedUp;

      if (DebugLog)
        Debug.Log($"[WorldDropSystem] '{victimId}' dropped {amount}G pickup.");
    }

    void OnGoldPickedUp(DropPickup pickup)
    {
      var wallet = WorldManager.Instance?.GoldWallet;
      if (wallet != null)
        wallet.AddGold(pickup.GoldAmount);

      if (DebugLog)
        Debug.Log($"[WorldDropSystem] Player picked up +{pickup.GoldAmount}G.");
    }

    // ══════════════════════════════════════════════════════
    //  Item 掉落 — 概率判定 + 加权抽取
    // ══════════════════════════════════════════════════════

    void ProcessItemDrop(WorldDatabase.DropEntryDef entry, Vector3 position, string victimId)
    {
      // 概率判定
      if (Random.value > entry.probability) return;
      if (entry.items == null || entry.items.Length == 0) return;

      // 抽取个数
      var dropCount = Random.Range(entry.min, entry.max + 1);
      if (dropCount <= 0) return;

      // 按权重随机抽取（可重复）
      var totalWeight = 0f;
      foreach (var item in entry.items)
        if (item != null && !string.IsNullOrEmpty(item.item_id))
          totalWeight += item.weight;

      if (totalWeight <= 0f) return;

      for (int i = 0; i < dropCount; i++)
      {
        var picked = RollWeightedItem(entry.items, totalWeight);
        if (picked == null || string.IsNullOrEmpty(picked.item_id)) continue;

        var itemDef = WorldDatabase.GetItem(picked.item_id);
        if (itemDef == null) continue;

        var pickup = CreatePickup(position);
        pickup.SetupItem(picked.item_id, picked.count, itemDef.ParsedQuality);
        pickup.OnPickup = OnItemPickedUp;

        if (DebugLog)
          Debug.Log($"[WorldDropSystem] '{victimId}' dropped {picked.count}x {picked.item_id} (quality={itemDef.quality}).");
      }
    }

    /// <summary>加权随机抽取物品。</summary>
    WorldDatabase.DropItemEntry RollWeightedItem(WorldDatabase.DropItemEntry[] items, float totalWeight)
    {
      var roll = Random.Range(0f, totalWeight);
      var cumulative = 0f;
      foreach (var item in items)
      {
        if (item == null || string.IsNullOrEmpty(item.item_id)) continue;
        cumulative += item.weight;
        if (roll <= cumulative) return item;
      }
      return items[items.Length - 1]; // 兜底
    }

    void OnItemPickedUp(DropPickup pickup)
    {
      var inventory = WorldManager.Instance?.Inventory;
      if (inventory != null)
        inventory.AddItem(pickup.ItemId, pickup.ItemCount);

      if (DebugLog)
        Debug.Log($"[WorldDropSystem] Player picked up {pickup.ItemCount}x {pickup.ItemId}.");
    }

    // ══════════════════════════════════════════════════════
    //  拾取物生成
    // ══════════════════════════════════════════════════════

    DropPickup CreatePickup(Vector3 position)
    {
      var go = new GameObject("DropPickup");
      go.transform.position = position;
      if (_pickupContainer != null)
        go.transform.SetParent(_pickupContainer.transform);

      var pickup = go.AddComponent<DropPickup>();
      _activePickups.Add(pickup);

      return pickup;
    }
  }
}
