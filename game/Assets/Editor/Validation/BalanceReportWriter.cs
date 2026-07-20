#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Game.Modes.Roguelike.Diagnostics;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class BalanceReportWriter
  {
    static string AuditRoot =>
      Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/audit/balance"));

    public static void EnsureAuditFolder()
    {
      if (!Directory.Exists(AuditRoot))
        Directory.CreateDirectory(AuditRoot);
    }

    public static void WriteProgressionCurveJson(int xpBase, float xpGrowth)
    {
      EnsureAuditFolder();
      var path = Path.Combine(AuditRoot, "progression_curve.json");
      var sb = new StringBuilder();
      sb.AppendLine("{");
      sb.AppendLine($"  \"xp_base\": {xpBase},");
      sb.AppendLine($"  \"xp_growth\": {xpGrowth:F3},");
      sb.AppendLine("  \"scenarios\": {");
      var first = true;
      foreach (ProgressionCurveSimulator.XpScenario scenario in Enum.GetValues(typeof(ProgressionCurveSimulator.XpScenario)))
      {
        if (!first)
          sb.AppendLine(",");
        first = false;
        var report = ProgressionCurveSimulator.SimulateScenario(scenario, xpBase, xpGrowth);
        sb.Append($"    \"{scenario}\": {{ \"xp_per_mob\": {report.XpPerMob}, \"milestones\": [");
        for (var i = 0; i < report.Milestones.Length; i++)
        {
          var m = report.Milestones[i];
          if (i > 0)
            sb.Append(", ");
          sb.Append($"{{\"wave\":{m.Wave},\"level\":{m.Level},\"cum_xp\":{m.CumulativeXp}}}");
        }
        sb.Append("] }");
      }
      sb.AppendLine();
      sb.AppendLine("  }");
      sb.AppendLine("}");
      File.WriteAllText(path, sb.ToString());
    }

    public static void AppendMarkdownSection(string title, string body)
    {
      EnsureAuditFolder();
      var path = Path.Combine(AuditRoot, "BALANCE_BASELINE.md");
      if (!File.Exists(path))
      {
        File.WriteAllText(path, "# Balance Baseline\n\n");
      }

      File.AppendAllText(path, $"## {title}\n\n```\n{body}\n```\n\n");
    }

    public static void WriteFinalSummary(string markdown)
    {
      EnsureAuditFolder();
      File.WriteAllText(Path.Combine(AuditRoot, "BALANCE_FINAL.md"), markdown);
    }
  }
}
#endif
