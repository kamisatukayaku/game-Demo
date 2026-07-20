using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;
using Game.Shared.Enemy.Collision;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  sealed class TrailBehavior : IDetachedWeaponBehavior
  {
    const int SegmentCapacity = 112;

    sealed class TrailSegment
    {
      public readonly Dictionary<int, float> HitCooldowns = new();
      public Vector2 Start;
      public Vector2 End;
      public float Remaining;
      public float DamageTimer;
      public bool Active;
      public bool Network;

      public void Reset(Vector2 start, Vector2 end, float lifetime, bool network)
      {
        Start = start;
        End = end;
        Remaining = lifetime;
        DamageTimer = 0f;
        Active = true;
        Network = network;
        HitCooldowns.Clear();
      }
    }

    readonly TrailSegment[] _segments = new TrailSegment[SegmentCapacity];
    DetachedWeaponRuntimeContext _context;
    Vector2 _wanderTarget;
    Vector2 _wanderVelocity;
    Vector2 _lastOwnerPos;
    bool _hasLastOwnerPos;
    Vector2 _lastSample;
    Vector2 _lastDirection;
    int _nextSegment;
    int _tier;
    float _sampleDistanceMult = 1f;
    bool _deferWanderTarget;
    DetachedWeaponVisualState _visual;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Trail;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _visual = context.Weapon.GetComponent<DetachedWeaponVisualState>();
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_trail_tier")), 1, 5);
      if (EvolutionFantasyDatabase.GetBehaviorVerb("trail") == "切割")
        _sampleDistanceMult = 0.88f;
      for (var i = 0; i < _segments.Length; i++)
        _segments[i] = new TrailSegment();
      _lastSample = GameplayPlane.Position2D(context.Weapon);
      _deferWanderTarget = true;
      _wanderTarget = _lastSample;
      _hasLastOwnerPos = false;
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;
      TickWander(deltaTime);
      SampleTrail();
      TickSegments(deltaTime);
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
        _lastSample = GameplayPlane.Position2D(_context.Weapon);
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
      _wanderTarget = GameplayPlane.Position2D(_context.Owner.transform)
        + Random.insideUnitCircle * _context.WanderRadius(_context.Definition.wander_radius);
      _wanderVelocity *= 0.35f;
    }

    void SampleTrail()
    {
      var current = GameplayPlane.Position2D(_context.Weapon);
      var attackRange = Mathf.Max(_context.Definition.wander_radius, 6f);
      if (!DetachedWeaponCombatQuery.HasLivingEnemyInRange(current, attackRange))
      {
        // Do not bridge the idle movement to the next active sample.
        _lastSample = current;
        _lastDirection = Vector2.zero;
        return;
      }

      var delta = current - _lastSample;
      if (delta.sqrMagnitude < _context.Definition.trail_sample_distance * _context.Definition.trail_sample_distance * _sampleDistanceMult * _sampleDistanceMult)
        return;

      var direction = delta.normalized;
      var turnAngle = _lastDirection.sqrMagnitude > 0.01f
        ? Vector2.SignedAngle(_lastDirection, direction)
        : 0f;
      AddSegment(_lastSample, current, false, false);

      if (_tier >= 4 && Mathf.Abs(turnAngle) >= 32f)
      {
        var side = Mathf.Sign(turnAngle);
        var normal = new Vector2(-direction.y, direction.x) * side;
        AddSegment(current, current + normal * _context.Definition.trail_fork_length, true, false);
      }

      if (_tier >= 5)
        TryCreateNetworkLink(current);

      _lastSample = current;
      _lastDirection = direction;
    }

    void AddSegment(Vector2 start, Vector2 end, bool branch, bool network)
    {
      var lifetime = _tier >= 3
        ? _context.Definition.trail_persistent_lifetime
        : _tier >= 2 ? _context.Definition.trail_long_lifetime : _context.Definition.trail_short_lifetime;
      if (branch)
        lifetime *= 0.72f;
      if (network)
        lifetime = _context.Definition.trail_persistent_lifetime;

      var segment = _segments[_nextSegment];
      _nextSegment = (_nextSegment + 1) % _segments.Length;
      segment.Reset(start, end, lifetime, network);
      GameEventBus.Publish(new TrailSegmentEvent(
        start,
        end,
        _context.Definition.trail_width * (network ? 0.72f : 1f),
        lifetime,
        branch,
        network));
    }

    void TryCreateNetworkLink(Vector2 current)
    {
      TrailSegment nearest = null;
      var best = _context.Definition.trail_network_distance * _context.Definition.trail_network_distance;
      foreach (var segment in _segments)
      {
        if (!segment.Active || segment.Network)
          continue;
        var sqr = (segment.End - current).sqrMagnitude;
        if (sqr < 1.44f || sqr >= best)
          continue;
        best = sqr;
        nearest = segment;
      }
      if (nearest != null)
        AddSegment(current, nearest.End, false, true);
    }

    void TickSegments(float deltaTime)
    {
      foreach (var segment in _segments)
      {
        if (!segment.Active)
          continue;
        segment.Remaining -= deltaTime;
        segment.DamageTimer -= deltaTime;
        if (segment.Remaining <= 0f)
        {
          segment.Active = false;
          segment.HitCooldowns.Clear();
          continue;
        }
        if (segment.DamageTimer > 0f)
          continue;
        segment.DamageTimer = 0.22f;
        DamageSegment(segment);
      }
    }

    void DamageSegment(TrailSegment segment)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var id = health.GetInstanceID();
        if (segment.HitCooldowns.TryGetValue(id, out var nextHit) && Time.time < nextHit)
          continue;
        if (!EnemyHitbox.IntersectsSegment(health.gameObject, segment.Start, segment.End,
              _context.Definition.trail_width * (segment.Network ? 0.36f : 0.5f)))
          continue;
        segment.HitCooldowns[id] = Time.time + 0.42f;
        DamagePipeline.Apply(
          DamageRequest.Direct(
            _context.Damage(_context.Definition.base_damage) * (segment.Network ? 0.28f : 0.36f),
            "energy",
            segment.Network ? "detached_trail_network" : "detached_trail",
            _context.Owner),
          health);
      }
    }

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
      var delta = end - start;
      var lengthSq = delta.sqrMagnitude;
      if (lengthSq < 0.0001f)
        return Vector2.Distance(point, start);
      var t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSq);
      return Vector2.Distance(point, start + delta * t);
    }

    public void Shutdown()
    {
      foreach (var segment in _segments)
      {
        segment.Active = false;
        segment.HitCooldowns.Clear();
      }
    }
  }
}
