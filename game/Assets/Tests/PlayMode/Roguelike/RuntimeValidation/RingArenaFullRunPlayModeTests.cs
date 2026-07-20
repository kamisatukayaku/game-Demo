using System.Collections;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Tests.PlayMode.Roguelike.RuntimeValidation
{
  [Category("RuntimeValidation")]
  public sealed class RingArenaFullRunPlayModeTests
  {
    const float CombatReadyTimeout = 60f;
    const float WaveTimeout = 900f;
    const float FullRunTimeout = 3600f;

    const int RangerSeed = 42002;
    const int RangerW1Seed = 1001;

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator Ranger_W1_CombatChain_Smoke()
    {
      RuntimeValidationTelemetry.Reset();
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter("ranger");
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, RangerW1Seed);
      RuntimeValidationSettings.SetAccelerated();
      RingArenaPlayModeSession.BeginCombatValidation();

      yield return CombatChainGatekeeper.MonitorWave1Combat("Ranger_W1_CombatChain");

      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesSpawned, 0);
      RuntimeValidationReportWriter.WriteText(
        "RANGER_W1_COMBAT_CHAIN.md",
        "# Ranger W1 Combat Chain\n\nStatus: **PASS**\n\n" + CombatChainTelemetry.FormatSnapshot());
    }

    [UnityTest]
    [Timeout(3600000)]
    public IEnumerator Unified_FullRun20Waves_Accelerated()
    {
      RuntimeValidationTelemetry.Reset();
      yield return RunUntilAllWavesComplete("unified", 42002, FullRunTimeout, accelerated: true, timeScale: RuntimeValidationSettings.FullRunAcceleratedTimeScale);

      Assert.Greater(RuntimeValidationTelemetry.EnemiesSpawned, 0);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0);
      Assert.Greater(RuntimeValidationTelemetry.BossesSpawned, 0);
      Assert.Greater(RuntimeValidationTelemetry.LevelUpPauseCount, 0);

      var director = WaveDirector.Instance;
      Assert.IsNotNull(director);
      Assert.AreEqual(WaveDirector.Phase.AllWavesComplete, director.CurrentPhase);

      AppendFullRunReport("Unified_FullRun20Waves", RuntimeValidationTelemetry.FormatSummary(), "PASS");
    }

    [UnityTest]
    [Timeout(3600000)]
    public IEnumerator Ranger_FullRun20Waves_Accelerated()
    {
      RuntimeValidationTelemetry.Reset();
      yield return RunUntilAllWavesComplete("ranger", RangerSeed, FullRunTimeout, accelerated: true, timeScale: RuntimeValidationSettings.FullRunAcceleratedTimeScale);

      Assert.Greater(RuntimeValidationTelemetry.EnemiesSpawned, 0, "Expected real spawns.");
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0, "Expected real kills.");
      Assert.Greater(RuntimeValidationTelemetry.BossesSpawned, 0, "Expected boss spawn.");
      Assert.Greater(RuntimeValidationTelemetry.LevelUpPauseCount, 0, "Expected level-up pauses.");

      var director = WaveDirector.Instance;
      Assert.IsNotNull(director);
      Assert.AreEqual(WaveDirector.Phase.AllWavesComplete, director.CurrentPhase, "Must reach AllWavesComplete.");

      AppendFullRunReport("Ranger_FullRun20Waves", RuntimeValidationTelemetry.FormatSummary(), "PASS");
    }

    [UnityTest]
    [Timeout(1800000)]
    public IEnumerator Ranger_DeathRestart_SecondRun()
    {
      RuntimeValidationTelemetry.Reset();
      yield return RunUntilWave("ranger", 2, RangerSeed, WaveTimeout, accelerated: true, runWave1Gates: true);
      Assert.Greater(RuntimeValidationTelemetry.LevelUpPauseCount, 0, "Expected upgrades before death.");

      RuntimeValidationPlayerSurvival.Restore();

      var player = GameObject.FindWithTag("Player");
      Assert.IsNotNull(player);
      var health = player.GetComponent<Health>();
      health.TakeDamage(health.MaxHp * 2f);
      yield return RingArenaPlayModeSession.WaitForPlayerDeath(45f);
      Assert.Greater(RuntimeValidationTelemetry.PlayerDeathCount, 0);

      PlayerDeathFailureUI.EnsureExists();
      PlayerDeathFailureUI.Show();

      var orphansBefore = RingArenaPlayModeSession.CountOrphanedValidationObjects();
      ArenaRunRestart.ReloadMainScene();
      yield return null;
      CombatRoot.RequestMainSceneInitialization(SceneManager.GetActiveScene());
      yield return RingArenaPlayModeSession.WaitForCombatReady(CombatReadyTimeout);

      var orphansAfter = RingArenaPlayModeSession.CountOrphanedValidationObjects();
      Assert.LessOrEqual(orphansAfter, orphansBefore + 15, "Run restart should not leak combat objects.");

      RuntimeValidationTelemetry.Reset();
      CombatChainTelemetry.Reset();
      RuntimeValidationSettings.SetAccelerated();
      RingArenaPlayModeSession.BeginCombatValidation();
      yield return RunUntilWaveFromCurrentState("ranger", 3, WaveTimeout, runWave1Gates: true);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0, "Second run must fight normally.");

      AppendFullRunReport("Ranger_DeathRestart", RuntimeValidationTelemetry.FormatSummary(), "PASS");
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator Unified_W1_Smoke_CompletesWave1()
    {
      yield return RunUntilWave("unified", 1, 42001, WaveTimeout, accelerated: true);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesSpawned, 0);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0);
      AppendFullRunReport("Unified_W1_Smoke", RuntimeValidationTelemetry.FormatSummary(), "PASS");
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator W1_Mage_Smoke_CompletesWave1()
    {
      yield return RunUntilWave("mage", 1, 41001, WaveTimeout, accelerated: true);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesSpawned, 0);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0);
      AppendFullRunReport("W1_Mage_Smoke", RuntimeValidationTelemetry.FormatSummary(), "PASS");
    }

    [UnityTest]
    [Explicit("Long-running 20-wave Play Mode validation — all starters")]
    public IEnumerator FullRun_AllStarters_Accelerated()
    {
      foreach (var starter in new[] { "mage", "ranger", "contact" })
      {
        RuntimeValidationTelemetry.Reset();
        yield return RunUntilAllWavesComplete(starter, 41000 + starter.GetHashCode(), FullRunTimeout, accelerated: true);
        Assert.Greater(RuntimeValidationTelemetry.BossesSpawned, 0, $"{starter}: boss expected.");
        AppendFullRunReport($"FullRun_{starter}", RuntimeValidationTelemetry.FormatSummary(), "PASS");
      }
    }

    static IEnumerator RunUntilAllWavesComplete(string starter, int seed, float timeout, bool accelerated, float timeScale = RuntimeValidationSettings.DefaultAcceleratedTimeScale)
    {
      try
      {
        var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter(starter);
        yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, seed);
        if (accelerated)
          RuntimeValidationSettings.SetAccelerated(timeScale);
        RingArenaPlayModeSession.BeginCombatValidation();
        if (accelerated)
          yield return CombatChainGatekeeper.MonitorWave1Combat($"{starter}_FullRun/W1");

        var elapsed = 0f;
        var nextProgressLog = 60f;
        while (elapsed < timeout)
        {
          ValidationBlockingUiAutoResponder.Tick();
          RuntimeValidationCombatAssist.Tick();
          ValidationBlockingUiAutoResponder.TrackFrozenState();
          if (ValidationBlockingUiAutoResponder.GetFrozenDurationSeconds() > 8f)
          {
            Assert.Fail(
              $"Simulation frozen (timeScale=0) for >8s. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("simulation_frozen")}");
          }

          if (elapsed >= nextProgressLog)
          {
            var director = WaveDirector.Instance;
            Debug.Log(
              $"[FullRun/{starter}] t={elapsed:F0}s scale={Time.timeScale:F1} phase={director?.CurrentPhase} wave={director?.CurrentWave} kills={RuntimeValidationTelemetry.EnemiesKilled} survival_boost={RuntimeValidationSettings.PlayerSurvivalBoostActive}");
            nextProgressLog += 60f;
          }

          if (!RuntimeValidationSettings.PlayerSurvivalBoostActive)
          {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            var health = player != null ? player.GetComponent<Health>() : null;
            if (health != null && health.IsDead)
            {
              Assert.Fail(
                $"Player died before AllWavesComplete for starter {starter}. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("player_died")}");
            }
          }

          if (WaveDirector.Instance != null
              && WaveDirector.Instance.CurrentPhase == WaveDirector.Phase.AllWavesComplete)
            yield break;

          elapsed += Time.unscaledDeltaTime;
          yield return null;
        }

        Assert.Fail($"Timed out before AllWavesComplete for starter {starter}. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("timeout")}");
      }
      finally
      {
        RuntimeValidationPlayerSurvival.Restore();
      }
    }

    static IEnumerator RunUntilWave(string starter, int targetWave, int seed, float timeout, bool accelerated, bool runWave1Gates = false)
    {
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter(starter);
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, seed);
      if (accelerated)
        RuntimeValidationSettings.SetAccelerated();
      RingArenaPlayModeSession.BeginCombatValidation();
      if (runWave1Gates)
        yield return CombatChainGatekeeper.MonitorWave1Combat($"{starter}_RunUntilWave/W1");

      var elapsed = 0f;
      while (elapsed < timeout)
      {
        ValidationBlockingUiAutoResponder.Tick();
        RuntimeValidationCombatAssist.Tick();
        ValidationBlockingUiAutoResponder.TrackFrozenState();
        if (ValidationBlockingUiAutoResponder.GetFrozenDurationSeconds() > 8f)
        {
          Assert.Fail(
            $"Simulation frozen (timeScale=0) for >8s. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("simulation_frozen")}");
        }

        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        var health = player != null ? player.GetComponent<Health>() : null;
        if (health != null && health.IsDead)
        {
          Assert.Fail(
            $"Player died before reaching wave {targetWave} for starter {starter}. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("player_died")}");
        }

        var director = WaveDirector.Instance;
        if (director != null
            && director.CurrentWave >= targetWave
            && (director.CurrentPhase == WaveDirector.Phase.BuildPhase
                || director.CurrentPhase == WaveDirector.Phase.AllWavesComplete))
          yield break;

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      Assert.Fail($"Timed out before wave {targetWave} for starter {starter}. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("timeout")}");
    }

    static IEnumerator RunUntilWaveFromCurrentState(string starter, int targetWave, float timeout, bool runWave1Gates = false)
    {
      if (runWave1Gates)
        yield return CombatChainGatekeeper.MonitorWave1Combat($"{starter}_DeathRestart/W1");

      var elapsed = 0f;
      while (elapsed < timeout)
      {
        ValidationBlockingUiAutoResponder.Tick();
        RuntimeValidationCombatAssist.Tick();
        ValidationBlockingUiAutoResponder.TrackFrozenState();
        if (ValidationBlockingUiAutoResponder.GetFrozenDurationSeconds() > 8f)
        {
          Assert.Fail(
            $"Simulation frozen (timeScale=0) for >8s. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("simulation_frozen")}");
        }

        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        var health = player != null ? player.GetComponent<Health>() : null;
        if (health != null && health.IsDead)
        {
          Assert.Fail(
            $"Player died before reaching wave {targetWave} for starter {starter}. Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("player_died")}");
        }

        var director = WaveDirector.Instance;
        if (director != null
            && director.CurrentWave >= targetWave
            && (director.CurrentPhase == WaveDirector.Phase.BuildPhase
                || director.CurrentPhase == WaveDirector.Phase.AllWavesComplete))
        {
          Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0,
            $"Reached wave {targetWave} without kills — likely stale wave state or harness skip.\n{CombatChainTelemetry.FormatSnapshot("no_kills")}");
          yield break;
        }

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      Assert.Fail($"Timed out before wave {targetWave} for starter {starter} (current scene). Phase={WaveDirector.Instance?.CurrentPhase} Wave={WaveDirector.Instance?.CurrentWave}\n{CombatChainTelemetry.FormatSnapshot("timeout")}");
    }

    static void AppendFullRunReport(string caseId, string body, string status)
    {
      var sb = new StringBuilder();
      sb.AppendLine($"## {caseId}");
      sb.AppendLine($"Status: {status}");
      sb.AppendLine($"Timestamp: {RuntimeValidationReportWriter.TimestampUtc()}");
      sb.AppendLine(body);
      RuntimeValidationReportWriter.WriteText("FULL_RUN_RESULTS.md", sb.ToString());

      var json = new StringBuilder();
      json.Append('{');
      json.Append($"\"case_id\":\"{caseId}\",");
      json.Append($"\"status\":\"{status}\",");
      json.Append($"\"timestamp\":\"{RuntimeValidationReportWriter.TimestampUtc()}\",");
      json.Append($"\"telemetry\":{TelemetryToJson()}");
      json.Append('}');
      RuntimeValidationReportWriter.WriteJson("full_run_results.json", json.ToString());
    }

    static string TelemetryToJson()
    {
      return $"{{\"scene_loads\":{RuntimeValidationTelemetry.SceneLoadCount}," +
             $"\"enemies_spawned\":{RuntimeValidationTelemetry.EnemiesSpawned}," +
             $"\"enemies_killed\":{RuntimeValidationTelemetry.EnemiesKilled}," +
             $"\"bosses_spawned\":{RuntimeValidationTelemetry.BossesSpawned}," +
             $"\"bosses_killed\":{RuntimeValidationTelemetry.BossesKilled}," +
             $"\"levelup_pause\":{RuntimeValidationTelemetry.LevelUpPauseCount}," +
             $"\"player_deaths\":{RuntimeValidationTelemetry.PlayerDeathCount}," +
             $"\"run_restarts\":{RuntimeValidationTelemetry.RunRestartCount}}}";
    }
  }
}
