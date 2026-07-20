using System.Collections.Generic;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  sealed class ContactWeaponBehavior : IDetachedWeaponBehavior
  {
    readonly Dictionary<int, float> _hitTimes = new();
    DetachedWeaponRuntimeContext _context;
    float _angle;
    Vector2 _followVelocity;

    public DetachedWeaponAttackMode Mode => DetachedWeaponAttackMode.Contact;

    public void Initialize(in DetachedWeaponRuntimeContext context)
    {
      _context = context;
      var marker = context.Weapon.GetComponent<DetachedWeaponSlotMarker>();
      _angle = marker != null ? marker.OrbitAngleDegrees : Random.value * 360f;
      context.Weapon.GetComponent<DetachedWeaponVisualState>()?.ClearBeams();
    }

    public void Tick(float deltaTime)
    {
      if (_context.Owner == null || _context.Weapon == null)
        return;

      var def = _context.Definition;
      _angle += _context.Scale(def.orbit_speed, "orbit_speed") * deltaTime;
      var offset = (Vector2)(Quaternion.Euler(0f, 0f, _angle) * Vector2.right)
        * _context.Scale(def.orbit_radius, "radius");
      var center = GameplayPlane.Position2D(_context.Owner.transform);
      var targetPos = center + offset;
      var current = GameplayPlane.Position2D(_context.Weapon);
      var next = DetachedWeaponMotion.SmoothOrbit(current, targetPos, ref _followVelocity, deltaTime);
      GameplayPlane.SetPosition2D(_context.Weapon, next);

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var now = Time.time;
      var hitRadius = Mathf.Max(0.1f, _context.Scale(def.contact_radius, "contact_radius"));
      foreach (var enemy in registry.GetInRange(GameplayPlane.Position2D(_context.Weapon), hitRadius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var id = health.GetInstanceID();
        if (_hitTimes.TryGetValue(id, out var nextHit) && now < nextHit)
          continue;

        _hitTimes[id] = now + def.hit_cooldown;
        DamagePipeline.Apply(
          DamageRequest.Direct(_context.Damage(def.base_damage), "physical", "detached_contact", _context.Owner),
          health);
      }
    }

    public void Shutdown() => _hitTimes.Clear();
  }
}
