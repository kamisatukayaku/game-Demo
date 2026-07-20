using System;
using System.Collections.Generic;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Projectile;
using Game.Shared.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class ArenaRunRestartEditorTests
  {
    const string MenuPath = "Tools/Validation/Run Arena Run Restart Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("PrepareForNewRun clears cross-run state", TestCrossRunReset, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Arena run restart tests failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[ArenaRunRestartEditorTests] PASS (1/1)");
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
        Debug.Log($"[ArenaRunRestartEditorTests] PASS: {name}");
      }
      catch (Exception ex)
      {
        failures.Add($"{name}: {ex.Message}");
      }
    }

    static void TestCrossRunReset()
    {
      try
      {
        GroundZoneProximityTracker.EditorDestroyForTests();
        RoguelikeTutorialDirector.EditorDestroyForTests();
        HuntContractRuntime.EditorDestroyForTests();
        TutorialPromptUI.EditorDestroyForTests();

        Time.timeScale = 0.5f;
        CombatTimePause.PushPause();

        DetachedWeaponPresentationSystem.ForceRecreateForTest();
        DetachedWeaponPresentationSystem.ResetPoolsForNewRun();

        UpgradeOfferPityTracker.ResetForNewRun();
        UpgradeOfferPityTracker.OnOfferBuilt(new LevelUpChoiceDatabase.LevelUpOffer());
        UpgradeOfferPityTracker.OnOfferBuilt(new LevelUpChoiceDatabase.LevelUpOffer());
        Require(UpgradeOfferPityTracker.LevelsWithoutPlayerOffer >= 2, "pity should accumulate before reset");

        var weaponGo = new GameObject("DetachedWeaponProbe");
        weaponGo.AddComponent<DetachedWeaponController>();

        XpPickup.Spawn(Vector3.up * 2f, 25);
        XpPickup.Spawn(Vector3.left, 15);
        Require(CountActiveXpPickups() >= 2, "xp pickups should exist before reset");

        var request = DamageRequest.Direct(1f, "physical", "restart_test", null);
        var projectile = ProjectileFactory.SpawnEnemyStraight(
          Vector3.zero, null, request, 5f, 0.2f, Color.white, "RestartProbe");
        Require(projectile.gameObject.activeInHierarchy, "projectile should be active before reset");

        ArenaRunRestart.PrepareForNewRun();

        Require(CountActiveXpPickups() == 0, "xp pickups should clear on run restart");
        Require(!projectile.gameObject.activeInHierarchy, "projectiles should despawn on run restart");

        Require(Mathf.Approximately(Time.timeScale, 1f), "timeScale should reset");
        Require(!CombatTimePause.IsPaused, "combat pause should clear");
        Require(UnityEngine.Object.FindObjectOfType<DetachedWeaponController>() == null,
          "detached weapons should be cleared");

        var summary = DetachedWeaponPresentationSystem.GetPoolDebugSummary();
        Require(summary.Contains("0/"), $"VFX pools should be inactive: {summary}");
        Require(UpgradeOfferPityTracker.LevelsWithoutPlayerOffer == 0, "pity tracker should reset");
      }
      finally
      {
        ArenaQuadrantBlocker.Clear();
        WaveDirector.EditorDestroyInstance();
        DetachedWeaponPresentationSystem.ForceRecreateForTest();
        GroundZoneProximityTracker.EditorDestroyForTests();
        RoguelikeTutorialDirector.EditorDestroyForTests();
        HuntContractRuntime.EditorDestroyForTests();
        TutorialPromptUI.EditorDestroyForTests();
      }
    }

    static int CountActiveXpPickups()
    {
      var count = 0;
      foreach (var pickup in UnityEngine.Object.FindObjectsByType<XpPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (pickup != null && pickup.gameObject.activeInHierarchy)
          count++;
      }

      return count;
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
