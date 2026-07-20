using System;

using System.Collections.Generic;

using Game.Modes.Roguelike.Build.Stats;

using Game.Modes.Roguelike.Gameplay.DetachedWeapons;

using Game.Modes.Roguelike.Progression;

using UnityEngine;



namespace Game.Modes.Roguelike.Progression.UpgradeRules

{

  /// <summary>

  /// Computes final offer weights for level-up candidates (base weight, build tags, chain, capstone, detached weapon).

  /// </summary>

  public static class UpgradeOfferWeightPolicy

  {

    public readonly struct WeightContext

    {

      public readonly IReadOnlyDictionary<string, int> PickStacks;

      public readonly float GameplayWeight;

      public readonly float AttributeWeight;

      public readonly float BuildTagBonusPerStack;

      public readonly float ChainContinuationBonus;

      public readonly float CapstoneWeightBoost;

      public readonly bool CapstoneBoostActive;

      public readonly Func<string, int> TagStackProvider;

      public readonly Func<LevelUpChoiceDatabase.UpgradeDef, bool> CapstoneMatcher;



      public WeightContext(

        IReadOnlyDictionary<string, int> pickStacks,

        float gameplayWeight,

        float attributeWeight,

        float buildTagBonusPerStack,

        float chainContinuationBonus,

        float capstoneWeightBoost,

        bool capstoneBoostActive,

        Func<string, int> tagStackProvider,

        Func<LevelUpChoiceDatabase.UpgradeDef, bool> capstoneMatcher)

      {

        PickStacks = pickStacks;

        GameplayWeight = gameplayWeight;

        AttributeWeight = attributeWeight;

        BuildTagBonusPerStack = buildTagBonusPerStack;

        ChainContinuationBonus = chainContinuationBonus;

        CapstoneWeightBoost = capstoneWeightBoost;

        CapstoneBoostActive = capstoneBoostActive;

        TagStackProvider = tagStackProvider;

        CapstoneMatcher = capstoneMatcher;

      }

    }



    public static float ComputeWeight(LevelUpChoiceDatabase.UpgradeDef def, in WeightContext context)

    {

      if (def == null)

        return 0.001f;



      var weight = def.offer_weight > 0f ? def.offer_weight : 1f;

      weight *= IsPlayerBodyUpgrade(def) || IsNumericUpgrade(def)

        ? context.AttributeWeight

        : context.GameplayWeight;

      if (UpgradeOfferGroupPolicy.Resolve(def) == UpgradeOfferGroup.Gameplay)
        weight *= 1.25f;

      if (UpgradeOfferGroupPolicy.Resolve(def) == UpgradeOfferGroup.Detached)

        weight *= 1.15f;



      if (def.tags != null)

      {

        foreach (var tag in def.tags)

          weight += Mathf.Min(5, context.TagStackProvider?.Invoke(tag) ?? 0) * context.BuildTagBonusPerStack;

      }



      if (HasPickedRequirement(def, context.PickStacks))

        weight *= context.ChainContinuationBonus;



      if (HasUpgradeTag(def, "detached_weapon"))

      {

        if (HasUpgradeTag(def, "part_spawn")

            && !DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())

          weight *= 2.0f;

        else if (DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())

          weight *= 1.0f;

        else

          weight *= 0.35f;



        weight *= EvolutionBuildGatesDatabase.GetDetachedEvolutionWeightMultiplier(

          def,

          ArenaBuildBootstrap.SelectedBuildId,

          context.PickStacks);

      }



      if (HasUpgradeTag(def, "dash") && HasUpgradeTag(def, "melee"))

        weight *= 1.85f;



      weight *= context.CapstoneWeightBoost;

      if (context.CapstoneBoostActive && (context.CapstoneMatcher?.Invoke(def) ?? false))

        weight *= 1.8f;

      if (Game.Shared.Runtime.GameSessionConfig.IsBossRush)
        weight *= BossRush.BossRushUpgradeOfferPolicy.GetWeightMultiplier(def);

      return Mathf.Clamp(Mathf.Max(0.001f, weight), 0.001f, PlayerBuildCaps.MaxOfferWeight);

    }



    public static bool BuildHasDetachedWeaponFocus(LevelUpChoiceDatabase.UpgradeDef def)

    {

      if (def == null || !HasUpgradeTag(def, "detached_weapon"))

        return false;



      foreach (var tag in def.tags)

      {

        if (tag is "detached_weapon" or "part_spawn" or "contact" or "part_count" or "part_damage")

          return true;

      }



      return def.id != null && def.id.StartsWith("num_part_", StringComparison.Ordinal);

    }



    public static bool HasPickedRequirement(

      LevelUpChoiceDatabase.UpgradeDef def,

      IReadOnlyDictionary<string, int> pickStacks)

    {

      if (def == null || pickStacks == null)

        return false;



      if (def.requires_ids != null)

      {

        foreach (var id in def.requires_ids)

        {

          if (!string.IsNullOrEmpty(id) && pickStacks.TryGetValue(id, out var count) && count > 0)

            return true;

        }

      }



      if (def.requires_any_ids != null)

      {

        foreach (var id in def.requires_any_ids)

        {

          if (!string.IsNullOrEmpty(id) && pickStacks.TryGetValue(id, out var count) && count > 0)

            return true;

        }

      }



      return false;

    }



    public static bool IsGameplayUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>

      def != null && !IsNumericUpgrade(def) && !IsPlayerBodyUpgrade(def);



    public static bool IsPlayerBodyUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>
      def != null && (
        string.Equals(def.offer_group, "player", StringComparison.OrdinalIgnoreCase)
        || string.Equals(def.category, "player", StringComparison.OrdinalIgnoreCase));

    public static bool IsNumericUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>
      def != null && (
        string.Equals(def.offer_group, "numeric", StringComparison.OrdinalIgnoreCase)
        || string.Equals(def.category, "numeric", StringComparison.OrdinalIgnoreCase)
        || string.Equals(def.category, "attribute", StringComparison.OrdinalIgnoreCase));



    public static bool IsDetachedWeaponUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>

      def != null && HasUpgradeTag(def, "detached_weapon");



    static bool HasUpgradeTag(LevelUpChoiceDatabase.UpgradeDef def, string tag)

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

  }

}


