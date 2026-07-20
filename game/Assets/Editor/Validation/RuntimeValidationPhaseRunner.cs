#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.Editor.Validation
{
  /// <summary>Runs ordered runtime validation Play Mode tests from the Editor (when batchmode is blocked).</summary>
  public static class RuntimeValidationPhaseRunner
  {
    const string ResultsDir = "Logs/runtime_validation_phases";

    static readonly string[] Phase1Tests =
    {
      "BossSkill_FpsConsistency_HexKing",
      "Ranger_FullRun20Waves_Accelerated",
      "Ranger_DeathRestart_SecondRun"
    };

    [MenuItem("Tools/Validation/Run Phase 1 Play Mode (Boss FPS + Ranger 20w/Restart)")]
    public static void RunPhase1()
    {
      Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", ResultsDir));
      RunPlayModeTests(Phase1Tests, "phase1_results.xml");
    }

    [MenuItem("Tools/Validation/Run Phase 1 — Boss FPS Only")]
    public static void RunPhase1BossFpsOnly()
    {
      Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", ResultsDir));
      RunPlayModeTests(new[] { "BossSkill_FpsConsistency_HexKing" }, "boss_fps_only.xml");
    }

    static void RunPlayModeTests(IReadOnlyList<string> testNames, string resultFile)
    {
      var api = ScriptableObject.CreateInstance<TestRunnerApi>();
      var resultsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ResultsDir, resultFile));
      var settings = new ExecutionSettings(new Filter
      {
        testMode = TestMode.PlayMode,
        testNames = testNames is string[] arr ? arr : new List<string>(testNames).ToArray()
      });

      var callback = ScriptableObject.CreateInstance<RunCallback>();
      callback.Init(resultsPath);
      api.RegisterCallbacks(callback);
      api.Execute(settings);
      Debug.Log($"[RuntimeValidationPhaseRunner] Started Play Mode tests → {resultsPath}");
    }

    sealed class RunCallback : ScriptableObject, ICallbacks
    {
      string _resultsPath;
      bool _failed;

      public void Init(string resultsPath) => _resultsPath = resultsPath;

      public void RunStarted(ITestAdaptor testsToRun) { }

      public void RunFinished(ITestResultAdaptor result)
      {
        _failed = result.FailCount > 0 || !result.HasChildren && result.TestStatus == TestStatus.Failed;
        Debug.Log(
          $"[RuntimeValidationPhaseRunner] FINISHED passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
        if (_failed)
          Debug.LogError($"[RuntimeValidationPhaseRunner] FAIL — see Test Runner / {_resultsPath}");
        else
          Debug.Log("[RuntimeValidationPhaseRunner] PASS");
      }

      public void TestStarted(ITestAdaptor test) =>
        Debug.Log($"[RuntimeValidationPhaseRunner] START {test.FullName}");

      public void TestFinished(ITestResultAdaptor result)
      {
        if (result.TestStatus == TestStatus.Failed)
          Debug.LogError($"[RuntimeValidationPhaseRunner] FAIL {result.FullName}: {result.Message}");
        else if (result.TestStatus == TestStatus.Passed)
          Debug.Log($"[RuntimeValidationPhaseRunner] PASS {result.Name}");
      }
    }
  }
}
#endif
