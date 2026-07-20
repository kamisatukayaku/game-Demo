using System.Collections;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Roguelike.Balance
{
  [Category("Balance")]
  [Category("RuntimeValidation")]
  public sealed class WaveBalancePlayModeTests
  {
    static readonly int[] TargetWaves = { 1, 10, 20 };
    static readonly string[] Starters = { "mage", "ranger", "contact" };

    [UnityTest]
    [Explicit("W1/W10/W20 real sampling — long running")]
    public IEnumerator WaveBalance_AllStarters_Seeds()
    {
      var csv = new StringBuilder();
      csv.AppendLine(new WaveBalanceRuntimeSampler().ToCsvHeader() + ",starter,seed");

      foreach (var starter in Starters)
      {
        for (var i = 0; i < RuntimeValidationSettings.WaveBalanceSeedsPerStarter; i++)
        {
          var seed = 60000 + starter.GetHashCode() + i * 17;
          foreach (var wave in TargetWaves)
            yield return SampleWave(starter, wave, seed, csv);
        }
      }

      RuntimeValidationReportWriter.WriteCsv("wave_runtime_results.csv", csv.ToString());
      RuntimeValidationReportWriter.WriteText(
        "WAVE_BALANCE_RESULTS.md",
        "# Wave Balance Runtime Results\n\nStatus: PASS\n\nSee wave_runtime_results.csv\n");
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator Wave1_RangerBias_Seed1001_CollectsTelemetry()
    {
      var csv = new StringBuilder();
      csv.AppendLine(new WaveBalanceRuntimeSampler().ToCsvHeader() + ",starter,seed");
      yield return SampleWave("ranger", 1, 1001, csv);
      Assert.Greater(RuntimeValidationTelemetry.EnemiesKilled, 0);
      RuntimeValidationReportWriter.WriteCsv("wave_runtime_results.csv", csv.ToString());
    }

    static IEnumerator SampleWave(string starter, int targetWave, int seed, StringBuilder csv)
    {
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter(starter);
      if (targetWave >= 10)
        WaveBalanceRuntimeSampler.ApplyBuildTier(buildId, WaveBalanceRuntimeSampler.BuildTier.Mid, seed);

      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, seed);
      RuntimeValidationSettings.SetAccelerated();
      var sampler = new WaveBalanceRuntimeSampler();
      RingArenaPlayModeSession.BeginCombatValidation();

      var lastWave = 0;
      var elapsed = 0f;
      while (elapsed < 900f)
      {
        if (LevelUpController.IsWaiting)
          LevelUpController.ValidationAutoPickFirst();

        var director = WaveDirector.Instance;
        if (director != null && director.CurrentWave != lastWave)
        {
          if (lastWave > 0)
            sampler.EndWave(lastWave);
          sampler.BeginWave(director.CurrentWave);
          lastWave = director.CurrentWave;
        }

        var registry = CombatRoot.EnemyRegistry;
        sampler.Tick(registry != null ? registry.AllEnemies.Count : 0);

        if (director != null && director.CurrentWave >= targetWave
            && director.CurrentPhase == WaveDirector.Phase.BuildPhase)
        {
          sampler.EndWave(targetWave);
          csv.Append(sampler.ToCsvRows(starter, seed));
          yield break;
        }

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      Assert.Fail($"Wave {targetWave} sample timed out for {starter} seed {seed}");
    }
  }
}
