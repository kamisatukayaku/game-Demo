using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  public static class RuntimeValidationReportWriter
  {
    static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string AuditRoot =>
      Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/audit/runtime_validation"));

    public static void EnsureDirectory()
    {
      Directory.CreateDirectory(AuditRoot);
    }

    public static void WriteText(string fileName, string content)
    {
      EnsureDirectory();
      var path = Path.Combine(AuditRoot, fileName);
      File.WriteAllText(path, content ?? string.Empty, Utf8NoBom);
    }

    public static void WriteJson(string fileName, string json) => WriteText(fileName, json);

    public static void WriteCsv(string fileName, string csv) => WriteText(fileName, csv);

    public static bool ValidateUtf8(string fileName, out string issue)
    {
      issue = null;
      var path = Path.Combine(AuditRoot, fileName);
      if (!File.Exists(path))
      {
        issue = $"missing: {fileName}";
        return false;
      }

      var text = File.ReadAllText(path, Utf8NoBom);
      if (text.Contains('\uFFFD') || text.Contains("脳") || text.Contains("卤"))
      {
        issue = $"corrupt pattern in {fileName}";
        return false;
      }

      return true;
    }

    public static string TimestampUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");
  }
}
