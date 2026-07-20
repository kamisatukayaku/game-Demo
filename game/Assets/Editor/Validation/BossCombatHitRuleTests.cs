using System;
using System.Collections.Generic;
using Game.Shared.Combat.Damage;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class BossCombatHitRuleTests
  {
    const string MenuPath = "Tools/Validation/Run Boss Combat Hit Rule Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("Instant hit once per attack instance", TestInstantHitOnce, failures);
      Run("Contact hit cooldown", TestContactCooldown, failures);
      Run("Tick hit FPS consistency", TestTickHitFpsConsistency, failures);
      Run("Dash-style instance isolation", TestDashInstanceIsolation, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Boss combat hit rule tests failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[BossCombatHitRuleTests] PASS (4/4)");
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
        Debug.Log($"[BossCombatHitRuleTests] PASS: {name}");
      }
      catch (Exception ex)
      {
        failures.Add($"{name}: {ex.Message}");
      }
    }

    static void TestInstantHitOnce()
    {
      BossAttackHitTracker.ResetRun();
      var attack = BossAttackHitTracker.NewAttackInstanceId();
      const int target = 42;
      Require(BossAttackHitTracker.TryInstantHit(attack, target), "first hit allowed");
      Require(!BossAttackHitTracker.TryInstantHit(attack, target), "duplicate instant hit blocked");
    }

    static void TestContactCooldown()
    {
      BossAttackHitTracker.ResetRun();
      const int source = 7;
      const int target = 9;
      const float interval = 0.5f;
      Require(BossAttackHitTracker.TryContactHit(source, target, interval), "first contact allowed");
      Require(!BossAttackHitTracker.TryContactHit(source, target, interval), "immediate contact blocked");
    }

    static void TestTickHitFpsConsistency()
    {
      var fpsTargets = new[] { 30, 60, 144 };
      var hitCounts = new int[fpsTargets.Length];

      for (var f = 0; f < fpsTargets.Length; f++)
      {
        BossAttackHitTracker.ResetRun();
        const string tickGroup = "laser_test";
        const int target = 100;
        const float interval = 0.25f;
        const float duration = 3f;
        var fps = fpsTargets[f];
        var dt = 1f / fps;
        var elapsed = 0f;
        while (elapsed < duration)
        {
          if (BossAttackHitTracker.TryTickHit(tickGroup, target, interval))
            hitCounts[f]++;
          elapsed += dt;
        }
      }

      for (var i = 1; i < hitCounts.Length; i++)
        Require(Mathf.Abs(hitCounts[i] - hitCounts[0]) <= 1,
          $"tick hit count drift fps30={hitCounts[0]} fps{fpsTargets[i]}={hitCounts[i]}");
    }

    static void TestDashInstanceIsolation()
    {
      BossAttackHitTracker.ResetRun();
      const int target = 55;
      var dashA = BossAttackHitTracker.NewAttackInstanceId();
      var dashB = BossAttackHitTracker.NewAttackInstanceId();
      Require(BossAttackHitTracker.TryInstantHit(dashA, target), "dash A first hit");
      Require(!BossAttackHitTracker.TryInstantHit(dashA, target), "dash A duplicate blocked");
      Require(BossAttackHitTracker.TryInstantHit(dashB, target), "dash B independent hit");
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
