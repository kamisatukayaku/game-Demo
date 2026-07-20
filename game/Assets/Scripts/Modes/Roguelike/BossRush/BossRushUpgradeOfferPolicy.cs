using Game.Modes.Roguelike.Progression;
using Game.Shared.Runtime;

namespace Game.Modes.Roguelike.BossRush
{
  /// <summary>Adjusts Boss Rush reward weights — de-emphasizes mob-farming upgrades unless minions are active.</summary>
  public static class BossRushUpgradeOfferPolicy
  {
    public static float GetWeightMultiplier(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (!GameSessionConfig.IsBossRush || def == null)
        return 1f;

      var encounter = BossRushEncounterRuntime.ActiveEncounter;
      var allowKillScaling = encounter != null && encounter.allow_minions && encounter.max_minions >= 4;
      var mult = 1f;

      if (UsesStat(def, "exp_gain_mult"))
        mult *= 0.05f;
      if (UsesStat(def, "xp_magnet_range_mult"))
        mult *= 0.05f;
      if (!allowKillScaling && (UsesStat(def, "heal_on_kill_flat") || UsesStat(def, "heal_on_kill_pct")))
        mult *= 0.08f;

      if (HasTag(def, "boss_reward"))
        mult *= 2.25f;
      if (HasTag(def, "risk_reward"))
        mult *= 1.4f;

      if (HasTag(def, "chain") || HasTag(def, "evolution") || HasTag(def, "capstone"))
        mult *= 1.35f;

      if (BossRushEncounterRuntime.IsNearFinalEncounter && IsGenericNumericBodyUpgrade(def))
        mult *= 0.45f;

      return mult;
    }

    static bool UsesStat(LevelUpChoiceDatabase.UpgradeDef def, string stat)
    {
      if (def?.modifiers == null || string.IsNullOrEmpty(stat))
        return false;

      foreach (var mod in def.modifiers)
      {
        if (mod != null && mod.stat == stat)
          return true;
      }

      return false;
    }

    static bool HasTag(LevelUpChoiceDatabase.UpgradeDef def, string tag)
    {
      if (def?.tags == null || string.IsNullOrEmpty(tag))
        return false;

      foreach (var t in def.tags)
      {
        if (t == tag)
          return true;
      }

      return false;
    }

    static bool IsGenericNumericBodyUpgrade(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;

      return def.id != null && def.id.StartsWith("num_player_")
             && !UsesStat(def, "dash_cooldown_reduction")
             && !UsesStat(def, "dash_invincible_time_add");
    }
  }
}
