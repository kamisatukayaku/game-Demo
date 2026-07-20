using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>
  /// Applies level_up_config.offer_config.auxiliary_offer_chance for ranged builds:
  /// when eligible auxiliary_shot upgrades exist, may inject one into the offer.
  /// </summary>
  public static class AuxiliaryOfferPolicy
  {
    public static float Chance
    {
      get
      {
        LevelUpChoiceDatabase.EnsureLoaded();
        return LevelUpChoiceDatabase.AuxiliaryOfferChance;
      }
    }

    public static bool TryInjectAuxiliaryUpgrade(
      System.Random rng,
      string weaponTheme,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      List<LevelUpChoiceDatabase.UpgradeDef> numericPool,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (picked == null || pickedIds == null || numericPool == null || rng == null)
        return false;

      if (!IsAuxiliaryEligibleTheme(weaponTheme))
        return false;

      var chance = Chance;
      if (chance <= 0f || rng.NextDouble() > chance)
        return false;

      LevelUpChoiceDatabase.UpgradeDef best = null;
      var bestWeight = 0f;
      foreach (var def in numericPool)
      {
        if (def == null || string.IsNullOrEmpty(def.id) || pickedIds.Contains(def.id))
          continue;
        if (!HasAuxiliaryShotTag(def))
          continue;
        if (UpgradeEligibilityRules.IsBlockedByPickHistory(def, weightContext.PickStacks))
          continue;
        if (!UpgradeEligibilityRules.MeetsUpgradeRequirements(def, weightContext.PickStacks))
          continue;

        var weight = UpgradeOfferWeightPolicy.ComputeWeight(def, weightContext);
        if (weight > bestWeight)
        {
          bestWeight = weight;
          best = def;
        }
      }

      if (best == null)
        return false;

      for (var i = picked.Count - 1; i >= 0; i--)
      {
        var existing = picked[i];
        if (existing != null && HasAuxiliaryShotTag(existing))
          picked.RemoveAt(i);
      }

      if (picked.Count >= LevelUpChoiceDatabase.ChoicesPerLevel)
        picked.RemoveAt(picked.Count - 1);

      picked.Add(best);
      pickedIds.Add(best.id);
      UpgradeOfferBuildTelemetry.RecordAuxiliaryInjection();
      return true;
    }

    static bool IsAuxiliaryEligibleTheme(string weaponTheme)
    {
      if (string.Equals(weaponTheme, "ranged", System.StringComparison.OrdinalIgnoreCase)
          || string.Equals(weaponTheme, "warrior", System.StringComparison.OrdinalIgnoreCase))
        return true;

      return string.Equals(weaponTheme, UnifiedBuildBootstrap.WeaponTheme, System.StringComparison.OrdinalIgnoreCase)
        && RunBuildState.HasTag("projectile");
    }

    static bool HasAuxiliaryShotTag(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def?.tags == null)
        return false;
      foreach (var tag in def.tags)
        if (tag == "auxiliary_shot")
          return true;
      return false;
    }
  }
}
