using System.Collections;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Enemy.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Roguelike.RuntimeValidation
{
  [Category("RuntimeValidation")]
  [Explicit("Frame rate sampling — excluded from unified validation runs")]
  public sealed class PerformancePlayModeTests
  {
    [UnityTest]
    public IEnumerator W1Baseline_PerformanceSample()
    {
      yield return SampleScenario("w1_baseline", "mage", 70001, 1, 20f);
    }

    [UnityTest]
    [Explicit("Extended performance matrix")]
    public IEnumerator PerformanceMatrix()
    {
      yield return SampleScenario("w1_baseline", "mage", 70001, 1, 20f);
      yield return SampleScenario("w10_mid_build", "ranger", 70002, 10, 35f);
      yield return SampleScenario("death_restart", "contact", 70003, 2, 25f);
    }

    static IEnumerator SampleScenario(string scenarioId, string starter, int seed, int targetWave, float sampleSeconds)
    {
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter(starter);
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, seed);
      RuntimeValidationSettings.SetAccelerated();
      RingArenaPlayModeSession.EnableAutoPlayer();

      var perf = new PerformanceRuntimeSampler();
      perf.BeginScenario(scenarioId);
      var elapsed = 0f;
      while (elapsed < sampleSeconds)
      {
        if (LevelUpController.IsWaiting)
          LevelUpController.ValidationAutoPickFirst();
        perf.TickFrame();
        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      var result = perf.EndScenario();
      var csv = new StringBuilder();
      csv.AppendLine(PerformanceRuntimeSampler.CsvHeader());
      csv.Append(PerformanceRuntimeSampler.ToCsvRow(result));
      RuntimeValidationReportWriter.WriteCsv("performance_results.csv", csv.ToString());
      RuntimeValidationReportWriter.WriteText(
        "PERFORMANCE_RESULTS.md",
        $"# Performance Results\n\nScenario: {scenarioId}\nStatus: {(result.Pass ? "PASS" : "FAIL")}\nP95={result.P95FrameMs:F2}ms P99={result.P99FrameMs:F2}ms\n");
    }
  }
}
