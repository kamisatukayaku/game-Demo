

using UnityEngine;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;

namespace Game.World
{
  /// <summary>
  /// 营地主控制器 ?协调营地状态机、等级成长、刷怪和核心实体?
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期?
  ///
  /// 状态机（参?docs/design.md §3.3）：
  ///
  ///   Dormant ──玩家靠近──?Alert ──玩家更近──?Active
  ///      ?                   ?                   ?
  ///      └────玩家远离────────┴────玩家远离────────?
  ///      ?                   ?                   ?
  ///      └────核心被摧毁──────┴────核心被摧毁──────?Destroyed
  ///
  ///   Dormant   : 仅计算等级成长，不刷怀"
  ///   Alert     : 开始刷怪（半速），玩家已进入侦测范围
  ///   Active    : 全速刷怪，玩家已进入交战范囀"
  ///   Destroyed : 停止刷怪，核心已摧毁，不可恢复
  ///
  /// 挂载方式?
  ///   场景中创建一个空?GameObject，挂?CampController?
  ///   子节点需包含挂载 CampCore ?GameObject?
  ///   EnemySpawner ?WorldManager 注入或通过 FindObjectOfType 查找?
  ///
  ///   GameObject 结构示例?
  ///     Camp_WolfDen (CampController)
  ///       └── CampCore (CampCore + Health + Collider2D + SpriteRenderer)
  /// </summary>
  [DisallowMultipleComponent]
  public class CampController : MonoBehaviour, IWorldSystem, IDistanceActivatable
  {
    // ══════════════════════════════════════════════════════
    //  状态枚与"
    // ══════════════════════════════════════════════════════

    public enum CampState
    {
      /// <summary>休眠：仅计算成长</summary>
      Dormant = 0,
      /// <summary>警戒：开始慢速刷?/summary>
      Alert = 1,
      /// <summary>活跃：全速刷?/summary>
      Active = 2,
      /// <summary>已被摧毁</summary>
      Destroyed = 3
    }

    // ══════════════════════════════════════════════════════
    //  Inspector 配置
    // ══════════════════════════════════════════════════════

    [Header("Camp Identity")]
    [SerializeField] string _campTypeId = "camp_basic";
    [SerializeField] string _campInstanceId; // 留空则自动生戀"

    [Header("Ranges")]
    [Tooltip("进入此范??Alert 状态")]
    [SerializeField] float _alertRadius = 16f;
    [Tooltip("进入此范围内 Active 状态")]
    [SerializeField] float _activeRadius = 8f;

    [Header("Core HP")]
    [Tooltip("营地核心基础 HP（乘以营地等级倍率）")]
    [SerializeField] float _coreBaseHp = 200f;
    [Tooltip("核心 HP 随营地等级的倍率（每级增加比例）")]
    [SerializeField] float _coreHpPerLevelMult = 1.15f;

    [Header("Debug")]
    [SerializeField] bool _debugLog;

    [Header("References")]
    [SerializeField] CampCore _campCore;

    // ══════════════════════════════════════════════════════
    //  公开状态
    // ══════════════════════════════════════════════════════

    public CampState CurrentState { get; private set; } = CampState.Dormant;
    public string CampInstanceId => _campInstanceId;
    public string CampTypeId => _campTypeId;
    public int CurrentCampLevel => _levelLogic?.CurrentLevel ?? 1;
    public float PlayerDistance { get; private set; } = -1f; // -1 = unknown

    // ══════════════════════════════════════════════════════
    //  IDistanceActivatable
    // ══════════════════════════════════════════════════════

    string IDistanceActivatable.ActivatableId => _campInstanceId ?? name;
    Vector2 IDistanceActivatable.WorldPosition => transform.position;
    DistanceActivationLevel IDistanceActivatable.CurrentActivationLevel { get; set; } = DistanceActivationLevel.Level2_Near;
    bool IDistanceActivatable.IsBossEntity => false; // 营地本身不是 Boss

    void IDistanceActivatable.OnActivationLevelChanged(DistanceActivationLevel newLevel, DistanceActivationLevel oldLevel)
    {
      // Level0: 关闭 Core 的碰撞和渲染，仅保留成长计算
      if (newLevel == DistanceActivationLevel.Level0_Far)
      {
        if (_campCore != null)
          DistanceActivationSystem.ApplyToGameObject(_campCore.gameObject, DistanceActivationLevel.Level0_Far);
        if (_spawnLogic != null)
          _spawnLogic.SetDistanceLevel(newLevel);
      }
      // Level1: 恢复渲染，但营地保持 Dormant（不刷?慢刷?
      else if (newLevel == DistanceActivationLevel.Level1_Mid)
      {
        if (_campCore != null)
          DistanceActivationSystem.ApplyToGameObject(_campCore.gameObject, DistanceActivationLevel.Level1_Mid);
        if (_spawnLogic != null)
          _spawnLogic.SetDistanceLevel(newLevel);
      }
      // Level2: 完全激洀"
      else
      {
        if (_campCore != null)
          DistanceActivationSystem.ApplyToGameObject(_campCore.gameObject, DistanceActivationLevel.Level2_Near);
        if (_spawnLogic != null)
          _spawnLogic.SetDistanceLevel(newLevel);
      }
    }

    /// <summary>注册?DistanceActivationSystem（WorldManager 会管理）</summary>
    public void RegisterToDistanceSystem(DistanceActivationSystem system)
    {
      if (system != null)
        system.Register(this);
    }

    // ══════════════════════════════════════════════════════
    //  内部组件
    // ══════════════════════════════════════════════════════

    CampLevelLogic _levelLogic;
    CampSpawnLogic _spawnLogic;
    WorldDatabase.CampTypeDef _typeDef;
    Transform _playerTransform;
    bool _initialized;
    bool _paused;

    // 营地核心敌人（由 EnemySpawner 生成，替代旧 CampCore 的伤害接收）
    GameObject _coreEnemyGO;
    EnemyCore _coreEnemy;
    float _defenseUpdateTimer;
    const float DefenseUpdateInterval = 1f;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Initialize
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// ?WorldManager 调用，初始化营地?
    /// 此时 WorldRuntimeContext ?WorldDatabase 已就绪?
    /// </summary>
    public void Initialize()
    {
      if (_initialized) return;

      // Step 1: 加载营地类型定义
      WorldDatabase.EnsureLoaded();
      _typeDef = WorldDatabase.GetCampType(_campTypeId);
      if (_typeDef == null)
      {
        Debug.LogError($"[CampController] Unknown camp type '{_campTypeId}'!");
        return;
      }

      // Step 2: 生成唯一实例 ID
      if (string.IsNullOrEmpty(_campInstanceId))
        _campInstanceId = $"{_campTypeId}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";

      // Step 3: 创建等级成长逻辑
      _levelLogic = new CampLevelLogic(_typeDef, _alertRadius);

      // Step 4: 创建刷怪逻辑（查?EnemySpawner?
      var enemySpawner = FindObjectOfType<EnemySpawner>();
      if (enemySpawner == null)
        Debug.LogWarning($"[CampController] No EnemySpawner found in scene. Camp spawning disabled.");
      _spawnLogic = new CampSpawnLogic(enemySpawner, _typeDef, _levelLogic);
      _spawnLogic.SetCampIdentity(_campInstanceId); // 供怪物标记来源

      // Step 5: 生成营地核心敌人（通过 EnemySpawner，挂载 EnemyCore）
      var campLevel = _levelLogic.CurrentLevel;
      var campLevelDef = WorldDatabase.GetCampLevel(campLevel);
      float hpMult = campLevelDef?.enemy_hp_mult ?? 1f;
      float dmgMult = campLevelDef?.enemy_damage_mult ?? 0f;
      float spdMult = campLevelDef?.enemy_speed_mult ?? 0f;
      _coreEnemyGO = _spawnLogic.SpawnCore(transform.position, hpMult, dmgMult, spdMult);
      if (_coreEnemyGO != null)
        _coreEnemy = _coreEnemyGO.GetComponent<EnemyCore>();

      // 禁用旧 CampCore 的可攻击性（保留视觉）
      if (_campCore == null) _campCore = GetComponentInChildren<CampCore>();
      if (_campCore != null)
        _campCore.gameObject.SetActive(false);

      // Step 5.5: 订阅核心死亡事件
      CombatEventBus.OnKill += OnAnyKill;

      // Step 6: 注册?WorldRuntimeContext
      var campData = new WorldCampData
      {
        CampId = _campInstanceId,
        CampLevel = _levelLogic.CurrentLevel,
        WorldPosition = transform.position,
        CampTypeId = _campTypeId,
        IsDestroyed = false,
        GrowthRate = _typeDef.growth_rate,
        PlayerProximityGrowthBonus = _typeDef.player_proximity_growth_bonus,
        MaxLevel = _typeDef.max_level,
        EnemyArchetypeIds = _typeDef.enemy_archetype_ids,
        LootPoolId = _typeDef.loot_pool_id
      };
      WorldRuntimeContext.RegisterCamp(_campInstanceId, campData);

      // Step 7: 查找 Player
      var playerGo = GameObject.FindWithTag("Player");
      if (playerGo == null) playerGo = GameObject.Find("Player");
      if (playerGo != null) _playerTransform = playerGo.transform;

      // Step 8: 初始状态"
      TransitionTo(CampState.Dormant);

      _initialized = true;
      if (_debugLog)
        Debug.Log($"[CampController] Initialized '{_campInstanceId}' " +
                  $"type={_campTypeId} LV.{_levelLogic.CurrentLevel} HP={_coreEnemy?.Health?.MaxHp}");
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Tick
    // ══════════════════════════════════════════════════════

    public void Tick(float deltaTime)
    {
      if (!_initialized || _paused || CurrentState == CampState.Destroyed)
        return;

      // Step 1: 计算玩家距离
      UpdatePlayerDistance();

      // Step 2: 营地等级成长（始终进行，不受状态影响）
      _levelLogic.Tick(deltaTime, PlayerDistance, WorldRuntimeContext.WorldLevel);

      // Step 3: 等级变化时更新Core HP + 活跃怪物属性
      if (_levelLogic.LeveledUpThisFrame)
      {
        UpdateCoreHp();
        UpdateRegisteredCampData();
        _spawnLogic?.RefreshActiveEnemyStats();
      }

      // Step 3.5: 每秒更新核心减伤
      UpdateCoreDefense(deltaTime);

      // Step 4: 更新状态机
      UpdateState();

      // Step 5: 刷怪
      _spawnLogic.Tick(deltaTime, transform.position, CurrentState);
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Pause/Resume/Shutdown
    // ══════════════════════════════════════════════════════

    public void OnPause()  => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      CombatEventBus.OnKill -= OnAnyKill;
      _spawnLogic?.Dispose();
      _initialized = false;

      if (_coreEnemyGO != null)
      {
        Object.Destroy(_coreEnemyGO);
        _coreEnemyGO = null;
        _coreEnemy = null;
      }

      if (!string.IsNullOrEmpty(_campInstanceId))
        WorldRuntimeContext.UnregisterCamp(_campInstanceId);

      if (_debugLog)
        Debug.Log($"[CampController] Shutdown '{_campInstanceId}'.");
    }

    // ══════════════════════════════════════════════════════
    //  状态机
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 根据玩家距离更新营地状态?
    ///
    /// 规则?
    ///   - 玩家距离 ?activeRadius ?Active
    ///   - 玩家距离 ?alertRadius  ?Alert
    ///   - 玩家距离 ?alertRadius  ?Dormant
    ///   - 核心已被摧毁            ?Destroyed（不可逆）
    /// </summary>
    void UpdateState()
    {
      if (CurrentState == CampState.Destroyed)
        return;

      if (_coreEnemyGO == null || (_coreEnemy?.Health != null && _coreEnemy.Health.IsDead))
      {
        TransitionTo(CampState.Destroyed);
        return;
      }

      if (PlayerDistance < 0f)
      {
        // 找不到玩??退?Dormant
        if (CurrentState != CampState.Dormant)
          TransitionTo(CampState.Dormant);
        return;
      }

      if (PlayerDistance <= _activeRadius)
      {
        if (CurrentState != CampState.Active)
          TransitionTo(CampState.Active);
      }
      else if (PlayerDistance <= _alertRadius)
      {
        if (CurrentState != CampState.Alert)
          TransitionTo(CampState.Alert);
      }
      else
      {
        if (CurrentState != CampState.Dormant)
          TransitionTo(CampState.Dormant);
      }
    }

    void TransitionTo(CampState newState)
    {
      if (CurrentState == newState) return;

      var oldState = CurrentState;
      CurrentState = newState;

      if (_debugLog)
        Debug.Log($"[CampController] '{_campInstanceId}' {oldState} {newState} " +
                  $"(dist={PlayerDistance:F1})");

      // 状态进?退出钩孀"
      switch (newState)
      {
        case CampState.Active:
          // 进入 Active：触?World 事件
          WorldEventBus.FireCampEntered(_campInstanceId);
          break;
        case CampState.Dormant:
          // 离开警戒范围
          if (oldState == CampState.Alert || oldState == CampState.Active)
            WorldEventBus.FireCampExited(_campInstanceId);
          break;
        case CampState.Destroyed:
          // 营地摧毁：怪物转为野外，销毁核心敌人
          _spawnLogic?.ReleaseAllToWild();
          if (_coreEnemyGO != null) { Object.Destroy(_coreEnemyGO); _coreEnemyGO = null; _coreEnemy = null; }
          break;
      }
    }

    // ══════════════════════════════════════════════════════
    //  玩家距离
    // ══════════════════════════════════════════════════════

    void UpdatePlayerDistance()
    {
      if (_playerTransform == null)
      {
        PlayerDistance = -1f;
        return;
      }

      PlayerDistance = Vector2.Distance(
        transform.position,
        _playerTransform.position);
    }

    // ══════════════════════════════════════════════════════
    //  核心减伤
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 每秒刷新核心减伤比例。公式：aliveEnemies / maxCap，上限 90%。
    /// 减伤通过 EnemyCore.DefenseRatio 直接作用于 DamagePipeline。
    /// </summary>
    void UpdateCoreDefense(float deltaTime)
    {
      if (_coreEnemy == null || _spawnLogic == null) return;
      _defenseUpdateTimer += deltaTime;
      if (_defenseUpdateTimer < DefenseUpdateInterval) return;
      _defenseUpdateTimer = 0f;

      int alive = _spawnLogic.GetAliveEnemyCount();
      int maxCap = _spawnLogic.GetMaxAliveEnemies();
      if (maxCap <= 0) { _coreEnemy.DefenseRatio = 0f; return; }

      float ratio = (float)alive / maxCap;
      _coreEnemy.DefenseRatio = UnityEngine.Mathf.Min(0.9f, ratio);

      if (_debugLog)
        Debug.Log($"[CampController] '{_campInstanceId}' core defense={_coreEnemy.DefenseRatio:P0} (alive={alive}/{maxCap})");
    }

    /// <summary>
    /// CombatEventBus 击杀回调：若受害方是当前营地的核心敌人，则触发营地摧毁。
    /// </summary>
    void OnAnyKill(CombatEventBus.KillArgs args)
    {
      if (args.Victim == null || _coreEnemyGO == null) return;
      if (args.Victim != _coreEnemyGO) return;

      if (_debugLog)
        Debug.Log($"[CampController] '{_campInstanceId}' core destroyed by {args.Killer?.name ?? "unknown"}!");

      _coreEnemyGO = null;
      _coreEnemy = null;
      TransitionTo(CampState.Destroyed);
    }

    // ══════════════════════════════════════════════════════
    //  CampCore 交互
    // ══════════════════════════════════════════════════════

    /// <summary>营地核心被摧毁后的回调（?CampCore.OnCoreDestroyed 调用?/summary>
    public void OnCoreDestroyed()
    {
      TransitionTo(CampState.Destroyed);

      // 发放清剿奖励?PlayerLevelSystem / GoldRewardService 通过事件总线自动处理
      if (_debugLog)
        Debug.Log($"[CampController] Camp '{_campInstanceId}' cleared!");
    }

    /// <summary>营地等级提升时更?CampCore HP</summary>
    void UpdateCoreHp()
    {
      if (_coreEnemyGO == null || _coreEnemy?.Health == null || _coreEnemy.Health.IsDead) return;

      var newHp = CalculateCoreHp(_levelLogic.CurrentLevel);
      var health = _coreEnemy.Health;
      // 保留血量比例，仅更新 MaxHp
      float ratio = health.MaxHp > 0f ? health.CurrentHp / health.MaxHp : 1f;
      health.Configure(newHp);
      if (ratio < 1f)
      {
        var currentHpField = typeof(Health).GetField("_currentHp",
          System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var targetHp = newHp * ratio;
        if (targetHp < 1f) targetHp = 1f;
        currentHpField?.SetValue(health, targetHp);
      }
    }

    /// <summary>计算营地核心 HP（基础?× 等级倍率^level?/summary>
    float CalculateCoreHp(int level)
    {
      return _coreBaseHp * Mathf.Pow(_coreHpPerLevelMult, level - 1);
    }

    // ══════════════════════════════════════════════════════
    //  WorldRuntimeContext 同步
    // ══════════════════════════════════════════════════════

    void UpdateRegisteredCampData()
    {
      if (string.IsNullOrEmpty(_campInstanceId)) return;

      var campData = new WorldCampData
      {
        CampId = _campInstanceId,
        CampLevel = _levelLogic.CurrentLevel,
        WorldPosition = transform.position,
        CampTypeId = _campTypeId,
        IsDestroyed = CurrentState == CampState.Destroyed,
        GrowthRate = _typeDef?.growth_rate ?? 0f,
        PlayerProximityGrowthBonus = _typeDef?.player_proximity_growth_bonus ?? 0f,
        MaxLevel = _typeDef?.max_level ?? 999,
        EnemyArchetypeIds = _typeDef?.enemy_archetype_ids,
        LootPoolId = _typeDef?.loot_pool_id
      };
      WorldRuntimeContext.RegisterCamp(_campInstanceId, campData);
    }

    // ══════════════════════════════════════════════════════
    //  查询
    // ══════════════════════════════════════════════════════

    /// <summary>获取当前营地等级对应的数值定?/summary>
    public WorldDatabase.CampLevelDef GetLevelDef() => _levelLogic?.CurrentLevelDef;

    // ── 辅助 ──────────────────────────────────────────

    static class Mathf
    {
      public static float Pow(float f, float p) => (float)System.Math.Pow(f, p);
    }
  }
}
