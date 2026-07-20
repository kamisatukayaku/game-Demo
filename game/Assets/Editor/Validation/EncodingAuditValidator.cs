#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class EncodingAuditValidator
  {
    static readonly string[] CorruptPatterns = { "\uFFFD", "脳", "卤", "â†'", "Ã—", "Ã—" };

    [MenuItem("Tools/Validation/Validate Audit Doc Encoding")]
    public static void ValidateAuditDocs()
    {
      var auditRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/audit"));
      var failures = new List<string>();
      foreach (var file in Directory.GetFiles(auditRoot, "*.*", SearchOption.AllDirectories))
      {
        if (!file.EndsWith(".md") && !file.EndsWith(".json") && !file.EndsWith(".csv"))
          continue;

        var text = File.ReadAllText(file, new UTF8Encoding(false, true));
        foreach (var pattern in CorruptPatterns)
        {
          if (text.Contains(pattern))
          {
            failures.Add($"{file}: corrupt pattern '{pattern}'");
            break;
          }
        }
      }

      if (failures.Count > 0)
        throw new System.InvalidOperationException("Encoding validation failed:\n- " + string.Join("\n- ", failures));

      RuntimeValidationReportWriter.WriteText(
        "ENCODING_VALIDATION.md",
        "# Encoding Validation\n\nStatus: PASS\n\nAll audit markdown/json/csv UTF-8 clean.\n");
      Debug.Log("[EncodingAuditValidator] PASS");
    }

    public static void FixReportWriterEncoding()
    {
      // Reports are written via RuntimeValidationReportWriter with UTF-8 no BOM.
    }
  }
}
#endif
