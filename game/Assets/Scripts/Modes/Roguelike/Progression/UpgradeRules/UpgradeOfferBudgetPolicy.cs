using System.Collections;
using System.Collections.Generic;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Group budget targets and slot planning for level-up offers.</summary>
  public static class UpgradeOfferBudgetPolicy
  {
    public readonly struct GroupTargets
    {
      public readonly float Gameplay;
      public readonly float Player;
      public readonly float Detached;
      public readonly float Numeric;

      public GroupTargets(float gameplay, float player, float detached, float numeric)
      {
        Gameplay = gameplay;
        Player = player;
        Detached = detached;
        Numeric = numeric;
      }
    }

    public static GroupTargets ResolveTargets(
      bool hasDetachedEntitlement,
      float gameplay,
      float player,
      float detached,
      float numeric)
    {
      if (hasDetachedEntitlement)
        return new GroupTargets(gameplay, player, detached, numeric);

      return new GroupTargets(0.50f, 0.30f, 0f, 0.20f);
    }

    public static List<UpgradeOfferGroup> BuildSlotPlan(
      System.Random random,
      int total,
      GroupTargets targets,
      bool forcePlayerPity,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools)
    {
      var plan = new List<UpgradeOfferGroup>(total);
      for (var i = 0; i < total; i++)
      {
        var weights = BuildPlanningWeights(targets);
        plan.Add(weights.Count > 0 ? RollGroup(random, weights) : UpgradeOfferGroup.Gameplay);
      }

      if (forcePlayerPity
          && !plan.Contains(UpgradeOfferGroup.Player)
          && HasPool(groupPools, UpgradeOfferGroup.Player))
      {
        ReplaceOneSlotWithPlayer(plan, targets);
        UpgradeOfferBuildTelemetry.RecordPityForced();
      }

      ShufflePlan(random, plan);
      return plan;
    }

    static void ReplaceOneSlotWithPlayer(List<UpgradeOfferGroup> plan, GroupTargets targets)
    {
      var replaceIndex = 0;
      var bestWeight = -1f;
      for (var i = 0; i < plan.Count; i++)
      {
        if (plan[i] == UpgradeOfferGroup.Player)
          continue;

        var weight = TargetWeight(plan[i], targets);
        if (weight > bestWeight)
        {
          bestWeight = weight;
          replaceIndex = i;
        }
      }

      plan[replaceIndex] = UpgradeOfferGroup.Player;
    }

    static float TargetWeight(UpgradeOfferGroup group, GroupTargets targets) => group switch
    {
      UpgradeOfferGroup.Gameplay => targets.Gameplay,
      UpgradeOfferGroup.Player => targets.Player,
      UpgradeOfferGroup.Detached => targets.Detached,
      UpgradeOfferGroup.Numeric => targets.Numeric,
      _ => 0f
    };

    static void ShufflePlan(System.Random random, List<UpgradeOfferGroup> plan)
    {
      for (var i = plan.Count - 1; i > 0; i--)
      {
        var j = random.Next(i + 1);
        (plan[i], plan[j]) = (plan[j], plan[i]);
      }
    }

    public static void FillDeficits(
      System.Random random,
      int total,
      List<LevelUpChoiceDatabase.UpgradeDef> picked,
      HashSet<string> pickedIds,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools,
      UpgradeOfferWeightPolicy.WeightContext weightContext,
      IReadOnlyList<UpgradeOfferGroup> slotPlan,
      GroupTargets targets,
      bool hasDetachedEntitlement)
    {
      if (picked.Count >= total || slotPlan == null || slotPlan.Count == 0)
        return;

      var planned = CountGroups(slotPlan);
      var actual = CountPickedGroups(picked);

      foreach (var group in DeficitOrder)
      {
        var missing = planned.TryGetValue(group, out var plannedCount)
          ? plannedCount - actual.GetValueOrDefault(group)
          : 0;
        for (var i = 0; i < missing && picked.Count < total; i++)
        {
          if (TryPickGroup(random, group, groupPools, picked, pickedIds, weightContext))
          {
            actual[group] = actual.GetValueOrDefault(group) + 1;
            continue;
          }

          var filled = false;
          foreach (var substitute in GetSubstitutes(group, hasDetachedEntitlement))
          {
            if (actual.GetValueOrDefault(substitute) >= planned.GetValueOrDefault(substitute))
              continue;
            if (!TryPickGroup(random, substitute, groupPools, picked, pickedIds, weightContext))
              continue;
            UpgradeOfferBuildTelemetry.RecordFallbackFill();
            actual[substitute] = actual.GetValueOrDefault(substitute) + 1;
            filled = true;
            break;
          }

          if (!filled)
            break;
        }
      }
    }

    static readonly UpgradeOfferGroup[] DeficitOrder =
    {
      UpgradeOfferGroup.Gameplay,
      UpgradeOfferGroup.Player,
      UpgradeOfferGroup.Detached,
      UpgradeOfferGroup.Numeric
    };

    static Dictionary<UpgradeOfferGroup, int> CountGroups(IReadOnlyList<UpgradeOfferGroup> slotPlan)
    {
      var counts = new Dictionary<UpgradeOfferGroup, int>();
      foreach (var group in slotPlan)
        counts[group] = counts.GetValueOrDefault(group) + 1;
      return counts;
    }

    static Dictionary<UpgradeOfferGroup, int> CountPickedGroups(
      IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> picked)
    {
      var counts = new Dictionary<UpgradeOfferGroup, int>();
      foreach (var def in picked)
      {
        if (def == null)
          continue;
        var group = UpgradeOfferGroupPolicy.Resolve(def);
        counts[group] = counts.GetValueOrDefault(group) + 1;
      }

      return counts;
    }

    static bool TryPickGroup(
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

    public static IEnumerable<UpgradeOfferGroup> GetBudgetSubstitutes(
      UpgradeOfferGroup missing,
      bool hasDetachedEntitlement) =>
      GetSubstitutes(missing, hasDetachedEntitlement);

    public static Dictionary<UpgradeOfferGroup, int> CountPlannedGroups(
      IReadOnlyList<UpgradeOfferGroup> slotPlan) =>
      CountGroups(slotPlan);

    public static Dictionary<UpgradeOfferGroup, int> CountActualGroups(
      IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> picked) =>
      CountPickedGroups(picked);

    static IEnumerable<UpgradeOfferGroup> GetSubstitutes(
      UpgradeOfferGroup missing,
      bool hasDetachedEntitlement)
    {
      if (!hasDetachedEntitlement)
      {
        switch (missing)
        {
          case UpgradeOfferGroup.Gameplay:
            yield return UpgradeOfferGroup.Numeric;
            yield return UpgradeOfferGroup.Player;
            yield break;
          case UpgradeOfferGroup.Player:
            yield return UpgradeOfferGroup.Gameplay;
            yield return UpgradeOfferGroup.Numeric;
            yield break;
          case UpgradeOfferGroup.Numeric:
            yield return UpgradeOfferGroup.Gameplay;
            yield return UpgradeOfferGroup.Player;
            yield break;
        }

        yield break;
      }

      switch (missing)
      {
        case UpgradeOfferGroup.Gameplay:
          yield return UpgradeOfferGroup.Numeric;
          yield return UpgradeOfferGroup.Player;
          yield return UpgradeOfferGroup.Detached;
          yield break;
        case UpgradeOfferGroup.Player:
          yield return UpgradeOfferGroup.Gameplay;
          yield return UpgradeOfferGroup.Numeric;
          yield return UpgradeOfferGroup.Detached;
          yield break;
        case UpgradeOfferGroup.Detached:
          yield return UpgradeOfferGroup.Gameplay;
          yield return UpgradeOfferGroup.Player;
          yield return UpgradeOfferGroup.Numeric;
          yield break;
        case UpgradeOfferGroup.Numeric:
          yield return UpgradeOfferGroup.Gameplay;
          yield return UpgradeOfferGroup.Player;
          yield return UpgradeOfferGroup.Detached;
          yield break;
      }
    }

    static List<(UpgradeOfferGroup group, float weight)> BuildPlanningWeights(GroupTargets targets)
    {
      var weights = new List<(UpgradeOfferGroup, float)>();
      TryAddPlanningWeight(weights, UpgradeOfferGroup.Gameplay, targets.Gameplay);
      TryAddPlanningWeight(weights, UpgradeOfferGroup.Player, targets.Player);
      TryAddPlanningWeight(weights, UpgradeOfferGroup.Detached, targets.Detached);
      TryAddPlanningWeight(weights, UpgradeOfferGroup.Numeric, targets.Numeric);

      if (weights.Count == 0)
        weights.Add((UpgradeOfferGroup.Gameplay, 1f));

      return weights;
    }

    static void TryAddPlanningWeight(
      List<(UpgradeOfferGroup group, float weight)> weights,
      UpgradeOfferGroup group,
      float target)
    {
      if (target <= 0f)
        return;
      weights.Add((group, Mathf.Max(0.01f, target)));
    }

    static List<(UpgradeOfferGroup group, float weight)> BuildActiveWeights(
      GroupTargets targets,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools)
    {
      var weights = new List<(UpgradeOfferGroup, float)>();
      TryAddWeight(weights, UpgradeOfferGroup.Gameplay, targets.Gameplay, groupPools);
      TryAddWeight(weights, UpgradeOfferGroup.Player, targets.Player, groupPools);
      TryAddWeight(weights, UpgradeOfferGroup.Detached, targets.Detached, groupPools);
      TryAddWeight(weights, UpgradeOfferGroup.Numeric, targets.Numeric, groupPools);

      if (weights.Count == 0)
        weights.Add((UpgradeOfferGroup.Gameplay, 1f));

      return weights;
    }

    static void TryAddWeight(
      List<(UpgradeOfferGroup group, float weight)> weights,
      UpgradeOfferGroup group,
      float target,
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools)
    {
      if (target <= 0f || !HasPool(groupPools, group))
        return;
      weights.Add((group, Mathf.Max(0.01f, target)));
    }

    static bool HasPool(
      IReadOnlyDictionary<UpgradeOfferGroup, List<LevelUpChoiceDatabase.UpgradeDef>> groupPools,
      UpgradeOfferGroup group) =>
      groupPools.TryGetValue(group, out var pool) && pool != null && pool.Count > 0;

    static UpgradeOfferGroup RollGroup(
      System.Random random,
      List<(UpgradeOfferGroup group, float weight)> weights)
    {
      var sum = 0f;
      foreach (var entry in weights)
        sum += entry.weight;

      var roll = (float)random.NextDouble() * sum;
      var chosen = weights[weights.Count - 1].group;
      foreach (var entry in weights)
      {
        roll -= entry.weight;
        if (roll <= 0f)
        {
          chosen = entry.group;
          break;
        }
      }

      return chosen;
    }
  }
}
