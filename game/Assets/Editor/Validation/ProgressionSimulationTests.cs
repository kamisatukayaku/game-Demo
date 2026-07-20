#if UNITY_EDITOR
using System.IO;
using System.Text;
using Game.Modes.Roguelike.Diagnostics;
using Game.Modes.Roguelike.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class ProgressionSimulationTests
  {
    const string MenuPath = "Tools/Validation/Run Progression Simulation Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      LevelUpChoiceDatabase.ResetForTests();
      LevelUpChoiceDatabase.EnsureLoaded();
      var curve = LevelUpChoiceDatabase.Curve;
      var failures = new StringBuilder();

      if (!ProgressionCurveSimulator.ValidateTargets(curve.xp_base, curve.xp_growth, out var report))
        failures.AppendLine(report);

      var formatted = ProgressionCurveSimulator.FormatReport(curve.xp_base, curve.xp_growth);
      Debug.Log(formatted);

      BalanceReportWriter.WriteProgressionCurveJson(curve.xp_base, curve.xp_growth);
      BalanceReportWriter.AppendMarkdownSection(
        "Progression Simulation",
        formatted);

      if (failures.Length > 0)
        throw new System.InvalidOperationException(failures.ToString());

      Debug.Log("[ProgressionSimulationTests] PASS");
    }
  }
}
#endif
