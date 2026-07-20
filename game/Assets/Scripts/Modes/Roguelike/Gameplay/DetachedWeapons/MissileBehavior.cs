using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  sealed class MissileBehavior : IDetachedWeaponBehavior
  {
    const int PoolSize = 28;
    readonly List<DetachedMissileProjectile> _projectiles = new();
    DetachedWeaponRuntimeContext _context;
    Vector2 _wanderTarget;
    Vector2 _wanderVelocity;
    Vector2 _lastOwnerPos;
    bool _hasLastOwnerPos;
    float _cooldown;
    float _salvoTimer;
    int _salvoRemaining;
    int _tier;
    bool _deferWanderTarget;
    DetachedWeaponVisualState _visual;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Missile;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _visual = context.Weapon.GetComponent<DetachedWeaponVisualState>();
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_missile_tier")), 1, 5);
      _cooldown = 0.45f;
      _deferWanderTarget = true;
      _wanderTarget = GameplayPlane.Position2D(context.Weapon);
      _hasLastOwnerPos = false;
      PrewarmPool();
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;

      TickWander(deltaTime);
      _cooldown -= deltaTime;
      _salvoTimer -= deltaTime;

      if (_salvoRemaining > 0 && _salvoTimer <= 0f)
      {
        if (Launch(false))
        {
          _salvoRemaining--;
          _salvoTimer = _tier >= 5 ? 0.08f : 0.14f;
        }
        else
        {
          _salvoRemaining = 0;
        }
      }

      if (_cooldown <= 0f && _salvoRemaining == 0)
      {
        var origin = GameplayPlane.Position2D(_context.Weapon);
        if (!DetachedWeaponCombatQuery.HasLivingEnemyInRange(origin, _context.Definition.attack_range))
        {
          _cooldown = 0.25f;
          _visual?.SetAttackActive(false);
          return;
        }

        _visual?.SetAttackActive(true);
        _salvoRemaining = _tier >= 5 ? 6 : _tier >= 2 ? 3 : 1;
        if (EvolutionFantasyDatabase.GetBehaviorVerb("missile") == "齐射")
          _salvoRemaining++;
        _salvoTimer = 0f;
        _cooldown = _context.ReducedCooldown(_context.Definition.attack_cooldown);
      }
      else if (_salvoRemaining == 0)
      {
        _visual?.SetAttackActive(false);
      }
    }

    void TickWander(float deltaTime)
    {
      if (_visual != null && _visual.IntroActive)
        return;

      if (_deferWanderTarget)
      {
        _deferWanderTarget = false;
        _wanderTarget = GameplayPlane.Position2D(_context.Weapon);
        _wanderVelocity = Vector2.zero;
        _hasLastOwnerPos = false;
        return;
      }

      var ownerPos = GameplayPlane.Position2D(_context.Owner.transform);
      DetachedWeaponMotion.TrackOwnerDelta(ref _wanderTarget, ownerPos, ref _lastOwnerPos, _hasLastOwnerPos);
      _hasLastOwnerPos = true;

      var position = GameplayPlane.Position2D(_context.Weapon);
      if ((position - _wanderTarget).sqrMagnitude < 0.2f)
        PickWanderTarget();

      var next = DetachedWeaponMotion.SmoothWander(
        position,
        _wanderTarget,
        ref _wanderVelocity,
        deltaTime,
        _context.WanderSpeed(_context.Definition.wander_speed));
      GameplayPlane.SetPosition2D(_context.Weapon, next);
    }

    void PickWanderTarget()
    {
      if (_context.Owner == null)
        return;
      _wanderTarget = GameplayPlane.Position2D(_context.Owner.transform)
        + Random.insideUnitCircle * _context.WanderRadius(_context.Definition.wander_radius);
      _wanderVelocity *= 0.35f;
    }

    bool Launch(
      bool child,
      Health preferredTarget = null,
      Vector2? directionOverride = null,
      Vector2? originOverride = null)
    {
      var projectile = Acquire();
      if (projectile == null)
        return false;

      var origin = originOverride ?? GameplayPlane.Position2D(_context.Weapon);
      var target = preferredTarget != null ? preferredTarget : FindNearest(origin, null);
      if (target == null && !directionOverride.HasValue)
        return false;

      var direction = directionOverride ??
        (GameplayPlane.Position2D(target.transform) - origin).normalized;
      projectile.Launch(
        this,
        _context.Owner,
        origin,
        direction,
        target,
        _context.Damage(_context.Definition.base_damage) * (child ? 0.55f : 1f),
        Mathf.Max(0.1f, _context.Definition.projectile_hit_radius),
        _tier >= 3,
        _tier >= 4 && !child,
        child ? "detached_missile_child" : "detached_missile");
      return true;
    }

    void PrewarmPool()
    {
      for (var i = 0; i < PoolSize; i++)
      {
        var go = new GameObject($"DetachedMissile_{i + 1}");
        go.transform.SetParent(_context.Weapon.parent, false);
        var projectile = go.AddComponent<DetachedMissileProjectile>();
        projectile.gameObject.SetActive(false);
        _projectiles.Add(projectile);
      }
    }

    DetachedMissileProjectile Acquire()
    {
      foreach (var projectile in _projectiles)
      {
        if (projectile != null && !projectile.gameObject.activeSelf)
          return projectile;
      }
      return null;
    }

    internal void OnProjectileHit(DetachedMissileProjectile projectile, Health hit, Vector2 hitPosition, bool split)
    {
      if (!split)
        return;

      var excluded = hit != null ? hit.transform : null;
      var spawned = 0;
      foreach (var target in FindNearestMany(hitPosition, excluded, 3))
      {
        var direction = (GameplayPlane.Position2D(target.transform) - hitPosition).normalized;
        if (Launch(true, target, direction, hitPosition))
          spawned++;
      }

      if (spawned > 0)
        GameEventBus.Publish(new TriggerActivatedEvent("DetachedMissileSplit", hitPosition, scale: 0.8f));
    }

    Health FindNearest(Vector2 origin, Transform excluded)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;
      Health bestTarget = null;
      var best = _context.Definition.attack_range * _context.Definition.attack_range;
      foreach (var enemy in registry.GetInRange(origin, _context.Definition.attack_range))
      {
        if (enemy == null || enemy.transform == excluded)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var sqr = (GameplayPlane.Position2D(enemy.transform) - origin).sqrMagnitude;
        if (sqr >= best)
          continue;
        best = sqr;
        bestTarget = health;
      }
      return bestTarget;
    }

    IEnumerable<Health> FindNearestMany(Vector2 origin, Transform excluded, int count)
    {
      var candidates = new List<Health>();
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return candidates;
      foreach (var enemy in registry.GetInRange(origin, 7f))
      {
        if (enemy == null || enemy.transform == excluded)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health != null && !health.IsDead)
          candidates.Add(health);
      }
      candidates.Sort((a, b) =>
        (GameplayPlane.Position2D(a.transform) - origin).sqrMagnitude.CompareTo(
          (GameplayPlane.Position2D(b.transform) - origin).sqrMagnitude));
      if (candidates.Count > count)
        candidates.RemoveRange(count, candidates.Count - count);
      return candidates;
    }

    public void Shutdown()
    {
      foreach (var projectile in _projectiles)
      {
        if (projectile != null)
          Object.Destroy(projectile.gameObject);
      }
      _projectiles.Clear();
    }
  }

  [DisallowMultipleComponent]
  sealed class DetachedMissileProjectile : MonoBehaviour
  {
    MissileBehavior _ownerBehavior;
    GameObject _source;
    Health _target;
    Vector2 _direction;
    float _damage;
    float _hitRadius;
    float _life;
    bool _homing;
    bool _split;
    string _projectileId;

    public void Launch(
      MissileBehavior behavior,
      GameObject source,
      Vector2 position,
      Vector2 direction,
      Health target,
      float damage,
      float hitRadius,
      bool homing,
      bool split,
      string projectileId)
    {
      _ownerBehavior = behavior;
      _source = source;
      _target = target;
      _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
      _damage = damage;
      _hitRadius = hitRadius;
      _homing = homing;
      _split = split;
      _projectileId = projectileId;
      _life = 3.2f;
      GameplayPlane.SetPosition2D(transform, position);
      transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
      gameObject.SetActive(true);
      GameEventBus.Publish(new ProjectileSpawnEvent(gameObject, transform.position, _projectileId, target != null ? target.gameObject : null));
    }

    void Update()
    {
      var dt = Time.deltaTime;
      _life -= dt;
      if (_life <= 0f)
      {
        Recycle();
        return;
      }

      if (_homing && _target != null && !_target.IsDead)
      {
        var desired = (GameplayPlane.Position2D(_target.transform) - GameplayPlane.Position2D(transform)).normalized;
        _direction = Vector2.Lerp(_direction, desired, 7.5f * dt).normalized;
      }

      var position = GameplayPlane.Position2D(transform) + _direction * (11.5f * dt);
      GameplayPlane.SetPosition2D(transform, position);
      transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      foreach (var enemy in registry.GetInRange(position, _hitRadius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        DamagePipeline.Apply(
          DamageRequest.Direct(_damage, "energy", _projectileId, _source),
          health);
        GameEventBus.Publish(new ProjectileHitEvent(gameObject, enemy.gameObject, transform.position, _projectileId));
        _ownerBehavior?.OnProjectileHit(this, health, position, _split);
        Recycle();
        return;
      }
    }

    void Recycle()
    {
      _target = null;
      _ownerBehavior = null;
      gameObject.SetActive(false);
    }
  }
}
