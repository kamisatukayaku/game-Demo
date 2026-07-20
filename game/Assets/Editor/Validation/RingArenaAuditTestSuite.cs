using Game.Editor.Architecture;
using Game.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  /// <summary>Runs audit closure test suites sequentially for BatchMode CI.</summary>
  public static class RingArenaAuditTestSuite
  {
    const string MenuPath = "Tools/Validation/Run Ring Arena Audit Test Suite";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      CoreRegressionTests.RunAll();
      ArenaMetaSaveEditorTests.RunAll();
      WaveDirectorEditorTests.RunAll();
      ArenaRunRestartEditorTests.RunAll();
      BossRushDirectorEditorTests.RunAll();
      BossRushFullRunHarness.RunHarness();
      ProgressionSimulationTests.RunAll();
      HealOnKillBudgetTests.RunAll();
      BuildMilestoneContactTests.RunAll();
      BossCombatHitRuleTests.RunAll();
      GameplayIntegrityPoolingEditorTests.RunAll();
      RingArenaTwentyWaveHarness.RunHarness();
      UpgradeOfferDistributionSimulatorEditor.RunAllStarters();
      ArchitectureValidator.ValidateFromMenu();
      CombatTests.RunAll();
      Debug.Log("[RingArenaAuditTestSuite] ALL PASS");
    }

    public static void RunBatchAndQuit()
    {
      RunAll();
      EditorApplication.Exit(0);
    }
  }
}
