using UnityEngine;

using Game.Shared.Combat.Buff;
using HealthComp = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Combat.Damage
{
  /// <summary>
  /// 步骤 2：Buff 修正（outgoing / incoming）?
  /// 从攻击方和目标方?BuffContainer 读取聚合倍率?
  /// </summary>
  public static class BuffDamageModifiers
  {
    public static float GetOutgoingMultiplier(GameObject attacker)
    {
      if (attacker == null) return 1f;

      var container = attacker.GetComponent<BuffContainer>();
      if (container == null) return 1f;

      return container.GetStatModifier("attack");
    }

    public static float GetIncomingMultiplier(HealthComp target)
    {
      if (target == null) return 1f;

      var container = target.GetComponent<BuffContainer>();
      if (container == null) return 1f;

      return container.GetStatModifier("damage_taken");
    }
  }
}