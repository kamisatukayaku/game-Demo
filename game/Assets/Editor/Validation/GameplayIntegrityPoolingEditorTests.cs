using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Combat.Damage;
using Game.Shared.Projectile;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class GameplayIntegrityPoolingEditorTests
  {
    const string MenuPath = "Tools/Validation/Run Gameplay Integrity Pooling Tests";
    const int ReuseCycles = 100;

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("StraightProjectile 100x reuse", TestStraightProjectileReuse100, failures);
      Run("ActiveProjectileRegistry despawn all", TestActiveProjectileRegistryDespawnAll, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Gameplay integrity pooling tests failed:\n- " + string.Join("\n- ", failures));

      Debug.Log($"[GameplayIntegrityPoolingEditorTests] PASS (2/2, {ReuseCycles}x reuse)");
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
        Debug.Log($"[GameplayIntegrityPoolingEditorTests] PASS: {name}");
      }
      catch (Exception ex)
      {
        failures.Add($"{name}: {ex.Message}");
      }
    }

    static void TestStraightProjectileReuse100()
    {
      var request = DamageRequest.Direct(5f, "physical", "pool_stress", null);
      var despawn = typeof(StraightProjectile).GetMethod(
        "Despawn", BindingFlags.Instance | BindingFlags.NonPublic);
      Require(despawn != null, "StraightProjectile.Despawn was not found");

      StraightProjectile first = null;
      var firstId = 0;
      for (var cycle = 0; cycle < ReuseCycles; cycle++)
      {
        var pos = new Vector3(cycle * 0.01f, cycle * 0.02f, 0f);
        var scale = 0.2f + cycle * 0.001f;
        var projectile = ProjectileFactory.SpawnEnemyStraight(
          pos, null, request, 8f, scale, Color.cyan, $"PoolStress_{cycle}");

        if (cycle == 0)
        {
          first = projectile;
          firstId = projectile.GetInstanceID();
        }
        else
          Require(projectile.GetInstanceID() == firstId, $"cycle {cycle}: instance not reused");

        Require(projectile.transform.position == pos, $"cycle {cycle}: position not reset");
        despawn.Invoke(projectile, null);
      }

      if (first != null)
        UnityEngine.Object.DestroyImmediate(first.gameObject);
    }

    static void TestActiveProjectileRegistryDespawnAll()
    {
      ActiveProjectileRegistry.ResetAll();
      var request = DamageRequest.Direct(1f, "physical", "registry_stress", null);
      var live = new List<StraightProjectile>();
      for (var i = 0; i < 8; i++)
      {
        live.Add(ProjectileFactory.SpawnEnemyStraight(
          Vector3.right * i, null, request, 5f, 0.2f, Color.white, $"RegistryStress_{i}"));
      }

      ActiveProjectileRegistry.DespawnAllActive();
      foreach (var projectile in live)
      {
        if (projectile != null)
          Require(!projectile.gameObject.activeInHierarchy, "active projectile should despawn on run reset");
      }

      ActiveProjectileRegistry.ResetAll();
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
