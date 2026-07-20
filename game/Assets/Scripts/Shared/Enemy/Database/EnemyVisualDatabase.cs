using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

namespace Game.Shared.Enemy.Database
{
  /// <summary>读取 enemy_visuals.json 中小?shape / palette 信息?/summary>
  public static class EnemyVisualDatabase
  {
    [Serializable]
    public class MinionVisualDef
    {
      public string enemy_id;
      public string shape_id;
      public string palette;
      public string motion;
    }

    static readonly Dictionary<string, MinionVisualDef> s_minions = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_minions.Clear();
      TryLoadJson(Parse);
    }

    public static MinionVisualDef GetMinion(string enemyId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(enemyId))
        return null;

      s_minions.TryGetValue(enemyId, out var def);
      return def;
    }

    public static Color GetFillColor(string paletteId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(paletteId))
        return new Color(0.91f, 0.36f, 0.30f);

      return paletteId switch
      {
        "mob_red_dark" => new Color(0.79f, 0.29f, 0.24f),
        "mob_red" => new Color(0.91f, 0.36f, 0.30f),
        _ => paletteId.Contains("dark")
          ? new Color(0.79f, 0.29f, 0.24f)
          : new Color(0.91f, 0.36f, 0.30f)
      };
    }

    public static string GetShapeDisplayName(string shapeId)
    {
      if (string.IsNullOrEmpty(shapeId))
        return "未知";

      return shapeId switch
      {
        "hex" => "六边彀",
        "oct" => "八边彀",
        "tri" => "三角彀",
        "square" => "正方彀",
        "pent" => "五边彀",
        "diamond" => "菱形",
        "star4" => "四角是",
        "star5" => "五角是",
        "star6" => "六角是",
        "star8" => "八角星",
        "hexagram" => "六芒是",
        _ => shapeId
      };
    }

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.minions == null)
        return;

      foreach (var entry in root.minions)
      {
        if (entry == null || string.IsNullOrEmpty(entry.enemy_id))
          continue;

        s_minions[entry.enemy_id] = entry;
      }
    }

    static bool TryLoadJson(Action<string> parser)
    {
      var candidates = new[]
      {
        Path.Combine(Application.dataPath, "../../data/combat/enemy_visuals.json"),
        Path.Combine(Application.dataPath, "../../data/enemy_visuals.json"),
      };

      foreach (var path in candidates)
      {
        if (!File.Exists(path))
          continue;

        parser(File.ReadAllText(path));
        return true;
      }

      var textAsset = Resources.Load<TextAsset>("Data/enemy_visuals");
      if (textAsset != null)
      {
        parser(textAsset.text);
        return true;
      }

      Debug.LogWarning("[EnemyVisualDatabase] enemy_visuals.json not found.");
      return false;
    }

    [Serializable]
    class Root
    {
      public MinionVisualDef[] minions;
    }
  }
}
