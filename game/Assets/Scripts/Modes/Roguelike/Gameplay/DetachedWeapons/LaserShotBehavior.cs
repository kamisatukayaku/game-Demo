using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  sealed class LaserShotBehavior : IDetachedWeaponBehavior
  {
    enum Phase { Cooldown, Warning, Firing }

    readonly List<Health> _targets = new();
    readonly List<Health> _beamHits = new();
    DetachedWeaponRuntimeContext _context;
    DetachedWeaponVisualState _visual;
    Phase _phase;
    float _phaseTimer;
    float _damageTimer;
    Vector2 _wanderTarget;
    Vector2 _wanderVelocity;
    Vector2 _lastOwnerPos;
    bool _hasLastOwnerPos;
    Vector2 _primaryDirection = Vector2.right;
    Vector2 _secondaryDirection = Vector2.left;
    Vector2 _tertiaryDirection = Vector2.up;
    Health _primaryTarget;
    Health _secondaryTarget;
    Health _tertiaryTarget;
    int _tier;
    float _fireDurationMult = 1f;

    bool _deferWanderTarget;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.LaserShot;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _visual = context.Weapon.GetComponent<DetachedWeaponVisualState>();
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_laser_tier")), 1, 5);
      if (EvolutionFantasyDatabase.GetBehaviorVerb("laser") == "折射")
        _fireDurationMult = 1.1f;
      _phase = Phase.Cooldown;
      _phaseTimer = 0.35f;
      _deferWanderTarget = true;
      _wanderTarget = GameplayPlane.Position2D(context.Weapon);
      _hasLastOwnerPos = false;
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;

      if (_phase == Phase.Cooldown)
        TickWander(deltaTime);

      _phaseTimer -= deltaTime;

      switch (_phase)
      {
        case Phase.Cooldown:
          _visual?.ClearBeams();
          if (_phaseTimer <= 0f && AcquireDirections())
            BeginWarning();
          break;
        case Phase.Warning:
          RefreshTrackedDirections(deltaTime);
          DrawBeams(true, 0f);
          if (_phaseTimer <= 0f)
            BeginFiring();
          break;
        case Phase.Firing:
          RefreshTrackedDirections(deltaTime);
          var duration = (_tier >= 4 ? 1.05f : 0.48f) * _fireDurationMult;
          var progress = 1f - Mathf.Clamp01(_phaseTimer / duration);
          DrawBeams(false, progress);
          _damageTimer -= deltaTime;
          if (_damageTimer <= 0f)
          {
            _damageTimer = 0.12f;
            ApplyBeamDamage(progress);
          }
          if (_phaseTimer <= 0f)
            BeginCooldown();
          break;
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
      if ((position - _wanderTarget).sqrMagnitude < 0.16f)
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
      var center = GameplayPlane.Position2D(_context.Owner.transform);
      _wanderTarget = center + Random.insideUnitCircle * _context.WanderRadius(_context.Definition.wander_radius);
      _wanderVelocity *= 0.35f;
    }

    bool AcquireDirections()
    {
      var origin = GameplayPlane.Position2D(_context.Weapon);
      RefreshTargetList(origin);
      if (_targets.Count == 0)
        return false;

      _primaryTarget = _targets[0];
      _secondaryTarget = _targets.Count > 1 ? _targets[1] : null;
      _tertiaryTarget = _targets.Count > 2 ? _targets[2] : null;
      _primaryDirection = DirectionToTarget(origin, _primaryTarget, _primaryDirection);
      _secondaryDirection = _secondaryTarget != null
        ? DirectionToTarget(origin, _secondaryTarget, _secondaryDirection)
        : new Vector2(-_primaryDirection.y, _primaryDirection.x);
      _tertiaryDirection = _tertiaryTarget != null
        ? DirectionToTarget(origin, _tertiaryTarget, _tertiaryDirection)
        : -_primaryDirection;
      return true;
    }

    void RefreshTrackedDirections(float deltaTime)
    {
      var origin = GameplayPlane.Position2D(_context.Weapon);
      RefreshTargetList(origin);
      _primaryTarget = ChooseTarget(null);
      _secondaryTarget = _tier >= 2 ? ChooseTarget(_primaryTarget) : null;
      _tertiaryTarget = _tier >= 5 ? ChooseTarget(_primaryTarget, _secondaryTarget) : null;
      if (_primaryTarget == null)
        return;

      var turnRate = (_phase == Phase.Warning ? 760f : 430f) * Mathf.Deg2Rad;
      _primaryDirection = SmoothDirection(_primaryDirection, DirectionToTarget(origin, _primaryTarget, _primaryDirection), turnRate, deltaTime);
      if (_tier >= 2)
      {
        var desiredSecondary = _secondaryTarget != null
          ? DirectionToTarget(origin, _secondaryTarget, _secondaryDirection)
          : new Vector2(-_primaryDirection.y, _primaryDirection.x);
        _secondaryDirection = SmoothDirection(_secondaryDirection, desiredSecondary, turnRate * 0.92f, deltaTime);
      }
      if (_tier >= 5)
      {
        var desiredTertiary = _tertiaryTarget != null
          ? DirectionToTarget(origin, _tertiaryTarget, _tertiaryDirection)
          : -_primaryDirection;
        _tertiaryDirection = SmoothDirection(_tertiaryDirection, desiredTertiary, turnRate * 0.86f, deltaTime);
      }
    }

    void RefreshTargetList(Vector2 origin)
    {
      _targets.Clear();
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(origin, _context.Definition.attack_range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health != null && !health.IsDead)
          _targets.Add(health);
      }

      _targets.Sort((a, b) => TargetScore(a, origin).CompareTo(TargetScore(b, origin)));
    }

    Health ChooseTarget(params Health[] excludes)
    {
      for (var i = 0; i < _targets.Count; i++)
      {
        var candidate = _targets[i];
        if (candidate == null || IsExcluded(candidate, excludes))
          continue;
        return candidate;
      }
      return null;
    }

    static bool IsExcluded(Health candidate, Health[] excludes)
    {
      if (excludes == null)
        return false;
      for (var i = 0; i < excludes.Length; i++)
        if (candidate == excludes[i])
          return true;
      return false;
    }

    static float TargetScore(Health target, Vector2 origin)
    {
      if (target == null)
        return float.MaxValue;
      var score = (GameplayPlane.Position2D(target.transform) - origin).sqrMagnitude;
      if (EnemySpawnMetadata.IsBossEnemy(target.gameObject))
        score -= 100000f;
      else if (target.tag == "Elite")
        score -= 4000f;
      return score;
    }

    static Vector2 DirectionToTarget(Vector2 origin, Health target, Vector2 fallback)
    {
      if (target == null)
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right;
      var direction = GameplayPlane.Position2D(target.transform) - origin;
      return direction.sqrMagnitude > 0.0001f ? direction.normalized : (fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right);
    }

    static Vector2 SmoothDirection(Vector2 current, Vector2 desired, float maxRadiansDelta, float deltaTime)
    {
      if (desired.sqrMagnitude <= 0.0001f)
        return current.sqrMagnitude > 0.0001f ? current.normalized : Vector2.right;
      if (current.sqrMagnitude <= 0.0001f)
        return desired.normalized;
      var currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
      var desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
      var nextAngle = Mathf.MoveTowardsAngle(
        currentAngle,
        desiredAngle,
        maxRadiansDelta * Mathf.Rad2Deg * Mathf.Max(0f, deltaTime));
      var radians = nextAngle * Mathf.Deg2Rad;
      return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    void BeginWarning()
    {
      _phase = Phase.Warning;
      _phaseTimer = 0.72f;
      _wanderVelocity = Vector2.zero;
      _wanderTarget = GameplayPlane.Position2D(_context.Weapon);
      _hasLastOwnerPos = false;
    }

    void BeginFiring()
    {
      _phase = Phase.Firing;
      _phaseTimer = _tier >= 4 ? 1.05f : 0.48f;
      _damageTimer = 0f;
    }

    void BeginCooldown()
    {
      _phase = Phase.Cooldown;
      _phaseTimer = _context.ReducedCooldown(_context.Definition.attack_cooldown);
      _deferWanderTarget = true;
      _visual?.ClearBeams();
    }

    void DrawBeams(bool warning, float progress)
    {
      if (_visual == null)
        return;

      var width = _tier >= 3 ? 0.72f : 0.2f;
      _visual.BeginFrame(warning, width);
      var origin = _context.Weapon.position;
      AddPrimaryVisual(origin, DirectionForProgress(_primaryDirection, progress, 1f));
      if (_tier >= 2)
        AddPrimaryVisual(origin, DirectionForProgress(_secondaryDirection, progress, -1f));
      if (_tier >= 5)
        AddPrimaryVisual(origin, DirectionForProgress(_tertiaryDirection, progress, 0.55f));
    }

    void AddPrimaryVisual(Vector3 origin, Vector2 direction)
    {
      _visual.AddBeam(origin, origin + (Vector3)(direction * _context.Definition.attack_range));
      if (_tier < 3 || _visual.Warning)
        return;

      var normal = new Vector2(-direction.y, direction.x) * 0.48f;
      _visual.AddBeam(origin + (Vector3)normal, origin + (Vector3)(direction * _context.Definition.attack_range + normal));
      _visual.AddBeam(origin - (Vector3)normal, origin + (Vector3)(direction * _context.Definition.attack_range - normal));
    }

    Vector2 DirectionForProgress(Vector2 baseDirection, float progress, float sign)
    {
      if (_tier < 4 || _phase != Phase.Firing)
        return baseDirection;
      var angle = Mathf.Lerp(-28f, 28f, progress) * sign;
      return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }

    void ApplyBeamDamage(float progress)
    {
      var origin = GameplayPlane.Position2D(_context.Weapon);
      DamageAlong(origin, DirectionForProgress(_primaryDirection, progress, 1f));
      if (_tier >= 2)
        DamageAlong(origin, DirectionForProgress(_secondaryDirection, progress, -1f));
      if (_tier >= 5)
        DamageAlong(origin, DirectionForProgress(_tertiaryDirection, progress, 0.55f));
    }

    void DamageAlong(Vector2 origin, Vector2 direction)
    {
      _beamHits.Clear();
      var end = origin + direction * _context.Definition.attack_range;
      var width = _tier >= 3 ? 0.5f : 0.16f;
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(origin, _context.Definition.attack_range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        if (DistanceToSegment(GameplayPlane.Position2D(enemy.transform), origin, end) > width)
          continue;

        DamagePipeline.Apply(
          DamageRequest.Direct(_context.Damage(_context.Definition.base_damage), "energy", "detached_laser", _context.Owner),
          health);
        _beamHits.Add(health);
      }

      if (_tier >= 5)
        ApplyPrismRefractions();
    }

    void ApplyPrismRefractions()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || _visual == null)
        return;

      foreach (var hit in _beamHits)
      {
        if (hit == null || hit.IsDead)
          continue;
        var start = GameplayPlane.Position2D(hit.transform);
        Health nearest = null;
        var best = 16f;
        foreach (var enemy in registry.GetInRange(start, 4f))
        {
          if (enemy == null || enemy.transform == hit.transform)
            continue;
          var health = enemy.GetComponent<Health>();
          if (health == null || health.IsDead || _beamHits.Contains(health))
            continue;
          var sqr = (GameplayPlane.Position2D(health.transform) - start).sqrMagnitude;
          if (sqr >= best)
            continue;
          best = sqr;
          nearest = health;
        }
        if (nearest == null)
          continue;

        _visual.AddBeam(hit.transform.position, nearest.transform.position);
        DamagePipeline.Apply(
          DamageRequest.Direct(_context.Damage(_context.Definition.base_damage) * 0.65f, "energy", "detached_prism", _context.Owner),
          nearest);
      }
    }

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
      var segment = end - start;
      var lengthSq = segment.sqrMagnitude;
      if (lengthSq < 0.0001f)
        return Vector2.Distance(point, start);
      var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSq);
      return Vector2.Distance(point, start + segment * t);
    }

    public void Shutdown()
    {
      _visual?.ClearBeams();
      _targets.Clear();
      _beamHits.Clear();
    }
  }
}
