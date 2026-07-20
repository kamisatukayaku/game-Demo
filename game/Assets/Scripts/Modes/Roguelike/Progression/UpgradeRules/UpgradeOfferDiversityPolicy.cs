using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>
  /// Enforces chain-continuation diversity in level-up offers (avoids all picks being chain continuations).
  /// </summary>
  public static class UpgradeOfferDiversityPolicy
  {
    public static void EnsureOfferDiversity(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      List<LevelUpChoiceDatabase.UpgradeDef> all,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (picked == null || picked.Count < 2)
        return;

      var chainCount = 0;
      foreach (var def in picked)
      {
        if (UpgradeOfferWeightPolicy.HasPickedRequirement(def, weightContext.PickStacks))
          chainCount++;
      }

      if (chainCount < picked.Count)
        return;

      LevelUpChoiceDatabase.UpgradeDef removed = null;
      UpgradeOfferGroup removedGroup = UpgradeOfferGroup.Gameplay;
      for (var i = picked.Count - 1; i >= 0; i--)
      {
        if (!UpgradeOfferWeightPolicy.HasPickedRequirement(picked[i], weightContext.PickStacks))
          continue;

        removed = picked[i];
        removedGroup = UpgradeOfferGroupPolicy.Resolve(removed);
        picked.RemoveAt(i);
        pickedIds.Remove(removed.id);
        break;
      }

      if (removed == null)
        return;

      var alternatives = all.FindAll(def =>
        def != null
        && !string.IsNullOrEmpty(def.id)
        && !pickedIds.Contains(def.id)
        && !UpgradeOfferWeightPolicy.HasPickedRequirement(def, weightContext.PickStacks)
        && UpgradeOfferGroupPolicy.Resolve(def) == removedGroup);

      if (alternatives.Count == 0)
        return;

      UpgradeWeightedPicker.PickWeightedUnique(random, alternatives, 1, picked, pickedIds, weightContext);
    }
  }
}
