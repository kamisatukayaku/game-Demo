using Game.Shared.Core;
using UnityEngine;
using Game.Modes.Roguelike.Build.Runtime;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  static class DetachedWeaponCombatQuery
  {
    public static bool HasLivingEnemyInRange(Vector2 origin, float range)
    {
      if (range <= 0f)
        return false;

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return false;

      foreach (var enemy in registry.GetInRange(origin, range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health != null && !health.IsDead)
          return true;
      }

      return false;
    }
  }

  public readonly struct DetachedWeaponRuntimeContext
  {
    public readonly GameObject Owner;
    public readonly Transform Weapon;
    public readonly DetachedWeaponDefinition Definition;

    public string EvolutionId => Definition == null || string.IsNullOrEmpty(Definition.id)
      ? "contact"
      : Definition.id.Replace("_weapon", string.Empty);

    public float Damage(float baseDamage) => baseDamage
      * (1f + RunBuildState.GetStat("detached_contact_level") * 0.15f
            + RunBuildState.GetStat("detached_part_damage_mult")
            + RunBuildState.GetStat($"detached_{EvolutionId}_damage_mult")
            + RunBuildState.GetStat("all_damage_mult"));

    public float WanderSpeed(float baseSpeed) => baseSpeed
      * (1f + RunBuildState.GetStat("detached_contact_orbit_speed_mult"));

    public float WanderRadius(float baseRadius) => baseRadius
      * (1f + RunBuildState.GetStat("detached_contact_radius_mult"));

    public float Scale(float baseValue, string suffix) => baseValue
      * (1f + RunBuildState.GetStat($"detached_{EvolutionId}_{suffix}_mult"));

    public float ReducedCooldown(float baseValue) => baseValue
      * (1f - Mathf.Clamp(RunBuildState.GetStat($"detached_{EvolutionId}_cooldown_reduce"), 0f, 0.75f));

    public DetachedWeaponRuntimeContext(
      GameObject owner,
      Transform weapon,
      DetachedWeaponDefinition definition)
    {
      Owner = owner;
      Weapon = weapon;
      Definition = definition;
    }
  }

  static class DetachedWeaponMotion
  {
    public static void TrackOwnerDelta(ref Vector2 anchor, Vector2 ownerPos, ref Vector2 lastOwnerPos, bool hasLastOwnerPos)
    {
      if (hasLastOwnerPos)
        anchor += ownerPos - lastOwnerPos;
      lastOwnerPos = ownerPos;
    }

    public static Vector2 SmoothFollow(
      Vector2 current,
      Vector2 target,
      ref Vector2 velocity,
      float deltaTime,
      float smoothTime = 0.07f,
      float maxSpeed = 28f)
    {
      if ((current - target).sqrMagnitude < 0.00001f)
      {
        velocity = Vector2.zero;
        return target;
      }

      return Vector2.SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed, deltaTime);
    }

    public static Vector2 SmoothOrbit(Vector2 current, Vector2 target, ref Vector2 velocity, float deltaTime) =>
      SmoothFollow(current, target, ref velocity, deltaTime, 0.055f, 36f);

    public static Vector2 SmoothWander(
      Vector2 current,
      Vector2 target,
      ref Vector2 velocity,
      float deltaTime,
      float wanderSpeed) =>
      SmoothFollow(
        current,
        target,
        ref velocity,
        deltaTime,
        Mathf.Clamp(0.18f - wanderSpeed * 0.012f, 0.07f, 0.16f),
        wanderSpeed * 1.35f);
  }
}
