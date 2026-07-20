using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>
  /// Weighted unique random selection for upgrade offers. Caller supplies <see cref="Random"/> for determinism.
  /// </summary>
  public static class UpgradeWeightedPicker
  {
    public static void PickWeightedUnique(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> source,
      int count,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      if (source == null || count <= 0)
        return;

      var working = new List<LevelUpChoiceDatabase.UpgradeDef>();
      foreach (var item in source)
      {
        if (item == null || string.IsNullOrEmpty(item.id) || pickedIds.Contains(item.id))
          continue;
        working.Add(item);
      }

      while (count > 0 && working.Count > 0)
      {
        var totalWeight = 0f;
        foreach (var item in working)
          totalWeight += UpgradeOfferWeightPolicy.ComputeWeight(item, weightContext);

        var roll = (float)random.NextDouble() * Mathf.Max(0.001f, totalWeight);
        var selectedIndex = working.Count - 1;
        for (var i = 0; i < working.Count; i++)
        {
          roll -= UpgradeOfferWeightPolicy.ComputeWeight(working[i], weightContext);
          if (roll <= 0f)
          {
            selectedIndex = i;
            break;
          }
        }

        var choice = working[selectedIndex];
        working.RemoveAt(selectedIndex);
        picked.Add(choice);
        pickedIds.Add(choice.id);
        count--;
      }
    }
  }
}
