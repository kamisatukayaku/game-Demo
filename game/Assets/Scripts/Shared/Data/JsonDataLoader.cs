using System.IO;
using System;
using UnityEngine;

namespace Game.Shared.Data
{
  /// <summary>?data/ ?Resources/Data/ 读取 JSON 文本?/summary>
  public static class JsonDataLoader
  {
    public static bool TryLoadText(string fileNameWithoutExtension, out string json)
    {
      json = null;
      var candidates = new[]
      {
        Path.Combine(Application.dataPath, "../../data/combat", fileNameWithoutExtension + ".json"),
        Path.Combine(Application.dataPath, "../../data/roguelike/boss_rush", fileNameWithoutExtension + ".json"),
        Path.Combine(Application.dataPath, "../../data/roguelike", fileNameWithoutExtension + ".json"),
        Path.Combine(Application.dataPath, "../../data", fileNameWithoutExtension + ".json"),
        Path.Combine(Application.dataPath, "../../data/roguelike/upgrades", fileNameWithoutExtension + ".json"),
      };

      foreach (var path in candidates)
      {
        if (!File.Exists(path))
          continue;

        json = File.ReadAllText(path);
        return true;
      }

      var asset = Resources.Load<TextAsset>("Data/" + fileNameWithoutExtension);
      if (asset != null)
      {
        json = asset.text;
        return true;
      }

      var nested = fileNameWithoutExtension.Replace('\\', '/');
      if (nested.Contains('/'))
      {
        asset = Resources.Load<TextAsset>("Data/" + nested);
        if (asset != null)
        {
          json = asset.text;
          return true;
        }
      }

      var fileStem = Path.GetFileName(nested);
      foreach (var resourceStem in new[] { "combat/" + fileStem, fileStem })
      {
        asset = Resources.Load<TextAsset>("Data/" + resourceStem);
        if (asset != null)
        {
          json = asset.text;
          return true;
        }
      }

      return false;
    }

    public static bool TryParse(string fileNameWithoutExtension, Action<string> parser)
    {
      if (!TryLoadText(fileNameWithoutExtension, out var json))
        return false;

      parser(json);
      return true;
    }
  }
}