#if UNITY_EDITOR
using System.IO;
using System.Text;
using Game.EditorTools;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class RuntimeValidationTestSuite
  {
    const string MenuPath = "Tools/Validation/Run Runtime Validation Suite";

    [MenuItem(MenuPath)]
    public static void RunEditorPhase()
    {
      WriteBaseline();
      UpgradeOfferBoundaryRunner.RunAll();
      EncodingAuditValidator.ValidateAuditDocs();
      Debug.Log("[RuntimeValidationTestSuite] Editor phase PASS");
    }

    [MenuItem("Tools/Validation/Run Runtime Validation Full (Editor + PlayMode hint)")]
    public static void RunFullHint()
    {
      RunEditorPhase();
      Debug.Log(
        "[RuntimeValidationTestSuite] Play Mode: run Unity Test Runner filter Category=RuntimeValidation or:\n" +
        "Unity -runTests -batchmode -projectPath <path> -testPlatform playmode -testCategory RuntimeValidation");
    }

    public static void RunBatchAndQuit()
    {
      RunEditorPhase();
      EditorApplication.Exit(0);
    }

    static void WriteBaseline()
    {
      var sb = new StringBuilder();
      sb.AppendLine("# Play Mode Baseline");
      sb.AppendLine();
      sb.AppendLine($"Generated: {RuntimeValidationReportWriter.TimestampUtc()}");
      sb.AppendLine();
      sb.AppendLine("## Scope");
      sb.AppendLine("- Real Play Mode combat (no editor mock kills)");
      sb.AppendLine("- Synthetic auto-player (no invincibility/direct damage)");
      sb.AppendLine("- Accelerated timeScale allowed for long runs");
      sb.AppendLine();
      sb.AppendLine("## Editor Preconditions");
      sb.AppendLine($"- DataSync mirrors: {(DataSyncMenu.ValidateJsonMirrors(out _) ? "PASS" : "FAIL")}");
      sb.AppendLine("- Upgrade boundary: 15 scenarios x 1000 rolls");
      sb.AppendLine("- auxiliary_offer_chance: consumed by AuxiliaryOfferPolicy");
      RuntimeValidationReportWriter.WriteText("PLAYMODE_BASELINE.md", sb.ToString());
    }
  }
}
#endif
