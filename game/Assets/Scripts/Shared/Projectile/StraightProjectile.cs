using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = Game.Shared.Combat.Health.Health;
using Game.Shared.Enemy.Collision;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Vfx;
using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Projectile
{
  /// <summary>
  /// 直线弹：发射瞬间锁定方向，不追踪；仍索敌选定目标，目标闪开可躲?
  /// 带帧跳过保护，高速弹也不会穿透?
  /// v2: 支持构筑效果（穿?爆炸/连锁/减?灼烧等）?
  /// v3: 远程无限射程 + 离屏销?+ 弱追踪（有限角速度/索敌半径）?
  /// v4: 尾迹散弹 / 命中分裂 / 侧向抛射等远程构筑弹种?
  /// </summary>
  [DisallowMultipleComponent]
  public class StraightProjectile : MonoBehaviour
  {
    const float DefaultHitRadius = 0.5f;
    const float OffScreenMargin = 1.5f;
    const float WeakHomingTurnRateDeg = 92f;
    const float WeakHomingSearchRadius = 11f;
    const float TrailSprayInterval = 0.14f;
    const float SubProjectileDamageRatio = 0.38f;
    const float SubProjectileSpeedMult = 0.92f;

    Vector3 _direction;
    Transform _target;
    DamageRequest _request;
    float _speed;
    float _lifetime;
    float _hitRadius;
    bool _directionalMode;
    bool _playerOnly;
    float _maxTravel;
    float _traveled;
    Vector3 _start;
    bool _offscreenCullingEnabled = true;

    Player.PlayerAttackDirector.ProjectileBuildModifiers _buildMods;
    int _remainingPierce;
    int _chainHitIndex;
    int _projectileDepth;
    float _trailTimer;
    bool _homingLogged;
    HashSet<Health> _hitTargets = new();
    bool _usesPool;
    bool _poolHasRangedVisual;
    bool _poolIsAuxiliary;
    bool _despawned;
    Vector3 _lastMoveDelta;

    static Transform s_cachedPlayer;
    static Camera s_cachedCamera;

    public float FlightSpeed => _speed;

    public Vector3 FlightDirection => _direction;

    internal void ConfigurePooling(bool rangedVisual, bool auxiliary = false)
    {
      _usesPool = true;
      _poolHasRangedVisual = rangedVisual;
      _poolIsAuxiliary = auxiliary;
    }

    internal void PrepareForLaunch()
    {
      _despawned = false;
      _target = null;
      _buildMods = default;
      _remainingPierce = 0;
      _chainHitIndex = 0;
      _projectileDepth = 0;
      _trailTimer = 0f;
      _homingLogged = false;
      _offscreenCullingEnabled = true;
      _hitTargets.Clear();
    }

    void Despawn()
    {
      if (_despawned)
        return;

      _despawned = true;
      ActiveProjectileRegistry.Unregister(this);
      if (_usesPool)
        ProjectileFactory.Release(this, _poolHasRangedVisual, _poolIsAuxiliary);
      else
        Destroy(gameObject);
    }

    internal void ForceDespawnForRunReset()
    {
      if (_despawned || !isActiveAndEnabled)
        return;

      Despawn();
    }

    public void ApplyGravityPull(Vector3 center, float strength, float deltaTime)
    {
      var toCenter = center - transform.position;
      toCenter.z = 0f;
      if (toCenter.sqrMagnitude < 0.0001f)
        return;
      _direction = Vector3.RotateTowards(
        _direction,
        toCenter.normalized,
        Mathf.Max(0f, strength) * deltaTime,
        0f).normalized;
    }

    public void SetOffscreenCulling(bool enabled)
    {
      _offscreenCullingEnabled = enabled;
    }

    public void SetBuildModifiers(Player.PlayerAttackDirector.ProjectileBuildModifiers mods)
    {
      _buildMods = mods;
      _projectileDepth = mods.projectileDepth;
      _remainingPierce = mods.pierceCount;
      _chainHitIndex = 0;
      _trailTimer = TrailSprayInterval * 0.35f;
      if (mods.ignoreHitTargetId != 0)
        RegisterIgnoredTarget(mods.ignoreHitTargetId);
    }

    void RegisterIgnoredTarget(int instanceId)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health != null && health.GetInstanceID() == instanceId)
        {
          _hitTargets.Add(health);
          return;
        }
      }
    }

    public void Launch(Vector3 origin, Transform target, in DamageRequest request, float speed,
      float lifetime = 4f, float hitRadius = 0f, bool playerOnly = false)
    {
      _directionalMode = false;
      _playerOnly = playerOnly;
      _target = target;
      _request = request;
      _speed = speed;
      _lifetime = lifetime;
      _hitRadius = hitRadius > 0f ? hitRadius
        : (request.HitRadius > 0f ? request.HitRadius : DefaultHitRadius);

      transform.position = origin;

      if (target != null)
      {
        var toTarget = target.position - origin;
        toTarget.z = 0f;
        _direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;
      }
      else
      {
        _direction = Vector3.right;
      }

      ActiveProjectileRegistry.Register(this, _request);
    }

    public void LaunchDirectional(Vector3 origin, Vector3 direction, in DamageRequest request, float speed,
      float maxRange, float lifetime = 4f, float hitRadius = 0f, bool playerOnly = false)
    {
      _directionalMode = true;
      _playerOnly = playerOnly;
      _target = null;
      _request = request;
      _speed = speed;
      _lifetime = lifetime;
      _maxTravel = Mathf.Max(0.5f, maxRange);
      _traveled = 0f;
      _start = origin;
      _hitRadius = hitRadius > 0f ? hitRadius
        : (request.HitRadius > 0f ? request.HitRadius : DefaultHitRadius);

      transform.position = origin;
      direction.z = 0f;
      _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
      ActiveProjectileRegistry.Register(this, _request);
    }

    public bool IsMonsterProjectile =>
      string.Equals(_request.DamageSourceId, "monster", System.StringComparison.OrdinalIgnoreCase);

    public void DeflectAwayFrom(Vector2 center)
    {
      var pos = GameplayPlane.Position2D(transform);
      var outward = pos - center;
      if (outward.sqrMagnitude < 0.0001f)
        outward = Vector2.right;
      _direction = new Vector3(outward.x, outward.y, 0f).normalized;
    }

    void Update()
    {
      _lifetime -= Time.deltaTime;
      if (_lifetime <= 0f)
      {
        Despawn();
        return;
      }

      if (_directionalMode)
      {
        UpdateDirectional();
        return;
      }

      if (_target == null)
      {
        transform.position += _direction * (_speed * Time.deltaTime);
        return;
      }

      var targetHealth = _target.GetComponent<Health>();
      if (targetHealth == null || targetHealth.IsDead)
      {
        Despawn();
        return;
      }

      var prevPos = transform.position;
      var moveDelta = _direction * (_speed * Time.deltaTime);
      var currPos = prevPos + moveDelta;

      var toTarget = _target.position - prevPos;
      toTarget.z = 0f;

      if (EnemyHitbox.IntersectsSegment(targetHealth.gameObject, prevPos, currPos, _hitRadius))
      {
        ApplyDamage(targetHealth);
        Despawn();
        return;
      }

      var moveLen = moveDelta.magnitude;
      if (moveLen < 0.0001f)
      {
        transform.position = currPos;
        return;
      }

      var moveDir = moveDelta / moveLen;
      var projT = Vector3.Dot(toTarget, moveDir);
      projT = Mathf.Clamp(projT, 0f, moveLen + _hitRadius);

      var closestPoint = prevPos + moveDir * Mathf.Min(projT, moveLen);
      closestPoint.z = 0f;

      var targetPos = _target.position;
      targetPos.z = 0f;

      if (EnemyHitbox.IntersectsSegment(targetHealth.gameObject, prevPos, currPos, _hitRadius))
      {
        ApplyDamage(targetHealth);
        Despawn();
        return;
      }

      transform.position = currPos;
    }

    const float DirectionalHitStepSeconds = 1f / 120f;

    bool TryHitPlayerAlongSegment(Vector3 from, Vector3 to)
    {
      var playerHealth = ResolvePlayerHealth();
      if (playerHealth == null)
        return false;

      var targetPos = playerHealth.transform.position;
      targetPos.z = 0f;
      var delta = to - from;
      delta.z = 0f;
      var dist = delta.magnitude;
      if (dist <= 0.0001f)
        return SegmentHitsPoint(from, to, targetPos, _hitRadius);

      var maxStep = Mathf.Max(0.01f, _speed * DirectionalHitStepSeconds);
      var dir = delta / dist;
      var traveled = 0f;
      var stepFrom = from;
      while (traveled < dist)
      {
        var stepLen = Mathf.Min(maxStep, dist - traveled);
        var stepTo = stepFrom + dir * stepLen;
        if (SegmentHitsPoint(stepFrom, stepTo, targetPos, _hitRadius))
        {
          ApplyDamage(playerHealth);
          return true;
        }

        traveled += stepLen;
        stepFrom = stepTo;
      }

      return false;
    }

    void UpdateDirectional()
    {
      // v3: 离屏检??超出屏幕边界一定余量后直接销毀"
      if (_offscreenCullingEnabled && IsOffScreen(transform.position))
      {
        Despawn();
        return;
      }

      // 弱追踪：有限角速度 + 有限索敌半径，可被走位躲开
      if (_buildMods.weakHoming)
        SteerWeakHomingTowardNearestEnemy();

      var prevPos = transform.position;
      var moveDelta = _direction * (_speed * Time.deltaTime);
      _lastMoveDelta = moveDelta;
      var currPos = prevPos + moveDelta;
      _traveled += moveDelta.magnitude;

      if (_buildMods.trailSprayCount > 0 && _projectileDepth < 1)
        UpdateTrailSpray();

      // 仅当 maxTravel 为有限值时检查射程（怪物/技能弹保留射程限制?
      if (_maxTravel < 9998f && _traveled >= _maxTravel)
      {
        Despawn();
        return;
      }

      // 怪物投射物只命中玩家，防止互相误伤"
      bool isMonsterSource = _request.DamageSourceId == "monster";

      if (_playerOnly || isMonsterSource)
      {
        if (TryHitPlayerAlongSegment(prevPos, currPos))
        {
          if (!ShouldContinueAfterHit())
          {
            Despawn();
            return;
          }
        }
      }
      else
      {
        var registry = CombatRoot.EnemyRegistry;
        if (registry != null)
        {
          foreach (var enemy in registry.AllEnemies)
          {
            if (enemy == null)
              continue;

            if (_request.Attacker != null && enemy.gameObject == _request.Attacker)
              continue;

            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead)
              continue;

            // 穿透弹不重复命中同一目标
            if (_hitTargets.Contains(health))
              continue;

            if (EnemyHitbox.IntersectsSegment(enemy.gameObject, prevPos, currPos, _hitRadius))
            {
              ApplyDamageWithBuildMods(health);
              _hitTargets.Add(health);
              if (!ShouldContinueAfterHit())
              {
                Despawn();
                return;
              }
            }
          }
        }
      }

      transform.position = currPos;
    }

    bool ShouldContinueAfterHit()
    {
      if (_remainingPierce > 0)
      {
        _remainingPierce--;
        _chainHitIndex++;
        if (_buildMods.pierceTrailFeedback && _lastMoveDelta.sqrMagnitude > 0.0001f)
          RangedPierceTrailVfx.Spawn(transform.position, _lastMoveDelta);
        GetComponent<RangedProjectileVfx>()?.PulsePierce();
        CombatDebugHookLocator.Range("pierce", "Pierce");
        return true;
      }
      return false;
    }

    static Health ResolvePlayerHealth()
    {
      if (s_cachedPlayer == null)
      {
        var playerGo = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (playerGo != null)
          s_cachedPlayer = playerGo.transform;
      }

      if (s_cachedPlayer == null)
        return null;

      var health = s_cachedPlayer.GetComponent<Health>();
      return health != null && !health.IsDead ? health : null;
    }

    static bool SegmentHitsPoint(Vector3 from, Vector3 to, Vector3 point, float radius)
    {
      var seg = to - from;
      seg.z = 0f;
      var len = seg.magnitude;
      if (len < 0.0001f)
        return (point - from).sqrMagnitude <= radius * radius;

      var dir = seg / len;
      var toPoint = point - from;
      toPoint.z = 0f;
      var t = Mathf.Clamp(Vector3.Dot(toPoint, dir), 0f, len);
      var closest = from + dir * t;
      closest.z = 0f;
      point.z = 0f;
      return (point - closest).sqrMagnitude <= radius * radius;
    }

    void ApplyDamage(Health health)
    {
      DamagePipeline.Apply(_request, health);
      SpawnRangedHitVfx(health);
    }

    void SpawnRangedHitVfx(Health health, int chainIndex = 0)
    {
      if (_request.DamageSourceId != "weapon" || health == null)
        return;

      var scale = Mathf.Max(0.65f, transform.localScale.x * (1.8f + chainIndex * 0.22f));
      RangedProjectileHitVfx.Spawn(health.transform.position, scale);
    }

    // ---- v3: 离屏 & 追踪 ----

    static Camera GetCamera()
    {
      if (s_cachedCamera == null)
        s_cachedCamera = Camera.main;
      return s_cachedCamera;
    }

    static bool IsOffScreen(Vector3 worldPos)
    {
      var cam = GetCamera();
      if (cam == null)
        return false; // 无摄像机时跳过检浀"

      var vp = cam.WorldToViewportPoint(worldPos);
      return vp.x < -OffScreenMargin || vp.x > 1f + OffScreenMargin
          || vp.y < -OffScreenMargin || vp.y > 1f + OffScreenMargin;
    }

    void SteerWeakHomingTowardNearestEnemy()
    {
      var enemyReg = CombatRoot.EnemyRegistry;
      if (enemyReg == null)
        return;

      Transform best = null;
      var bestScore = float.MaxValue;

      foreach (var enemy in enemyReg.AllEnemies)
      {
        if (enemy == null) continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead) continue;

        var toEnemy = enemy.transform.position - transform.position;
        toEnemy.z = 0f;
        var dist = toEnemy.magnitude;
        if (dist > WeakHomingSearchRadius) continue;

        var dir = toEnemy / Mathf.Max(0.001f, dist);
        var angle = Vector3.Angle(_direction, dir);
        var score = angle + dist * 4f;
        if (score < bestScore)
        {
          bestScore = score;
          best = enemy.transform;
        }
      }

      if (best == null)
        return;

      var toTarget = best.position - transform.position;
      toTarget.z = 0f;
      if (toTarget.sqrMagnitude < 0.0001f)
        return;

      var desiredDir = toTarget.normalized;
      var turnRate = _buildMods.homingTurnRate > 0f ? _buildMods.homingTurnRate : WeakHomingTurnRateDeg;
      var maxRadians = turnRate * Mathf.Deg2Rad * Time.deltaTime;
      _direction = Vector3.RotateTowards(_direction, desiredDir, maxRadians, 0f).normalized;
      if (!_homingLogged)
      {
        _homingLogged = true;
        CombatDebugHookLocator.Range("homing", "Homing");
      }
    }

    void UpdateTrailSpray()
    {
      _trailTimer -= Time.deltaTime;
      if (_trailTimer > 0f)
        return;

      _trailTimer = TrailSprayInterval;
      var count = Mathf.Max(1, _buildMods.trailSprayCount);
      for (var i = 0; i < count; i++)
      {
        Vector3 dir;
        if (_buildMods.sideShed)
        {
          var sign = i % 2 == 0 ? 1f : -1f;
          dir = new Vector3(-_direction.y * sign, _direction.x * sign, 0f).normalized;
        }
        else
        {
          var jitter = Random.Range(-75f, 75f);
          dir = (Quaternion.Euler(0f, 0f, jitter) * _direction).normalized;
        }

        SpawnSubProjectile(transform.position, dir);
      }
    }

    void SpawnSplitBullets(Health primaryTarget)
    {
      var splitCount = Mathf.Max(1, _buildMods.splitOnHitCount);
      var total = splitCount + 1;
      var origin = primaryTarget.transform.position;
      var baseAngle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
      var ignoreId = primaryTarget.GetInstanceID();
      var push = Mathf.Max(0.45f, _hitRadius * 1.35f);

      for (var i = 0; i < total; i++)
      {
        var t = i / (float)(total - 1) - 0.5f;
        var angle = baseAngle + t * 48f;
        var rad = angle * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        SpawnSubProjectile(origin + dir * push, dir, 0.55f, 0.62f, ignoreId);
      }
    }

    void SpawnSubProjectile(
      Vector3 origin,
      Vector3 direction,
      float damageScale = SubProjectileDamageRatio,
      float scaleMult = 0.55f,
      int ignoreHitTargetId = 0)
    {
      if (_projectileDepth >= 1 || direction.sqrMagnitude < 0.0001f)
        return;

      var subMods = _buildMods;
      subMods.projectileDepth = _projectileDepth + 1;
      subMods.trailSprayCount = 0;
      subMods.splitOnHitCount = 0;
      subMods.weakHoming = false;
      subMods.heavyShot = false;
      subMods.sideShed = false;
      subMods.pierceCount = 0;
      subMods.explosionRadius = 0f;
      subMods.chainCount = 0;
      subMods.explosionVacuum = false;
      subMods.ignoreHitTargetId = ignoreHitTargetId;

      var req = _request;
      req.Base *= damageScale;

      var renderer = GetComponent<Renderer>();
      var color = renderer != null && renderer.sharedMaterial != null
        ? renderer.sharedMaterial.color
        : new Color(1f, 0.92f, 0.35f, 1f);

      var scale = transform.localScale.x * scaleMult;
      ProjectileFactory.SpawnDirectional(
        origin,
        direction.normalized,
        req,
        _speed * SubProjectileSpeedMult,
        scale,
        color,
        _maxTravel > 9998f ? 9999f : _maxTravel * 0.75f,
        "RangedSubProjectile",
        hitRadius: _hitRadius * 0.85f,
        buildMods: subMods,
        visualKind: RangedProjectileVisualKind.Split);
    }

    void SteerTowardNearestEnemy()
    {
      SteerWeakHomingTowardNearestEnemy();
    }

    /// <summary>应用伤害并触发构筑效果（爆炸/连锁/减?灼烧等）?/summary>
    void ApplyDamageWithBuildMods(Health health)
    {
      // 调整伤害（精?Boss/远程加成?
      var finalDmg = _request.Base;
      var go = health.gameObject;
      bool isBoss = EnemySpawnMetadata.IsBossEnemy(go);
      bool isEliteOrBoss = go.tag == "Elite" || isBoss;
      if (isEliteOrBoss)
      {
        if (_buildMods.eliteDamageMult > 0f)
          finalDmg *= (1f + _buildMods.eliteDamageMult);
        if (_buildMods.bossDamageMult > 0f && isBoss)
          finalDmg *= (1f + _buildMods.bossDamageMult);
      }

      // 对减速目标加戀"
      if (_buildMods.slowTargetDamageMult > 0f)
      {
        var buff = health.GetComponent<BuffContainer>();
        if (buff != null && buff.HasSlowEffect())
          finalDmg *= (1f + _buildMods.slowTargetDamageMult);
      }

      // 远程距离加成
      if (_buildMods.longRangeDamageMult > 0f)
      {
        var dist = Vector3.Distance(transform.position, health.transform.position);
        if (dist > 5f)
          finalDmg *= (1f + _buildMods.longRangeDamageMult);
      }

      if (_chainHitIndex > 0 && !_buildMods.pierceNoFalloff)
        finalDmg *= Mathf.Pow(0.90f, _chainHitIndex);

      var req = _request;
      req.Base = finalDmg;
      DamagePipeline.Apply(req, health);
      SpawnRangedHitVfx(health, _chainHitIndex);

      if (_buildMods.splitOnHitCount > 0 && _projectileDepth < 1)
      {
        SpawnSplitBullets(health);
        CombatDebugHookLocator.Range("split", "Split");
      }

      var allowSecondary = _projectileDepth < 1;
      ProjectileHitEffects.ApplyOnHit(
        _request.Attacker,
        health,
        finalDmg,
        ProjectileHitEffects.FromBuildMods(_buildMods),
        allowSecondary);
    }
  }
}
