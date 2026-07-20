using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Visual;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Laser;
using Game.Shared.Projectile;
using Game.Shared.Runtime.Physics;
using Game.Shared.Stats;
using Game.Shared.Vfx;
using HealthComponent = global::Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 敌人攻击组件：攻��循环（前摇→执行→冷却）+ 4种交付类型。
  /// 附加到敌人 GameObject 上，由 EnemyCore 驱动。
  /// 不挂载此组件 → 敌人不可攻击（如营地核心）。
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyAttack : MonoBehaviour
  {
    [Header("Identity")]
    [SerializeField] string attackProfileId;
    [SerializeField] string attackDamageType = "physical";
    [SerializeField] string attackDamageSource = "monster";
    [SerializeField] EnemyAttackKind attackKind = EnemyAttackKind.Melee;

    [Header("Timing")]
    [SerializeField] float attackDamage = 6f;
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] float attackWindup = 0.2f;

    [Header("Range")]
    [SerializeField] float attackRange = 1.1f;
    [SerializeField] float rangedAttackRange = 5.5f;
    [SerializeField] float chargeEngageRange = 6.5f;

    [Header("Projectile")]
    [SerializeField] float projectileSpeed = 6f;
    [SerializeField] float projectileTurnRateDeg = 85f;
    [SerializeField] float projectileScale = 0.32f;
    [SerializeField] Color projectileColor = new(0.95f, 0.35f, 0.85f, 1f);
    [SerializeField] string projectileHomingMode;
    [SerializeField] float projectileLockLossAngle = 60f;

    [Header("Beam")]
    [SerializeField] Color laserColor = new(1f, 0.42f, 0.15f, 1f);

    float _dashSpeedMult = 2.2f;
    float _dashSpeedScaling = 1f;
    float _dashDistance;
    int _projectileCount = 1;
    float _spreadDeg = 12f;
    float _beamHalfWidth = 0.22f;
    bool _beamPierce;
    float _beamDuration = 0.65f;
    float _beamTickInterval = 0.15f;

    EnemyDeliveryType _deliveryType = EnemyDeliveryType.Melee;

    /// <summary>
    /// 攻击发射源。非 null 时所有弹体/激光/冲刺从该 Transform.position 发出。
    /// Boss 通过此字段让部件成为攻击发射点。
    /// </summary>
    public Transform AttackOrigin { get; set; }

    Vector3 SpawnPosition => AttackOrigin != null ? AttackOrigin.position : transform.position;

    float _attackTimer;
    float _windupTimer;
    bool _isWindingUp;
    bool _inSpecialAttack;
    bool _chargeDashPhase;
    Vector2 _chargeDashDir;
    Vector2 _chargeDashStartPos;
    bool _chargeDashStartValid;
    Vector2 _chargeDashPrevPos;
    bool _chargeDashPrevPosValid;
    Coroutine _attackCoroutine;
    LaserEnemyAttack _activeLaser;
    GameObject _activeLaserAimLine;
    Material _activeLaserAimMaterial;
    ChargeWarningIndicator _activeChargeWarning;
    ChargeDashTrailEffect _chargeTrail;
    EntityPhysicsBody _physics;
    EnemyCore _core;

    const float EnemyProjectileFullRange =
      WorldGridConstants.GameplayViewportTiles * WorldGridConstants.TileSize;
    const float ChargeSpawnGraceSeconds = 3f;
    float _spawnedAt;

    // ── 属性 ──────────────────────────────────────
    public EnemyDeliveryType DeliveryType => _deliveryType;
    public EnemyAttackKind AttackKind => attackKind;
    public bool IsChargeAttackActive => _inSpecialAttack && _deliveryType == EnemyDeliveryType.ChargeDash;
    public bool IsInChargeDash => _chargeDashPhase;
    public bool IsInSpecialAttack => _inSpecialAttack;
    public bool IsWindingUp => _isWindingUp;
    public bool CanStartAttack => !_isWindingUp && !_inSpecialAttack && _attackTimer <= 0f;
    public string AttackProfileId => attackProfileId;
    public string AttackDamageSource => attackDamageSource;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float AttackRange => attackRange;
    public float RangedAttackRange => rangedAttackRange;
    public float ChargeEngageRange => chargeEngageRange;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileScale => projectileScale;
    public Color ProjectileColor => projectileColor;

    void Awake()
    {
      _spawnedAt = Time.time;
      _physics = GetComponent<EntityPhysicsBody>();
      _core = GetComponent<EnemyCore>();
      _chargeTrail = ChargeDashTrailEffect.Ensure(gameObject);
    }

    // ── 配置注入 ──────────────────────────────────

    public void ConfigureFromCore(
      string profileId,
      float damage, float cooldown, float windup, float rangeVal,
      bool ranged, string attackMode,
      float projSpeed, float projTurnRate, float projScale,
      string homingMode, float lockLossAngle,
      float aggroRangeForCharge)
    {
      attackProfileId = profileId;
      attackDamageType = AttackProfileDatabase.GetDamageType(profileId, "physical");
      attackDamageSource = AttackProfileDatabase.GetDamageSource(profileId, "monster");
      attackDamage = damage;
      attackCooldown = cooldown;
      attackWindup = windup;
      aggroRange = aggroRangeForCharge;

      ApplyAttackProfile(profileId, attackMode, ranged, rangeVal);

      if (ranged || _deliveryType == EnemyDeliveryType.Projectile || _deliveryType == EnemyDeliveryType.Beam)
      {
        attackKind = EnemyAttackKind.Ranged;
        rangedAttackRange = rangeVal;
        if (projTurnRate > 0f) projectileTurnRateDeg = projTurnRate;
        if (projSpeed > 0f) projectileSpeed = projSpeed;
        if (projScale > 0f) projectileScale = projScale;
        if (!string.IsNullOrEmpty(homingMode)) projectileHomingMode = homingMode;
        else if (_deliveryType == EnemyDeliveryType.Projectile) projectileHomingMode = "none";
        if (lockLossAngle > 0f) projectileLockLossAngle = lockLossAngle;
      }
      else
      {
        attackKind = EnemyAttackKind.Melee;
        attackRange = rangeVal;
      }
    }

    float aggroRange;

    public void ApplyScaledStats(
      float damage, float cooldown, float windup, float rangeVal, float dashMult = 1f)
    {
      attackDamage = damage;
      attackCooldown = cooldown;
      attackWindup = windup;
      _dashSpeedScaling = dashMult;
      if (attackKind == EnemyAttackKind.Ranged || _deliveryType == EnemyDeliveryType.Beam)
        rangedAttackRange = rangeVal;
      else
        attackRange = rangeVal;
    }

    void ApplyAttackProfile(string profileId, string attackMode, bool ranged, float rangeVal)
    {
      var profile = AttackProfileDatabase.Get(profileId);
      if (profile != null)
      {
        if (profile.windup > 0f) attackWindup = profile.windup;
        if (profile.cooldown > 0f) attackCooldown = profile.cooldown;
        if (profile.range > 0f)
        {
          if (profile.delivery == "projectile" || profile.delivery == "beam" || attackMode == "barrage" || attackMode == "laser")
            rangedAttackRange = profile.range;
          else
            attackRange = profile.range;
        }
        if (profile.base_damage > 0f) attackDamage = profile.base_damage;
        _dashSpeedMult = profile.dash_speed_mult > 0f ? profile.dash_speed_mult : 2.2f;
        _dashDistance = profile.dash_distance > 0f ? profile.dash_distance : 0f;
        _projectileCount = profile.projectile_count > 0 ? profile.projectile_count : 1;
        _spreadDeg = profile.spread_deg > 0f ? profile.spread_deg : 12f;
        _beamHalfWidth = profile.beam_half_width > 0f ? profile.beam_half_width : 0.22f;
        _beamPierce = profile.beam_pierce;
        if (profile.beam_duration > 0f) _beamDuration = profile.beam_duration;
        if (profile.beam_tick_interval > 0f) _beamTickInterval = profile.beam_tick_interval;
        if (profile.projectile_speed > 0f) projectileSpeed = profile.projectile_speed;
        if (profile.projectile_scale > 0f) projectileScale = profile.projectile_scale;
        if (!string.IsNullOrEmpty(profile.projectile_homing)) projectileHomingMode = profile.projectile_homing;
      }

      _deliveryType = ResolveDeliveryType(profile, attackMode, ranged);
      if (_deliveryType == EnemyDeliveryType.ChargeDash)
        chargeEngageRange = Mathf.Clamp(aggroRange * 0.92f, 5.5f, 14f);
    }

    static EnemyDeliveryType ResolveDeliveryType(AttackProfileDatabase.AttackProfile profile, string attackMode, bool ranged)
    {
      if (profile?.delivery == "beam" || attackMode == "laser") return EnemyDeliveryType.Beam;
      if (profile?.delivery == "projectile" || attackMode == "barrage" || ranged) return EnemyDeliveryType.Projectile;
      if (profile != null && profile.charge_dash || attackMode == "charge") return EnemyDeliveryType.ChargeDash;
      return EnemyDeliveryType.Melee;
    }

    // ── 攻击循环 Tick ──────────────────────────────

    public void Tick(float dt, Transform chaseTarget, EnemyAffixSet affixSet,
      System.Action finishCallback, System.Action onChargeDashStep,
      System.Func<Vector2?> playerVelFn)
    {
      _attackTimer = Mathf.Max(0f, _attackTimer - dt);

      if (_isWindingUp)
      {
        _windupTimer -= dt;
        if (_windupTimer <= 0f)
        {
          _isWindingUp = false;
          ExecuteAttack(chaseTarget, affixSet);
        }
        return;
      }

      if (_inSpecialAttack && !_chargeDashPhase)
        return;

      if (chaseTarget == null)
        return;

      var dist = GameplayPlane.PlanarDistance(transform.position, chaseTarget.position);
      var effectiveRange = GetEffectiveAttackRange();

      if (dist <= effectiveRange && _attackTimer <= 0f && !_isWindingUp)
        StartAttack(chaseTarget, affixSet);
    }

    public float GetEffectiveAttackRange()
    {
      if (_deliveryType == EnemyDeliveryType.Projectile || attackKind == EnemyAttackKind.Ranged)
        return EnemyProjectileFullRange;
      if (_deliveryType == EnemyDeliveryType.ChargeDash)
        return chargeEngageRange > 0f ? chargeEngageRange : 999f;
      return attackRange;
    }

    public float GetStopRange()
    {
      return attackKind == EnemyAttackKind.Ranged
        ? Mathf.Min(rangedAttackRange, 999f)
        : attackRange;
    }

    // ── 开始攻击 ──────────────────────────────────

    void StartAttack(Transform chaseTarget, EnemyAffixSet affixSet)
    {
      if (!isActiveAndEnabled)
        return;

      if (_core != null && _core.IsStoppingSprint)
        return;

      switch (_deliveryType)
      {
        case EnemyDeliveryType.ChargeDash:
          if (Time.time < _spawnedAt + ChargeSpawnGraceSeconds)
          {
            _attackTimer = 0.4f;
            return;
          }
          _attackCoroutine = StartCoroutine(ChargeAttackRoutine(chaseTarget, affixSet));
          return;
        case EnemyDeliveryType.Beam:
          _attackCoroutine = StartCoroutine(LaserAttackRoutine(chaseTarget, affixSet));
          return;
        case EnemyDeliveryType.Projectile:
          _attackCoroutine = StartCoroutine(RangedAttackRoutine(chaseTarget, affixSet));
          return;
      }

      if (attackWindup > 0f)
      {
        _isWindingUp = true;
        _windupTimer = attackWindup;
        if (gameObject.activeInHierarchy)
          StartCoroutine(WindupVisual());
      }
      else
      {
        ExecuteAttack(chaseTarget, affixSet);
      }
    }

    // ── 四种交付类型 ───────────────────────────────

    void ExecuteAttack(Transform chaseTarget, EnemyAffixSet affixSet)
    {
      var playerHealth = chaseTarget != null ? chaseTarget.GetComponent<HealthComponent>() : null;
      if (playerHealth == null || playerHealth.IsDead) return;

      if (attackKind == EnemyAttackKind.Ranged)
        FireProjectile(chaseTarget);
      else
      {
        var result = DamagePipeline.Apply(BuildAttackRequest(), playerHealth);
        if (result.FinalDamage > 0f)
          ApplyOnHitBuffs(playerHealth);
      }

      FinishAttackCooldown();
      _core?.GetComponent<EnemyMovement>()?.ResetVelocity();
    }

    IEnumerator RangedAttackRoutine(Transform chaseTarget, EnemyAffixSet affixSet)
    {
      _inSpecialAttack = true;
      var movement = _core != null ? _core.Movement : null;
      movement?.ResetVelocity();
      _core?.MotionVisual?.PlayRangedAttackPulse(attackWindup);

      var affixes = affixSet?.GetValidAttackAffixes(EnemyDeliveryType.Projectile);
      bool hasAffixes = affixes != null && affixes.Count > 0;

      float elapsed = 0f;
      while (elapsed < attackWindup) { elapsed += Time.deltaTime; yield return null; }

      if (!hasAffixes || chaseTarget == null)
      {
        ExecuteRangedAttack(chaseTarget);
        FinishAttackCooldown();
        _inSpecialAttack = false;
        _core?.TryApplyPendingAffixSet();
        _attackCoroutine = null;
        yield break;
      }

      var aimDir = PlanarOffsetTo(chaseTarget.position);
      if (aimDir.sqrMagnitude < 0.0001f) aimDir = Vector2.right;
      var aimDirNorm = aimDir.normalized;

      var config = new RangedAffixHandler.FireConfig
      {
        FireCount = 1, FireInterval = attackCooldown,
        AimDirection = aimDirNorm, BaseCooldown = attackCooldown, BaseSpread = _spreadDeg
      };
      RangedAffixHandler.PrepareFire(affixes, ref config, null);

      ShotgunConfig shotgunCfg = default;
      bool hasShotgun = false;
      for (int a = 0; a < affixes.Count; a++)
        if (affixes[a].Type == AttackAffixType.Shotgun)
        {
          shotgunCfg = new ShotgunConfig { pelletCount = Mathf.Max(1, Mathf.RoundToInt(affixes[a].GetParam(1, 1))), spreadPerShot = affixes[a].GetParam(2, 0f) };
          hasShotgun = true; break;
        }

      for (int burst = 0; burst < config.FireCount; burst++)
      {
        if (chaseTarget == null || (_core?.Health != null && _core.Health.IsDead)) break;
        if (burst > 0 && chaseTarget != null)
        {
          var liveDir = PlanarOffsetTo(chaseTarget.position);
          if (liveDir.sqrMagnitude > 0.0001f) config.AimDirection = liveDir.normalized;
        }
        if (hasShotgun) FireShotgun(config.AimDirection, shotgunCfg.pelletCount, shotgunCfg.spreadPerShot);
        else FireProjectileAtAim(config.AimDirection);
        if (burst < config.FireCount - 1) yield return new WaitForSeconds(config.FireInterval);
      }

      FinishAttackCooldown();
      _inSpecialAttack = false;
      _core?.TryApplyPendingAffixSet();
      _attackCoroutine = null;
    }

    IEnumerator LaserAttackRoutine(Transform chaseTarget, EnemyAffixSet affixSet)
    {
      _inSpecialAttack = true;
      if (_core != null) _core.Movement?.ResetVelocity();
      if (chaseTarget == null) { _inSpecialAttack = false; _core?.TryApplyPendingAffixSet(); _attackCoroutine = null; yield break; }

      var affixes = affixSet?.GetValidAttackAffixes(EnemyDeliveryType.Beam);
      bool hasAffixes = affixes != null && affixes.Count > 0;
      var lockedFireDir = PlanarOffsetTo(chaseTarget.position);
      lockedFireDir = lockedFireDir.sqrMagnitude > 0.0001f ? lockedFireDir.normalized : Vector2.right;
      FacePlanarDirection(lockedFireDir);
      _core?.MotionVisual?.BeginLaserCharge(attackWindup, lockedFireDir);

      // ── 蓄力瞄准线（红色细线，无伤害）──
      CreateLaserAimLine(lockedFireDir);

      float elapsed = 0f;
      while (elapsed < attackWindup)
      {
        elapsed += Time.deltaTime;
        // 更新瞄准线末端（方向可能因 affix 变化）
        if (hasAffixes && chaseTarget != null)
        {
          var toTarget = PlanarOffsetTo(chaseTarget.position);
          lockedFireDir = SpecialAffixHandler.UpdateWindupDirection(affixes, lockedFireDir, toTarget, Time.deltaTime, null);
          FacePlanarDirection(lockedFireDir);
        }
        UpdateLaserAimLine(lockedFireDir);
        yield return null;
      }

      ClearLaserAimLine();
      _core?.MotionVisual?.EndLaserCharge(lockedFireDir);

      var settings = LaserBeamSettings.FromProfile(laserColor, GetEffectiveAttackRange(), _beamHalfWidth, _beamDuration, _beamTickInterval);
      _activeLaser = LaserBeamPool.Acquire();
      _activeLaser.Begin(transform, chaseTarget, settings, BuildAttackRequest(), ApplyOnHitBuffs, lockedFireDirection: lockedFireDir);

      while (_activeLaser != null && _activeLaser.IsRunning) yield return null;
      _activeLaser = null;

      FinishAttackCooldown();
      _inSpecialAttack = false;
      _core?.TryApplyPendingAffixSet();
      _attackCoroutine = null;
    }

    IEnumerator ChargeAttackRoutine(Transform chaseTarget, EnemyAffixSet affixSet)
    {
      _inSpecialAttack = true;
      if (_core != null) _core.Movement?.ResetVelocity();
      _chargeDashPhase = false;

      if (chaseTarget == null) { _inSpecialAttack = false; _core?.TryApplyPendingAffixSet(); _attackCoroutine = null; yield break; }

      var affixes = affixSet?.GetValidAttackAffixes(EnemyDeliveryType.ChargeDash);
      bool hasAffixes = affixes != null && affixes.Count > 0;
      var toTarget = PlanarOffsetTo(chaseTarget.position);
      var dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
      if (hasAffixes) dir = SpecialAffixHandler.UpdateWindupDirection(affixes, dir, toTarget, Time.deltaTime, null);
      _chargeDashDir = dir;
      FacePlanarDirection(dir);
      _core?.MotionVisual?.UseChargeSpin();

      int chargesUsed = 0;
      bool lastChargeHit = false;

      while (true)
      {
        chargesUsed++;
        var chargeParams = new ChargeEnemyAttack.Params
        {
          Owner = transform, Target = chaseTarget, Windup = attackWindup,
          DashSpeedMult = _dashSpeedMult, DashSpeedScaling = _dashSpeedScaling,
          DashDistance = _dashDistance, AttackRange = attackRange, VisualRadius = ResolveChargeVisualRadius(),
          Request = BuildAttackRequest(), OnHit = ApplyOnHitBuffs, Layer = gameObject.layer,
          OnDashStep = (u, pos, start, end, arenaDash) =>
          {
            if (!_chargeDashPhase) return;
            if (!_chargeDashPrevPosValid) { _chargeDashPrevPos = start; _chargeDashPrevPosValid = true; }
            TryChargeDashCollisions(_chargeDashPrevPos, pos);
            _chargeDashPrevPos = pos;
          }
        };

        yield return ChargeEnemyAttack.Execute(chargeParams,
          ind => _activeChargeWarning = ind,
          () =>
          {
            _chargeDashStartPos = GameplayPlane.Position2D(transform);
            _chargeDashStartValid = true;
            _chargeDashPrevPos = _chargeDashStartPos;
            _chargeDashPrevPosValid = true;
            _chargeDashPhase = true;
            if (chaseTarget != null)
            {
              var liveDir = PlanarOffsetTo(chaseTarget.position);
              if (liveDir.sqrMagnitude > 0.0001f) _chargeDashDir = liveDir.normalized;
            }
            if (_core != null && _core.EnableChargeParticle && _chargeTrail != null)
              _chargeTrail.Begin(_chargeDashDir.sqrMagnitude > 0.0001f ? _chargeDashDir : Vector2.right, ResolveChargeVisualRadius());
          });

        _chargeDashPhase = false;
        _chargeDashPrevPosValid = false;
        _chargeTrail?.End();

        if (hasAffixes && chaseTarget != null && (_core?.Health == null || !_core.Health.IsDead))
        {
          if (SpecialAffixHandler.ShouldMultiCharge(affixes, chargesUsed, lastChargeHit, out float intervalMult))
          {
            yield return new WaitForSeconds(attackCooldown * intervalMult * 0.3f);
            toTarget = PlanarOffsetTo(chaseTarget.position);
            dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
            dir = SpecialAffixHandler.UpdateWindupDirection(affixes, dir, toTarget, Time.deltaTime, null);
            _chargeDashDir = dir;
            FacePlanarDirection(dir);
            _chargeDashStartPos = Vector2.zero;
            _chargeDashStartValid = false;
            continue;
          }
        }
        break;
      }

      _core?.MotionVisual?.ResetSpinMultiplier();
      FinishChargeDashCooldown();
      if (_core != null && _core.IsFastWispStyle)
        _core.PauseMovement(0.22f);
      _inSpecialAttack = false;
      _core?.TryApplyPendingAffixSet();
      if (_core != null) _core.Movement?.ResetVelocity();
      _attackCoroutine = null;
    }

    // ── 辅助攻击方法 ──────────────────────────────

    void ExecuteRangedAttack(Transform chaseTarget)
    {
      if (chaseTarget == null) return;
      FireProjectile(chaseTarget);
    }

    void FireProjectileAtAim(Vector2 aimDirection)
    {
      var request = BuildAttackRequest();
      var profile = AttackProfileDatabase.Get(attackProfileId);
      var origin = SpawnPosition + new Vector3(aimDirection.x, aimDirection.y, 0f) * 0.45f;
      EnemyTriangleProjectile.SpawnDirectional(origin, new Vector3(aimDirection.x, aimDirection.y, 0f), request,
        projectileSpeed, projectileScale, projectileColor, GetEnemyProjectileRange(), profile?.hit_radius ?? 0f);
    }

    void FireShotgun(Vector2 aimDirection, int pelletCount, float spreadDeg)
    {
      if (pelletCount <= 0) return;
      var request = BuildAttackRequest();
      var profile = AttackProfileDatabase.Get(attackProfileId);
      var origin = SpawnPosition + new Vector3(aimDirection.x, aimDirection.y, 0f) * 0.45f;
      float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
      for (int i = 0; i < pelletCount; i++)
      {
        float t = pelletCount <= 1 ? 0f : (i / (float)(pelletCount - 1) - 0.5f);
        float rad = (baseAngle + t * spreadDeg) * Mathf.Deg2Rad;
        EnemyTriangleProjectile.SpawnDirectional(origin, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f), request,
          projectileSpeed, projectileScale, projectileColor, GetEnemyProjectileRange(), profile?.hit_radius ?? 0f);
      }
    }

    void FireProjectile(Transform target)
    {
      var request = BuildAttackRequest();
      var profile = AttackProfileDatabase.Get(attackProfileId);
      var origin = SpawnPosition;
      int count = Mathf.Max(1, _projectileCount);
      var toTarget = target.position - origin; toTarget.z = 0f;
      var baseDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;
      var spawnOrigin = origin + baseDir * 0.45f;
      if (count <= 1) { SpawnSingle(spawnOrigin, target, request, profile?.hit_radius ?? 0f, 0f); return; }
      float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
      for (int i = 0; i < count; i++)
      {
        float t = (i / (float)(count - 1) - 0.5f);
        SpawnSingle(spawnOrigin, target, request, profile?.hit_radius ?? 0f, t * _spreadDeg);
      }
    }

    void SpawnSingle(Vector3 origin, Transform target, DamageRequest request, float hitRadius, float angleOffsetDeg)
    {
      if (Mathf.Abs(angleOffsetDeg) > 0.01f && target != null)
      {
        var toTgt = target.position - origin; toTgt.z = 0f;
        float baseAngle = Mathf.Atan2(toTgt.y, toTgt.x) * Mathf.Rad2Deg + angleOffsetDeg;
        var rad = baseAngle * Mathf.Deg2Rad;
        EnemyTriangleProjectile.SpawnDirectional(origin, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f), request,
          projectileSpeed, projectileScale, projectileColor, GetEnemyProjectileRange(), hitRadius);
        return;
      }
      switch (projectileHomingMode)
      {
        case "none":
        default: EnemyTriangleProjectile.Spawn(origin, target, request, projectileSpeed, projectileScale, projectileColor, hitRadius); break;
        case "lock_loss": ProjectileFactory.SpawnLockLossHoming(origin, target, request, projectileSpeed, projectileTurnRateDeg, projectileLockLossAngle, projectileScale, projectileColor, "EnemyLockLossProjectile", hitRadius: hitRadius); break;
        case "weak": ProjectileFactory.SpawnWeakHoming(origin, target, request, projectileSpeed, projectileTurnRateDeg, projectileScale, projectileColor, "EnemyProjectile", hitRadius: hitRadius); break;
      }
    }

    float GetEnemyProjectileRange() =>
      (_deliveryType == EnemyDeliveryType.Projectile || attackKind == EnemyAttackKind.Ranged) ? EnemyProjectileFullRange : rangedAttackRange;

    void FinishAttackCooldown()
    {
      var bc = GetComponent<BuffContainer>();
      float atkSpdMult = Mathf.Max(0.05f, bc != null ? bc.GetStatModifier("attack_speed") : 1f);
      _attackTimer = Mathf.Max(0.05f, attackCooldown / atkSpdMult);
    }

    void FinishChargeDashCooldown()
    {
      var bc = GetComponent<BuffContainer>();
      float atkSpdMult = Mathf.Max(0.05f, bc != null ? bc.GetStatModifier("attack_speed") : 1f);
      float cd = Mathf.Max(0.05f, attackCooldown / atkSpdMult);
      if (bc != null && bc.HasSlowEffect()) cd *= 1.75f;
      _attackTimer = cd;
    }

    DamageRequest BuildAttackRequest()
    {
      var req = DamageRequest.Direct(attackDamage, attackDamageType, attackDamageSource, gameObject);
      if (!string.IsNullOrEmpty(attackProfileId)) req.AttackProfileId = attackProfileId;
      return req;
    }

    void ApplyOnHitBuffs(HealthComponent target)
    {
      var meta = GetComponent<EnemySpawnMetadata>();
      if (meta?.onHitBuffs == null || meta.onHitBuffs.Length == 0 || target == null) return;
      var bc = target.GetComponent<BuffContainer>();
      if (bc == null) return;
      var ctx = new BuffContainer.BuffApplyContext { sourceEntity = gameObject, sourceKind = attackDamageSource, abilityId = attackProfileId, stacks = 1 };
      foreach (var id in meta.onHitBuffs) if (!string.IsNullOrEmpty(id)) bc.ApplyBuff(id, ctx);
    }

    // ── 视觉辅助 ──────────────────────────────────

    IEnumerator WindupVisual()
    {
      var visual = transform.Find("Visual");
      if (visual == null) yield break;
      var orig = visual.localScale;
      var targetScale = orig * 1.15f;
      float elapsed = 0f;
      while (elapsed < attackWindup) { elapsed += Time.deltaTime; visual.localScale = Vector3.Lerp(orig, targetScale, Mathf.Sin(elapsed / attackWindup * Mathf.PI)); yield return null; }
      visual.localScale = orig;
    }

    void FacePlanarDirection(Vector2 dir)
    {
      transform.rotation = Quaternion.LookRotation(Vector3.forward, new Vector3(dir.x, dir.y, 0f));
    }

    float ResolveChargeVisualRadius()
    {
      var visual = transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual == null) return 0.55f;
      var bounds = visual.localScale;
      return Mathf.Max(0.35f, Mathf.Max(bounds.x, bounds.y) * 0.42f);
    }

    Vector2 PlanarOffsetTo(Vector3 worldTarget)
    {
      return GameplayPlane.ToPlanar(worldTarget) - GameplayPlane.Position2D(transform);
    }

    struct ShotgunConfig { public int pelletCount; public float spreadPerShot; }

    // ── 强制开火 ──────────────────────────────

    /// <summary>
    /// 强制开火：跳过冷却、射程、前摇等所有 AI 检查，直接触发攻击。
    /// 用于 Boss 技能系统中技能主动调用，而非等待 EnemyCore 的 AI 循环判定。
    /// target 为 null 时使用当前追逐目标。
    /// </summary>
    public void ForceFire(Transform target)
    {
      if (!isActiveAndEnabled) return;

      // 清理可能卡住的状态
      StopAllCoroutines();
      _attackCoroutine = null;
      ClearLaserAimLine();
      StopActiveLaser();
      _activeChargeWarning?.HideImmediate();
      _activeChargeWarning = null;
      _chargeDashPhase = false;

      // 重置为可攻击状态
      _attackTimer = 0f;
      _isWindingUp = false;
      _inSpecialAttack = false;

      var tgt = target != null ? target : (_core != null ? _core.ChaseTarget : null);
      if (tgt == null) return;

      StartAttack(tgt, null);
    }

    // ── 中断 / 取消 ──────────────────────────────

    public void InterruptAttack()
    {
      if (_isWindingUp)
      {
        _isWindingUp = false;
        StopAllCoroutines();
        _chargeDashPhase = false;
        _attackCoroutine = null;
        ClearLaserAimLine();
        StopActiveLaser();
        _activeChargeWarning?.HideImmediate();
        _activeChargeWarning = null;
        _core?.MotionVisual?.CancelLaserCharge();
        _core?.MotionVisual?.ResetSpinMultiplier();
      }
      // 无论是否在蓄力，都重置特殊攻击状态（修复 #4.1）
      _inSpecialAttack = false;
    }

    void CreateLaserAimLine(Vector2 direction)
    {
      ClearLaserAimLine();

      _activeLaserAimLine = new GameObject("LaserAimLine");
      var aimLr = _activeLaserAimLine.AddComponent<LineRenderer>();
      aimLr.startWidth = 0.04f;
      aimLr.endWidth = 0.04f;
      _activeLaserAimMaterial = new Material(Shader.Find("Sprites/Default"));
      aimLr.material = _activeLaserAimMaterial;
      aimLr.startColor = new Color(1f, 0.08f, 0.03f, 0.72f);
      aimLr.endColor = new Color(0.9f, 0.01f, 0.005f, 0.45f);
      aimLr.positionCount = 2;
      aimLr.sortingOrder = 5;
      UpdateLaserAimLine(direction);
    }

    void UpdateLaserAimLine(Vector2 direction)
    {
      if (_activeLaserAimLine == null)
        return;

      var aimLr = _activeLaserAimLine.GetComponent<LineRenderer>();
      if (aimLr == null)
        return;

      var start = SpawnPosition;
      var end = start + new Vector3(direction.x, direction.y, 0f) * GetEffectiveAttackRange();
      aimLr.SetPosition(0, start);
      aimLr.SetPosition(1, end);
    }

    void ClearLaserAimLine()
    {
      if (_activeLaserAimLine != null)
      {
        Destroy(_activeLaserAimLine);
        _activeLaserAimLine = null;
      }

      if (_activeLaserAimMaterial != null)
      {
        Destroy(_activeLaserAimMaterial);
        _activeLaserAimMaterial = null;
      }
    }

    void StopActiveLaser()
    {
      if (_activeLaser == null) return;
      _activeLaser.Stop();
      _activeLaser = null;
    }

    void OnDisable()
    {
      ClearLaserAimLine();
      StopActiveLaser();
      _activeChargeWarning?.HideImmediate();
      _activeChargeWarning = null;
      _chargeTrail?.Cancel();
      _core?.MotionVisual?.CancelLaserCharge();
    }

    void OnDestroy()
    {
      ClearLaserAimLine();
      StopActiveLaser();
      _activeChargeWarning?.HideImmediate();
      _activeChargeWarning = null;
      _chargeTrail?.Cancel();
    }

    public Vector2 GetChargeIncomingDirection()
    {
      if (_chargeDashDir.sqrMagnitude > 0.0001f) return _chargeDashDir.normalized;
      return Vector2.right;
    }

    // ── 反弹冲撞对接 ──────────────────────────────

    public void SetChargeDashDirection(Vector2 dir) => _chargeDashDir = dir;
    public bool ChargeDashStartValid => _chargeDashStartValid;
    public Vector2 ChargeDashStartPos => _chargeDashStartPos;
    public void ClearChargeDashState()
    {
      _chargeDashPhase = false;
      _chargeDashPrevPosValid = false;
      _inSpecialAttack = false;
      _attackTimer = attackCooldown * 0.55f;
      if (_core != null) _core.Movement?.ResetVelocity();
      StopAllCoroutines();
      _attackCoroutine = null;
      _activeChargeWarning?.HideImmediate();
      _activeChargeWarning = null;
      _chargeTrail?.End();
    }

    void TryChargeDashCollisions(Vector2 prevPos, Vector2 currPos)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return;
      foreach (var other in registry.AllEnemies)
      {
        if (other == null || other.gameObject == gameObject) continue;
        if (!other.IsReflectReturnActive) continue;
        if (!SegmentIntersectsCircle(prevPos, currPos, GameplayPlane.Position2D(other.transform), ResolveChargeVisualRadius() + 0.14f)) continue;
        if (_core != null) _core.NotifyReflectContact(other);
        break;
      }
    }

    static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, Vector2 center, float radius)
    {
      var ab = b - a;
      var lenSq = ab.sqrMagnitude;
      if (lenSq < 0.0001f) return (center - a).sqrMagnitude <= radius * radius;
      float t = Mathf.Clamp01(Vector2.Dot(center - a, ab) / lenSq);
      return (center - (a + ab * t)).sqrMagnitude <= radius * radius;
    }
  }
}
