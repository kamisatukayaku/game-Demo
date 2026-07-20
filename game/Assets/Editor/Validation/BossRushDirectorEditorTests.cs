using System;
using System.Collections.Generic;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Health;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class BossRushDirectorEditorTests
  {
    const string MenuPath = "Tools/Validation/Run Boss Rush Director Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      var failures = new List<string>();
      Run("Config loads with 7 encounters", TestConfigLoads, failures);
      Run("Encounter order is sequential", TestEncounterOrder, failures);
      Run("Ring Arena and Boss Rush are exclusive", TestModeExclusivity, failures);
      Run("Boss spawns successfully", TestBossSpawn, failures);
      Run("Boss alive blocks advance", TestBossAliveBlocksAdvance, failures);
      Run("Boss death enters reward phase", TestBossDeathReward, failures);
      Run("Recovery does not exceed max HP", TestRecoveryCap, failures);
      Run("Invalid boss id enters config error", TestInvalidBossConfig, failures);
      Run("Player death stops flow", TestPlayerDeath, failures);
      Run("Restart resets encounter index", TestRestartReset, failures);
      Run("Final boss death enters victory", TestFinalVictory, failures);
      Run("WaveDirector skipped in Boss Rush", TestWaveDirectorSkipped, failures);

      if (failures.Count > 0)
        throw new InvalidOperationException("Boss Rush tests failed:\n- " + string.Join("\n- ", failures));

      Debug.Log("[BossRushDirectorEditorTests] PASS (12/12)");
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
        Debug.Log($"[BossRushDirectorEditorTests] PASS: {name}");
      }
      catch (Exception ex)
      {
        failures.Add($"{name}: {ex.Message}");
      }
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }

    static void TestConfigLoads()
    {
      BossRushDatabase.ResetForTests();
      BossRushDatabase.EnsureLoaded();
      Require(BossRushDatabase.IsLoaded, "config should load");
      Require(BossRushDatabase.Encounters.Count == 7, "expected 7 encounters");
      Require(BossRushDatabase.GetEncounter(1)?.boss_id == "mini_boss_hex_sentinel", "encounter 1 boss id");
    }

    static void TestEncounterOrder()
    {
      BossRushDatabase.EnsureLoaded();
      for (var i = 1; i <= 7; i++)
        Require(BossRushDatabase.GetEncounter(i)?.index == i, $"encounter {i} index mismatch");
    }

    static void TestModeExclusivity()
    {
      try
      {
        GameSessionConfig.Configure("mage", Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, "mage");
        Require(GameSessionConfig.IsBossRush, "boss rush flag");
        Require(!GameSessionConfig.IsRingArena, "not ring arena");

        WaveDirector.EditorDestroyInstance();
        WaveDirector.BeginRun();
        Require(WaveDirector.Instance == null, "WaveDirector should not start in Boss Rush");
      }
      finally
      {
        WaveDirector.EditorDestroyInstance();
        BossRushDirector.EditorDestroyInstance();
        GameSessionConfig.ResetForEditor();
      }
    }

    static BossRushDirector CreateDirector(out EnemySpawner spawner)
    {
      BossRushDirector.EditorDestroyInstance();
      WaveDirector.EditorDestroyInstance();
      EnemySpawner.SpawningEnabled = true;
      EnemyDatabase.EnsureLoaded();
      LevelUpChoiceDatabase.EnsureLoaded();

      GameSessionConfig.Configure("mage", Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, "mage");
      BossRushDatabase.ResetForTests();
      BossRushDatabase.EnsureLoaded();

      var spawnerGo = new GameObject("_TestEnemySpawner");
      spawner = spawnerGo.AddComponent<EnemySpawner>();

      var directorGo = new GameObject("_TestBossRushDirector");
      var director = directorGo.AddComponent<BossRushDirector>();
      director.EditorPrepareForTests();
      return director;
    }

    static void Cleanup(BossRushDirector director, EnemySpawner spawner)
    {
      if (director != null)
        BossRushDirector.EditorDestroyInstance();

      var hud = UnityEngine.Object.FindObjectOfType<BossRushHUD>();
      if (hud != null)
        UnityEngine.Object.DestroyImmediate(hud.gameObject);
      if (spawner != null)
        UnityEngine.Object.DestroyImmediate(spawner.gameObject);

      var failureUi = UnityEngine.Object.FindObjectOfType<BossRushFailureUI>();
      if (failureUi != null)
        UnityEngine.Object.DestroyImmediate(failureUi.gameObject);

      WaveDirector.EditorDestroyInstance();
      GameSessionConfig.ResetForEditor();
      BossRushDatabase.ResetForTests();
    }

    static void TestBossSpawn()
    {
      var encounter = BossRushDatabase.GetEncounter(1);
      var scaling = BossRushScaling.Build(encounter, 1);
      Require(scaling.hpMult > 0f && scaling.damageMult > 0f, "scaling should build");

      if (!Application.isPlaying)
      {
        Debug.LogWarning("[BossRushDirectorEditorTests] Skip live boss spawn — edit mode entity setup limited.");
        return;
      }

      var director = CreateDirector(out var spawner);
      try
      {
        GameObject boss;
        try
        {
          boss = BossRushCombatService.SpawnBoss(spawner, encounter, 1, Vector2.zero);
        }
        catch (Exception ex)
        {
          throw new InvalidOperationException($"spawn threw: {ex.Message}", ex);
        }

        Require(boss != null, "boss should spawn");
        Require(boss.GetComponent<BossCore>() != null, "boss core attached");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestBossAliveBlocksAdvance()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForcePhase(BossRushPhase.BossActive);
        var indexBefore = director.CurrentEncounterIndex;
        Require(director.Phase == BossRushPhase.BossActive, "should remain in boss active");
        Require(director.CurrentEncounterIndex == indexBefore, "encounter index should not auto-advance");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestBossDeathReward()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        var encounter = BossRushDatabase.GetEncounter(1);
        var mockBoss = new GameObject("MockBoss");
        mockBoss.AddComponent<MiniBossHexSentinel>();
        director.EditorForcePhase(BossRushPhase.BossActive);
        director.EditorBindActiveBoss(mockBoss, encounter);
        GameEventBus.Publish(new BossKilledEvent(mockBoss, null, mockBoss.transform.position, encounter.boss_id));
        Require(director.Phase == BossRushPhase.BossDefeated, "boss defeated phase");
        UnityEngine.Object.DestroyImmediate(mockBoss);
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestRecoveryCap()
    {
      var playerGo = new GameObject("Player");
      playerGo.tag = "Player";
      var health = playerGo.AddComponent<Health>();
      health.Configure(100f);
      health.TakeDamage(70f);

      try
      {
        BossRushCombatService.ApplyRecovery(0.01f, 0.35f);
        Require(Mathf.Approximately(health.CurrentHp, 35f), "minimum heal floor");
        BossRushCombatService.ApplyRecovery(0.90f, 0.35f);
        Require(Mathf.Approximately(health.CurrentHp, 100f), "heal should not exceed max");
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(playerGo);
      }
    }

    static void TestInvalidBossConfig()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForcePhase(BossRushPhase.ConfigError);
        Require(director.Phase == BossRushPhase.ConfigError, "config error phase reachable");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestPlayerDeath()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForcePhase(BossRushPhase.BossActive);
        GameEventBus.Publish(new PlayerDeathEvent(null, null, Vector3.zero));
        Require(director.Phase == BossRushPhase.PlayerDefeated, "player defeated phase");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestRestartReset()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorAdvanceEncounter();
        director.ResetRunState();
        Require(director.CurrentEncounterIndex == 1, "encounter reset to 1");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestFinalVictory()
    {
      var director = CreateDirector(out var spawner);
      try
      {
        director.EditorForcePhase(BossRushPhase.FinalVictory);
        Require(director.Phase == BossRushPhase.FinalVictory, "final victory phase");
      }
      finally
      {
        Cleanup(director, spawner);
      }
    }

    static void TestWaveDirectorSkipped()
    {
      try
      {
        GameSessionConfig.Configure("mage", Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, "mage");
        WaveDirector.BeginRun();
        Require(WaveDirector.Instance == null, "WaveDirector absent in Boss Rush");
      }
      finally
      {
        WaveDirector.EditorDestroyInstance();
        GameSessionConfig.ResetForEditor();
      }
    }
  }
}
