using UnityEngine;
using Game.Shared.Gameplay.Reflect;

namespace Game.Shared.Gameplay.Reflect
{
  /// <summary>玩家护盾反弹门（DamagePipeline / 激光裁剪调用，不依?Roguelike 类型）?/summary>
  public interface IPlayerReflectGate
  {
    bool TryFullyBlockIncoming(
      GameObject player,
      GameObject attacker,
      float incomingDamage,
      out float reflectedBaseDamage);

    bool TryClipEnemyLaserAtShield(
      GameObject player,
      Vector3 beamOrigin,
      Vector3 beamDir,
      float beamDist,
      out Vector3 clipPoint);
  }

  public static class PlayerReflectGateLocator
  {
    static IPlayerReflectGate s_gate;

    public static IPlayerReflectGate Gate => s_gate;

    public static void Register(IPlayerReflectGate gate) => s_gate = gate;

    public static void Clear() => s_gate = null;
  }
}