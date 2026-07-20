using System.IO;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
  /// <summary>
  /// 将仓库根目录 data/ 同步到 Assets/Resources/Data/；
  /// 将 Assets/Sprites/ 同步到 Assets/Resources/Sprites/，保证运行时 Resources.Load 可用。
  /// </summary>
  public static class DataSyncMenu
  {
    const string ResourcesDataRoot = "Assets/Resources/Data";
    const string SourceSpritesRoot = "Assets/Sprites";
    const string ResourcesSpritesRoot = "Assets/Resources/Sprites";

    public static string SourceDataPath =>
      Path.GetFullPath(Path.Combine(Application.dataPath, "../../data"));

    [MenuItem("Tools/Sync Data → Resources/Data")]
    public static void SyncFromMenu()
    {
      var count = SyncAll(verbose: true);
      AssetDatabase.Refresh();
      Debug.Log($"[DataSync] Synced {count} file(s) to Resources (Data + Sprites)");
    }

    public static int SyncAll(bool verbose = false)
    {
      var count = SyncJsonData(verbose);
      count += SyncSpriteAssets(verbose);
      return count;
    }

    static bool ShouldSkipJsonSync(string relativePath, string fileName)
    {
      if (fileName.EndsWith("_core_tests.json", System.StringComparison.OrdinalIgnoreCase))
        return true;
      if (relativePath.IndexOf("/test/", System.StringComparison.OrdinalIgnoreCase) >= 0
          || relativePath.IndexOf("\\test\\", System.StringComparison.OrdinalIgnoreCase) >= 0)
        return true;
      return false;
    }

    public static bool ValidateJsonMirrors(out string error)
    {
      var src = SourceDataPath;
      var destinationRoot = Path.GetFullPath(ResourcesDataRoot);
      if (!Directory.Exists(src))
      {
        error = $"Source folder not found: {src}";
        return false;
      }

      foreach (var sourcePath in Directory.GetFiles(src, "*.json", SearchOption.AllDirectories))
      {
        var relative = sourcePath.Substring(src.Length)
          .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (ShouldSkipJsonSync(relative, Path.GetFileName(sourcePath)))
          continue;
        var mirrorPath = Path.Combine(destinationRoot, relative);
        if (!File.Exists(mirrorPath))
        {
          error = $"Missing Resources mirror: {relative}";
          return false;
        }

        if (!string.Equals(File.ReadAllText(sourcePath), File.ReadAllText(mirrorPath), System.StringComparison.Ordinal))
        {
          error = $"Stale Resources mirror: {relative}";
          return false;
        }
      }

      error = null;
      return true;
    }

    static int SyncJsonData(bool verbose)
    {
      var src = SourceDataPath;
      if (!Directory.Exists(src))
      {
        Debug.LogError($"[DataSync] Source folder not found: {src}");
        return 0;
      }

      Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Data"));
      var count = 0;

      var files = Directory.GetFiles(src, "*.json", SearchOption.AllDirectories);
      var basenames = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
      foreach (var file in files)
      {
        var relative = file.Substring(src.Length)
          .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(file);
        if (ShouldSkipJsonSync(relative, name))
          continue;

        basenames[name] = basenames.TryGetValue(name, out var existing) ? existing + 1 : 1;

        var dest = Path.Combine(ResourcesDataRoot, relative);
        if (CopyIfChanged(file, dest))
          count++;
      }

      // Compatibility mirror for older Shared databases that still load Data/{name}.
      // Only unique basenames are flattened, so future same-name configs cannot overwrite each other.
      foreach (var file in files)
      {
        var name = Path.GetFileName(file);
        var relative = file.Substring(src.Length)
          .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (ShouldSkipJsonSync(relative, name))
          continue;
        if (basenames[name] != 1)
          continue;

        var legacyDest = Path.Combine(ResourcesDataRoot, name);
        if (CopyIfChanged(file, legacyDest))
          count++;
      }

      if (verbose && count == 0)
        Debug.Log("[DataSync] All JSON files up to date.");

      return count;
    }

    static int SyncSpriteAssets(bool verbose)
    {
      var srcRoot = Path.GetFullPath(SourceSpritesRoot);
      if (!Directory.Exists(srcRoot))
        return 0;

      var count = 0;
      foreach (var file in Directory.GetFiles(srcRoot, "*.*", SearchOption.AllDirectories))
      {
        if (file.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
          continue;

        var ext = Path.GetExtension(file);
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
          continue;

        var relative = file.Substring(srcRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dest = Path.Combine(ResourcesSpritesRoot, relative);
        if (CopyIfChanged(file, dest))
          count++;
      }

      if (verbose && count == 0)
        Debug.Log("[DataSync] All sprite files up to date.");

      return count;
    }

    static bool CopyIfChanged(string sourcePath, string assetPath)
    {
      var destFull = Path.GetFullPath(assetPath);
      Directory.CreateDirectory(Path.GetDirectoryName(destFull) ?? Application.dataPath);

      if (File.Exists(destFull))
      {
        var srcText = File.ReadAllText(sourcePath);
        var dstText = File.ReadAllText(destFull);
        if (srcText == dstText)
          return false;
      }

      File.Copy(sourcePath, destFull, overwrite: true);
      return true;
    }
  }

  /// <summary>打包前自动同步 data/，避免 Build 与 Editor 数据分裂。</summary>
  public class DataSyncPreprocessBuild : IPreprocessBuildWithReport
  {
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
      DataSyncMenu.SyncAll();
      if (!DataSyncMenu.ValidateJsonMirrors(out var error))
        throw new BuildFailedException($"[DataSync] {error}");
    }
  }
}
