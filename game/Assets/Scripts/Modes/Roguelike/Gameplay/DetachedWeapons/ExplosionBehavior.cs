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
  sealed class ExplosionBehavior : IDetachedWeaponBehavior
  {
    enum BurstKind { Primary, Expanding, Chain, Death, Finisher }

    struct PendingBurst
    {
      public Vector2 Position;
      public float Delay;
      public float Radius;
      public float DamageScale;
      public BurstKind Kind;
      public int Depth;
    }

    readonly List<PendingBurst> _pending = new();
    readonly List<Health> _hits = new();
    readonly Dictionary<int, float> _contactCooldowns = new();
    DetachedWeaponRuntimeContext _context;
    Vector2 _wanderTarget;
    Vector2 _wanderVelocity;
    Vector2 _lastOwnerPos;
    bool _hasLastOwnerPos;
    int _tier;
    int _chainProgress;
    float _primaryRadiusMult = 1f;
    bool _deferWanderTarget;
    DetachedWeaponVisualState _visual;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Explosion;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _visual = context.Weapon.GetComponent<DetachedWeaponVisualState>();
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_explosion_tier")), 1, 5);
      if (EvolutionFantasyDatabase.GetBehaviorVerb("explosion") == "连锁")
        _primaryRadiusMult = 1.1f;
      _deferWanderTarget = true;
      _wanderTarget = GameplayPlane.Position2D(context.Weapon);
      _hasLastOwnerPos = false;
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;
      TickWander(deltaTime);
      DetectContact();
      TickPending(deltaTime);
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
      if ((position - _wanderTarget).sqrMagnitude < 0.18f)
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
      var center = GameplayPlane.Position2D(_context.Owner.transform);
      _wanderTarget = center + Random.insideUnitCircle * _context.WanderRadius(_context.Definition.wander_radius);
      _wanderVelocity *= 0.35f;
    }

    void DetectContact()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      var position = GameplayPlane.Position2D(_context.Weapon);
      var hitRadius = Mathf.Max(0.1f, _context.Scale(_context.Definition.contact_radius, "contact_radius"));
      foreach (var enemy in registry.GetInRange(position, hitRadius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var key = health.GetInstanceID();
        if (_contactCooldowns.TryGetValue(key, out var nextHitTime) && Time.time < nextHitTime)
          continue;
        _contactCooldowns[key] = Time.time + _context.Definition.hit_cooldown;
        Queue(position, 0f, _context.Definition.effect_radius, 1f, BurstKind.Primary, 0);
        break;
      }
    }

    void TickPending(float deltaTime)
    {
      for (var i = _pending.Count - 1; i >= 0; i--)
      {
        var burst = _pending[i];
        burst.Delay -= deltaTime;
        if (burst.Delay > 0f)
        {
          _pending[i] = burst;
          continue;
        }
        _pending.RemoveAt(i);
        Detonate(burst);
      }
    }

    void Detonate(PendingBurst burst)
    {
      var nuclear = burst.Kind == BurstKind.Finisher;
      GameEventBus.Publish(new TriggerActivatedEvent(
        nuclear ? "DetachedNuclearExplosion" : "DetachedExplosion",
        burst.Position,
        _context.Weapon.gameObject,
        burst.Radius,
        (float)burst.Kind,
        burst.Kind == BurstKind.Expanding));

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      _hits.Clear();
      foreach (var enemy in registry.GetInRange(burst.Position, burst.Radius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        _hits.Add(health);
      }

      foreach (var health in _hits)
      {
        var wasAlive = !health.IsDead;
        DamagePipeline.Apply(
          DamageRequest.Direct(
            _context.Damage(_context.Definition.base_damage) * burst.DamageScale,
            "energy",
            nuclear ? "detached_nuclear_chain" : "detached_explosion",
            _context.Owner),
          health);

        if (_tier >= 3 && burst.Depth < 1 && burst.Kind != BurstKind.Expanding && burst.Kind != BurstKind.Finisher)
        {
          Queue(
            GameplayPlane.Position2D(health.transform),
            _context.Definition.secondary_delay,
            _context.Definition.effect_radius * 0.82f,
            0.62f,
            BurstKind.Chain,
            burst.Depth + 1);
        }

        if (_tier >= 4 && wasAlive && health.IsDead)
        {
          Queue(
            GameplayPlane.Position2D(health.transform),
            _context.Definition.secondary_delay * 0.55f,
            _context.Definition.effect_radius,
            0.82f,
            BurstKind.Death,
            burst.Depth + 1);
        }
      }

      if (_tier >= 2 && burst.Kind == BurstKind.Primary)
      {
        Queue(
          burst.Position,
          _context.Definition.secondary_delay,
          _context.Definition.secondary_radius,
          0.72f,
          BurstKind.Expanding,
          burst.Depth);
      }

      if (_tier >= 5 && burst.Kind != BurstKind.Finisher)
      {
        _chainProgress++;
        if (_chainProgress >= Mathf.Max(2, _context.Definition.finisher_threshold))
        {
          _chainProgress = 0;
          Queue(
            burst.Position,
            _context.Definition.secondary_delay,
            _context.Definition.finisher_radius,
            2.1f,
            BurstKind.Finisher,
            0);
        }
      }
    }

    void Queue(Vector2 position, float delay, float radius, float damageScale, BurstKind kind, int depth)
    {
      if (kind == BurstKind.Primary)
        radius *= _primaryRadiusMult;

      _pending.Add(new PendingBurst
      {
        Position = position,
        Delay = delay,
        Radius = radius,
        DamageScale = damageScale,
        Kind = kind,
        Depth = depth
      });
    }

    public void Shutdown()
    {
      _pending.Clear();
      _hits.Clear();
      _contactCooldowns.Clear();
    }
  }
}
