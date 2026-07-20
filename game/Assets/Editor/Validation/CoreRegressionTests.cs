using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Shared.Combat.Buff;
using Game.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class CoreRegressionTests
  {
    const string MenuPath = "Tools/Validation/Run Core Regression Tests";
    const string SentinelKey = "CoreRegressionTests.MetaSentinel";

    readonly struct ProbeEvent : IGameEvent
    {
      public readonly int Value;

      public ProbeEvent(int value)
      {
        Value = value;
      }
    }

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("GameEventBus stable handles", TestStableEventHandles, failures);
      Run("Input reset preserves non-input data", TestInputResetIsolation, failures);
      Run("JSON source and Resources mirrors match", TestJsonMirrors, failures);
      Run("Straight projectile pool reuses and resets objects", TestStraightProjectilePool, failures);
      Run("Upgrade prerequisite chains remain deterministic", TestUpgradePrerequisites, failures);
      Run("Upgrade max-level ids excluded from offers", TestUpgradeMaxLevelFiltering, failures);
      Run("Upgrade weighted picker is seed-deterministic", TestUpgradeWeightedPickerDeterminism, failures);
      Run("Upgrade picker excludes already-used ids", TestUpgradePickerExcludesUsedIds, failures);
      Run("Upgrade weight increases with tag stacks", TestUpgradeTagStackWeightMonotonic, failures);
      Run("Upgrade capstone boost preserves multipliers", TestUpgradeCapstoneWeightBoost, failures);
      Run("Upgrade weight formula remains unchanged", TestUpgradeWeightFormula, failures);
      Run("Tutorial steps complete only once", TestTutorialStepCompletionOnce, failures);
      Run("Tutorial reset preserves unrelated PlayerPrefs", TestTutorialResetIsolation, failures);
      Run("Ground zone unknown id uses safe defaults", TestGroundZoneUnknownDefaults, failures);
      Run("Tutorial databases load without blocking", TestTutorialDatabaseLoad, failures);
      Run("Gameplay ground zone ids exist in JSON", TestGroundZoneGameplayIds, failures);
      Run("Detached weapon visual attaches once", TestDetachedWeaponVisualSingleAttach, failures);
      Run("Detached weapon presentation pools reset", TestDetachedWeaponPresentationPoolReset, failures);
      Run("Combat test fixtures isolated from production DB", TestCombatFixtureIsolation, failures);
      Run("Upgrade player pity resets on offer appearance", TestUpgradePlayerPityReset, failures);
      Run("Build Resources exclude combat test fixtures", TestResourcesExcludeCombatFixtures, failures);
      Run("GameEventBus ClearAll removes listeners", TestGameEventBusClearAll, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Core regression validation failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[CoreRegressionTests] PASS (22/22)");
    }

    public static void RunBatchAndQuit()
    {
      RunAll();
      EditorApplication.Exit(0);
    }

    static void Run(string name, Action test, ICollection<string> failures)
    {
      try
      {
        test();
        Debug.Log($"[CoreRegressionTests] PASS: {name}");
      }
      catch (Exception exception)
      {
        failures.Add($"{name}: {exception.Message}");
      }
    }

    static void TestStableEventHandles()
    {
      GameEventBus.ClearAll();
      var firstCalls = 0;
      var secondCalls = 0;

      var first = GameEventBus.Subscribe<ProbeEvent>(_ => firstCalls++);
      var second = GameEventBus.Subscribe<ProbeEvent>(_ => secondCalls++);
      GameEventBus.Unsubscribe(first);
      GameEventBus.Publish(new ProbeEvent(1));
      GameEventBus.Unsubscribe(second);
      GameEventBus.Publish(new ProbeEvent(2));
      GameEventBus.ClearAll();

      Require(firstCalls == 0, "removed listener was invoked");
      Require(secondCalls == 1, $"remaining listener invocation count was {secondCalls}");
    }

    static void TestInputResetIsolation()
    {
      GameInputBindings.EnsureLoaded();
      var bindings = new Dictionary<GameInputBindings.InputAction, KeyCode>();
      foreach (var action in GameInputBindings.RegisteredActions)
        bindings[action] = GameInputBindings.Get(action);

      var hadSentinel = PlayerPrefs.HasKey(SentinelKey);
      var oldSentinel = PlayerPrefs.GetString(SentinelKey, string.Empty);

      try
      {
        PlayerPrefs.SetString(SentinelKey, "keep");
        GameInputBindings.Set(GameInputBindings.InputAction.Attack, KeyCode.Space);
        GameInputBindings.ResetToDefaults();

        Require(PlayerPrefs.GetString(SentinelKey, string.Empty) == "keep",
          "ResetToDefaults removed unrelated PlayerPrefs data");
        Require(GameInputBindings.Get(GameInputBindings.InputAction.Attack) == KeyCode.Mouse0,
          "Attack binding did not return to its default");
      }
      finally
      {
        foreach (var pair in bindings)
          GameInputBindings.Set(pair.Key, pair.Value);

        if (hadSentinel)
          PlayerPrefs.SetString(SentinelKey, oldSentinel);
        else
          PlayerPrefs.DeleteKey(SentinelKey);
        PlayerPrefs.Save();
      }
    }

    static void TestJsonMirrors()
    {
      Require(DataSyncMenu.ValidateJsonMirrors(out var error), error);
    }

    static void TestStraightProjectilePool()
    {
      var request = DamageRequest.Direct(1f, "physical", "regression_test", null);
      var first = ProjectileFactory.SpawnEnemyStraight(
        Vector3.zero, null, request, 1f, 0.2f, Color.cyan, "PoolProbeA");
      var firstId = first.GetInstanceID();

      var despawn = typeof(StraightProjectile).GetMethod(
        "Despawn", BindingFlags.Instance | BindingFlags.NonPublic);
      Require(despawn != null, "StraightProjectile.Despawn was not found");
      despawn.Invoke(first, null);

      var expectedPosition = new Vector3(3f, 4f, 0f);
      var second = ProjectileFactory.SpawnEnemyStraight(
        expectedPosition, null, request, 1f, 0.45f, Color.red, "PoolProbeB");

      try
      {
        Require(second.GetInstanceID() == firstId, "pooled projectile instance was not reused");
        Require(second.transform.position == expectedPosition, "pooled projectile position was not reset");
        Require(second.transform.localScale == Vector3.one * 0.45f, "pooled projectile scale was not reset");
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(second.gameObject);
      }
    }

    static void TestUpgradePrerequisites()
    {
      var picked = new Dictionary<string, int>
      {
        ["root"] = 1,
        ["branch_b"] = 1
      };
      var definition = new LevelUpChoiceDatabase.UpgradeDef
      {
        requires_ids = new[] { "root" },
        requires_any_ids = new[] { "branch_a", "branch_b" }
      };

      Require(UpgradeEligibilityRules.MeetsUpgradeRequirements(definition, picked),
        "valid all/any prerequisite combination was rejected");
      picked.Remove("root");
      Require(!UpgradeEligibilityRules.MeetsUpgradeRequirements(definition, picked),
        "missing mandatory prerequisite was accepted");
      picked["root"] = 1;
      picked.Remove("branch_b");
      Require(!UpgradeEligibilityRules.MeetsUpgradeRequirements(definition, picked),
        "missing any-of prerequisite was accepted");
    }

    static void TestUpgradeMaxLevelFiltering()
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      ArenaBuildBootstrap.ConfigureForSimulation(ArenaBuildBootstrap.Mage);

      LevelUpChoiceDatabase.UpgradeDef firstTier = LevelUpChoiceDatabase.FindById("num_common_damage_01");

      Require(firstTier != null, "expected num_common_damage_01 in database");
      Require(RunBuildState.ApplyChoice(firstTier), "failed to apply first numeric tier");

      var offer = RunBuildState.GetPendingOffer();
      Require(offer.choices != null, "offer had no choices");
      foreach (var choice in offer.choices)
      {
        Require(choice == null || choice.id != "num_common_damage_01",
          "maxed non-repeatable tier reappeared in offer");
        if (choice != null)
          Require(!UpgradeEligibilityRules.IsBlockedByPickHistory(choice, RunBuildState.PickStacks),
            $"blocked upgrade appeared in offer: {choice.id}");
      }

      for (var level = 2; level <= 5; level++)
      {
        var tier = LevelUpChoiceDatabase.FindById($"num_common_damage_{level:00}");
        if (tier != null)
          RunBuildState.ApplyChoice(tier);
      }

      offer = RunBuildState.GetPendingOffer();
      foreach (var choice in offer.choices)
      {
        if (choice == null || string.IsNullOrEmpty(choice.id))
          continue;
        Require(!choice.id.StartsWith("num_common_damage_", System.StringComparison.Ordinal),
          $"completed damage chain tier reappeared: {choice.id}");
      }
    }

    static void TestUpgradeWeightedPickerDeterminism()
    {
      var pool = BuildTestUpgradePool();
      var weightContext = new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1f,
        false,
        _ => 0,
        _ => false);

      var first = PickIds(new System.Random(4242), pool, weightContext, 3);
      var second = PickIds(new System.Random(4242), pool, weightContext, 3);
      Require(first.Count == second.Count, "deterministic pick count mismatch");
      for (var i = 0; i < first.Count; i++)
        Require(first[i] == second[i], "deterministic pick order mismatch");
      Require(string.Join(",", first) == "test_e,test_c,test_d",
        "seed 4242 golden pick sequence drifted");
    }

    static void TestUpgradePickerExcludesUsedIds()
    {
      var pool = BuildTestUpgradePool();
      var originallyUsed = new HashSet<string> { "test_a", "test_b", "test_c" };
      var usedIds = new HashSet<string>(originallyUsed);
      var weightContext = new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1f,
        false,
        _ => 0,
        _ => false);
      var picked = new List<LevelUpChoiceDatabase.UpgradeDef>();
      UpgradeWeightedPicker.PickWeightedUnique(new System.Random(7), pool, 3, picked, usedIds, weightContext);
      Require(picked.Count == 2, $"picker should return the two unused upgrades, got {picked.Count}");
      foreach (var choice in picked)
        Require(choice != null && !originallyUsed.Contains(choice.id),
          "picker returned an upgrade that was already used");
    }

    static void TestUpgradeTagStackWeightMonotonic()
    {
      var tagStacks = new Dictionary<string, int>();
      var def = new LevelUpChoiceDatabase.UpgradeDef
      {
        offer_weight = 1f,
        category = "gameplay",
        tags = new[] { "arcane" }
      };
      var context = new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1f,
        false,
        tag => tagStacks.TryGetValue(tag, out var count) ? count : 0,
        _ => false);

      var baseline = UpgradeOfferWeightPolicy.ComputeWeight(def, context);
      tagStacks["arcane"] = 1;
      var afterOne = UpgradeOfferWeightPolicy.ComputeWeight(def, context);
      tagStacks["arcane"] = 2;
      var afterTwo = UpgradeOfferWeightPolicy.ComputeWeight(def, context);

      Require(afterOne > baseline, "tag stack increase did not raise offer weight");
      Require(afterTwo >= afterOne, "higher tag stack lowered offer weight");
    }

    static void TestUpgradeCapstoneWeightBoost()
    {
      var def = new LevelUpChoiceDatabase.UpgradeDef
      {
        offer_weight = 1f,
        category = "gameplay",
        tags = new[] { "detached_weapon" }
      };
      var baseline = UpgradeOfferWeightPolicy.ComputeWeight(def, new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1f,
        false,
        _ => 0,
        _ => true));
      var arcBoost = UpgradeOfferWeightPolicy.ComputeWeight(def, new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        2f,
        false,
        _ => 0,
        _ => true));
      var eventBoost = UpgradeOfferWeightPolicy.ComputeWeight(def, new UpgradeOfferWeightPolicy.WeightContext(
        new Dictionary<string, int>(),
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1f,
        true,
        _ => 0,
        _ => true));

      Require(Mathf.Approximately(arcBoost, baseline * 2f), "capstone arc boost multiplier drifted");
      Require(Mathf.Approximately(eventBoost, baseline * 1.8f), "capstone event boost multiplier drifted");
    }

    static void TestUpgradeWeightFormula()
    {
      var def = new LevelUpChoiceDatabase.UpgradeDef
      {
        id = "formula_probe",
        offer_weight = 2f,
        category = "gameplay",
        tags = new[] { "arcane", "detached_weapon" },
        requires_ids = new[] { "root" }
      };
      var picked = new Dictionary<string, int> { ["root"] = 1 };
      var tagStacks = new Dictionary<string, int> { ["arcane"] = 2 };
      var context = new UpgradeOfferWeightPolicy.WeightContext(
        picked,
        1.35f,
        0.28f,
        0.55f,
        2.4f,
        1.5f,
        true,
        tag => tagStacks.TryGetValue(tag, out var count) ? count : 0,
        _ => true);

      var actual = UpgradeOfferWeightPolicy.ComputeWeight(def, context);
      const float expected = 9.53694f;
      Require(Mathf.Abs(actual - expected) < 0.0001f,
        $"weight formula drifted: expected {expected}, actual {actual}");
    }

    static List<string> PickIds(
      System.Random random,
      List<LevelUpChoiceDatabase.UpgradeDef> pool,
      UpgradeOfferWeightPolicy.WeightContext weightContext,
      int count)
    {
      var picked = new List<LevelUpChoiceDatabase.UpgradeDef>();
      var usedIds = new HashSet<string>();
      UpgradeWeightedPicker.PickWeightedUnique(random, pool, count, picked, usedIds, weightContext);
      var ids = new List<string>();
      foreach (var def in picked)
        ids.Add(def.id);
      return ids;
    }

    static List<LevelUpChoiceDatabase.UpgradeDef> BuildTestUpgradePool()
    {
      return new List<LevelUpChoiceDatabase.UpgradeDef>
      {
        new LevelUpChoiceDatabase.UpgradeDef { id = "test_a", category = "gameplay", offer_weight = 1f },
        new LevelUpChoiceDatabase.UpgradeDef { id = "test_b", category = "attribute", offer_weight = 1f },
        new LevelUpChoiceDatabase.UpgradeDef { id = "test_c", category = "gameplay", offer_weight = 2f },
        new LevelUpChoiceDatabase.UpgradeDef { id = "test_d", category = "numeric", offer_weight = 1.5f },
        new LevelUpChoiceDatabase.UpgradeDef { id = "test_e", category = "gameplay", offer_weight = 0.8f }
      };
    }

    static void TestTutorialStepCompletionOnce()
    {
      const string sentinel = "CoreRegressionTests.TutorialSentinel";
      const string step = "move";
      var hadSentinel = PlayerPrefs.HasKey(sentinel);
      var oldSentinel = PlayerPrefs.GetString(sentinel, string.Empty);
      try
      {
        PlayerPrefs.SetString(sentinel, "keep");
        RoguelikeTutorialState.ResetAllKnown(new[] { step }, null);
        Require(!RoguelikeTutorialState.IsStepComplete(step), "fresh tutorial step was already complete");
        RoguelikeTutorialState.MarkStepComplete(step);
        Require(RoguelikeTutorialState.IsStepComplete(step), "tutorial step was not marked complete");
        RoguelikeTutorialState.MarkStepComplete(step);
        Require(RoguelikeTutorialState.IsStepComplete(step), "tutorial step completion regressed");
      }
      finally
      {
        RoguelikeTutorialState.ResetAllKnown(new[] { step }, null);
        if (hadSentinel)
          PlayerPrefs.SetString(sentinel, oldSentinel);
        else
          PlayerPrefs.DeleteKey(sentinel);
        PlayerPrefs.Save();
      }
    }

    static void TestTutorialResetIsolation()
    {
      const string sentinel = "CoreRegressionTests.TutorialSentinel";
      const string step = "dash";
      var hadSentinel = PlayerPrefs.HasKey(sentinel);
      var oldSentinel = PlayerPrefs.GetString(sentinel, string.Empty);
      try
      {
        PlayerPrefs.SetString(sentinel, "keep");
        PlayerPrefs.SetInt(RoguelikeTutorialState.KeyPrefix + "Step." + step, 1);
        PlayerPrefs.Save();
        RoguelikeTutorialState.ResetAllKnown(new[] { step }, new[] { "xp_boost_zone" });
        Require(PlayerPrefs.GetString(sentinel, string.Empty) == "keep",
          "tutorial reset removed unrelated PlayerPrefs data");
        Require(PlayerPrefs.GetInt(RoguelikeTutorialState.KeyPrefix + "Step." + step, 0) == 0,
          "tutorial step key was not cleared");
      }
      finally
      {
        RoguelikeTutorialState.ResetAllKnown(new[] { step }, new[] { "xp_boost_zone" });
        if (hadSentinel)
          PlayerPrefs.SetString(sentinel, oldSentinel);
        else
          PlayerPrefs.DeleteKey(sentinel);
        PlayerPrefs.Save();
      }
    }

    static void TestGroundZoneUnknownDefaults()
    {
      GroundZoneDefinitionDatabase.EnsureLoaded();
      var def = GroundZoneDefinitionDatabase.Get("not_a_real_zone_id");
      Require(def != null, "unknown zone definition was null");
      Require(!string.IsNullOrEmpty(def.displayName), "unknown zone display name missing");
      Require(!string.IsNullOrEmpty(def.description), "unknown zone description missing");
      Require(GroundZoneDefinitionDatabase.ParseType(def.type) == GroundZoneType.Neutral,
        "unknown zone type default was not neutral");
    }

    static void TestTutorialDatabaseLoad()
    {
      TutorialStepDatabase.EnsureLoaded();
      GroundZoneDefinitionDatabase.EnsureLoaded();
      Require(TutorialStepDatabase.TryGet("move", out var step), "tutorial step database missing move");
      Require(!string.IsNullOrEmpty(step.message), "move tutorial message missing");
      var zone = GroundZoneDefinitionDatabase.Get("xp_boost_zone");
      Require(zone != null && zone.displayName.Contains("经验"), "xp_boost_zone definition failed to load");
    }

    static void TestGroundZoneGameplayIds()
    {
      GroundZoneDefinitionDatabase.EnsureLoaded();
      foreach (var id in GroundZoneIds.GameplayPublished)
      {
        var def = GroundZoneDefinitionDatabase.Get(id);
        Require(def != null && def.id == id, $"gameplay ground zone id missing from JSON: {id}");
      }
    }

    static void TestDetachedWeaponVisualSingleAttach()
    {
      var go = new GameObject("DetachedWeaponProbe");
      try
      {
        go.AddComponent<DetachedWeaponController>();
        DetachedWeaponPresentationSystem.EnsureExists();
        DetachedWeaponPresentationSystem.RefreshExistingWeapons();
        var count = 0;
        foreach (var component in go.GetComponents<Component>())
        {
          if (component != null && component.GetType().Name == "DetachedWeaponVisual")
            count++;
        }
        Require(count == 1, $"expected one DetachedWeaponVisual, found {count}");
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(go);
      }
    }

    static void TestDetachedWeaponPresentationPoolReset()
    {
      DetachedWeaponPresentationSystem.ForceRecreateForTest();
      DetachedWeaponPresentationSystem.PlayMissileBurst(Vector3.zero, true);
      DetachedWeaponPresentationSystem.ResetPoolsForNewRun();
      var summary = DetachedWeaponPresentationSystem.GetPoolDebugSummary();
      Require(summary.Contains("0/"), $"presentation pools were not reset: {summary}");
    }

    static void TestCombatFixtureIsolation()
    {
      Require(!CombatTestFixtures.ProductionContainsTestOnlyId("buff_haste", "weapon_starter_laser"),
        "production DB contains editor test-only ids before fixture load");

      CombatTestFixtures.EnsureLoaded();
      Require(BuffDatabase.Exists("buff_haste"), "test fixture buff_haste not loaded");
      Require(AttackProfileDatabase.Get("weapon_starter_laser") != null,
        "test fixture weapon_starter_laser not loaded");

      CombatTestFixtures.Unload();
      Require(!BuffDatabase.Exists("buff_haste"), "buff_haste remained after fixture unload");
      Require(AttackProfileDatabase.Get("weapon_starter_laser") == null,
        "weapon_starter_laser remained after fixture unload");
    }

    static void TestResourcesExcludeCombatFixtures()
    {
      var resourcesRoot = System.IO.Path.Combine(Application.dataPath, "Resources/Data/combat");
      Require(!System.IO.File.Exists(System.IO.Path.Combine(resourcesRoot, "buffs_core_tests.json")),
        "Resources still contains buffs_core_tests.json");
      Require(!System.IO.File.Exists(System.IO.Path.Combine(resourcesRoot, "attacks_core_tests.json")),
        "Resources still contains attacks_core_tests.json");
    }

    static void TestUpgradePlayerPityReset()
    {
      UpgradeOfferPityTracker.ResetForNewRun();
      UpgradeOfferPityTracker.OnOfferBuilt(new LevelUpChoiceDatabase.LevelUpOffer
      {
        choices = new[]
        {
          new LevelUpChoiceDatabase.UpgradeDef { id = "num_common_damage_01", offer_group = "numeric" }
        }
      });
      UpgradeOfferPityTracker.OnOfferBuilt(new LevelUpChoiceDatabase.LevelUpOffer
      {
        choices = new[]
        {
          new LevelUpChoiceDatabase.UpgradeDef { id = "num_common_attack_speed_01", offer_group = "numeric" }
        }
      });
      Require(UpgradeOfferPityTracker.ShouldForcePlayerSlot(), "pity should force player after two misses");

      UpgradeOfferPityTracker.OnOfferBuilt(new LevelUpChoiceDatabase.LevelUpOffer
      {
        choices = new[]
        {
          new LevelUpChoiceDatabase.UpgradeDef { id = "num_player_max_hp_01", offer_group = "player" }
        }
      });
      Require(!UpgradeOfferPityTracker.ShouldForcePlayerSlot(), "pity should reset when player card appears");
      Require(UpgradeOfferPityTracker.OffersWithoutPlayerCard == 0, "miss counter should reset to zero");
    }

    static void TestGameEventBusClearAll()
    {
      GameEventBus.ClearAll();
      var calls = 0;
      var handle = GameEventBus.Subscribe<ProbeEvent>(_ => calls++);
      GameEventBus.ClearAll();
      GameEventBus.Publish(new ProbeEvent(1));
      Require(calls == 0, "ClearAll did not remove event listeners");
      if (handle.Valid)
        GameEventBus.Unsubscribe(handle);
      GameEventBus.ClearAll();
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
