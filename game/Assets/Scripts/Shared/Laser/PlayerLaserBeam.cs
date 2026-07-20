using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Enemy.AI;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 玩家激光：直线贯穿路径上所有敌人，攻速慢；短暂显示光束?
  /// </summary>
  public static class PlayerLaserBeam
  {
    public static void Fire(
      Vector3 origin,
      Transform aimTarget,
      float maxRange,
      in DamageRequest request,
      float beamHalfWidth,
      Color beamColor,
      float visualDuration = 0.14f)
    {
      var direction = ResolveDirection(origin, aimTarget);
      DamageEnemiesOnBeam(origin, direction, maxRange, request, beamHalfWidth);
      SpawnBeamVisual(origin, direction, maxRange, beamHalfWidth, beamColor, visualDuration);
    }

    static Vector3 ResolveDirection(Vector3 origin, Transform aimTarget)
    {
      if (aimTarget == null)
        return Vector3.right;

      var dir = aimTarget.position - origin;
      dir.z = 0f;
      return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
    }

    static void DamageEnemiesOnBeam(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      in DamageRequest request,
      float beamHalfWidth)
    {
      var damaged = new HashSet<int>();
      var enemies = Object.FindObjectsOfType<EnemyCore>();

      foreach (var enemy in enemies)
      {
        if (enemy == null)
          continue;

        var id = enemy.GetInstanceID();
        if (damaged.Contains(id))
          continue;

        if (!IsOnBeam(origin, direction, maxRange, beamHalfWidth, enemy.transform.position))
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        DamagePipeline.Apply(request, health);
        damaged.Add(id);
      }
    }

    public static bool IsOnBeam(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      float beamHalfWidth,
      Vector3 point)
    {
      var offset = point - origin;
      offset.z = 0f;
      var along = Vector3.Dot(offset, direction);
      if (along < 0f || along > maxRange)
        return false;

      var perpendicular = offset - direction * along;
      return perpendicular.magnitude <= beamHalfWidth;
    }

    static void SpawnBeamVisual(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      float beamHalfWidth,
      Color color,
      float duration)
    {
      // 使用粒子光束效果替代 Cube 矩形长条
      LaserBeamParticleEffect.Spawn(origin, direction, maxRange, beamHalfWidth, color, duration);
    }

  }
}
