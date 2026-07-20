using System.Collections.Generic;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Guarantees foundation/mechanic choices during early unified-build levels.</summary>
  public static class FoundationOfferPolicy
  {
    public static void EnsureEarlyBuildChoices(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      List<LevelUpChoiceDatabase.UpgradeDef> all,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (picked == null || picked.Count == 0 || all == null || ExperienceSystem.Level > 5)
        return;

      var hasFoundation = false;
      var allNumeric = true;
      foreach (var def in picked)
      {
        if (IsFoundationOrMechanic(def))
        {
          hasFoundation = true;
          allNumeric = false;
          break;
        }

        if (UpgradeOfferGroupPolicy.Resolve(def) != UpgradeOfferGroup.Numeric)
          allNumeric = false;
      }

      if (hasFoundation && !allNumeric)
        return;

      var foundationPool = all.FindAll(def =>
        def != null
        && !string.IsNullOrEmpty(def.id)
        && !pickedIds.Contains(def.id)
        && IsFoundationOrMechanic(def));

      if (foundationPool.Count == 0)
        return;

      if (hasFoundation && allNumeric)
      {
        for (var i = picked.Count - 1; i >= 0; i--)
        {
          if (UpgradeOfferGroupPolicy.Resolve(picked[i]) != UpgradeOfferGroup.Numeric)
            continue;
          pickedIds.Remove(picked[i].id);
          picked.RemoveAt(i);
          break;
        }
      }
      else if (!hasFoundation && picked.Count >= 1)
      {
        var replaceIndex = -1;
        for (var i = picked.Count - 1; i >= 0; i--)
        {
          if (UpgradeOfferGroupPolicy.Resolve(picked[i]) == UpgradeOfferGroup.Numeric)
          {
            replaceIndex = i;
            break;
          }
        }

        if (replaceIndex < 0)
          replaceIndex = picked.Count - 1;

        pickedIds.Remove(picked[replaceIndex].id);
        picked.RemoveAt(replaceIndex);
      }

      UpgradeWeightedPicker.PickWeightedUnique(
        random,
        foundationPool,
        1,
        picked,
        pickedIds,
        weightContext);
    }

    static bool IsFoundationOrMechanic(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;
      if (def.introduces_mechanic)
        return true;
      if (def.tags == null)
        return false;
      foreach (var tag in def.tags)
      {
        if (tag == "foundation" || tag == "mechanic")
          return true;
      }

      return false;
    }
  }
}
