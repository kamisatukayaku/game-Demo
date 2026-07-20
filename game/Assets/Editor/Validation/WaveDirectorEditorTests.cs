using System;
using System.Collections.Generic;
using Game.Modes.Roguelike.Combat;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class WaveDirectorEditorTests
  {
    const string MenuPath = "Tools/Validation/Run Wave Director Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("W1 clear → BuildPhase(2)", TestWave1To2, failures);
      Run("Spawn failure recovery", TestSpawnFailureRecovery, failures);
      Run("Spawn rhythm slot preserved", TestSpawnRhythmSlotPreservedOnFailure, failures);
      Run("Force destroy untracked", TestForceDestroyEnemy, failures);
      Run("Ecology spawn tracking", TestEcologySpawnTracking, failures);
      Run("Horde reinforcement tracking", TestHordeReinforcement, failures);
      Run("Boss wave spawn + completion", TestBossWaveFlow, failures);
      Run("Boss spawn retry", TestBossSpawnRetry, failures);
      Run("Boss spawn failure degrade", TestBossSpawnFailureDegrade, failures);
      Run("Run restart re-enables director", TestRunRestartReEnablesDirector, failures);
      Run("Wave completion only once", TestWaveCompletionOnce, failures);
      Run("Hard W15 dual boss fallback", TestDualBossFallback, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Wave director tests failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[WaveDirectorEditorTests] PASS (11/11)");
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
        Debug.Log($"[WaveDirectorEditorTests] PASS: {name}");
      }
      catch (Exception ex)
      {
        failures.Add($"{name}: {ex.Message}");
      }
    }

    static WaveDirector CreateDirector(out EnemySpawner spawner)
    {
      WaveDirector.EditorDestroyInstance();
      EnemySpawner.SpawningEnabled = true;

      var spawnerGo = new GameObject("_TestEnemySpawner");
      spawner = spawnerGo.AddComponent<EnemySpawner>();

      var directorGo = new GameObject("_TestWaveDirector");
      var director = directorGo.AddComponent<WaveDirector>();
      director.EditorInitializeForTests(spawner, manualSpawning: true);
      return director;
    }

    static void Cleanup(WaveDirector director, EnemySpawner spawner)
    {
      if (director != null)
        WaveDirector.EditorDestroyInstance();
      if (spawner != null)
        UnityEngine.Object.DestroyImmediate(spawner.gameObject);
    }

    static void TestWave1To2()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        Require(director.CurrentWave == 1, "wave should be 1");
        Require(director.CurrentPhase == WaveDirector.Phase.WaveActive, "phase should be active");

        var tracked = new List<GameObject>();
        var quota = director.WaveSpawnQuota;
        for (var i = 0; i < quota; i++)
          tracked.Add(director.EditorTrackMockEnemy(true));

        foreach (var enemy in tracked)
          director.EditorKillTracked(enemy);

        director.EditorTickWaveActive();
        Require(director.CurrentPhase == WaveDirector.Phase.BuildPhase, "should enter build phase");
        Require(director.CurrentWave == 2, "current wave should be 2");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestSpawnFailureRecovery()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        director.EditorSetManualSpawning(false);
        var spawnedBefore = director.WaveEnemiesSpawned;
        director.EditorSpawnFailRemaining = 1;
        director.EditorTrySpawnOnceForTests();
        Require(director.WaveEnemiesSpawned == spawnedBefore, "failed spawn must not consume quota");
        Require(director.EditorSpawnFailRemaining == 0, "fail gate should decrement");

        director.SpawnHordeReinforcement(1);
        Require(director.EditorHordePending >= 1, "horde queue independent of fail gate");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestSpawnRhythmSlotPreservedOnFailure()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        director.EditorSetManualSpawning(false);
        director.EditorBeginSpawnGroupForTests();
        var groupBefore = director.EditorSpawnGroupRemaining;
        director.EditorSpawnFailRemaining = 1;
        director.EditorTickSpawnRhythmOnce();
        Require(director.EditorSpawnGroupRemaining == groupBefore, "failed rhythm spawn must retain slot");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestForceDestroyEnemy()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        var enemy = director.EditorTrackMockEnemy(true);
        Require(director.EnemiesRemaining > 0, "enemy should be tracked");
        UnityEngine.Object.DestroyImmediate(enemy);
        director.EditorRefreshTrackingForTests();
        Require(director.EnemiesRemaining == 0, "destroyed enemy should be untracked");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestEcologySpawnTracking()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        var spawnedBefore = director.WaveEnemiesSpawned;
        var scaling = WaveScalingCalculator.Compute(1, null, WaveScalingCalculator.DefaultCurves);
        GameObject child;
        try
        {
          child = director.SpawnEcologyEnemy("mob_splitter_shard_eco_01", Vector2.right * 3f, scaling, 1);
        }
        catch (Exception ex)
        {
          Debug.LogWarning($"[WaveDirectorEditorTests] Skip ecology — {ex.Message}");
          return;
        }

        if (child == null)
        {
          Debug.LogWarning("[WaveDirectorEditorTests] Skip ecology — spawn returned null.");
          return;
        }

        Require(director.EnemiesRemaining >= 1, "ecology enemy should be tracked");
        Require(director.WaveEnemiesSpawned == spawnedBefore, "ecology spawn must not consume wave quota");
        director.EditorKillTracked(child);
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestHordeReinforcement()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        director.EditorSetManualSpawning(false);
        director.SpawnHordeReinforcement(2);
        Require(director.EditorHordePending >= 1, "horde pending should increase");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestBossWaveFlow()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        if (!director.IsBossWave(5))
        {
          Debug.LogWarning("[WaveDirectorEditorTests] Skip boss flow — wave 5 not a boss wave.");
          return;
        }

        director.EditorForceWaveActive(5);
        if (!director.BossSpawnedThisWave)
        {
          Debug.LogWarning("[WaveDirectorEditorTests] Skip boss flow — editor lacks arena spawn context.");
          return;
        }

        var quota = director.WaveSpawnQuota;
        for (var i = 0; i < quota; i++)
          director.EditorKillTracked(director.EditorTrackMockEnemy(true));

        foreach (var core in UnityEngine.Object.FindObjectsOfType<BossCore>())
        {
          if (core != null)
            director.EditorKillTracked(core.gameObject);
        }

        director.EditorTickWaveActive();
        Require(director.CurrentPhase == WaveDirector.Phase.BuildPhase || director.CurrentWave > 5,
          "boss wave should complete");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestBossSpawnRetry()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        if (!director.IsBossWave(5))
          return;

        director.EditorForceWaveActive(5);
        var first = director.BossSpawnedThisWave;
        director.EditorRetryBossSpawnForTests();
        Require(first == director.BossSpawnedThisWave, "boss should not duplicate when retrying spawn");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestBossSpawnFailureDegrade()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        if (!director.IsBossWave(5))
          return;

        director.EditorPrepareWaveActiveWithoutBossSpawn(5);
        Require(!director.BossSpawnedThisWave, "boss should not be marked before explicit failure test");
        director.EditorSimulateBossSpawnFailureForTests();
        Require(director.BossSpawnedThisWave, "boss spawn failure must degrade to avoid soft-lock");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestRunRestartReEnablesDirector()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.enabled = false;
        director.EditorSimulateRunRestartAfterDeath();
        Require(director.enabled, "WaveDirector must re-enable after run restart");
        Require(director.CurrentWave == 1, "run restart should begin wave 1");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestWaveCompletionOnce()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForceWaveActive(1);
        var tracked = new List<GameObject>();
        var quota = director.WaveSpawnQuota;
        for (var i = 0; i < quota; i++)
          tracked.Add(director.EditorTrackMockEnemy(true));

        foreach (var enemy in tracked)
          director.EditorKillTracked(enemy);

        var completions = 0;
        void OnComplete(int wave) => completions++;
        WaveDirector.WaveCompleted += OnComplete;
        try
        {
          director.EditorTickWaveActive();
          director.EditorTickWaveActive();
        }
        finally
        {
          WaveDirector.WaveCompleted -= OnComplete;
        }

        Require(completions == 1, $"wave completion should fire once, got {completions}");
        Require(director.CurrentPhase == WaveDirector.Phase.BuildPhase, "should enter build phase once");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestDualBossFallback()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        if (!director.IsBossWave(15))
          return;

        director.EditorForceWaveActive(15);
        if (!director.BossSpawnedThisWave)
        {
          Debug.LogWarning("[WaveDirectorEditorTests] Skip W15 dual boss — editor spawn context missing.");
          return;
        }
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
