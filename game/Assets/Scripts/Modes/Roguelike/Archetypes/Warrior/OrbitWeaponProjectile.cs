using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Player;
using Game.Modes.Roguelike.Gameplay.Events;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  /// <summary>Drives an existing orbit weapon through launch and return phases.</summary>
  [DisallowMultipleComponent]
  public sealed class OrbitWeaponProjectile : MonoBehaviour
  {
    readonly HashSet<Health> _hitTargets = new();
    readonly Dictionary<Health, float> _nextTrailHitTimes = new();

    GameObject _owner;
    WarriorContext _ctx;
    Transform _target;
    Transform _orbitRoot;
    Func<Vector3> _returnPositionProvider;
    Action _onReturned;
    Vector3 _direction;
    float _remainingLife;
    float _maxLaunchDistance;
    int _remainingPierce;
    int _remainingBounces;
    bool _returning;
    bool _docking;
    bool _repathing;
    Vector3 _dockStart;
    float _dockElapsed;
    float _repathElapsed;
    float _lastEnemyDistance;
    float _lastOwnerDistance;
    float _trailTimer;
    float _trailDmgTimer;
    bool _completed;
    Queue<Vector3> _trailPoints;
    const float DockDuration = 0.18f;
    const float RepathDuration = 1.25f;

    public static OrbitWeaponProjectile Launch(
      Transform orbitWeapon,
      Transform target,
      GameObject owner,
      WarriorContext ctx,
      Transform orbitRoot,
      Func<Vector3> returnPositionProvider,
      Action onReturned)
    {
      if (orbitWeapon == null)
        return null;

      orbitWeapon.SetParent(null, true);
      var projectile = orbitWeapon.gameObject.AddComponent<OrbitWeaponProjectile>();
      projectile._owner = owner;
      projectile._ctx = ctx;
      projectile._target = target;
      projectile._orbitRoot = orbitRoot;
      projectile._returnPositionProvider = returnPositionProvider;
      projectile._onReturned = onReturned;
      projectile._remainingLife = 3f;
      projectile._maxLaunchDistance = Mathf.Max(6f, (ctx.Radius + ctx.RangeExpansion) * 3f);
      projectile._remainingPierce = ctx.SpiritBladePierce;
      projectile._remainingBounces = ctx.ProjectileBounce;
      projectile._direction = target != null
        ? PlanarDirection(target.position - orbitWeapon.position)
        : Vector3.right;

      if (ctx.SpiritBladeTrail)
        projectile._trailPoints = new Queue<Vector3>();

      return projectile;
    }

    void Update()
    {
      var movementStart = transform.position;
      if (_returning)
        UpdateReturn();
      else if (_repathing)
        UpdateRepath();
      else
        UpdateLaunch();

      if (!enabled)
        return;

      _remainingLife -= Time.deltaTime;
      if (_remainingLife <= 0f)
      {
        if (_returning)
          _remainingLife = 0.25f;
        else
          BeginReturn();
        return;
      }

      UpdateTrail();
      if (!_repathing && !_returning)
        TryHitEnemy(movementStart, transform.position);
    }

    void UpdateLaunch()
    {
      if (_owner == null)
      {
        CompleteReturn();
        return;
      }

      if (!IsValidTarget(_target))
        _target = FindNextTarget();

      if (_target == null)
      {
        BeginReturn();
        return;
      }

      var toTarget = _target.position - transform.position;
      if (toTarget.sqrMagnitude > 0.0001f)
        _direction = PlanarDirection(toTarget);

      transform.position += _direction * (GetLaunchSpeed() * Time.deltaTime);
      transform.Rotate(0f, 0f, _ctx.RotationSpeed * Time.deltaTime * 0.3f);

      var fromOwner = transform.position - _owner.transform.position;
      fromOwner.z = 0f;
      if (fromOwner.sqrMagnitude >= _maxLaunchDistance * _maxLaunchDistance)
        BeginReturn();
    }

    void UpdateRepath()
    {
      if (_owner == null)
      {
        CompleteReturn();
        return;
      }

      _repathElapsed += Time.deltaTime;
      var returnPoint = GetReturnPosition();
      var toReturn = returnPoint - transform.position;
      toReturn.z = 0f;
      if (toReturn.sqrMagnitude > 0.0001f)
        _direction = PlanarDirection(toReturn);

      var returnSpeed = GetReturnSpeed();
      transform.position = Vector3.MoveTowards(transform.position, returnPoint, returnSpeed * Time.deltaTime);
      transform.Rotate(0f, 0f, -_ctx.RotationSpeed * Time.deltaTime * 0.22f);

      var t = Mathf.Clamp01(_repathElapsed / RepathDuration);

      if (t < 1f)
        return;

      _repathing = false;
      var next = FindNextTarget();
      _lastOwnerDistance = DistanceToOwner();
      _lastEnemyDistance = next != null ? Vector3.Distance(transform.position, next.position) : float.PositiveInfinity;
      if (next != null && _lastEnemyDistance < _lastOwnerDistance)
      {
        _target = next;
        _direction = PlanarDirection(_target.position - transform.position);
        _remainingLife = Mathf.Max(_remainingLife, 1.5f);
        return;
      }

      BeginReturn();
    }

    void UpdateReturn()
    {
      if (_owner == null)
      {
        CompleteReturn();
        return;
      }

      if (_docking)
      {
        UpdateDockingReturn();
        return;
      }

      var targetPosition = GetReturnPosition();
      var toReturnPoint = targetPosition - transform.position;
      toReturnPoint.z = 0f;
      if (toReturnPoint.sqrMagnitude < 0.16f)
      {
        BeginDockingReturn();
        return;
      }

      _direction = PlanarDirection(toReturnPoint);
      var step = GetReturnSpeed() * Time.deltaTime;
      if (toReturnPoint.magnitude <= step)
      {
        transform.position = targetPosition;
        BeginDockingReturn();
        return;
      }

      transform.position += _direction * step;
      transform.Rotate(0f, 0f, -_ctx.RotationSpeed * Time.deltaTime * 0.3f);
    }

    void BeginDockingReturn()
    {
      if (_docking)
        return;

      _docking = true;
      _dockStart = transform.position;
      _dockElapsed = 0f;
      _remainingLife = Mathf.Max(_remainingLife, DockDuration + 0.08f);
    }

    void UpdateDockingReturn()
    {
      var targetPosition = GetReturnPosition();
      _dockElapsed += Time.deltaTime;
      var t = Mathf.Clamp01(_dockElapsed / DockDuration);
      t = t * t * (3f - 2f * t);
      transform.position = Vector3.Lerp(_dockStart, targetPosition, t);
      transform.Rotate(0f, 0f, -_ctx.RotationSpeed * Time.deltaTime * 0.2f);

      if (_dockElapsed >= DockDuration || (transform.position - targetPosition).sqrMagnitude < 0.0025f)
        CompleteReturn();
    }

    Vector3 GetReturnPosition()
    {
      if (_returnPositionProvider != null)
        return _returnPositionProvider.Invoke();
      if (_owner != null)
        return _owner.transform.position;
      return transform.position;
    }

    public void BeginReturn()
    {
      if (_returning)
        return;

      _returning = true;
      _docking = false;
      _repathing = false;
      _target = null;
      _remainingLife = 3f;
      _hitTargets.Clear();
    }

    void BeginRepath()
    {
      if (_owner == null)
      {
        BeginReturn();
        return;
      }

      _repathing = true;
      _returning = false;
      _docking = false;
      _target = null;
      _repathElapsed = 0f;
      _remainingLife = Mathf.Max(_remainingLife, RepathDuration + 0.8f);
    }

    void CompleteReturn()
    {
      if (_completed)
        return;

      _completed = true;
      enabled = false;

      var callback = _onReturned;
      _onReturned = null;
      if (callback != null)
        callback.Invoke();
      else if (_orbitRoot != null)
        transform.SetParent(_orbitRoot, true);
      Destroy(this);
    }

    void OnDestroy()
    {
      if (_completed)
        return;

      _completed = true;
      var callback = _onReturned;
      _onReturned = null;
      callback?.Invoke();
    }

    void TryHitEnemy(Vector3 from, Vector3 to)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var range = _ctx.EffectiveWeaponSize + _ctx.RangeExpansion + 0.2f;
      EnemyCore hitEnemy = null;
      var bestProgress = float.MaxValue;
      var from2 = new Vector2(from.x, from.y);
      var to2 = new Vector2(to.x, to.y);
      var segment = to2 - from2;
      var segmentLengthSq = segment.sqrMagnitude;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || _hitTargets.Contains(health))
          continue;

        var enemyPosition = GameplayPlane.Position2D(enemy.transform);
        var progress = segmentLengthSq > 0.000001f
          ? Mathf.Clamp01(Vector2.Dot(enemyPosition - from2, segment) / segmentLengthSq)
          : 0f;
        var closest = from2 + segment * progress;
        if ((enemyPosition - closest).sqrMagnitude > range * range || progress >= bestProgress)
          continue;

        bestProgress = progress;
        hitEnemy = enemy;
      }

      if (hitEnemy != null)
      {
        var health = hitEnemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          return;

        _hitTargets.Add(health);

        var damage = _returning ? _ctx.EffectiveDamage * 0.8f : _ctx.EffectiveDamage;
        DamagePipeline.Apply(
          DamageRequest.Direct(damage, "physical", "warrior_spiritblade", _owner),
          health);
        GameEventBus.Publish(new TriggerActivatedEvent(
          "WarriorOrbitHit",
          hitEnemy.transform.position,
          gameObject,
          _ctx.EffectiveWeaponSize,
          alternate: _ctx.TitanFlag || _ctx.MeleeKnockbackChance > 0f));
        ApplyKnockback(hitEnemy);

        if (_remainingPierce > 0)
        {
          _remainingPierce--;
          _target = FindNextTarget();
          if (_target != null)
            _direction = PlanarDirection(_target.position - transform.position);
          return;
        }

        if (_remainingBounces > 0)
          _remainingBounces--;

        BeginRepath();
      }
    }

    void ApplyKnockback(EnemyCore enemy)
    {
      if (_ctx.MeleeKnockbackChance <= 0f || UnityEngine.Random.value > _ctx.MeleeKnockbackChance)
        return;

      var movement = enemy.GetComponent<EnemyMovement>();
      if (movement == null)
        return;

      var enemyPosition = GameplayPlane.Position2D(enemy.transform);
      var impactPosition = GameplayPlane.Position2D(transform);
      var velocity = movement.GetKnockbackDirection(enemyPosition, impactPosition);
      movement.ApplyHitStun();
      movement.ApplyKnockback(velocity * _ctx.EffectiveKnockbackMultiplier);
    }

    Transform FindNextTarget()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;

      Transform nearest = null;
      var bestDistance = float.MaxValue;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || _hitTargets.Contains(health))
          continue;

        var distance = (enemy.transform.position - transform.position).sqrMagnitude;
        if (distance < bestDistance)
        {
          bestDistance = distance;
          nearest = enemy.transform;
        }
      }
      return nearest;
    }

    float DistanceToOwner()
    {
      if (_owner == null)
        return float.PositiveInfinity;
      return Vector3.Distance(transform.position, _owner.transform.position);
    }

    float GetLaunchSpeed()
    {
      return Mathf.Max(_ctx.SpiritBladeSpeed, GetOwnerSpeedFloor());
    }

    float GetReturnSpeed()
    {
      return Mathf.Max(_ctx.SpiritBladeReturnSpeed, GetOwnerSpeedFloor());
    }

    float GetOwnerSpeedFloor()
    {
      const float fallbackPlayerSpeed = 8f;
      var playerSpeed = fallbackPlayerSpeed;
      if (_owner != null && _owner.TryGetComponent<PlayerSphereController>(out var movement))
        playerSpeed = Mathf.Max(fallbackPlayerSpeed, movement.CurrentMoveSpeed);
      return playerSpeed * 2f;
    }

    static bool IsValidTarget(Transform target)
    {
      if (target == null)
        return false;
      var health = target.GetComponent<Health>();
      return health != null && !health.IsDead;
    }

    static Vector3 PlanarDirection(Vector3 direction)
    {
      direction.z = 0f;
      return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
    }

    void UpdateTrail()
    {
      if (_trailPoints == null)
        return;

      _trailTimer += Time.deltaTime;
      if (_trailTimer <= 0.08f)
        return;

      _trailTimer = 0f;
      _trailPoints.Enqueue(transform.position);
      if (_trailPoints.Count > 20)
        _trailPoints.Dequeue();

      var trailCd = Mathf.Max(0.05f, _ctx.SpiritBladeTrailCD > 0f ? _ctx.SpiritBladeTrailCD : 0.15f);
      _trailDmgTimer += 0.08f;
      if (_trailDmgTimer < trailCd) return;
      _trailDmgTimer = 0f;

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return;
      var trailRange = _ctx.EffectiveWeaponSize + 0.15f;
      var trailDmg = _ctx.EffectiveDamage * 0.35f;
      var nextAllowedAt = Time.time + trailCd;
      foreach (var enemy in registry.GetInRange(GameplayPlane.Position2D(transform), trailRange))
      {
        if (enemy == null) continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead) continue;
        if (_nextTrailHitTimes.TryGetValue(health, out var next) && Time.time < next) continue;
        _nextTrailHitTimes[health] = nextAllowedAt;
        DamagePipeline.Apply(
          DamageRequest.Direct(trailDmg, "physical", "warrior_spiritblade_trail", _owner),
          health);
      }
    }

    public static void Spawn(Vector3 position, Vector3 direction, GameObject owner, object unused) { }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
      var state = _returning ? (_docking ? "Dock" : "Return") : _repathing ? "Repath" : "Attack";
      Gizmos.color = _returning ? Color.cyan : _repathing ? Color.yellow : Color.red;
      Gizmos.DrawWireSphere(transform.position, 0.18f);

      if (_target != null)
      {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, _target.position);
        _lastEnemyDistance = Vector3.Distance(transform.position, _target.position);
      }

      if (_owner != null)
      {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _owner.transform.position);
        _lastOwnerDistance = Vector3.Distance(transform.position, _owner.transform.position);
      }

      Handles.Label(
        transform.position + Vector3.up * 0.45f,
        $"Spirit Blade\nState: {state}\nEnemy: {(_lastEnemyDistance < float.PositiveInfinity ? _lastEnemyDistance.ToString("F1") : "-")}\nPlayer: {(_lastOwnerDistance < float.PositiveInfinity ? _lastOwnerDistance.ToString("F1") : "-")}");
    }
#endif
  }
}
