using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  public static class UpgradeEligibilityRules
  {
    public static bool MeetsUpgradeRequirements(
      LevelUpChoiceDatabase.UpgradeDef definition,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (definition == null)
        return true;

      if (!HasAnyRequiredUpgrade(definition.requires_any_ids, pickStacks))
        return false;

      return HasAllRequiredUpgrades(definition.requires_ids, pickStacks);
    }

    public static bool IsBlockedByPickHistory(
      LevelUpChoiceDatabase.UpgradeDef definition,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (definition == null || pickStacks == null)
        return false;

      if (!pickStacks.TryGetValue(definition.id, out var stacks) || stacks <= 0)
        return false;

      if (definition.repeatable && definition.max_stacks > 1)
        return stacks >= definition.max_stacks;

      return true;
    }

    public static bool IsChainComplete(
      LevelUpChoiceDatabase.UpgradeDef definition,
      IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> source,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (definition == null || source == null || pickStacks == null || pickStacks.Count == 0)
        return false;

      if (string.IsNullOrEmpty(definition.exclusive_group))
        return false;

      LevelUpChoiceDatabase.UpgradeDef highestPicked = null;
      foreach (var upgrade in source)
      {
        if (upgrade == null
            || upgrade.exclusive_group != definition.exclusive_group
            || !pickStacks.TryGetValue(upgrade.id, out var stacks)
            || stacks <= 0)
          continue;

        if (highestPicked == null || upgrade.tier > highestPicked.tier)
          highestPicked = upgrade;
      }

      if (highestPicked == null)
        return false;

      foreach (var upgrade in source)
      {
        if (upgrade == null || upgrade.exclusive_group != definition.exclusive_group)
          continue;
        if (upgrade.tier > highestPicked.tier)
          return false;
      }

      return true;
    }

    public static bool MeetsStatPrerequisite(LevelUpChoiceDatabase.PrerequisiteDef prerequisite)
    {
      if (prerequisite == null || string.IsNullOrEmpty(prerequisite.stat))
        return true;

      var currentValue = RunBuildState.GetStat(prerequisite.stat);
      return prerequisite.op switch
      {
        "gte" => currentValue >= prerequisite.value,
        "gt" => currentValue > prerequisite.value,
        "lte" => currentValue <= prerequisite.value,
        "lt" => currentValue < prerequisite.value,
        "eq" => Mathf.Approximately(currentValue, prerequisite.value),
        "neq" => !Mathf.Approximately(currentValue, prerequisite.value),
        _ => true
      };
    }

    static bool HasAnyRequiredUpgrade(
      IReadOnlyList<string> requiredIds,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (requiredIds == null || requiredIds.Count == 0)
        return true;
      if (pickStacks == null)
        return false;

      foreach (var id in requiredIds)
      {
        if (!string.IsNullOrEmpty(id)
            && pickStacks.TryGetValue(id, out var count)
            && count > 0)
          return true;
      }

      return false;
    }

    static bool HasAllRequiredUpgrades(
      IReadOnlyList<string> requiredIds,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (requiredIds == null || requiredIds.Count == 0)
        return true;
      if (pickStacks == null)
        return false;

      foreach (var id in requiredIds)
      {
        if (string.IsNullOrEmpty(id))
          continue;
        if (!pickStacks.TryGetValue(id, out var count) || count <= 0)
          return false;
      }

      return true;
    }
  }
}
