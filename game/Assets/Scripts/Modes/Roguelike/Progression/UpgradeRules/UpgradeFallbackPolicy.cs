using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Safe offer fill when primary pools are exhausted.</summary>
  public static class UpgradeFallbackPolicy
  {
    static readonly UpgradeOfferGroup[] FallbackOrder =
    {
      UpgradeOfferGroup.Gameplay,
      UpgradeOfferGroup.Player,
      UpgradeOfferGroup.Detached,
      UpgradeOfferGroup.Numeric
    };

    public static bool TryPickFromGroupOnly(
      System.Random random,
      UpgradeOfferGroup group,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext) =>
      TryPickFromGroup(random, group, groupPools, picked, pickedIds, weightContext);

    public static bool TryPickOne(
      System.Random random,
      UpgradeOfferGroup preferred,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (TryPickFromGroup(random, preferred, groupPools, picked, pickedIds, weightContext))
        return true;

      foreach (var group in FallbackOrder)
      {
        if (group == preferred)
          continue;
        if (group == UpgradeOfferGroup.Detached && !DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())
          continue;
        if (TryPickFromGroup(random, group, groupPools, picked, pickedIds, weightContext))
          return true;
      }

      return TryPickFromRoutes(random, picked, pickedIds, weightContext);
    }

    static bool TryPickFromGroup(
      System.Random random,
      UpgradeOfferGroup group,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (!groupPools.TryGetValue(group, out var pool) || pool == null || pool.Count == 0)
        return false;

      var before = picked.Count;
      UpgradeWeightedPicker.PickWeightedUnique(random, pool, 1, picked, pickedIds, weightContext);
      return picked.Count > before;
    }

    public static bool TryPickFromRoutes(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      var theme = ArenaBuildBootstrap.SelectedBuildId switch
      {
        ArenaBuildBootstrap.Shooter => "ranged",
        ArenaBuildBootstrap.Contact => "warrior",
        _ => "mage"
      };

      return TryPickFromAllRoutes(random, theme, picked, pickedIds, weightContext);
    }

    public static bool TryPickFromAllRoutes(
      System.Random random,
      string weaponTheme,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      var before = picked.Count;
      var merged = new List<LevelUpChoiceDatabase.UpgradeDef>();

      foreach (var route in new[] { "player", "skill", "equipment" })
      {
        var pool = LevelUpChoiceDatabase.GetCandidates(
          route,
          weaponTheme,
          0,
          BuildProgressionState.SkillTier,
          BuildProgressionState.PlayerTier,
          RunBuildState.PickStacks,
          weaponTheme);
        if (pool == null || pool.Count == 0)
          continue;
        merged.AddRange(pool);
      }

      if (merged.Count == 0)
        return false;

      UpgradeWeightedPicker.PickWeightedUnique(random, merged, 1, picked, pickedIds, weightContext);
      return picked.Count > before;
    }

    public static bool TryPickExhaustionFallback(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      var before = picked.Count;
      var pool = LevelUpChoiceDatabase.GetExhaustionFallbackCandidates(RunBuildState.PickStacks);
      if (pool == null || pool.Count == 0)
        return false;

      UpgradeWeightedPicker.PickWeightedUnique(random, pool, 1, picked, pickedIds, weightContext);
      return picked.Count > before;
    }
  }
}
