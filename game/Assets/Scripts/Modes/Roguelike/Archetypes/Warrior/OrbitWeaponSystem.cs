using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Modes.Roguelike.Gameplay.Events;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  /// <summary>
  /// Manages all orbit weapons: creation, rotation, damage + splash,
  /// spirit blade launch/return scheduling.
  /// Extracted from the old monolithic WarriorController.
  /// </summary>
  public sealed class OrbitWeaponSystem
  {
    readonly List<OrbitWeaponController> _weapons = new();
    readonly Dictionary<Health, float> _nextHitTimes = new();

    GameObject _player;
    Transform _root;
    WarriorContext _ctx;
    float _orbitAngle;
    float _spiritLaunchTimer;
    float _spiritLaunchInterval = 1.5f;
    int _spiritCooldownGroup;
    float _dashBladeWindowUntil;
    EventListenerHandle _dashEndedHandle;
    float _lastRotationSpeed;
    int _lastWeaponCount;
    float _resonanceTimer;

    public WarriorContext Context => _ctx;

    public OrbitWeaponSystem(GameObject player, WarriorContext ctx)
    {
      _player = player;
      _ctx = ctx;
      _lastRotationSpeed = ctx.RotationSpeed;
      _lastWeaponCount = ctx.EffectiveWeaponCount;
      _root = new GameObject("WarriorOrbitSystem").transform;
      _root.SetParent(player.transform, false);
      _dashEndedHandle = GameEventBus.Subscribe<DashEndedEvent>(OnDashEnded);
    }

    /// <summary>Call when context changes (stats updated).</summary>
    public void Refresh(WarriorContext ctx)
    {
      var oldSpeed = _ctx.RotationSpeed;
      var oldCount = _ctx.EffectiveWeaponCount;
      _ctx = ctx;
      RebuildVisuals();
      if (_ctx.RotationSpeed > oldSpeed + 0.01f)
        GameEventBus.Publish(new TriggerActivatedEvent(
          "WarriorOrbitSpeedUp",
          _player != null ? _player.transform.position : Vector3.zero,
          _player,
          _ctx.Radius + _ctx.RangeExpansion));
      if (_ctx.EffectiveWeaponCount > oldCount)
      {
        for (var i = oldCount; i < _ctx.EffectiveWeaponCount && i < _weapons.Count; i++)
          AttachWeaponVfx(_weapons[i], i);
      }
      _lastRotationSpeed = _ctx.RotationSpeed;
      _lastWeaponCount = _ctx.EffectiveWeaponCount;
    }

    public void Shutdown()
    {
      foreach (var weapon in _weapons)
      {
        if (weapon.Visual != null && _root != null)
          weapon.Visual.SetParent(_root, true);
      }
      if (_dashEndedHandle.Valid)
        GameEventBus.Unsubscribe(_dashEndedHandle);
      if (_root != null)
        Object.Destroy(_root.gameObject);
      _weapons.Clear();
    }

    /// <summary>Per-frame update.</summary>
    public void Tick(float deltaTime)
    {
      RepairWeaponFlightStates();
      UpdateOrbit(deltaTime);
      ApplyBladeDamage();
      UpdateSpiritBlades(deltaTime);
      UpdateResonance(deltaTime);
    }

    // ── orbit ──

    void UpdateOrbit(float deltaTime)
    {
      _orbitAngle = Mathf.Repeat(
        _orbitAngle + _ctx.RotationSpeed * deltaTime, 360f);
      var radius = _ctx.Radius + _ctx.RangeExpansion;
      var wc = _ctx.EffectiveWeaponCount;

      for (var i = 0; i < _weapons.Count; i++)
      {
        var w = _weapons[i];
        var active = i < wc;
        if (w.Visual == null) continue;
        w.Visual.gameObject.SetActive(active);
        if (!active || w.IsFlying) continue;

        var angle = (_orbitAngle + 360f * i / wc) * Mathf.Deg2Rad;
        w.Visual.localPosition = new Vector3(
          Mathf.Cos(angle) * radius,
          Mathf.Sin(angle) * radius, 0f);
        w.Visual.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
      }
    }

    void RebuildVisuals()
    {
      var wc = _ctx.EffectiveWeaponCount;
      while (_weapons.Count < wc)
        CreateWeapon(_weapons.Count);

      for (var i = 0; i < _weapons.Count; i++)
      {
        var exists = i < wc;
        var v = _weapons[i].Visual;
        if (v == null && exists && !_weapons[i].IsFlying)
        {
          CreateWeapon(i);
          exists = true;
        }
        if (v != null)
        {
          v.gameObject.SetActive(exists);
          v.localScale = Vector3.one * _ctx.EffectiveWeaponSize;
          if (exists)
            AttachWeaponVfx(_weapons[i], i);
        }
      }
    }

    void CreateWeapon(int index)
    {
      while (_weapons.Count <= index)
        _weapons.Add(new OrbitWeaponController { Index = _weapons.Count });

      var blade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      blade.name = $"WarriorOrbit_{index}";
      blade.transform.SetParent(_root, false);
      var col = blade.GetComponent<Collider>();
      if (col != null) Object.Destroy(col);
      var ren = blade.GetComponent<Renderer>();
      if (ren != null) ren.material.color = Color.black;
      blade.transform.localScale = Vector3.one * _ctx.EffectiveWeaponSize;

      _weapons[index].Visual = blade.transform;
      _weapons[index].Index = index;
      AttachWeaponVfx(_weapons[index], index);
    }

    void AttachWeaponVfx(OrbitWeaponController weapon, int index)
    {
      if (weapon?.Visual == null)
        return;

      GameEventBus.Publish(new TriggerActivatedEvent(
        "WarriorOrbitAttach",
        weapon.Visual.position,
        weapon.Visual.gameObject,
        _ctx.Radius + _ctx.RangeExpansion,
        index,
        alternate: _ctx.TitanFlag));
    }

    // ── damage + splash ──

    void ApplyBladeDamage()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return;

      foreach (var weapon in _weapons)
      {
        if (weapon.IsFlying || weapon.Visual == null || !weapon.Visual.gameObject.activeSelf)
          continue;

        var center = GameplayPlane.Position2D(weapon.Visual);
        var scanRange = _ctx.EffectiveWeaponSize + _ctx.RangeExpansion;
        foreach (var enemy in registry.GetInRange(center, scanRange))
        {
          if (enemy == null) continue;
          var health = enemy.GetComponent<Health>();
          if (health == null || health.IsDead) continue;
          if (_nextHitTimes.TryGetValue(health, out var next) && Time.time < next)
            continue;

          _nextHitTimes[health] = Time.time + _ctx.HitInterval;
          DamagePipeline.Apply(
            DamageRequest.Direct(_ctx.EffectiveDamage, "physical", "warrior_orbit", _player),
            health);
          GameEventBus.Publish(new TriggerActivatedEvent(
            "WarriorOrbitHit",
            enemy.transform.position,
            weapon.Visual.gameObject,
            _ctx.EffectiveWeaponSize,
            alternate: _ctx.TitanFlag || _ctx.MeleeKnockbackChance > 0f));
          ApplyKnockback(enemy, weapon.Visual.position);

          // Death Swarm splash (satellite line)
          var splash = _ctx.OrbitSplashRatio;
          if (splash > 0f)
          {
            foreach (var nearby in registry.GetInRange(GameplayPlane.Position2D(enemy.transform), 1.5f))
            {
              if (nearby == null || nearby == enemy) continue;
              var h = nearby.GetComponent<Health>();
              if (h == null || h.IsDead) continue;
              DamagePipeline.Apply(
                DamageRequest.Direct(_ctx.EffectiveDamage * splash, "physical", "warrior_orbit_splash", _player),
                h);
            }
          }
        }
      }
    }

    // ── spirit blade ──

    void ApplyKnockback(EnemyCore enemy, Vector3 impactOrigin)
    {
      if (_ctx.MeleeKnockbackChance <= 0f || Random.value > _ctx.MeleeKnockbackChance)
        return;

      var movement = enemy.GetComponent<EnemyMovement>();
      if (movement == null)
        return;

      var enemyPosition = GameplayPlane.Position2D(enemy.transform);
      var impactPosition = new Vector2(impactOrigin.x, impactOrigin.y);
      var velocity = movement.GetKnockbackDirection(enemyPosition, impactPosition);
      movement.ApplyHitStun();
      movement.ApplyKnockback(velocity * _ctx.EffectiveKnockbackMultiplier);
    }

    void UpdateSpiritBlades(float deltaTime)
    {
      if (!_ctx.SpiritEnabled) return;
      if (IsUnifiedDashBladeMode() && Time.time > _dashBladeWindowUntil) return;

      // Launch timer
      _spiritLaunchTimer -= deltaTime;
      if (_spiritLaunchTimer > 0f) return;

      if (_ctx.SpiritInfinite)
        _spiritLaunchInterval = 0.5f;

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return;

      // Launch N blades
      var launchCount = _ctx.SpiritLaunchCount + 1; // +1 for the base blade
      if (_ctx.SpiritLaunchCount >= 99) launchCount = _ctx.EffectiveWeaponCount; // blade_storm → all weapons
      launchCount = Mathf.Min(launchCount, _ctx.EffectiveWeaponCount);

      var reserveOrbiting = ShouldReserveOrbitingWeapons() ? 1 : 0;
      var orbitingCount = CountOrbitingWeapons();
      var maxLaunch = Mathf.Min(launchCount, orbitingCount - reserveOrbiting);
      if (maxLaunch <= 0)
      {
        _spiritLaunchTimer = Mathf.Min(_spiritLaunchInterval, 0.35f);
        return;
      }

      _spiritLaunchTimer = _spiritLaunchInterval;

      var launched = 0;
      var reservedTargets = new HashSet<EnemyCore>();
      for (var i = 0; i < _weapons.Count && launched < maxLaunch; i++)
      {
        var weapon = _weapons[i];
        if (!weapon.CanLaunch) continue;
        if (weapon.Visual == null) continue;

        EnemyCore target = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in registry.AllEnemies)
        {
          if (candidate == null || reservedTargets.Contains(candidate) || candidate.Health == null || candidate.Health.IsDead)
            continue;
          var distance = (candidate.transform.position - weapon.Visual.position).sqrMagnitude;
          if (distance < bestDistance)
          {
            bestDistance = distance;
            target = candidate;
          }
        }

        if (target == null)
          target = registry.GetNearest(GameplayPlane.Position2D(weapon.Visual));
        if (target == null) continue;
        reservedTargets.Add(target);

        weapon.State = BladeState.Launch;
        weapon.ActiveProjectile = OrbitWeaponProjectile.Launch(
          weapon.Visual,
          target.transform,
          _player,
          _ctx,
          _root,
          () => _root != null ? _root.TransformPoint(GetOrbitLocalPosition(weapon.Index)) : weapon.Visual.position,
          () => RestoreWeaponToOrbit(weapon));
        launched++;
      }

      // Clean up old hit entries periodically
      if (Time.frameCount % 300 == 0)
        _nextHitTimes.Clear(); // simplest cleanup, avoids memory leak
    }

    void OnDashEnded(DashEndedEvent evt)
    {
      if (_player == null || evt.Player != _player || !_ctx.SpiritEnabled)
        return;

      _dashBladeWindowUntil = Time.time + (_ctx.SpiritInfinite ? 1.05f : 0.65f);
      _spiritLaunchTimer = Mathf.Min(_spiritLaunchTimer, 0f);
    }

    static bool IsUnifiedDashBladeMode() =>
      Game.Modes.Roguelike.Build.Runtime.RunBuildState.WeaponTheme == Game.Modes.Roguelike.Progression.UnifiedBuildBootstrap.WeaponTheme
      && Game.Modes.Roguelike.Build.Progression.BuildProgressionState.HasMechanic("dash_melee");

    void UpdateResonance(float deltaTime)
    {
      if (_ctx.EffectiveWeaponCount < 5 || _player == null)
        return;

      _resonanceTimer -= deltaTime;
      if (_resonanceTimer > 0f)
        return;

      _resonanceTimer = Random.Range(0.18f, 0.42f);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "WarriorOrbitResonance",
        _player.transform.position,
        _player,
        _ctx.Radius + _ctx.RangeExpansion));
    }

    void RestoreWeaponToOrbit(OrbitWeaponController weapon)
    {
      if (weapon == null)
        return;

      weapon.ActiveProjectile = null;
      weapon.State = BladeState.Orbit;
      if (weapon.Visual == null)
        return;

      weapon.Visual.SetParent(_root, false);
      weapon.Visual.localScale = Vector3.one * _ctx.EffectiveWeaponSize;
      weapon.Visual.localPosition = GetOrbitLocalPosition(weapon.Index);
      var toWeapon = weapon.Visual.localPosition;
      if (toWeapon.sqrMagnitude > 0.0001f)
        weapon.Visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toWeapon.y, toWeapon.x) * Mathf.Rad2Deg + 90f);
      weapon.Visual.gameObject.SetActive(true);
    }

    Vector3 GetOrbitLocalPosition(int index)
    {
      var count = Mathf.Max(1, _ctx.EffectiveWeaponCount);
      var radius = _ctx.Radius + _ctx.RangeExpansion;
      var angle = (_orbitAngle + 360f * index / count) * Mathf.Deg2Rad;
      return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
    }

    bool ShouldReserveOrbitingWeapons()
    {
      if (_ctx.SpiritInfinite || _ctx.SpiritLaunchCount >= 99)
        return false;
      return _ctx.EffectiveWeaponCount >= 2;
    }

    int CountOrbitingWeapons()
    {
      var count = 0;
      foreach (var weapon in _weapons)
      {
        if (weapon?.Visual == null || !weapon.Visual.gameObject.activeSelf)
          continue;
        if (!weapon.IsFlying)
          count++;
      }
      return count;
    }

    void RepairWeaponFlightStates()
    {
      foreach (var weapon in _weapons)
      {
        if (weapon == null)
          continue;

        if (weapon.ActiveProjectile == null && weapon.State != BladeState.Orbit)
          weapon.State = BladeState.Orbit;
      }
    }

#if UNITY_EDITOR
    public void DrawDebugGizmos()
    {
      if (_weapons == null)
        return;

      foreach (var weapon in _weapons)
      {
        if (weapon == null || weapon.Visual == null || weapon.ActiveProjectile != null)
          continue;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(weapon.Visual.position, 0.14f);
        Handles.Label(
          weapon.Visual.position + Vector3.up * 0.35f,
          $"Spirit Blade\nState: Orbit\nEnemy: -\nPlayer: 0.0");
      }
    }
#endif
  }
}
