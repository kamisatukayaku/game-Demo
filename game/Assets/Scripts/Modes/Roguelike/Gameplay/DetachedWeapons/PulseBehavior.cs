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
  sealed class PulseBehavior : IDetachedWeaponBehavior
  {
    sealed class PulseWave
    {
      public readonly HashSet<int> HitIds = new();
      public Vector2 Origin;
      public float Radius;
      public float MaxRadius;
      public float DamageScale;
      public bool Secondary;
      public int Serial;

      public void Reset(Vector2 origin, float maxRadius, float damageScale, bool secondary, int serial)
      {
        Origin = origin;
        Radius = 0f;
        MaxRadius = maxRadius;
        DamageScale = damageScale;
        Secondary = secondary;
        Serial = serial;
        HitIds.Clear();
      }
    }

    sealed class ResonanceZone
    {
      public Vector2 Position;
      public float Remaining;
      public float DamageTimer;
    }

    readonly List<PulseWave> _activeWaves = new();
    readonly Stack<PulseWave> _wavePool = new();
    readonly List<ResonanceZone> _zones = new();
    readonly Stack<ResonanceZone> _zonePool = new();
    readonly HashSet<long> _resonatedPairs = new();
    readonly List<Health> _hitBuffer = new();
    DetachedWeaponRuntimeContext _context;
    Vector2 _wanderTarget;
    Vector2 _wanderVelocity;
    Vector2 _lastOwnerPos;
    bool _hasLastOwnerPos;
    float _cooldown;
    float _secondWaveDelay;
    bool _secondWavePending;
    int _tier;
    int _serial;
    int _cycleCount;
    bool _chargeAnnounced;
    float _pulseSpeedMult = 1f;
    bool _deferWanderTarget;
    DetachedWeaponVisualState _visual;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Pulse;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      _visual = context.Weapon.GetComponent<DetachedWeaponVisualState>();
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_pulse_tier")), 1, 5);
      if (EvolutionFantasyDatabase.GetBehaviorVerb("pulse") == "共振")
        _pulseSpeedMult = 1.08f;
      _cooldown = 0.4f;
      _deferWanderTarget = true;
      _wanderTarget = GameplayPlane.Position2D(context.Weapon);
      _hasLastOwnerPos = false;
      for (var i = 0; i < 24; i++)
        _wavePool.Push(new PulseWave());
      for (var i = 0; i < 10; i++)
        _zonePool.Push(new ResonanceZone());
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;
      TickWander(deltaTime);
      TickCasting(deltaTime);
      TickWaves(deltaTime);
      TickZones(deltaTime);
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
      _wanderTarget = GameplayPlane.Position2D(_context.Owner.transform)
        + Random.insideUnitCircle * _context.WanderRadius(_context.Definition.wander_radius);
      _wanderVelocity *= 0.35f;
    }

    void TickCasting(float deltaTime)
    {
      _cooldown -= deltaTime;
      if (!_chargeAnnounced && _cooldown > 0f && _cooldown <= 0.55f
          && DetachedWeaponCombatQuery.HasLivingEnemyInRange(
            GameplayPlane.Position2D(_context.Weapon),
            ResolvePulseRange()))
      {
        _chargeAnnounced = true;
        GameEventBus.Publish(new TriggerActivatedEvent(
          "DetachedPulseCharge",
          GameplayPlane.Position2D(_context.Weapon),
          _context.Weapon.gameObject,
          _context.Definition.pulse_max_radius,
          Mathf.Max(0.12f, _cooldown),
          false));
      }

      if (_secondWavePending)
      {
        _secondWaveDelay -= deltaTime;
        if (_secondWaveDelay <= 0f)
        {
          _secondWavePending = false;
          SpawnWave(GameplayPlane.Position2D(_context.Weapon), 0.42f, false, false);
        }
      }

      if (_cooldown > 0f)
        return;

      var origin = GameplayPlane.Position2D(_context.Weapon);
      if (!DetachedWeaponCombatQuery.HasLivingEnemyInRange(origin, ResolvePulseRange()))
      {
        _cooldown = 0.25f;
        _chargeAnnounced = false;
        _visual?.SetAttackActive(false);
        return;
      }

      _visual?.SetAttackActive(true);
      SpawnWave(origin, 1f, false, false);
      if (_tier >= 2)
      {
        _secondWavePending = true;
        _secondWaveDelay = 0.24f;
      }

      _cycleCount++;
      if (_tier >= 5 && _cycleCount >= Mathf.Max(2, _context.Definition.finisher_threshold))
      {
        _cycleCount = 0;
        SpawnWave(
          GameplayPlane.Position2D(_context.Owner.transform),
          1.65f,
          false,
          true);
      }
      _cooldown = _context.ReducedCooldown(_context.Definition.attack_cooldown);
      _chargeAnnounced = false;
    }

    float ResolvePulseRange() =>
      Mathf.Max(2f, _context.Definition.pulse_max_radius);

    void SpawnWave(Vector2 origin, float damageScale, bool secondary, bool arena)
    {
      if (_wavePool.Count == 0)
        return;
      var wave = _wavePool.Pop();
      var maxRadius = arena ? _context.Definition.finisher_radius
        : secondary ? _context.Definition.pulse_max_radius * 0.48f
        : _context.Definition.pulse_max_radius;
      wave.Reset(origin, maxRadius, damageScale, secondary, ++_serial);
      _activeWaves.Add(wave);
      GameEventBus.Publish(new TriggerActivatedEvent(
        arena ? "DetachedArenaPulse" : "DetachedPulseWave",
        origin,
        _context.Weapon.gameObject,
        maxRadius,
        maxRadius / _context.Definition.pulse_speed,
        secondary));
    }

    void TickWaves(float deltaTime)
    {
      for (var i = _activeWaves.Count - 1; i >= 0; i--)
      {
        var wave = _activeWaves[i];
        wave.Radius += _context.Definition.pulse_speed * _pulseSpeedMult * deltaTime;
        DamageWaveFront(wave);
        if (wave.Radius < wave.MaxRadius)
          continue;
        _activeWaves.RemoveAt(i);
        wave.HitIds.Clear();
        _wavePool.Push(wave);
      }

      if (_tier >= 4)
        DetectResonance();
    }

    void DamageWaveFront(PulseWave wave)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      var halfWidth = _context.Definition.pulse_thickness * 0.5f;
      _hitBuffer.Clear();
      foreach (var enemy in registry.GetInRange(wave.Origin, wave.Radius + halfWidth))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || wave.HitIds.Contains(health.GetInstanceID()))
          continue;
        var distance = Vector2.Distance(wave.Origin, GameplayPlane.Position2D(health.transform));
        if (Mathf.Abs(distance - wave.Radius) <= halfWidth)
          _hitBuffer.Add(health);
      }

      foreach (var health in _hitBuffer)
      {
        wave.HitIds.Add(health.GetInstanceID());
        DamagePipeline.Apply(
          DamageRequest.Direct(
            _context.Damage(_context.Definition.base_damage) * wave.DamageScale,
            "energy",
            "detached_pulse",
            _context.Owner),
          health);

        if (_tier >= 3 && !wave.Secondary && _wavePool.Count > 0)
          SpawnWave(GameplayPlane.Position2D(health.transform), 0.24f, true, false);
      }
    }

    void DetectResonance()
    {
      if (_resonatedPairs.Count > 4096)
        _resonatedPairs.Clear();
      for (var i = 0; i < _activeWaves.Count; i++)
      {
        for (var j = i + 1; j < _activeWaves.Count; j++)
        {
          var a = _activeWaves[i];
          var b = _activeWaves[j];
          if (!a.Secondary && !b.Secondary)
            continue;
          var key = ((long)Mathf.Min(a.Serial, b.Serial) << 32) | (uint)Mathf.Max(a.Serial, b.Serial);
          if (_resonatedPairs.Contains(key))
            continue;
          var centerDistance = Vector2.Distance(a.Origin, b.Origin);
          var intersects = centerDistance <= a.Radius + b.Radius
            && centerDistance >= Mathf.Abs(a.Radius - b.Radius);
          if (!intersects)
            continue;
          _resonatedPairs.Add(key);
          SpawnZone((a.Origin + b.Origin) * 0.5f);
        }
      }
    }

    void SpawnZone(Vector2 position)
    {
      if (_zonePool.Count == 0)
        return;
      var zone = _zonePool.Pop();
      zone.Position = position;
      zone.Remaining = _context.Definition.pulse_zone_duration;
      zone.DamageTimer = 0f;
      _zones.Add(zone);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "DetachedPulseResonance",
        position,
        _context.Weapon.gameObject,
        _context.Definition.effect_radius,
        zone.Remaining));
    }

    void TickZones(float deltaTime)
    {
      for (var i = _zones.Count - 1; i >= 0; i--)
      {
        var zone = _zones[i];
        zone.Remaining -= deltaTime;
        zone.DamageTimer -= deltaTime;
        if (zone.DamageTimer <= 0f)
        {
          zone.DamageTimer = 0.3f;
          DamageZone(zone);
        }
        if (zone.Remaining > 0f)
          continue;
        _zones.RemoveAt(i);
        _zonePool.Push(zone);
      }
    }

    void DamageZone(ResonanceZone zone)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;
      foreach (var enemy in registry.GetInRange(zone.Position, _context.Definition.effect_radius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        DamagePipeline.Apply(
          DamageRequest.Direct(_context.Damage(_context.Definition.base_damage) * 0.26f, "energy", "detached_resonance", _context.Owner),
          health);
      }
    }

    public void Shutdown()
    {
      foreach (var wave in _activeWaves)
        _wavePool.Push(wave);
      _activeWaves.Clear();
      foreach (var zone in _zones)
        _zonePool.Push(zone);
      _zones.Clear();
      _resonatedPairs.Clear();
      _hitBuffer.Clear();
    }
  }
}
