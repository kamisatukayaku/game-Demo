using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  /// <summary>Editor-only 20-wave progression harness (fast mock kills, no Play Mode timing).</summary>
  public static class RingArenaTwentyWaveHarness
  {
    const string MenuPath = "Tools/Validation/Run 20-Wave Harness";

    [MenuItem(MenuPath)]
    public static void RunHarness()
    {
      var log = new StringBuilder();
      var failures = new List<string>();

      WaveDirector director = null;
      EnemySpawner spawner = null;
      try
      {
        EnemySpawner.SpawningEnabled = true;
        WaveDirector.EditorDestroyInstance();

        var spawnerGo = new GameObject("_HarnessSpawner");
        spawner = spawnerGo.AddComponent<EnemySpawner>();
        var directorGo = new GameObject("_HarnessWaveDirector");
        director = directorGo.AddComponent<WaveDirector>();
        director.EditorInitializeForTests(spawner, manualSpawning: true);

        var total = director.TotalWaves;
        for (var wave = 1; wave <= total; wave++)
        {
          var sw = System.Diagnostics.Stopwatch.StartNew();
          director.EditorForceWaveActive(wave);

          var boss = director.IsBossWave(wave);
          if (boss && !director.BossSpawnedThisWave)
          {
            Debug.LogWarning(
              $"[RingArenaTwentyWaveHarness] Wave {wave}: simulating boss spawned (editor lacks arena context).");
            director.EditorMarkBossSpawnedForTests();
          }

          var tracked = new List<GameObject>();
          var quota = director.WaveSpawnQuota;
          for (var i = 0; i < quota; i++)
            tracked.Add(director.EditorTrackMockEnemy(true));

          foreach (var enemy in tracked)
            director.EditorKillTracked(enemy);

          if (boss)
          {
            foreach (var core in UnityEngine.Object.FindObjectsOfType<BossCore>())
            {
              if (core != null)
                director.EditorKillTracked(core.gameObject);
            }
          }

          director.EditorTickWaveActive();
          sw.Stop();

          var waveOk = director.CurrentWave > wave
            || director.CurrentPhase == WaveDirector.Phase.BuildPhase
            || (wave == total && director.CurrentPhase == WaveDirector.Phase.AllWavesComplete);
          log.AppendLine(
            $"Wave={wave} Spawned={quota} Extra=0 Boss={(boss ? 1 : 0)} Killed={quota + (boss ? 1 : 0)} " +
            $"Remaining={director.EnemiesRemaining} DurationMs={sw.ElapsedMilliseconds} " +
            $"Result={(waveOk ? "OK" : "STUCK")}");

          if (director.CurrentPhase != WaveDirector.Phase.BuildPhase
              && director.CurrentPhase != WaveDirector.Phase.AllWavesComplete
              && director.CurrentWave <= wave)
            failures.Add($"Wave {wave} did not advance (phase={director.CurrentPhase})");
        }

        if (director.CurrentPhase != WaveDirector.Phase.AllWavesComplete
            && director.CurrentWave < director.TotalWaves)
          failures.Add("Harness did not reach AllWavesComplete");
      }
      finally
      {
        if (director != null)
          WaveDirector.EditorDestroyInstance();
        if (spawner != null)
          Object.DestroyImmediate(spawner.gameObject);
      }

      Debug.Log(log.ToString());
      if (failures.Count > 0)
        throw new System.InvalidOperationException("20-wave harness failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[RingArenaTwentyWaveHarness] PASS");
    }

    public static void RunBatchAndQuit()
    {
      RunHarness();
      EditorApplication.Exit(0);
    }

    static void Require(bool condition, string message, ICollection<string> failures)
    {
      if (!condition)
        failures.Add(message);
    }
  }
}
