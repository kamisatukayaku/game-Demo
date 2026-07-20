using System;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Legacy ranged upgrade ids from old saves missing explicit tags.</summary>
  static class RangedLegacyUpgradeMigration
  {
    public static bool TryInferTags(string id, out string[] tags)
    {
      tags = null;
      if (string.IsNullOrEmpty(id) || !id.StartsWith("eq_ranged_", StringComparison.Ordinal))
        return false;

      if (id.StartsWith("eq_ranged_bh_", StringComparison.Ordinal))
      {
        tags = new[] { "projectile", "rapid", "spread" };
        return true;
      }

      if (id.StartsWith("eq_ranged_sn_", StringComparison.Ordinal))
      {
        tags = new[] { "projectile", "critical", "pierce" };
        return true;
      }

      if (id.StartsWith("eq_ranged_fr_", StringComparison.Ordinal))
      {
        tags = new[] { "projectile", "ice" };
        return true;
      }

      return false;
    }
  }
}
