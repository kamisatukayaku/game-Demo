using System.Collections.Generic;
using System;
using Game.Shared.Core;

namespace Game.World
{
  /// <summary>
  /// World 模式的全局运行时数据中心。
  ///
  /// 类似 Arena 模式的 Run 构筑状态，
  /// 作为 World 模式所有子系统读取/写入状态的中心节点。
  ///
  /// 模式隔离：
  ///   - Arena 模式：ExperienceSystem + 构筑 stat（经 Shared Locator 注入）
  ///   - World 模式：WorldRuntimeContext（独立玩家等级/经济）+ 共享构筑 stat（经 IBuildStatWriter）
  ///
  /// 生命周期：
  ///   - WorldManager.InitWorld() 中调用 WorldRuntimeContext.Reset()
  ///   - WorldManager.Shutdown() 中调用 WorldRuntimeContext.Clear()
  ///
  /// 不继承 MonoBehaviour，不自动创建。由 WorldManager 显式管理状态。
  /// </summary>
  public static class WorldRuntimeContext
  {
    // ── 世界等级 ──────────────────────────────────────
    //
    // 世界等级由 WorldLevelSystem 管理（DangerValue → WorldExp → 升级）。
    // 这里仅保留读取通道和同步接口，不再包含等级计算逻辑。
    //
    // 作用：野外怪物与部分全局事件的难度基准。
    // 参见：docs/design.md §3.4

    /// <summary>当前世界等级（≥1）。由 WorldLevelSystem 维护。</summary>
    public static int WorldLevel => s_worldLevel;

    /// <summary>当前累积的世界经验。由 WorldLevelSystem 维护。</summary>
    public static float WorldExp { get; private set; }

    /// <summary>当前全地图 DangerValue。由 WorldLevelSystem 维护。</summary>
    public static float DangerValue { get; private set; }

    /// <summary>世界等级变化事件：(oldLevel, newLevel)。由 WorldEventBus 广播。</summary>
    public static event Action<int, int> WorldLevelChanged;

    /// <summary>
    /// [由 WorldLevelSystem 调用] 同步世界等级和相关状态。
    /// 外部系统请通过 WorldEventBus.WorldLevelChanged 监听变化。
    /// </summary>
    public static void SyncWorldLevel(int newLevel)
    {
      var oldLevel = s_worldLevel;
      if (newLevel == oldLevel) return;
      s_worldLevel = newLevel;
      s_worldLevelRaw = newLevel;
      WorldLevelChanged?.Invoke(oldLevel, newLevel);
    }

    /// <summary>
    /// [由 WorldLevelSystem 调用] 更新 WorldExp 和 DangerValue。
    /// </summary>
    public static void SyncWorldStats(float exp, float danger)
    {
      WorldExp = exp;
      DangerValue = danger;
    }

    /// <summary>
    /// [已弃用] 原始的等级调整方法。新代码应使用 WorldLevelSystem。
    /// 保留此方法仅为兼容旧骨架代码（如 WorldManager.Update 中的 natural_growth）。
    /// </summary>
    [Obsolete("Use WorldLevelSystem instead.")]
    public static void ModifyWorldLevel(float delta, string source = "unknown")
    {
      // no-op: 现在由 WorldLevelSystem 管理
    }

    // ── 玩家等级（World 模式专属，独立于 ExperienceSystem）──
    //
    // Arena 模式：ExperienceSystem（XP → Level → 三选一升级）
    // World 模式：保留 Arena 的 ExperienceSystem 用于战斗内升级 + 三选一，
    //           同时拥有独立的 WorldPlayerLevel 用于局外成长树解锁。
    // 两者并行不冲突。

    /// <summary>World 模式玩家等级（独立于 Arena ExperienceSystem）</summary>
    public static int WorldPlayerLevel => s_worldPlayerLevel;

    /// <summary>World 模式玩家经验值（float，由 PlayerLevelSystem 维护）</summary>
    public static float WorldPlayerXp => s_worldPlayerXp;

    /// <summary>玩家等级变化事件：(oldLevel, newLevel)</summary>
    public static event Action<int, int> WorldPlayerLevelUp;

    /// <summary>
    /// [由 PlayerLevelSystem 调用] 同步玩家等级和 XP。
    /// 外部系统通过 WorldEventBus.WorldPlayerLevelUp 监听变化。
    /// </summary>
    public static void SyncPlayerLevel(int level, float xp)
    {
      var old = s_worldPlayerLevel;
      s_worldPlayerLevel = level;
      s_worldPlayerXp = xp;
      if (old != level)
        WorldPlayerLevelUp?.Invoke(old, level);
    }

    /// <summary>
    /// [已弃用] 旧的营地清剿 XP 发放方法。新代码应使用 PlayerLevelSystem。
    /// 保留此方法仅为兼容 CampController.OnCoreDestroyed 中的旧调用。
    /// </summary>
    [Obsolete("Use PlayerLevelSystem.AddXp(kill) or event-driven XP.")]
    public static void AddWorldPlayerXp(int amount)
    {
      // no-op: 现在由 PlayerLevelSystem 通过事件总线管理
    }

    // ── 经济系统 ──────────────────────────────────────
    //
    // World 模式独立经济：保留 Arena 的 XP 用于战斗升级三选一，
    // 额外增加 WorldGold 用于营地商店、事件交易等。

    /// <summary>World 模式专用货币。由 GoldWallet 维护。</summary>
    public static int WorldGold => s_worldGold;

    /// <summary>金币变化事件：(oldValue, newValue, delta)</summary>
    public static event Action<int, int, int> GoldChanged;

    /// <summary>
    /// [由 GoldWallet 调用] 同步金币余额。
    /// </summary>
    public static void SyncGold(int balance)
    {
      var old = s_worldGold;
      if (old == balance) return;
      s_worldGold = balance;
      GoldChanged?.Invoke(old, balance, balance - old);
    }

    /// <summary>
    /// [已弃用] 旧的金币修改方法。新代码应使用 GoldWallet.AddGold() / SpendGold()。
    /// 保留此方法仅为兼容 CampController.OnCoreDestroyed 中的旧调用。
    /// </summary>
    [Obsolete("Use GoldWallet.AddGold() / SpendGold().")]
    public static bool ModifyGold(int delta)
    {
      // no-op: 现在由 GoldWallet 管理
      return false;
    }

    // ── 背包系统 ──────────────────────────────────────
    //
    // 背包分为武器/道具列表（可堆叠）和饰品列表（独立条目）。
    // 由 InventorySystem 维护，通过 SyncInventory 同步到此上下文。

    /// <summary>武器/道具槽位列表（itemId → 堆叠数量）</summary>
    public static IReadOnlyDictionary<string, int> Weapons => s_weaponSlots;

    /// <summary>饰品 ID 列表（每个条目独立，不堆叠）</summary>
    public static IReadOnlyList<string> Accessories => s_accessorySlots;

    /// <summary>物品栏槽位 (0-8 → item_id，null=空)</summary>
    public static IReadOnlyList<string> ItemSlots => s_itemSlots;

    /// <summary>当前选中的物品栏索引 (-1=无)</summary>
    public static int SelectedItemSlot { get; set; } = -1;

    /// <summary>物品栏变化事件</summary>
    public static event Action ItemSlotsChanged;

    /// <summary>将物品绑定到指定槽位。</summary>
    public static void BindItemToSlot(int slotIndex, string itemId)
    {
      if (slotIndex < 0 || slotIndex >= 9) return;
      s_itemSlots[slotIndex] = itemId;
      ItemSlotsChanged?.Invoke();
    }

    /// <summary>清空指定槽位。</summary>
    public static void ClearItemSlot(int slotIndex)
    {
      if (slotIndex < 0 || slotIndex >= 9) return;
      s_itemSlots[slotIndex] = null;
      ItemSlotsChanged?.Invoke();
    }

    /// <summary>背包变化事件（无参数，UI 层刷新即可）</summary>
    public static event Action InventoryChanged;

    /// <summary>
    /// [由 InventorySystem 调用] 同步背包状态到 WorldRuntimeContext。
    /// 外部系统通过 InventoryChanged 事件监听变化。
    /// </summary>
    public static void SyncInventory(Dictionary<string, int> weapons, List<string> accessories)
    {
      s_weaponSlots.Clear();
      if (weapons != null)
      {
        foreach (var kv in weapons)
          s_weaponSlots[kv.Key] = kv.Value;
      }
      s_accessorySlots.Clear();
      if (accessories != null)
        s_accessorySlots.AddRange(accessories);
      InventoryChanged?.Invoke();
    }

    // ── 营地注册 ──────────────────────────────────────

    /// <summary>当前活跃的营地列表（世界坐标 → 营地数据）</summary>
    public static IReadOnlyDictionary<string, WorldCampData> Camps => s_camps;

    /// <summary>注册一个营地</summary>
    public static void RegisterCamp(string campId, WorldCampData data)
    {
      s_camps[campId] = data;
    }

    /// <summary>注销一个营地（被摧毁时）</summary>
    public static void UnregisterCamp(string campId)
    {
      s_camps.Remove(campId);
    }

    // ── 事件系统状态 ──────────────────────────────────

    /// <summary>当前活跃的随机事件 ID 集合</summary>
    public static HashSet<string> ActiveEvents => s_activeEvents;
    public static HashSet<string> TriggeredEvents => s_triggeredEvents;
    public static void MarkEventTriggered(string eventId) => s_triggeredEvents.Add(eventId);
    public static void ActivateEvent(string eventId) => s_activeEvents.Add(eventId);
    public static void DeactivateEvent(string eventId) => s_activeEvents.Remove(eventId);

    // ── 地图数据引用 ──────────────────────────────────

    /// <summary>WorldGenerator 生成的地图结果。生成完成后赋值。</summary>
    public static WorldGenResult MapData { get; private set; }

    /// <summary>是否已完成世界生成</summary>
    public static bool IsWorldGenerated { get; private set; }

    // ── 结局状态 ──────────────────────────────────────

    /// <summary>本局是否已结束</summary>
    public static bool IsRunEnded { get; private set; }

    /// <summary>本局结局类型</summary>
    public static WorldEndType EndType { get; private set; }

    /// <summary>本局结算分数（由 MetaProgressionSystem.FinalizeRun 计算）</summary>
    public static float RunScore { get; private set; }

    /// <summary>本局获得的 BattleExp</summary>
    public static float RunBattleExp { get; private set; }

    /// <summary>已摧毁的非Boss营地数量</summary>
    public static int NonBossCampsDestroyed { get; private set; }

    /// <summary>总非Boss营地数量</summary>
    public static int TotalNonBossCamps { get; private set; }

    /// <summary>已击杀的Boss数量</summary>
    public static int BossesKilled { get; private set; }

    /// <summary>总Boss数量</summary>
    public static int TotalBosses { get; private set; }

    /// <summary>标记本局结束</summary>
    public static void MarkRunEnded(WorldEndType endType, float score, float battleExp)
    {
      IsRunEnded = true;
      EndType = endType;
      RunScore = score;
      RunBattleExp = battleExp;
    }

    /// <summary>记录非Boss营地总数（世界生成后调用）</summary>
    public static void SetTotalCamps(int nonBossCount, int bossCount)
    {
      TotalNonBossCamps = nonBossCount;
      TotalBosses = bossCount;
    }

    /// <summary>记录营地被摧毁（增计数）</summary>
    public static void RecordCampDestroyed(bool isBossCamp)
    {
      if (isBossCamp)
        BossesKilled++;
      else
        NonBossCampsDestroyed++;
    }

    // ── 模式守卫 ──────────────────────────────────────

    /// <summary>当前是否运行在 World 模式下。
    /// 所有 World 代码应在关键路径检查此标志，确保不影响 Arena 模式。</summary>
    public static bool IsWorldModeActive { get; private set; }

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    static int s_worldLevel = 1;
    static float s_worldLevelRaw = 1f;
    static int s_worldPlayerLevel = 1;
    static float s_worldPlayerXp;
    static int s_worldGold;
    static readonly Dictionary<string, int> s_weaponSlots = new();
    static readonly List<string> s_accessorySlots = new();
    static readonly string[] s_itemSlots = new string[9];
    static readonly Dictionary<string, WorldCampData> s_camps = new();
    static readonly HashSet<string> s_activeEvents = new();
    static readonly HashSet<string> s_triggeredEvents = new();

    // ══════════════════════════════════════════════════════
    //  生命周期（由 WorldManager 调用）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 重置所有 World 状态到初始值。由 WorldManager.InitWorld() 调用。
    /// </summary>
    public static void Reset()
    {
      s_worldLevel = 1;
      s_worldLevelRaw = 1f;
      WorldExp = 0f;
      DangerValue = 0f;
      s_worldPlayerLevel = 1;
      s_worldPlayerXp = 0f;
      s_worldGold = 0;
      s_weaponSlots.Clear();
      s_accessorySlots.Clear();
      for (int i = 0; i < 9; i++) s_itemSlots[i] = null;
      SelectedItemSlot = -1;
      s_camps.Clear();
      s_activeEvents.Clear();
      s_triggeredEvents.Clear();
      MapData = null;
      IsWorldGenerated = false;
      IsRunEnded = false;
      EndType = WorldEndType.Failure;
      RunScore = 0f;
      RunBattleExp = 0f;
      NonBossCampsDestroyed = 0;
      TotalNonBossCamps = 0;
      BossesKilled = 0;
      TotalBosses = 0;
      IsWorldModeActive = true;
    }

    /// <summary>
    /// 标记世界生成完成。由 WorldGenerator.Generate() 成功后调用。
    /// </summary>
    public static void MarkWorldGenerated(WorldGenResult result)
    {
        MapData = result;
        IsWorldGenerated = true;
    }

    /// <summary>
    /// 清空所有状态。由 WorldManager.Shutdown() 调用。
    /// </summary>
    public static void Clear()
    {
      Reset();
      IsWorldModeActive = false;
      // 清除所有事件订阅，防止 Arena 模式受 World 事件影响
      WorldLevelChanged = null;
      WorldPlayerLevelUp = null;
      GoldChanged = null;
      InventoryChanged = null;
      ItemSlotsChanged = null;
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    /// <summary>经验→等级换算公式（简单线性，待配表）</summary>
    static int CalcLevel(int xp)
    {
      // 临时公式：每 100 XP 升 1 级，后续可改为 data/world_player_level.json 驱动
      return 1 + xp / 100;
    }

    /// <summary>辅助：取 Unity Mathf，避免命名空间冲?/summary>
    static class Mathf
    {
      public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
      public static int Max(int a, int b) => a > b ? a : b;
      public static int RoundToInt(float v) => (int)(v + 0.5f);
    }
  }

  // ══════════════════════════════════════════════════════
  //  数据结构
  // ══════════════════════════════════════════════════════

  /// <summary>
  /// 营地运行时数据。
  /// 营地是地图上的一次性结构体，击杀后不再刷新。
  /// 拥有独立的营地等级，独立于世界等级。
  /// 参见：docs/design.md §3.3
  /// </summary>
  [Serializable]
  public struct WorldCampData
  {
    public string CampId;

    /// <summary>营地等级（独立于世界等级）</summary>
    public int CampLevel;
    public UnityEngine.Vector2 WorldPosition;
    public string CampTypeId;
    public bool IsDestroyed;
    public float GrowthRate;
    public float PlayerProximityGrowthBonus;
    public int MaxLevel;

    /// <summary>此营地怪物 archetype 列表（ID 引用 enemies.json）</summary>
    public string[] EnemyArchetypeIds;

    /// <summary>营地掉落池 ID（引用 loot_tables.json）</summary>
    public string LootPoolId;
  }

  /// <summary>
  /// World 模式结局类型。影响跨局系统中获得的分数倍率。
  /// </summary>
  public enum WorldEndType
  {
    /// <summary>失败 — 未达成其它条件时玩家死亡</summary>
    Failure,
    /// <summary>通关结局1 — 摧毁所有非Boss营地后玩家死亡</summary>
    Clear1,
    /// <summary>通关结局2 — 击杀所有Boss</summary>
    Clear2
  }
}
