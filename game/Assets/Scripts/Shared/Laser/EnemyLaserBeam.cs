using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 怪物激光入口（兼容?API）。新实现?<see cref="LaserEnemyAttack"/>?
  /// </summary>
  public static class EnemyLaserBeam
  {
    public static void Fire(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      in DamageRequest request,
      float beamHalfWidth,
      bool piercePlayer,
      Color beamColor,
      float visualDuration = 0.22f)
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return;

      var ownerGo = request.Attacker != null ? request.Attacker.transform : null;
      if (ownerGo == null)
      {
        // 无法确定发射者时退化为瞬时命中检测?
        if (PlayerLaserBeam.IsOnBeam(origin, direction, maxRange, beamHalfWidth, player.transform.position))
        {
          var health = player.GetComponent<Health>();
          if (health != null && !health.IsDead)
            DamagePipeline.Apply(request, health);
        }

        return;
      }

      var settings = LaserBeamSettings.FromProfile(
        beamColor,
        maxRange,
        beamHalfWidth,
        visualDuration,
        visualDuration * 0.5f);

      var attack = LaserBeamPool.Acquire();
      attack.Begin(ownerGo, player.transform, settings, request);
    }
  }
}