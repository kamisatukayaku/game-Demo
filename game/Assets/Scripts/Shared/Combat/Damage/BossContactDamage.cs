using UnityEngine;
using PlayerHealth = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Enemy.AI;

namespace Game.Shared.Combat.Damage
{
  /// <summary>Single-hit boss melee contact with brief player i-frames and contact interval.</summary>
  public static class BossContactDamage
  {
    public const float DefaultPostHitIFrame = 0.38f;

    public static void ApplyPlayerMeleeHit(
      GameObject boss,
      Transform player,
      float damage,
      string damageType,
      string sourceId,
      float maxHpFraction = BossPlayerDamageRules.DefaultMaxHpFraction,
      float postHitIFrame = DefaultPostHitIFrame,
      int attackInstanceId = 0)
    {
      if (boss == null || player == null)
        return;

      var health = player.GetComponent<PlayerHealth>();
      if (health == null || health.IsDead || health.IsInvulnerable)
        return;

      var interval = BossBalanceDatabase.Defaults.contact_hit_interval_sec;
      if (attackInstanceId > 0)
      {
        if (!BossAttackHitTracker.TryInstantHit(attackInstanceId, player.gameObject.GetInstanceID()))
          return;
      }
      else if (!BossAttackHitTracker.TryContactHit(
                 boss.GetInstanceID(),
                 player.gameObject.GetInstanceID(),
                 interval))
      {
        return;
      }

      var capped = BossPlayerDamageRules.CapSingleHit(damage, health, maxHpFraction);
      var result = DamagePipeline.Apply(
        DamageRequest.Direct(capped, damageType, sourceId, boss),
        health);

      if (result.FinalDamage > 0f)
      {
        health.GrantInvulnerability(postHitIFrame);
        BossCombatDebugLog.ReportPlayerHit(result.FinalDamage, sourceId);
      }
    }
  }
}
