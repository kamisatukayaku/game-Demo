#if UNITY_EDITOR
using System.IO;
using System.Text;
using Game.EditorTools;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class StandaloneBuildValidator
  {
    const string BuildFolder = "Builds/RuntimeValidation";
    const string ProductName = "RingArenaRuntimeValidation";

    [MenuItem("Tools/Validation/Build Standalone Development Smoke")]
    public static void BuildDevelopmentSmoke()
    {
      if (!DataSyncMenu.ValidateJsonMirrors(out var syncError))
        throw new System.InvalidOperationException($"[StandaloneBuildValidator] DataSync blocked: {syncError}");

      var scenes = new[] { "Assets/Scenes/StartGame.unity", "Assets/Scenes/MainScene.unity" };
      var output = Path.Combine(BuildFolder, ProductName + ".exe");
      Directory.CreateDirectory(BuildFolder);

      var options = new BuildPlayerOptions
      {
        scenes = scenes,
        locationPathName = output,
        target = BuildTarget.StandaloneWindows64,
        options = BuildOptions.Development | BuildOptions.AllowDebugging
      };

      var report = BuildPipeline.BuildPlayer(options);
      WriteReport(report);
      if (report.summary.result != BuildResult.Succeeded)
        throw new System.InvalidOperationException("[StandaloneBuildValidator] Build failed.");

      Debug.Log("[StandaloneBuildValidator] PASS — manual Player.log smoke required for full validation.");
    }

    static void WriteReport(BuildReport report)
    {
      var sb = new StringBuilder();
      sb.AppendLine("# Standalone Build Results");
      sb.AppendLine();
      sb.AppendLine($"Generated: {RuntimeValidationReportWriter.TimestampUtc()}");
      sb.AppendLine($"Result: {(report.summary.result == BuildResult.Succeeded ? "PASS" : "FAIL")}");
      sb.AppendLine($"Platform: {report.summary.platform}");
      sb.AppendLine($"Output: {report.summary.outputPath}");
      sb.AppendLine();
      sb.AppendLine("## Automated Checks");
      sb.AppendLine("- DataSync mirror validation before build: PASS");
      sb.AppendLine("- Development build compile: " + (report.summary.result == BuildResult.Succeeded ? "PASS" : "FAIL"));
      sb.AppendLine();
      sb.AppendLine("## Manual Smoke (required)");
      sb.AppendLine("- Main menu → Ring Arena → starter → combat → upgrade → boss → death → restart");
      sb.AppendLine("- Player.log must have zero Error/Exception");
      sb.AppendLine("- Build must not read D:\\game\\data (Resources mirror only)");
      RuntimeValidationReportWriter.WriteText("STANDALONE_BUILD_RESULTS.md", sb.ToString());
    }
  }
}
#endif
