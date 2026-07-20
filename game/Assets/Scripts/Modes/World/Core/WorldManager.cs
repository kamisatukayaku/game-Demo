using System.Collections.Generic;
using System;

using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Stats;

namespace Game.World
{
  /// <summary>
  /// World 模式主管理器。
  ///
  /// 职责：
  ///   1. World 模式生命周期（初始化 → 运行 → 暂停/恢复 → 关闭）
  ///   2. 驱动所有 IWorldSystem 子系统的 Tick
  ///   3. 协调世界生成流程
  ///   4. 管理 Arena ↔ World 模式切换
  ///
  /// 挂载方式：
  ///   - 必须手动放置在 World 模式专用场景的 GameObject 上
  ///   - 或由显式 Factory（WorldModeFactory）创建
  ///   - 禁止使用 [RuntimeInitializeOnLoadMethod] 或 EnsureExists 自动创建
  ///
  /// Arena 模式隔离：
  ///   - WorldManager 不挂载在任何 Arena 场景中
  ///   - 所有 Tick 逻辑通过 if (!IsActive) return; 守卫
  ///   - Shutdown 时清空 WorldEventBus 和 WorldRuntimeContext
  ///
  /// 参见：docs/design.md §2-§6（World 模式设计）
  /// </summary>
  [DisallowMultipleComponent]
  public class WorldManager : MonoBehaviour
  {
    [Header("World 生成")]
    [SerializeField] WorldGenConfig _worldGenConfig = null;
    [Header("运行")]
    [SerializeField] bool _paused;
    [SerializeField] bool _debugLog;

    public static WorldManager Instance { get; private set; }

    /// <summary>供 UI 层查询的地图管理器</summary>
    public WorldMapManager MapManager { get; private set; }

    /// <summary>供 UI 层查询的金币钱包</summary>
    public GoldWallet GoldWallet { get; private set; }

    /// <summary>供 UI 层查询的背包系统</summary>
    public InventorySystem Inventory { get; private set; }

    /// <summary>供 UI 层查询的属性管理器</summary>
    public AttributeManager Attributes { get; private set; }

    // ══════════════════════════════════════════════════════
    //  公开状态
    // ══════════════════════════════════════════════════════

    /// <summary>World 模式是否正在运行</summary>
    public bool IsActive { get; private set; }

    /// <summary>是否暂停（打开 UI、升级弹窗等）</summary>
    public bool IsPaused
    {
      get => _paused;
      set
      {
        if (_paused == value) return;
        _paused = value;
        if (_paused) PauseAllSystems();
        else ResumeAllSystems();
      }
    }

    WorldGenerator _generator;
    WorldCombatBridge _combatBridge;
    readonly List<IWorldSystem> _systems = new();

    void Awake()
    {
      if (Instance != null && Instance != this) { Destroy(gameObject); return; }
      Instance = this;
    }
    void OnDestroy()
    {
      if (Instance == this) Instance = null;
      if (IsActive) Shutdown();
    }

    void Update()
    {
      if (!IsActive || _paused) return;

      var dt = Time.deltaTime;

      // 驱动所有子系统（WorldLevelSystem 在 Tick 中自动更新 DangerValue → WorldExp → 升级）
      for (int i = 0; i < _systems.Count; i++)
      {
        _systems[i].Tick(dt);
      }
    }

    // ══════════════════════════════════════════════════════
    //  公开 API — 生命周期
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 初始化 World 模式。
    ///
    /// 调用时机：场景加载后、GameSessionConfig.GameMode == Explore 时。
    /// 典型调用方：CombatRoot 或专用 WorldModeFactory。
    ///
    /// 步骤:
    ///   1. 重置 WorldRuntimeContext
    ///   2. 创建 WorldGenerator → 生成世界地图
    ///   3. 创建 WorldCombatBridge → 开始监听战斗事件
    ///   4. 创建并初始化各 IWorldSystem 子系统
    ///   5. 广播 WorldMapGenerated 事件
    /// </summary>
    public void InitWorld()
    {
      if (IsActive)
      {
        Debug.LogWarning("[WorldManager] Already initialized, skipping.");
        return;
      }

      if (_debugLog)
        Debug.Log("[WorldManager] Initializing World mode...");

      // Step 0: 注册 World 模式特有键位
      WorldInputKeys.RegisterWorldKeys();

      // Step 1: 重置运行时上下文
      WorldRuntimeContext.Reset();

      // Step 2: 世界生成（若未配置则使用 Default，包含 12 营地 + 3 Boss + 2 商店 + 8 事件点）
      _generator = new WorldGenerator();
      var config = _worldGenConfig != null ? _worldGenConfig : WorldGenConfig.Default;
      var result = _generator.Generate(config);
      WorldRuntimeContext.MarkWorldGenerated(result);

      // Step 3: 战斗桥接
      _combatBridge = new WorldCombatBridge();
      _combatBridge.StartListening();

      // Step 4: 创建子系统
      CreateWorldSystems();

      // Step 4.5: 注册 World 专用的 CombatStatProvider（防御/伤害减免等）
      CombatStatProviderLocator.Register(new WorldCombatStatProvider());

      // Step 5: 通知地图生成完成
      WorldEventBus.FireWorldMapGenerated();
      NotifySystems_OnWorldGenerated();

      IsActive = true;

      if (_debugLog)
        Debug.Log("[WorldManager] World mode initialized.");
    }

    /// <summary>
    /// 关闭 World 模式。
    ///
    /// 调用时机：单局结束（胜利/失败/放弃）、切换回 Arena 模式。
    /// 清理顺序:
    ///   1. Shutdown 所有子系统
    ///   2. 停止战斗桥接
    ///   3. 清空 WorldRuntimeContext
    ///   4. 清空 WorldEventBus 订阅
    /// </summary>
    public void Shutdown()
    {
      if (!IsActive) return;

      if (_debugLog)
        Debug.Log("[WorldManager] Shutting down World mode...");

      // Step 1: 子系统清理
      foreach (var sys in _systems)
      {
        try { sys.Shutdown(); }
        catch (Exception e) { Debug.LogError($"[WorldManager] Error shutting down {sys.GetType().Name}: {e}"); }
      }
      _systems.Clear();

      // Step 2: 战斗桥接清理
      _combatBridge?.Dispose();
      _combatBridge = null;

      // Step 3: 运行时上下文清理
      WorldRuntimeContext.Clear();

      // Step 4: 事件总线清理
      WorldEventBus.ClearAllSubscribers();

      // Step 4.5: 注销 CombatStatProvider
      CombatStatProviderLocator.Clear();

      // Step 5: 删除 World 模式特有键位
      WorldInputKeys.UnregisterWorldKeys();

      _generator = null;
      IsActive = false;

      if (_debugLog)
        Debug.Log("[WorldManager] World mode shut down.");
    }

    void PauseAllSystems() { foreach (var sys in _systems) sys.OnPause(); }
    void ResumeAllSystems() { foreach (var sys in _systems) sys.OnResume(); }

    // ══════════════════════════════════════════════════════
    //  子系统管理
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 创建并初始化所有 World 子系统。
    ///
    /// 当前骨架阶段仅列出子系统清单，不创建实例。
    /// 后续开发时在此方法中 new 各子系统并 AddSystem()。
    ///
    /// 计划子系统：
    ///   - CampSystem          : 营地创建/等级成长/摧毁处理
    ///   - WorldEconomySystem  : 金币获取/消费/商店交互
    ///   - WorldEventSystem    : 随机事件触发/分支选择/后果结算
    ///   - MetaGrowthTreeSystem: 局外天赋树解锁/跨局成长
    ///   - WorldSpawnSystem    : 野外怪物独立刷怪调度
    ///   - WorldBossSystem     : 野外 Boss 管理/地图标记/召唤物逻辑
    /// </summary>
    void CreateWorldSystems()
    {
      // WorldLevelSystem — 核心：DangerValue → WorldExp → 世界等级
      var worldLevelSys = new WorldLevelSystem
      {
        MinLevelDiff = 2,
        DangerToExpRate = 1.0f,
        CampDestroyedExpPenalty = 10f,
        DebugLog = _debugLog
      };
      worldLevelSys.Initialize();
      AddSystem(worldLevelSys);

      // PlayerLevelSystem — 独立玩家等级（击杀/营地/事件 → XP → 攻/血/防加成）
      var playerLevelSys = new PlayerLevelSystem
      {
        XpPerKill = 5f,
        EliteKillXpMult = 3f,
        BossKillXpMult = 10f,
        XpPerCampDestroyed = 20f,
        XpPerEventCompleted = 15f,
        DebugLog = _debugLog
      };
      playerLevelSys.Initialize();
      AddSystem(playerLevelSys);

      // GoldWallet — 金币余额管理（存取 + 事件通知）
      var wallet = new GoldWallet { DebugLog = _debugLog };
      wallet.Initialize();
      AddSystem(wallet);

      // GoldRewardService — 金币奖励发放（击杀/营地/事件 → GoldWallet）
      var goldReward = new GoldRewardService(wallet)
      {
        GoldPerKill = 1,
        EliteKillGoldMult = 3f,
        BossKillGoldMult = 10f,
        GoldPerCampDestroyed = 10,
        GoldPerEventCompleted = 15,
        DebugLog = _debugLog
      };
      goldReward.Initialize();
      AddSystem(goldReward);

      // EventNodeExecutor — 节点图效果执行引擎
      var executor = new EventNodeExecutor(wallet, playerLevelSys, worldLevelSys)
      {
        DebugLog = _debugLog
      };

      // EventManager — 事件管理器（选择/触发/结算，节点图版本）
      var eventManager = new EventManager { DebugLog = _debugLog };
      eventManager.SetExecutor(executor);
      eventManager.Initialize();
      AddSystem(eventManager);

      // EventTrigger — 事件触发器（营地摧毁/Boss击杀/地图点位）
      var eventTrigger = new EventTrigger(eventManager)
      {
        CampDestroyTriggerChance = 0.3f,
        BossKillTriggerChance = 0.6f,
        MapPointTriggerChance = 1.0f,
        CooldownSeconds = 15f,
        DebugLog = _debugLog
      };
      eventTrigger.Initialize();
      AddSystem(eventTrigger);

      // WorldMapManager — 地图标记管理（营地/Boss/商人/事件 POI 数据层）
      var mapManager = new WorldMapManager
      {
        DiscoveryRadius = 40f,
        DebugLog = _debugLog
      };
      mapManager.Initialize();
      AddSystem(mapManager);

      // AttributeManager — 动态属性表（需在 MetaProgression 之前注册，因为后者通过
      // BuildStatWriterLocator.Writer.AddStat() 写入，而 AttributeManager 就是 Writer）
      var attrMgr = new AttributeManager { DebugLog = _debugLog };
      attrMgr.Initialize(); // 此时自动注册为 BuildStatWriterLocator.Writer
      AddSystem(attrMgr);
      Attributes = attrMgr;

      // MetaProgressionSystem — 局外永久成长树（BattleScore→Exp→解锁节点）
      var metaProgression = new MetaProgressionSystem { DebugLog = _debugLog };
      metaProgression.Initialize();
      AddSystem(metaProgression);

      // 每局开始时应用已解锁的属性加成
      metaProgression.ApplyAttributeBonuses();

      // InventorySystem — 背包系统（武器道具 + 饰品管理）
      var inventory = new InventorySystem { DebugLog = _debugLog };
      inventory.Initialize();
      AddSystem(inventory);
      Inventory = inventory;

      // WorldDropSystem — 怪物掉落（XP自动/金币拾取物/物品拾取物）
      var dropSys = new WorldDropSystem { DebugLog = _debugLog };
      dropSys.Initialize();
      AddSystem(dropSys);

      // 禁用旧系统对击杀的直接金币/经验奖励，
      // 因为 WorldDropSystem 会通过掉落物/掉落 JSON 统一处理。
      goldReward.GoldPerKill = 0;
      playerLevelSys.XpPerKill = 0;

      // WorldEndingSystem — 结局判定（监听玩家死亡/营地摧毁/Boss击杀）
      var endingSys = new WorldEndingSystem { DebugLog = _debugLog };
      endingSys.Initialize();
      AddSystem(endingSys);

      // WildSpawnSystem — 野外怪物自然生成（玩家周围周期性刷怪）
      var wildSpawnSys = new WildSpawnSystem { DebugLog = _debugLog };
      wildSpawnSys.Initialize();
      AddSystem(wildSpawnSys);

      // WorldGenAppender — 营地摧毁→商店追加 + 事件点动态生成
      var appender = new WorldGenAppender { DebugLog = _debugLog };
      appender.Initialize();
      AddSystem(appender);

      // 营地摧毁 → 必定追加对应类型商店 + 事件点
      WorldEventBus.CampDestroyed += (campId, data) =>
      {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) appender.TryAppendCampClearShop(player.transform.position, data.CampTypeId);
        appender.OnCampDestroyed(player != null ? player.transform.position : Vector2.zero);
      };

      // 玩家等级提升 → 事件点生成
      WorldEventBus.WorldPlayerLevelUp += (oldLv, newLv) => appender.OnPlayerLevelUp();

      // 世界等级提升 → 事件点生成
      WorldEventBus.WorldLevelChanged += (oldLv, newLv) =>
      {
        if (newLv > oldLv) appender.OnWorldLevelUp();
      };

      // DistanceActivationSystem — 距离 LOD 优化（Level0/1/2）
      var distanceSys = new DistanceActivationSystem
      {
        Level0Radius = 80f,
        Level1Radius = 40f,
        UpdateInterval = 0.5f,
        DebugLog = _debugLog
      };
      distanceSys.Initialize();
      AddSystem(distanceSys);

      // CampPlacementSystem — 营地实例化（将世界生成数据转为场景中实际 CampController）
      var campPlacementSys = new CampPlacementSystem { DebugLog = _debugLog };
      campPlacementSys.Initialize();
      campPlacementSys.SetDistanceSystem(distanceSys);
      AddSystem(campPlacementSys);

      if (_debugLog)
        Debug.Log("[WorldManager] World systems created.");
    }

    /// <summary>
    /// 注册一个 World 子系统，纳入生命周期管理。
    /// </summary>
    public void AddSystem(IWorldSystem system)
    {
      if (system == null) return;
      _systems.Add(system);

      if (_debugLog)
        Debug.Log($"[WorldManager] Registered system: {system.GetType().Name}");
    }

    /// <summary>
    /// 获取已注册的子系统（按类型）。
    /// </summary>
    public T GetSystem<T>() where T : class, IWorldSystem
    {
      foreach (var sys in _systems)
        if (sys is T t) return t;
      return null;
    }

    void NotifySystems_OnWorldGenerated()
    {
      if (WorldRuntimeContext.MapData == null) return;
      foreach (var sys in _systems)
        if (sys is IWorldSystem_OnWorldGenerated listener)
          listener.OnWorldGenerated(WorldRuntimeContext.MapData);
    }

    // ══════════════════════════════════════════════════════
    //  Editor 辅助
    // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Init World (Editor Test)")]
    void Editor_InitWorld()
    {
      if (Application.isPlaying)
        InitWorld();
      else
        Debug.LogWarning("[WorldManager] InitWorld() requires Play Mode.");
    }

    [ContextMenu("Shutdown World (Editor Test)")]
    void Editor_Shutdown()
    {
      if (Application.isPlaying)
        Shutdown();
      else
        Debug.LogWarning("[WorldManager] Shutdown() requires Play Mode.");
    }
#endif
  }
}
