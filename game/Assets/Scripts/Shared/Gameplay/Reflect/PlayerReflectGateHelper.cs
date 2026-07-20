using UnityEngine;
using Game.Shared.Gameplay.Reflect;

namespace Game.Shared.Gameplay.Reflect
{
  /// <summary>Shared 层调用玩家反弹门的便捷入口?/summary>
  public static class PlayerReflectGateHelper
  {
    public static bool TryFullyBlockIncoming(
      GameObject player,
      GameObject attacker,
      float incomingDamage,
      out float reflectedBaseDamage)
    {
      reflectedBaseDamage = 0f;
      var gate = PlayerReflectGateLocator.Gate;
      return gate != null
             && gate.TryFullyBlockIncoming(player, attacker, incomingDamage, out reflectedBaseDamage);
    }

    public static bool TryClipEnemyLaserAtShield(
      GameObject player,
      Vector3 beamOrigin,
      Vector3 beamDir,
      float beamDist,
      out Vector3 clipPoint)
    {
      clipPoint = beamOrigin + beamDir.normalized * beamDist;
      var gate = PlayerReflectGateLocator.Gate;
      return gate != null
             && gate.TryClipEnemyLaserAtShield(player, beamOrigin, beamDir, beamDist, out clipPoint);
    }
  }
}