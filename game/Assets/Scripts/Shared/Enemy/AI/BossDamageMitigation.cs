using UnityEngine;
namespace Game.Shared.Enemy.AI
{
  /// <summary>Category-based AoE resistance for arena bosses — no flat 0.32 penalty.</summary>
  [DisallowMultipleComponent]
  public sealed class BossDamageMitigation : MonoBehaviour
  {
    string _lastSourceId;

    public static float ApplyIfBossTarget(GameObject target, float damage, string sourceId)
    {
      if (target == null || damage <= 0f)
        return damage;

      var mitigation = target.GetComponent<BossDamageMitigation>();
      if (mitigation == null)
        return damage;

      mitigation._lastSourceId = sourceId;
      return mitigation.ModifyIncomingDamage(damage);
    }

    public float ModifyIncomingDamage(float damage)
    {
      if (damage <= 0f)
        return damage;

      var mult = BossBalanceDatabase.GetAoeMultiplier(_lastSourceId);
      return mult >= 0.999f ? damage : damage * mult;
    }
  }
}
