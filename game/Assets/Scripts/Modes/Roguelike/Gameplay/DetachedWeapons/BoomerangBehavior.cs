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
  sealed class BoomerangBehavior : IDetachedWeaponBehavior
  {
    enum FlightPhase { Outbound, Returning }

    sealed class Flight
    {
      public readonly HashSet<int> HitIds = new();
      public Transform Body;
      public bool IsMain;
      public bool Active;
      public FlightPhase Phase;
      public Vector2 Direction;
      public Vector2 LaunchPosition;
      public float MaxDistance;
      public float ReturnTime;
      public int RecastsRemaining;
      public int TargetIndex;
    }

    readonly List<Flight> _flights = new();
    readonly List<Health> _targets = new();
    DetachedWeaponRuntimeContext _context;
    float _cooldown;
    int _tier;
    float _flightSpeedMult = 1f;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Boomerang;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_boomerang_tier")), 1, 5);
      if (EvolutionFantasyDatabase.GetBehaviorVerb("boomerang") == "回旋")
        _flightSpeedMult = 1.1f;
      _cooldown = 0.35f;
      _flights.Add(new Flight { Body = context.Weapon, IsMain = true });
      context.Weapon.GetComponent<DetachedWeaponVisualState>()?.SetAttackActive(false);
      var echoCount = Mathf.Max(0, context.Definition.boomerang_storm_count - 1);
      for (var i = 0; i < echoCount; i++)
      {
        var go = new GameObject($"BoomerangEcho_{i + 1}");
        go.transform.SetParent(context.Weapon.parent, false);
        go.AddComponent<DetachedWeaponVisualState>().SetVisual("boomerang_core");
        go.SetActive(false);
        _flights.Add(new Flight { Body = go.transform, IsMain = false });
      }
      DockInactiveFlights();
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;

      _cooldown -= deltaTime;
      var anyActive = false;
      foreach (var flight in _flights)
      {
        if (!flight.Active)
          continue;
        anyActive = true;
        TickFlight(flight, deltaTime);
      }

      if (!anyActive && _cooldown <= 0f)
      {
        RefreshTargets();
        if (_targets.Count == 0)
        {
          _cooldown = 0.25f;
          _context.Weapon.GetComponent<DetachedWeaponVisualState>()?.SetAttackActive(false);
        }
        else
        {
          LaunchVolley();
        }
      }
      DockInactiveFlights();
    }

    void LaunchVolley()
    {
      var count = _tier >= 5
        ? Mathf.Min(_flights.Count, Mathf.Max(1, _context.Definition.boomerang_storm_count))
        : 1;
      var baseDirection = DirectionToTarget(0, Vector2.right);
      for (var i = 0; i < count; i++)
      {
        var spread = count <= 1 ? 0f : Mathf.Lerp(-34f, 34f, i / (float)(count - 1));
        var targetDirection = DirectionToTarget(i, Quaternion.Euler(0f, 0f, spread) * baseDirection);
        BeginOutbound(_flights[i], targetDirection, i, true);
      }
      _cooldown = _context.ReducedCooldown(_context.Definition.attack_cooldown);
    }

    void BeginOutbound(Flight flight, Vector2 direction, int targetIndex, bool fromPlayer)
    {
      if (!flight.IsMain)
        flight.Body.gameObject.SetActive(true);
      var origin = fromPlayer
        ? GameplayPlane.Position2D(_context.Owner.transform)
        : GameplayPlane.Position2D(flight.Body);
      GameplayPlane.SetPosition2D(flight.Body, origin);
      flight.Active = true;
      flight.Phase = FlightPhase.Outbound;
      flight.Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
      flight.LaunchPosition = origin;
      flight.MaxDistance = _tier >= 2
        ? _context.Definition.boomerang_long_distance
        : _context.Definition.boomerang_short_distance;
      flight.ReturnTime = 0f;
      flight.RecastsRemaining = _tier >= 4 ? Mathf.Max(1, _context.Definition.boomerang_recasts) : 0;
      flight.TargetIndex = targetIndex;
      flight.HitIds.Clear();
      flight.Body.GetComponent<DetachedWeaponVisualState>()?.SetAttackActive(true);
      PublishAttach(flight);
    }

    void TickFlight(Flight flight, float deltaTime)
    {
      var speed = Mathf.Max(_context.Definition.boomerang_speed,
        ResolveOwnerMoveSpeed() * 2f) * _flightSpeedMult;
      var position = GameplayPlane.Position2D(flight.Body);
      if (flight.Phase == FlightPhase.Outbound)
      {
        position += flight.Direction * speed * deltaTime;
        GameplayPlane.SetPosition2D(flight.Body, position);
        var hit = DamageContacts(flight);
        if ((_tier < 3 && hit) || Vector2.Distance(position, flight.LaunchPosition) >= flight.MaxDistance)
          BeginReturn(flight);
        return;
      }

      flight.ReturnTime += deltaTime;
      var ownerPosition = GameplayPlane.Position2D(_context.Owner.transform);
      var next = Vector2.MoveTowards(position, ownerPosition, speed * 1.15f * deltaTime);
      GameplayPlane.SetPosition2D(flight.Body, next);
      DamageContacts(flight);

      if (_tier >= 4 && flight.RecastsRemaining > 0 && flight.ReturnTime >= 0.18f)
      {
        var target = FindNearest(next, flight.HitIds);
        if (target != null)
        {
          flight.RecastsRemaining--;
          flight.Phase = FlightPhase.Outbound;
          flight.Direction = (GameplayPlane.Position2D(target.transform) - next).normalized;
          flight.LaunchPosition = next;
          flight.MaxDistance = Mathf.Min(
            _context.Definition.boomerang_long_distance,
            Vector2.Distance(next, GameplayPlane.Position2D(target.transform)) + 1.2f);
          flight.ReturnTime = 0f;
          flight.HitIds.Clear();
          GameEventBus.Publish(new TriggerActivatedEvent(
            "DetachedBoomerangRecast",
            flight.Body.position,
            flight.Body.gameObject,
            0.7f));
          return;
        }
      }

      if ((next - ownerPosition).sqrMagnitude <= 0.08f)
        CompleteFlight(flight);
    }

    void BeginReturn(Flight flight)
    {
      flight.Phase = FlightPhase.Returning;
      flight.ReturnTime = 0f;
      flight.HitIds.Clear();
      GameEventBus.Publish(new TriggerActivatedEvent(
        "DetachedBoomerangTurn",
        flight.Body.position,
        flight.Body.gameObject,
        0.75f));
    }

    bool DamageContacts(Flight flight)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return false;
      var hitAny = false;
      var position = GameplayPlane.Position2D(flight.Body);
      var hitRadius = Mathf.Max(0.1f, _context.Scale(_context.Definition.contact_radius, "contact_radius"));
      foreach (var enemy in registry.GetInRange(position, hitRadius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || !flight.HitIds.Add(health.GetInstanceID()))
          continue;
        DamagePipeline.Apply(
          DamageRequest.Direct(_context.Damage(_context.Definition.base_damage), "physical", "detached_boomerang", _context.Owner),
          health);
        GameEventBus.Publish(new TriggerActivatedEvent(
          "DetachedBoomerangHit",
          health.transform.position,
          flight.Body.gameObject,
          1f,
          0f,
          flight.Phase == FlightPhase.Returning));
        hitAny = true;
      }
      return hitAny;
    }

    void CompleteFlight(Flight flight)
    {
      flight.Active = false;
      flight.HitIds.Clear();
      GameplayPlane.SetPosition2D(flight.Body, GameplayPlane.Position2D(_context.Owner.transform));
      flight.Body.GetComponent<DetachedWeaponVisualState>()?.SetAttackActive(false);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "DetachedBoomerangReturn",
        flight.Body.position,
        flight.Body.gameObject,
        0.55f));
      if (!flight.IsMain)
        flight.Body.gameObject.SetActive(false);
    }

    void DockInactiveFlights()
    {
      var center = GameplayPlane.Position2D(_context.Owner.transform);
      foreach (var flight in _flights)
      {
        if (flight.Active)
          continue;
        GameplayPlane.SetPosition2D(flight.Body, center);
      }
    }

    void RefreshTargets()
    {
      _targets.Clear();
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      var origin = GameplayPlane.Position2D(_context.Owner.transform);
      foreach (var enemy in registry.GetInRange(origin, _context.Definition.attack_range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health != null && !health.IsDead)
          _targets.Add(health);
      }
      _targets.Sort((a, b) =>
        (GameplayPlane.Position2D(a.transform) - origin).sqrMagnitude.CompareTo(
          (GameplayPlane.Position2D(b.transform) - origin).sqrMagnitude));
    }

    Vector2 DirectionToTarget(int index, Vector2 fallback)
    {
      if (_targets.Count == 0)
        return fallback.normalized;
      var target = _targets[index % _targets.Count];
      return (GameplayPlane.Position2D(target.transform) - GameplayPlane.Position2D(_context.Owner.transform)).normalized;
    }

    Health FindNearest(Vector2 origin, HashSet<int> excludedIds)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;
      Health nearest = null;
      var best = _context.Definition.attack_range * _context.Definition.attack_range;
      foreach (var enemy in registry.GetInRange(origin, _context.Definition.attack_range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || excludedIds.Contains(health.GetInstanceID()))
          continue;
        var sqr = (GameplayPlane.Position2D(health.transform) - origin).sqrMagnitude;
        if (sqr >= best)
          continue;
        best = sqr;
        nearest = health;
      }
      return nearest;
    }

    float ResolveOwnerMoveSpeed()
    {
      var body = _context.Owner.GetComponent<Rigidbody2D>();
      return body != null ? body.velocity.magnitude : 5f;
    }

    void PublishAttach(Flight flight)
    {
      GameEventBus.Publish(new TriggerActivatedEvent(
        "DetachedBoomerangAttach",
        flight.Body.position,
        flight.Body.gameObject,
        1f,
        flight.TargetIndex,
        !flight.IsMain));
    }

    public void Shutdown()
    {
      for (var i = 1; i < _flights.Count; i++)
      {
        if (_flights[i].Body != null)
          Object.Destroy(_flights[i].Body.gameObject);
      }
      _flights.Clear();
      _targets.Clear();
    }
  }
}
