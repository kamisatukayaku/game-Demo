using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Death;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Visual;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Game.Shared.Runtime;
using Game.Shared.Runtime.Physics;
using Game.Shared.Stats;
using HealthComponent = global::Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 敌人核心协调器。持有 AI 决策、词条、来源、模式管理。
  /// 将移动/攻击/视觉委托给子组件。
  /// 
  /// 外部 API 保持与旧 EnemySphereController 完全兼容。
  /// 
  /// 特殊敌人（如营地核心）不挂载 EnemyMovement/EnemyAttack → 不可移动/攻击。
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyCore : MonoBehaviour
  {
    [Header("Identity")]
    [SerializeField] string enemyId = "mob_hex_01";

    [Header("Move (shared view)")]
    [SerializeField] float moveSpeed = 2.5f;
    [SerializeField] float aggroRange = 8f;
    [SerializeField] float attackRange = 1.1f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float deceleration = 8f;

    [Header("Attack (shared view)")]
    [SerializeField] string attackProfileId;
    [SerializeField] string attackDamageType = "physical";
    [SerializeField] string attackDamageSource = "monster";
    [SerializeField] EnemyAttackKind attackKind = EnemyAttackKind.Melee;
    [SerializeField] float attackDamage = 6f;
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] float attackWindup = 0.2f;
    [SerializeField] float rangedAttackRange = 5.5f;

    [Header("Hit Response")]
    [SerializeField] float hitStunDuration = 0.08f;
    [SerializeField] float knockbackDistance = 0.15f;

    [Header("Path")]
    [SerializeField] bool isLaneEnemy;
    [SerializeField] EnemyAiStyle aiStyle = EnemyAiStyle.MeleeChase;
    [SerializeField] float leashMult = 1f;

    [Header("VFX")]
    [SerializeField] public bool enableChargeParticle;

    [Header("Sprint")]
    [SerializeField] float _sprintSpeedMultiplier = 1.5f;
    [SerializeField] float _sprintStopDuration = 0.5f;

    [SerializeField] Transform chaseTarget;

    // ── 子组件引用 ─────────────────────────────
    HealthComponent _health;
    DamageDisplay _damageDisplay;
    EntityPhysicsBody _physics;
    SprintWindEffect _sprintWind;
    EnemyMovement _movement;
    EnemyAttack _attack;
    EnemyVisualHub _visual;

    // ── 奔跑状态 ───────────────────────────────
    bool _isSprinting;
    bool _isStoppingSprint;
    float _sprintStopTimer;

    // ── 反弹返回 ───────────────────────────────
    bool _reflectReturnActive;
    Coroutine _reflectReturnCoroutine;
    GameObject _reflectReturnAttacker;
    int _reflectReturnChainDepth;
    HashSet<EnemyCore> _reflectReturnHitTargets;

    // ── 波次/竞技场 ────────────────────────────
    bool _waveMode;
    Vector2 _waveTarget;
    Vector2 _laneAdvanceDir = Vector2.right;
    bool _arenaOrbitMode;
    float _circlePathAngle;
    float _orbitDirection = 1f;
    float _spawnedAt;

    // ── Boss 标志 ──────────────────────────────
    bool _isBoss;

    // ── 来源 ──────────────────────────────────
    EnemyOrigin _origin = EnemyOrigin.Wild;
    Vector2 _campPosition;
    string _campId;
    bool _campExists;
    float _campRegenRate;

    // ── 游荡 ──────────────────────────────────
    Vector2 _wanderTarget;
    float _wanderTimer;
    bool _hasWanderTarget;
    const float WanderSpeedFraction = 0.35f;
    const float WanderNearCampRadius = 10f;
    const float WanderCampInnerRadius = 5f;
    const float WanderSelfRadius = 6f;
    const float WanderTargetReachDist = 0.5f;
    const float WanderMaxTargetTime = 5f;

    // ── 索敌 ──────────────────────────────────
    float _lastDamageTime = float.MinValue;
    bool _lastDamageFromPlayer;
    float _forcedAggroUntil = float.MinValue;
    const float ForcedAggroDuration = 5f;
    const float WanderRegenNoDamageWait = 4f;

    // ── 协同 ──────────────────────────────────
    Vector2? _cachedMeleeCoopAllyPos;
    float _meleeCoopCacheTimer;
    Vector2? _cachedRangedCoopAllyPos;
    float _rangedCoopCacheTimer;
    const float kCoopCacheDuration = 1.0f;

    // ── 闪避 ──────────────────────────────────
    ProjectileInfo? _trackedDodgeProjectile;
    float _dodgeProjectileSafeDistSq;

    // ── 词条 ──────────────────────────────────
    EnemyAffixSet _affixSet;
    EnemyAffixSet _pendingAffixSet;
    bool _hasPendingAffixSet;

    // ── 敌人类型 + 减伤 ─────────────────────
    string _enemyType = "";
    float _defenseRatio;
    string _aiProfileId = "";
    Vector2 _fastWispOrbitDir;
    float _fastWispRetargetTimer;
    float _movementPauseTimer;

    /// <summary>敌人类型字符串（生成本时设定，如 "camp_core:abc123"）。</summary>
    public string EnemyType { get => _enemyType; set => _enemyType = value ?? ""; }

    /// <summary>外部减伤比例 (0~1)。DamagePipeline 在结算时乘以 (1-DefenseRatio)。</summary>
    public float DefenseRatio { get => _defenseRatio; set => _defenseRatio = Mathf.Clamp01(value); }

    /// <summary>
    /// 禁用 AI 风格影响。为 true 时 ResolveMoveDirection 不做风筝/狙击判断，
    /// BruteTank 不加额外加速度。World 模式应设为 true（由词条系统驱动移动）。
    /// </summary>
    public bool DisableAiStyle { get; set; }

    static Transform s_cachedPlayer;

    // ── 公开属性（兼容旧 API）─────────────────

    public string EnemyId => enemyId;
    public EnemyAttackKind AttackKind => attackKind;
    public float AggroRange => aggroRange;
    public bool IsLaneEnemy => isLaneEnemy;
    public bool IsChargeAttackActive => _attack != null && _attack.IsChargeAttackActive;
    public bool IsInChargeDash => _attack != null && _attack.IsInChargeDash;
    public bool IsReflectReturnActive => _reflectReturnActive;
    public bool IsSprinting => _isSprinting;
    public bool IsStoppingSprint => _isStoppingSprint;
    public bool EnableChargeParticle => enableChargeParticle;
    public HealthComponent Health => _health;
    public EnemyMovement Movement => _movement;
    public EnemyAttack Attack => _attack;
    public EnemyVisualHub MotionVisual => _visual;
    public Transform ChaseTarget => chaseTarget;
    public bool IsFastWispStyle => aiStyle == EnemyAiStyle.FastWisp || _aiProfileId == "ai_fast_wisp";

    public Vector2 GetChargeIncomingDirection() =>
      _attack != null ? _attack.GetChargeIncomingDirection() : Vector2.right;

    public void SetLaneEnemy(bool v) => isLaneEnemy = v;
    public void SetLaneAdvanceDirection(Vector2 dir)
      => _laneAdvanceDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

    // ── 配置 ───────────────────────────────────

    public void ConfigureAi(EnemyAiProfileDatabase.AiProfile ai, string moveMode)
    {
      if (ai == null) return;
      _aiProfileId = ai.id ?? "";
      leashMult = ai.leash_mult > 0f ? ai.leash_mult : 1f;
      aiStyle = ai.id switch
      {
        "ai_ranged_kite" => EnemyAiStyle.RangedKite,
        "ai_ranged_sniper" => EnemyAiStyle.RangedSniper,
        "ai_barrage_kite" => EnemyAiStyle.RangedKite,
        "ai_barrage_sniper" => EnemyAiStyle.RangedSniper,
        "ai_laser_kite" => EnemyAiStyle.RangedKite,
        "ai_laser_sniper" => EnemyAiStyle.RangedSniper,
        "ai_brute_tank" => EnemyAiStyle.BruteTank,
        "ai_contact_brute" => EnemyAiStyle.BruteTank,
        "ai_lane_follow" => EnemyAiStyle.LaneFollow,
        "ai_stationary" => EnemyAiStyle.Stationary,
        "ai_fast_wisp" => EnemyAiStyle.FastWisp,
        _ => EnemyAiStyle.MeleeChase
      };
      if (moveMode == "lane_follow" || ai.id == "ai_lane_follow")
        isLaneEnemy = true;
    }

    public void ConfigureFromDef(
      string id, float speed, float damage, bool ranged, float aggro,
      float attackRangeValue, float windup, float cooldown, float projTurnRate,
      bool laneEnemy, float projSpeedOverride = -1f, float projScaleOverride = -1f,
      string profileId = null, string homingMode = null, float turnRateFromAttack = -1f,
      float lockLossAngle = -1f, string attackMode = null)
    {
      RefreshChildComponents();

      enemyId = id;
      moveSpeed = speed;
      attackDamage = damage;
      attackProfileId = profileId;
      aggroRange = aggro;
      attackWindup = windup;
      attackCooldown = cooldown;
      isLaneEnemy = laneEnemy;

      // 分发到子组件
      _movement?.ConfigureFromCore(speed, acceleration, deceleration, hitStunDuration, knockbackDistance);
      _attack?.ConfigureFromCore(profileId, damage, cooldown, windup, attackRangeValue,
        ranged, attackMode, projSpeedOverride, projTurnRate, projScaleOverride,
        homingMode, lockLossAngle, aggro);
    }

    public void ApplyScaledStats(float speed, float damage, float aggro,
      float attackRangeValue, float windup, float cooldown, float dashSpeedMult = 1f)
    {
      RefreshChildComponents();

      moveSpeed = speed;
      attackDamage = damage;
      aggroRange = aggro;
      attackWindup = windup;
      attackCooldown = cooldown;
      _movement?.ApplyScaledStats(speed, dashSpeedMult);
      _attack?.ApplyScaledStats(damage, cooldown, windup, attackRangeValue, dashSpeedMult);
    }

    public void SetWaveMode(Vector3 basePos)
    {
      _waveMode = true;
      _waveTarget = GameplayPlane.ToPlanar(basePos);
    }

    public void SetArenaOrbitPath(Vector2 spawnPos)
    {
      if (!ArenaLayoutLocator.Layout.IsActive) return;
      _arenaOrbitMode = true;
      _circlePathAngle = ArenaLayoutLocator.Layout.AngleAtPosition(spawnPos);
      _orbitDirection = Random.value < 0.5f ? -1f : 1f;
      SnapToCirclePath();
      _visual?.SetupOrbitVisual(26f);
    }

    // ── 来源 ───────────────────────────────────

    public void SetCampOrigin(string campId, Vector2 campPos, float regenRate)
    {
      _origin = EnemyOrigin.Camp;
      _campId = campId;
      _campPosition = campPos;
      _campRegenRate = regenRate;
      _campExists = true;
    }

    public void SetWildOrigin()
    {
      _origin = EnemyOrigin.Wild;
      _campExists = false;
      _campId = null;
    }

    public void MarkCampDestroyed()
    {
      _origin = EnemyOrigin.Wild;
      _campExists = false;
      _hasWanderTarget = false;
    }

    public EnemyOrigin Origin => _origin;

    // ── 词条 ───────────────────────────────────

    public void SetAffixSet(EnemyAffixSet affixSet)
    {
      if (_attack != null && (_attack.IsInSpecialAttack || _attack.IsWindingUp))
      {
        _pendingAffixSet = affixSet;
        _hasPendingAffixSet = true;
        return;
      }
      ApplyAffixSetImmediate(affixSet);
    }

    void ApplyAffixSetImmediate(EnemyAffixSet affixSet)
    {
      _affixSet = affixSet;
      _cachedMeleeCoopAllyPos = null;
      _cachedRangedCoopAllyPos = null;
      _meleeCoopCacheTimer = 0f;
      _rangedCoopCacheTimer = 0f;
      _trackedDodgeProjectile = null;
      _dodgeProjectileSafeDistSq = 0f;
    }

    public void TryApplyPendingAffixSet()
    {
      if (_hasPendingAffixSet && (_attack == null || (!_attack.IsInSpecialAttack && !_attack.IsWindingUp)))
      {
        _hasPendingAffixSet = false;
        ApplyAffixSetImmediate(_pendingAffixSet);
      }
    }

    // ── 奔跑 ───────────────────────────────────

    public void StartSprinting(float speedMult = 1.5f, float stopDuration = 0.5f)
    {
      if (_isSprinting || _isStoppingSprint || _health != null && _health.IsDead) return;
      _isSprinting = true;
      _isStoppingSprint = false;
      _sprintStopTimer = 0f;
      if (speedMult > 1f) _sprintSpeedMultiplier = speedMult;
      if (stopDuration > 0f) _sprintStopDuration = stopDuration;
      _movement?.SetSprintState(true, speedMult);
      _visual?.ApplySprintScale();
      _sprintWind?.Begin(Vector2.right);
    }

    public void StopSprinting()
    {
      if (!_isSprinting) return;
      _isSprinting = false;
      _isStoppingSprint = true;
      _sprintStopTimer = _sprintStopDuration;
      _movement?.SetSprintState(false);
      _visual?.RestoreSprintScale();
      _sprintWind?.End();
    }

    public void CancelSprint()
    {
      _isSprinting = false;
      _isStoppingSprint = false;
      _sprintStopTimer = 0f;
      _movement?.SetSprintState(false);
      _visual?.RestoreSprintScale();
      _sprintWind?.End();
    }

    public void PauseMovement(float duration)
    {
      _movementPauseTimer = Mathf.Max(_movementPauseTimer, duration);
      _movement?.ResetVelocity();
    }

    // ── 反弹返回 ───────────────────────────────

    public void ReflectChargeReturn(GameObject attacker, int chainDepth = 0)
    {
      if (chainDepth > 6) return;
      if (_reflectReturnActive) return;
      if (chainDepth == 0 && !IsInChargeDash && !IsChargeAttackActive) return;
      if (!IsInChargeDash && !IsChargeAttackActive) return;

      _attack?.ClearChargeDashState();
      _reflectReturnAttacker = attacker ?? ResolvePlayerAttacker();
      _reflectReturnChainDepth = chainDepth;

      var h = _health ?? GetComponent<HealthComponent>();
      if (h != null && !h.IsDead && _reflectReturnAttacker != null && chainDepth == 0)
      {
        var req = DamageRequest.Direct(CombatStatProviderLocator.Provider.ComputeReflectDamage(Mathf.Max(attackDamage, 10f)),
          "physical", "reflect", _reflectReturnAttacker);
        DamagePipeline.Apply(req, h);
      }

      var current = GameplayPlane.Position2D(transform);
      var returnDir = -GetChargeIncomingDirection();
      if (returnDir.sqrMagnitude < 0.0001f) returnDir = Vector2.left;
      var returnDist = _attack != null && _attack.ChargeDashStartValid
        ? Vector2.Distance(_attack.ChargeDashStartPos, current) : attackRange * 0.75f;
      returnDist = Mathf.Clamp(returnDist, 1.1f, WorldGridConstants.HalfViewportWorldSize() * 0.85f);
      float incomingSpeed = 9f * (_attack != null ? _attack.DeliveryType == EnemyDeliveryType.ChargeDash ? 2.2f : 2.2f : 2.2f);
      float returnSpeed = Mathf.Max(incomingSpeed * 0.98f, 7.5f);
      _reflectReturnCoroutine = StartCoroutine(ChargeReflectReturnRoutine(returnDir, returnDist, returnSpeed));
    }

    public void NotifyReflectContact(EnemyCore charger)
    {
      if (!_reflectReturnActive || charger == null || _reflectReturnHitTargets == null) return;
      if (_reflectReturnHitTargets.Contains(charger)) return;
      _reflectReturnHitTargets.Add(charger);
      var att = _reflectReturnAttacker ?? ResolvePlayerAttacker();
      ApplyReflectCollisionDamage(charger, att);
    }

    // ── 生命周期 ───────────────────────────────

    void Awake()
    {
      _spawnedAt = Time.time;
      _health = GetComponent<HealthComponent>();
      _damageDisplay = GetComponent<DamageDisplay>();
      _physics = GetComponent<EntityPhysicsBody>();
      _sprintWind = SprintWindEffect.Ensure(gameObject);
      _movement = GetComponent<EnemyMovement>();
      _attack = GetComponent<EnemyAttack>();

      if (_health != null)
      {
        _health.Damaged += OnDamaged;
        _health.Died += OnEnemyDied;
      }
      if (chaseTarget == null) chaseTarget = FindPlayer();
    }

    void Start()
    {
      RefreshChildComponents();

      if (_physics == null) _physics = GetComponent<EntityPhysicsBody>();
      _visual = GetComponent<EnemyVisualHub>();
      if (_visual == null) _visual = gameObject.AddComponent<EnemyVisualHub>();

      if (_attack != null && _attack.DeliveryType == EnemyDeliveryType.Beam)
        _visual.SetupBeamVisual();
      if (_arenaOrbitMode)
        _visual.SetupOrbitVisual(26f);

      _isBoss = GetComponent<BossCore>() != null;
      if (_isBoss)
        GameEventBus.Publish(new BossSpawnedEvent(gameObject, transform.position, enemyId));

      var reg = CombatRoot.EnemyRegistry;
      if (reg != null) reg.Register(this);
    }

    void RefreshChildComponents()
    {
      if (_health == null) _health = GetComponent<HealthComponent>();
      if (_damageDisplay == null) _damageDisplay = GetComponent<DamageDisplay>();
      if (_physics == null) _physics = GetComponent<EntityPhysicsBody>();
      if (_movement == null) _movement = GetComponent<EnemyMovement>();
      if (_attack == null) _attack = GetComponent<EnemyAttack>();
    }

    void Update()
    {
      if (!isActiveAndEnabled) return;
      if (_health != null && _health.IsDead) return;
      if (GetComponent<CombatFreezeBehaviour>() != null)
      {
        _movement?.ResetVelocity();
        _visual?.SetMoving(false);
        return;
      }

      if (_waveMode) { UpdateWaveMode(); return; }
      UpdateCombatChase();
    }

    void OnDestroy()
    {
      if (_attack != null) Destroy(_attack); // trigger OnDestroy for cleanup

      CombatRoot.EnemyRegistry?.Unregister(this);
      if (_health != null)
      {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnEnemyDied;
      }
    }

    // ── 死亡 ───────────────────────────────────

    void OnEnemyDied()
    {
      var dh = GetComponent<EnemyDeathHandler>();
      var lootTableId = dh != null ? dh.LootTableId : "common_mob";
      var isBoss = _isBoss;
      var killer = _health != null ? _health.LastAttacker : null;
      GameEventBus.Publish(new EnemyKilledEvent(gameObject, killer, transform.position, enemyId, lootTableId, isBoss));
      if (isBoss) GameEventBus.Publish(new BossKilledEvent(gameObject, killer, transform.position, enemyId));
    }

    // ── 受伤 ───────────────────────────────────

    void OnDamaged(float amount)
    {
      _lastDamageTime = Time.time;
      _lastDamageFromPlayer = IsPlayerSource(_health?.LastAttacker);
      if (_lastDamageFromPlayer)
      {
        _forcedAggroUntil = Time.time + ForcedAggroDuration;
        if (chaseTarget == null) chaseTarget = FindPlayer();
      }
      if (_isSprinting || _isStoppingSprint) CancelSprint();

      _movement?.ApplyHitStun();

      if (chaseTarget != null)
      {
        var knockbackVel = _movement != null
          ? _movement.GetKnockbackDirection(GameplayPlane.Position2D(transform), GameplayPlane.Position2D(chaseTarget))
          : Vector2.zero;
        _movement?.ApplyKnockback(knockbackVel);
      }
    }

    static bool IsPlayerSource(GameObject source)
    {
      if (source == null) return false;
      if (source.CompareTag("Player")) return true;
      var n = source.name;
      return n == "Player" || n.StartsWith("Player", System.StringComparison.OrdinalIgnoreCase)
           || n.StartsWith("Proj_Player", System.StringComparison.OrdinalIgnoreCase);
    }

    // ── AI 主循环 ──────────────────────────────

    void UpdateCombatChase()
    {
      if (_isStoppingSprint)
      {
        _sprintStopTimer -= Time.deltaTime;
        if (_sprintStopTimer <= 0f) { _isStoppingSprint = false; _sprintStopTimer = 0f; }
      }

      if (_attack == null || (!_attack.IsInSpecialAttack && !_attack.IsWindingUp))
        TryApplyPendingAffixSet();

      if (_attack != null && _attack.IsInSpecialAttack)
      {
        _visual?.SetMoving(false);
        if (_reflectReturnActive) return;
        if (_arenaOrbitMode) SnapToCirclePath();
        return;
      }
      if (_reflectReturnActive) return;
      if (_movement != null && _movement.TryUpdateHitStun(Time.deltaTime)) return;
      if (_movementPauseTimer > 0f)
      {
        _movementPauseTimer -= Time.deltaTime;
        _movement?.Stop(Time.deltaTime);
        _visual?.SetMoving(false);
        return;
      }

      if (chaseTarget == null) { chaseTarget = FindPlayer(); if (chaseTarget == null) return; }

      if (ShouldAlwaysChasePlayer())
        chaseTarget = FindPlayer();

      var dt = Time.deltaTime;
      var toTarget = PlanarOffsetTo(chaseTarget.position);
      var dist = toTarget.magnitude;
      var effectiveAggro = GetEffectiveAggroRange();

      if (dist > effectiveAggro && !IsForcedAggro() && !ShouldAlwaysChasePlayer())
      {
        UpdateWander(dt);
        return;
      }

      bool IsForcedAggro() => Time.time < _forcedAggroUntil && chaseTarget != null;

      // 朝向
      if (dist > 0.01f)
        _visual?.FaceDirectionSlerp(toTarget.normalized, dt);

      // 攻击
      var stopRange = _attack != null ? _attack.GetStopRange() : attackRange;
      _attack?.Tick(dt, chaseTarget, _affixSet, null, null, () => null);

      if (_attack != null && (_attack.IsInSpecialAttack || _attack.IsWindingUp))
      {
        _movement?.Stop(dt);
        _visual?.SetMoving(false);
        return;
      }

      if (IsArenaMode() && !ShouldAlwaysChasePlayer())
      {
        var arenaMoveDir = ApplyMovementAffixes(toTarget, dist, stopRange);
        if (arenaMoveDir.sqrMagnitude > 0.0001f && _movement != null)
        {
          float effSpeed = _movement.GetEffectiveSpeed(GetBuffSpeedMult());
          _movement.MoveDirection(arenaMoveDir, 1f, dt, GetBuffSpeedMult());
          UpdateSprintWindOnOrbit();
          _visual?.SetMoving(true);
        }
        else { SnapToCirclePath(); _visual?.SetMoving(false); }
        return;
      }

      var moveDir = ApplyMovementAffixes(toTarget, dist, stopRange);
      if (moveDir.sqrMagnitude > 0.0001f && _movement != null)
      {
        _movement.MoveDirection(moveDir, 1f, dt, GetBuffSpeedMult());
        UpdateSprintWindDirection(moveDir);
      }
      else if (_movement != null)
      {
        _movement.Stop(dt);
      }
      _visual?.SetMoving(_movement != null && _movement.IsMoving);
    }

    float GetBuffSpeedMult()
    {
      var bc = GetComponent<BuffContainer>();
      return bc != null ? bc.GetStatModifier("move_speed") : 1f;
    }

    // ── 游荡 ───────────────────────────────────

    void UpdateWander(float dt)
    {
      var selfPos = GameplayPlane.Position2D(transform);

      if (!_hasWanderTarget || _wanderTimer > WanderMaxTargetTime)
      {
        if (_origin == EnemyOrigin.Camp && _campExists)
        {
          var distToCamp = Vector2.Distance(selfPos, _campPosition);
          _wanderTarget = distToCamp > WanderNearCampRadius
            ? _campPosition + Random.insideUnitCircle * WanderCampInnerRadius
            : _campPosition + Random.insideUnitCircle * WanderNearCampRadius;
        }
        else
          _wanderTarget = selfPos + Random.insideUnitCircle * WanderSelfRadius;
        _hasWanderTarget = true;
        _wanderTimer = 0f;
      }
      _wanderTimer += dt;
      _movement?.MoveWander(_wanderTarget, selfPos, dt);
      _visual?.SetMoving(_movement != null && _movement.IsMoving);

      if (_origin == EnemyOrigin.Camp && _campExists && Vector2.Distance(selfPos, _campPosition) <= WanderNearCampRadius)
        TryWanderRegen(dt);
    }

    void TryWanderRegen(float dt)
    {
      if (_health == null || _health.IsDead) return;
      if (_health.CurrentHp >= _health.MaxHp) return;
      if (Time.time - _lastDamageTime < WanderRegenNoDamageWait) return;
      float amount = _health.MaxHp * _campRegenRate * dt;
      if (amount > 0f) _health.Heal(amount);
    }

    // ── 波次 ───────────────────────────────────

    void UpdateWaveMode()
    {
      if (_arenaOrbitMode && ArenaLayoutLocator.Layout.IsActive && !ShouldAlwaysChasePlayer())
      { UpdateArenaOrbitWave(); return; }
      if (_movement != null && _movement.TryUpdateHitStun(Time.deltaTime)) return;
      if (chaseTarget == null) chaseTarget = FindPlayer();
      if (ShouldAlwaysChasePlayer() || IsPlayerInAggroRange()) { UpdateCombatChase(); return; }

      float dt = Time.deltaTime;
      var selfPlanar = GameplayPlane.Position2D(transform);
      var playerPlanar = chaseTarget != null ? GameplayPlane.Position2D(chaseTarget) : _waveTarget;
      _waveTarget = playerPlanar;
      var toWave = _waveTarget - selfPlanar;
      var toPlayer = playerPlanar - selfPlanar;

      Vector2 moveDir;
      if (isLaneEnemy && toWave.sqrMagnitude > 0.01f)
      {
        var waveDir = toWave.normalized;
        moveDir = (waveDir * 0.55f + _laneAdvanceDir * 0.45f).normalized;
        if (toPlayer.sqrMagnitude > 0.01f && toPlayer.magnitude < aggroRange * 1.5f)
          moveDir = (moveDir + toPlayer.normalized * 0.25f).normalized;
      }
      else
        moveDir = toWave.sqrMagnitude > 0.01f ? toWave.normalized : Vector2.right;

      if (toWave.magnitude < 0.5f)
      {
        var ph = chaseTarget?.GetComponent<HealthComponent>();
        if (ph != null && !ph.IsDead) DamagePipeline.Apply(BuildWaveRequest(), ph);
        if (_health != null) _health.TakeDamage(_health.MaxHp);
        else Destroy(gameObject);
        return;
      }

      if (_movement != null)
      {
        _movement.MoveDirection(moveDir, 1f, dt, GetBuffSpeedMult());
        _visual?.SetMoving(true);
      }
      UpdateSprintWindDirection(moveDir);
    }

    void UpdateArenaOrbitWave()
    {
      if (_movement != null && _movement.TryUpdateHitStun(Time.deltaTime)) return;
      if (_attack == null || (!_attack.IsInSpecialAttack && !_attack.IsWindingUp))
        TryApplyPendingAffixSet();
      if (_attack != null && _attack.IsInSpecialAttack) { SnapToCirclePath(); _visual?.SetMoving(false); return; }
      if (chaseTarget == null) chaseTarget = FindPlayer();

      float dt = Time.deltaTime;
      if (_movement != null)
        AdvanceAlongCircle(_orbitDirection * _movement.GetEffectiveSpeed(GetBuffSpeedMult()) * dt / ArenaLayoutLocator.Layout.PathRadius);
      _visual?.SetMoving(true);
      UpdateSprintWindOnOrbit();
      if (chaseTarget != null)
        _attack?.Tick(dt, chaseTarget, _affixSet, null, null, () => null);
    }

    DamageRequest BuildWaveRequest()
    {
      var req = DamageRequest.Direct(attackDamage, attackDamageType, attackDamageSource, gameObject);
      if (!string.IsNullOrEmpty(attackProfileId)) req.AttackProfileId = attackProfileId;
      return req;
    }

    // ── 距离/范围 ───────────────────────────────

    const float RoguelikeGlobalAggroRange = 9999f;

    static bool IsRoguelikeGlobalAggro() =>
      GameSessionConfig.RunConfigured &&
      GameSessionConfig.SelectedMode == GameSessionConfig.GameMode.Arena;

    float GetEffectiveAggroRange()
    {
      if (IsRoguelikeGlobalAggro())
        return RoguelikeGlobalAggroRange;

      if (ArenaLayoutLocator.Layout.IsActive)
        return ArenaLayoutLocator.Layout.FullCombatRange;

      if (IsArenaMode())
        return ArenaLayoutLocator.Layout.FullCombatRange;

      return aggroRange * Mathf.Clamp(leashMult, 0.35f, 2f);
    }

    bool ShouldAlwaysChasePlayer() =>
      IsRoguelikeGlobalAggro() && aiStyle != EnemyAiStyle.Stationary;

    bool IsArenaMode() => _arenaOrbitMode && ArenaLayoutLocator.Layout.IsActive;
    bool IsPlayerInAggroRange() => chaseTarget != null &&
      PlanarOffsetTo(chaseTarget.position).magnitude <= GetEffectiveAggroRange();

    // ── 词条移动 ───────────────────────────────

    Vector2 ApplyMovementAffixes(Vector2 toTarget, float dist, float stopRange)
    {
      if (_affixSet == null || _affixSet.MovementAffixes.Count == 0)
        return ResolveMoveDirection(toTarget, dist, stopRange);

      var enemyPos = GameplayPlane.Position2D(transform);
      var playerPos = chaseTarget != null ? GameplayPlane.Position2D(chaseTarget) : enemyPos + toTarget;
      Vector2 weightedDir = Vector2.zero;
      float totalWeight = 0f;
      bool anySprint = false;

      for (int i = 0; i < _affixSet.MovementAffixes.Count; i++)
      {
        var affix = _affixSet.MovementAffixes[i];
        if (affix.Weight <= 0f) continue;
        MoveStrategyResult result = MoveStrategyResult.None;
        var currentVel = _movement != null ? _movement.Velocity : Vector2.zero;

        switch (affix.Type)
        {
          case MoveAffixType.MeleeBasic:
            result = EnemyMovementStrategies.MeleeBasic(enemyPos, playerPos,
              _movement != null ? _movement.GetEffectiveSpeed(GetBuffSpeedMult()) : moveSpeed, null, affix.GetParam(0, 999f), affix.GetParam(1, 0.7f), affix.GetParam(2, 1.0f)); break;
          case MoveAffixType.DodgeProjectile:
            var projList = GetDodgeProjectiles();
            result = EnemyMovementStrategies.DodgeProjectile(enemyPos, 0.45f, projList, affix.GetParam(0, 5f), currentVel);
            if (result.HasDirection && projList.Count > 0 && !_trackedDodgeProjectile.HasValue)
              TrackDodgeProjectile(projList[0], affix.GetParam(0, 5f));
            break;
          case MoveAffixType.MeleeSpread:
            result = EnemyMovementStrategies.MeleeSpread(enemyPos, GatherMeleeAllyPositions(), affix.GetParam(0, 2.5f)); break;
          case MoveAffixType.MeleeCooperative:
            if (IsArenaMode()) break;
            result = EnemyMovementStrategies.MeleeCooperative(enemyPos, playerPos, GetRangedCoopPositions(), affix.GetParam(0, 6f)); break;
          case MoveAffixType.RangedBasic:
            result = EnemyMovementStrategies.RangedBasic(enemyPos, playerPos, rangedAttackRange, affix.GetParam(0, 0.65f)); break;
          case MoveAffixType.RangedDodge:
            var rProjList = GetDodgeProjectiles();
            result = EnemyMovementStrategies.RangedDodge(enemyPos, 0.45f, rProjList, affix.GetParam(0, 4.5f), currentVel);
            if (result.HasDirection && rProjList.Count > 0 && !_trackedDodgeProjectile.HasValue)
              TrackDodgeProjectile(rProjList[0], affix.GetParam(0, 4.5f));
            break;
          case MoveAffixType.RangedCooperative:
            if (IsArenaMode()) break;
            result = EnemyMovementStrategies.RangedCooperative(enemyPos, playerPos, GetMeleeCoopPositions(), affix.GetParam(0, 5f)); break;
        }
        if (!result.HasDirection) continue;
        float w = result.Weight * affix.Weight;
        weightedDir += result.Direction * w;
        totalWeight += w;
        if (result.ShouldSprint) anySprint = true;
      }

      if (totalWeight < 0.0001f || weightedDir.sqrMagnitude < 0.0001f)
        return ResolveMoveDirection(toTarget, dist, stopRange);

      var finalDir = (weightedDir / totalWeight).normalized;
      if (IsArenaMode() && !ShouldAlwaysChasePlayer()) return MapToArenaOrbit(finalDir);

      if (anySprint && !_isSprinting && !_isStoppingSprint && dist > attackRange * 1.8f)
        StartSprinting(_sprintSpeedMultiplier, _sprintStopDuration);
      else if (!anySprint && _isSprinting) StopSprinting();

      return finalDir;
    }

    Vector2 ResolveMoveDirection(Vector2 toTarget, float dist, float stopRange)
    {
      if (dist <= 0.01f) return Vector2.zero;
      var dir = toTarget / dist;

      if (ShouldAlwaysChasePlayer())
      {
        if (dist <= stopRange) return Vector2.zero;
        return dir;
      }

      // AI 风格被禁用时仅做追近，不做风筝/狙击等特殊行为
      if (DisableAiStyle)
      {
        if (dist <= stopRange) return Vector2.zero;
        return dir;
      }

      if (aiStyle == EnemyAiStyle.FastWisp)
        return ResolveFastWispDirection(dir, dist, stopRange);

      if (attackKind == EnemyAttackKind.Ranged)
      {
        float preferred = rangedAttackRange * (aiStyle == EnemyAiStyle.RangedSniper ? 0.95f : 0.85f);
        if (aiStyle == EnemyAiStyle.RangedKite && dist < preferred * 0.65f) return -dir;
        if (aiStyle == EnemyAiStyle.RangedSniper && dist < preferred * 0.55f) return -dir * 0.75f;
      }
      if (dist <= stopRange) return Vector2.zero;
      if (isLaneEnemy && (aiStyle == EnemyAiStyle.LaneFollow || aiStyle == EnemyAiStyle.MeleeChase))
        dir = (dir + _laneAdvanceDir * 0.4f).normalized;
      return dir;
    }

    Vector2 ResolveFastWispDirection(Vector2 toPlayerDir, float dist, float stopRange)
    {
      if (_fastWispRetargetTimer <= 0f || _fastWispOrbitDir.sqrMagnitude < 0.0001f)
      {
        var tangent = new Vector2(-toPlayerDir.y, toPlayerDir.x);
        _fastWispOrbitDir = tangent * (Random.value < 0.5f ? -1f : 1f);
        _fastWispOrbitDir = (_fastWispOrbitDir + Random.insideUnitCircle * 0.35f).normalized;
        _fastWispRetargetTimer = Random.Range(0.35f, 0.85f);
      }
      _fastWispRetargetTimer -= Time.deltaTime;

      var minOrbit = Mathf.Max(stopRange + 1.3f, 2.8f);
      var maxOrbit = Mathf.Max(minOrbit + 2.2f, 6.8f);
      if (dist > maxOrbit)
        return (toPlayerDir * 0.7f + _fastWispOrbitDir * 0.3f).normalized;
      if (dist < minOrbit)
        return (-toPlayerDir * 0.65f + _fastWispOrbitDir * 0.35f).normalized;
      return (_fastWispOrbitDir * 0.78f + toPlayerDir * Random.Range(-0.24f, 0.18f)).normalized;
    }

    // ── 竞技场轨道 ───────────────────────────────

    void SnapToCirclePath()
    {
      if (!_arenaOrbitMode || !ArenaLayoutLocator.Layout.IsActive) return;
      GameplayPlane.SetPosition2D(transform, ArenaLayoutLocator.Layout.PositionAtAngle(_circlePathAngle));
    }

    void AdvanceAlongCircle(float signedAngleRad)
    {
      if (Mathf.Abs(signedAngleRad) < 0.00001f) return;
      _circlePathAngle = Mathf.Repeat(_circlePathAngle + signedAngleRad, Mathf.PI * 2f);
      SnapToCirclePath();
    }

    Vector2 MapToArenaOrbit(Vector2 worldDir)
    {
      if (!ArenaLayoutLocator.Layout.IsActive) return worldDir;
      var selfPos = GameplayPlane.Position2D(transform);
      var radial = selfPos - ArenaLayoutLocator.Layout.Center;
      if (radial.sqrMagnitude < 0.0001f) return worldDir;
      var tangent = new Vector2(-radial.y * _orbitDirection, radial.x * _orbitDirection).normalized;
      float dot = Vector2.Dot(worldDir, tangent);
      if (Mathf.Abs(dot) < 0.15f) return Vector2.zero;
      return dot > 0f ? tangent : -tangent;
    }

    // ── 反弹返回协程 ────────────────────────────

    IEnumerator ChargeReflectReturnRoutine(Vector2 returnDir, float dist, float speed)
    {
      _reflectReturnActive = true;
      _visual?.UseReflectReturnSpin();
      returnDir.Normalize();
      var start = GameplayPlane.Position2D(transform);
      var end = start + returnDir * dist;
      float dur = Mathf.Clamp(dist / Mathf.Max(speed, 5f), 0.22f, 0.95f);
      float elapsed = 0f;
      var prev = start;
      var hitSet = new HashSet<EnemyCore> { this };
      _reflectReturnHitTargets = hitSet;
      _visual?.FaceDirection(returnDir);

      while (elapsed < dur)
      {
        elapsed += Time.deltaTime;
        float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / dur), 2f);
        var pos = Vector2.Lerp(start, end, t);
        GameplayPlane.SetPosition2D(transform, pos);
        TryReflectReturnContacts(prev, pos, hitSet);
        prev = pos;
        yield return null;
      }
      GameplayPlane.SetPosition2D(transform, end);
      TryReflectReturnContacts(prev, end, hitSet);
      _reflectReturnActive = false;
      _reflectReturnHitTargets = null;
      _visual?.ResetSpinMultiplier();
      if (_arenaOrbitMode) { _circlePathAngle = ArenaLayoutLocator.Layout.AngleAtPosition(GameplayPlane.Position2D(transform)); SnapToCirclePath(); _visual?.EnableSlowOrbitSpin(26f); }
      _reflectReturnCoroutine = null;
    }

    void TryReflectReturnContacts(Vector2 prev, Vector2 curr, HashSet<EnemyCore> hitSet)
    {
      var reg = CombatRoot.EnemyRegistry;
      var att = _reflectReturnAttacker ?? ResolvePlayerAttacker();
      if (reg == null || att == null) return;
      foreach (var other in reg.AllEnemies)
      {
        if (other == null || other == this || hitSet.Contains(other)) continue;
        if (!SegmentIntersectsCircle(prev, curr, GameplayPlane.Position2D(other.transform),
          ResolveBodyRadius() + other.ResolveBodyRadius() + 0.14f)) continue;
        hitSet.Add(other);
        ApplyReflectCollisionDamage(other, att);
        if (!other.IsReflectReturnActive && other.IsInChargeDash)
          other.ReflectChargeReturn(att, _reflectReturnChainDepth + 1);
      }
    }

    void ApplyReflectCollisionDamage(EnemyCore other, GameObject attacker)
    {
      if (other == null || attacker == null) return;
      var oh = other.GetComponent<HealthComponent>();
      if (oh == null || oh.IsDead) return;
      float dmg = CombatStatProviderLocator.Provider.ComputeReflectDamage(Mathf.Max(attackDamage * 0.85f, 8f));
      DamagePipeline.Apply(DamageRequest.Direct(dmg, "physical", "reflect", attacker), oh);
    }

    float ResolveBodyRadius() => _physics != null ? Mathf.Max(0.28f, _physics.CollisionRadius) : 0.55f;

    static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, Vector2 center, float radius)
    {
      var ab = b - a;
      var lenSq = ab.sqrMagnitude;
      if (lenSq < 0.0001f) return (center - a).sqrMagnitude <= radius * radius;
      float t = Mathf.Clamp01(Vector2.Dot(center - a, ab) / lenSq);
      return (center - (a + ab * t)).sqrMagnitude <= radius * radius;
    }

    static GameObject ResolvePlayerAttacker()
    {
      if (s_cachedPlayer != null) return s_cachedPlayer.gameObject;
      var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
      if (p != null) s_cachedPlayer = p.transform;
      return p;
    }

    // ── 协同/闪避辅助 ──────────────────────────

    List<Vector2> GetMeleeCoopPositions()
    {
      if (_cachedMeleeCoopAllyPos.HasValue && _meleeCoopCacheTimer > 0f) { _meleeCoopCacheTimer -= Time.deltaTime; _s_coopSingleList[0] = _cachedMeleeCoopAllyPos.Value; return _s_coopSingleList; }
      return RefreshCoopCache(CoopKind.MeleeCover);
    }
    List<Vector2> GetRangedCoopPositions()
    {
      if (_cachedRangedCoopAllyPos.HasValue && _rangedCoopCacheTimer > 0f) { _rangedCoopCacheTimer -= Time.deltaTime; _s_coopSingleList[0] = _cachedRangedCoopAllyPos.Value; return _s_coopSingleList; }
      return RefreshCoopCache(CoopKind.RangedCover);
    }

    enum CoopKind { MeleeCover, RangedCover }

    List<Vector2> RefreshCoopCache(CoopKind kind)
    {
      _s_coopFullList.Clear();
      var reg = CombatRoot.EnemyRegistry;
      if (reg != null)
      {
        var sp = GameplayPlane.Position2D(transform);
        float closest = float.MaxValue;
        Vector2 cp = Vector2.zero;
        bool found = false;
        foreach (var o in reg.AllEnemies)
        {
          if (o == null || o == this || o.Health != null && o.Health.IsDead) continue;
          bool needed = kind == CoopKind.MeleeCover ? o.AttackKind == EnemyAttackKind.Ranged : o.AttackKind == EnemyAttackKind.Melee;
          if (!needed) continue;
          var op = GameplayPlane.Position2D(o.transform);
          float dsq = (op - sp).sqrMagnitude;
          if (dsq < closest) { closest = dsq; cp = op; found = true; }
        }
        if (found)
        {
          if (kind == CoopKind.MeleeCover) { _cachedMeleeCoopAllyPos = cp; _meleeCoopCacheTimer = kCoopCacheDuration; }
          else { _cachedRangedCoopAllyPos = cp; _rangedCoopCacheTimer = kCoopCacheDuration; }
          _s_coopSingleList[0] = cp;
          return _s_coopSingleList;
        }
      }
      return _s_coopFullList;
    }

    static readonly List<Vector2> _s_coopSingleList = new List<Vector2>(1) { Vector2.zero };
    static readonly List<Vector2> _s_coopFullList = new List<Vector2>();

    List<ProjectileInfo> GetDodgeProjectiles()
    {
      _s_dodgeList.Clear();
      if (_trackedDodgeProjectile.HasValue)
      {
        var t = _trackedDodgeProjectile.Value;
        var sp = GameplayPlane.Position2D(transform);
        var toEnemy = sp - t.Position;
        if (Vector2.Dot(toEnemy, t.Direction) > 0f && Vector2.Dot(toEnemy, t.Direction) < _dodgeProjectileSafeDistSq)
        { _s_dodgeList.Add(t); return _s_dodgeList; }
        _trackedDodgeProjectile = null;
      }
      return _s_dodgeList;
    }

    void TrackDodgeProjectile(ProjectileInfo p, float safeDist)
    {
      _trackedDodgeProjectile = p;
      _dodgeProjectileSafeDistSq = safeDist * safeDist;
    }

    static readonly List<ProjectileInfo> _s_dodgeList = new List<ProjectileInfo>();

    List<Vector2> GatherMeleeAllyPositions()
    {
      _s_meleeAllyList.Clear();
      var reg = CombatRoot.EnemyRegistry;
      if (reg == null) return _s_meleeAllyList;
      var sp = GameplayPlane.Position2D(transform);
      foreach (var o in reg.AllEnemies)
      {
        if (o == null || o == this || (o.Health != null && o.Health.IsDead)) continue;
        if (o.AttackKind != EnemyAttackKind.Melee) continue;
        var op = GameplayPlane.Position2D(o.transform);
        if ((op - sp).sqrMagnitude < 25f) _s_meleeAllyList.Add(op);
      }
      return _s_meleeAllyList;
    }
    static readonly List<Vector2> _s_meleeAllyList = new List<Vector2>();

    // ── 奔跑风粒子 ──────────────────────────────

    void UpdateSprintWindDirection(Vector2 dir)
    {
      if (!_isSprinting || _sprintWind == null || dir.sqrMagnitude <= 0.0001f) return;
      _sprintWind.UpdateDirection(dir);
    }

    void UpdateSprintWindOnOrbit()
    {
      if (!_isSprinting || _sprintWind == null || !ArenaLayoutLocator.Layout.IsActive) return;
      var selfPos = GameplayPlane.Position2D(transform);
      var radial = selfPos - ArenaLayoutLocator.Layout.Center;
      var tangent = new Vector2(-radial.y * _orbitDirection, radial.x * _orbitDirection);
      if (tangent.sqrMagnitude > 0.0001f) _sprintWind.UpdateDirection(tangent.normalized);
    }

    // ── 工具 ───────────────────────────────────

    Vector2 PlanarOffsetTo(Vector3 worldTarget)
      => GameplayPlane.ToPlanar(worldTarget) - GameplayPlane.Position2D(transform);

    static Transform FindPlayer()
    {
      if (s_cachedPlayer != null) return s_cachedPlayer;
      var p = GameObject.Find("Player");
      if (p != null) { s_cachedPlayer = p.transform; return s_cachedPlayer; }
      var t = GameObject.FindGameObjectWithTag("Player");
      if (t != null) { s_cachedPlayer = t.transform; return s_cachedPlayer; }
      return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
      Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.35f);
      Gizmos.DrawWireSphere(transform.position, aggroRange);
      Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.6f);
      Gizmos.DrawWireSphere(transform.position, attackRange);
      if (attackKind == EnemyAttackKind.Ranged)
      {
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
      }
    }
#endif
  }
}
