using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace Game.Editor.Architecture
{
  /// <summary>
  /// 架构依赖校验器（Phase 2）。
  /// Rule 1: Shared 不能引用 Modes
  /// Rule 2: World 不能引用 Roguelike
  /// Rule 3: Editor 可以引用所有（不校验）
  /// Rule 4: Modes 可以引用 Shared（不校验）
  /// </summary>
  public static class ArchitectureValidator
  {
    const string SharedRoot = "Assets/Scripts/Shared";
    const string WorldRoot = "Assets/Scripts/Modes/World";
    const string ReportFileName = "ArchitectureValidationReport.txt";

    static readonly Regex ModesUsing = new(
      @"^\s*using\s+(Game\.Modes\.(\w+)(?:\.\w+)*)",
      RegexOptions.Compiled);

    public struct Violation
    {
      public string Rule;
      public string File;
      public int Line;
      public string Detail;
    }

    [MenuItem("Tools/Architecture/Validate Dependencies")]
    public static void ValidateFromMenu()
    {
      var violations = CollectViolations();
      WriteReport(violations);

      if (violations.Count == 0)
      {
        Debug.Log("[Architecture] OK — all dependency rules satisfied.");
        return;
      }

      Debug.LogError($"[Architecture] FAILED — {violations.Count} violation(s). See {ReportFileName}");
      foreach (var v in violations)
        Debug.LogError($"[Architecture Error] {v.Rule} | {v.File}:{v.Line} | {v.Detail}");
    }

    public static IReadOnlyList<Violation> CollectViolations()
    {
      var list = new List<Violation>();
      ScanFolder(SharedRoot, "Rule1", (match, line) =>
      {
        list.Add(new Violation
        {
          Rule = "Rule1: Shared must not reference Modes",
          File = line.File,
          Line = line.LineNumber,
          Detail = $"using {match.Groups[1].Value}"
        });
      });

      ScanFolder(WorldRoot, "Rule2", (match, line) =>
      {
        if (!match.Groups[1].Value.StartsWith("Game.Modes.Roguelike"))
          return;

        list.Add(new Violation
        {
          Rule = "Rule2: World must not reference Roguelike",
          File = line.File,
          Line = line.LineNumber,
          Detail = $"using {match.Groups[1].Value}"
        });
      });

      return list;
    }

    struct LineInfo
    {
      public string File;
      public int LineNumber;
      public string Text;
    }

    static void ScanFolder(string assetsRelativeRoot, string ruleTag, System.Action<Match, LineInfo> onMatch)
    {
      var abs = Path.Combine(Application.dataPath, assetsRelativeRoot.Replace("Assets/", ""));
      if (!Directory.Exists(abs))
        return;

      foreach (var path in Directory.GetFiles(abs, "*.cs", SearchOption.AllDirectories))
      {
        var rel = assetsRelativeRoot + path.Substring(abs.Length).Replace('\\', '/');
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
          var m = ModesUsing.Match(lines[i]);
          if (m.Success)
          {
            onMatch(m, new LineInfo
            {
              File = rel,
              LineNumber = i + 1,
              Text = lines[i].Trim()
            });
          }
        }
      }
    }

    static void WriteReport(IReadOnlyList<Violation> violations)
    {
      var sb = new StringBuilder();
      sb.AppendLine("Architecture Validation Report");
      sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      sb.AppendLine();
      sb.AppendLine("Rules:");
      sb.AppendLine("  Rule 1: Shared must not reference Modes (Game.Modes.*)");
      sb.AppendLine("  Rule 2: World must not reference Roguelike (Game.Modes.Roguelike.*)");
      sb.AppendLine("  Rule 3: Editor may reference all assemblies");
      sb.AppendLine("  Rule 4: Modes may reference Shared");
      sb.AppendLine();

      if (violations.Count == 0)
      {
        sb.AppendLine("Status: PASS");
        sb.AppendLine("No violations found.");
      }
      else
      {
        sb.AppendLine($"Status: FAIL ({violations.Count} violation(s))");
        sb.AppendLine();
        foreach (var v in violations)
        {
          sb.AppendLine($"[{v.Rule}]");
          sb.AppendLine($"  File: {v.File}");
          sb.AppendLine($"  Line: {v.Line}");
          sb.AppendLine($"  Dependency: {v.Detail}");
          sb.AppendLine();
        }
      }

      var reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFileName);
      File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
      Debug.Log($"[Architecture] Report written to {reportPath}");
    }

    public static void RunBatchAndQuit()
    {
      ValidateFromMenu();
      var reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFileName);
      if (File.Exists(reportPath) && File.ReadAllText(reportPath).Contains("Status: FAIL"))
        throw new System.InvalidOperationException("[Architecture] Dependency validation failed.");
      EditorApplication.Exit(0);
    }
  }
}
#endif