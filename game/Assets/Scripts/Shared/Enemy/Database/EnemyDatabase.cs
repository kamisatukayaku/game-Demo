using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

using Game.Shared.Enemy.Spawn;
namespace Game.Shared.Enemy.Database
{
  /// <summary>读取 enemies.json，供刷怪与图鉴 UI 使用?/summary>
  public static class EnemyDatabase
  {
    [Serializable]
    public class EnemyDef
    {
      public string id;
      public string move_mode;
      public string attack_mode;
      public string ai_profile;
      public float base_hp;
      public float base_damage;
      public float move_speed;
      public float visual_scale;
      public string[] tags;
      public string attack_profile_id;
      public string loot_table_id;
      public string[] spawn_weight_tags;
      public bool hide_in_roguelike_preview;
    }

    static readonly Dictionary<string, EnemyDef> s_enemies = new();
    static bool s_loaded;

    public static IReadOnlyDictionary<string, EnemyDef> All
    {
      get
      {
        EnsureLoaded();
        return s_enemies;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_enemies.Clear();
      TryLoadJson(Parse);
    }

    public static EnemyDef Get(string enemyId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(enemyId))
        return null;

      s_enemies.TryGetValue(enemyId, out var def);
      return def;
    }

    public static List<EnemyDef> GetMinions()
    {
      EnsureLoaded();
      var list = new List<EnemyDef>();
      foreach (var kv in s_enemies)
      {
        var def = kv.Value;
        if (def != null && def.id.StartsWith("mob_") && !def.hide_in_roguelike_preview)
          list.Add(def);
      }

      list.Sort(CompareMinions);
      return list;
    }

    static int CompareMinions(EnemyDef a, EnemyDef b)
    {
      var mode = string.CompareOrdinal(a.attack_mode, b.attack_mode);
      if (mode != 0)
        return mode;
      return string.CompareOrdinal(a.id, b.id);
    }

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.definitions == null)
      {
        Debug.LogWarning("[EnemyDatabase] Failed to parse enemies.json definitions.");
        return;
      }

      foreach (var def in root.definitions)
      {
        if (def == null || string.IsNullOrEmpty(def.id))
          continue;

        s_enemies[def.id] = def;
      }
    }

    static bool TryLoadJson(Action<string> parser)
    {
      var candidates = new[]
      {
        Path.Combine(Application.dataPath, "../../data/combat/enemies.json"),
        Path.Combine(Application.dataPath, "../../data/enemies.json"),
      };

      foreach (var path in candidates)
      {
        if (!File.Exists(path))
          continue;

        parser(File.ReadAllText(path));
        return true;
      }

      var textAsset = Resources.Load<TextAsset>("Data/enemies");
      if (textAsset != null)
      {
        parser(textAsset.text);
        return true;
      }

      Debug.LogWarning("[EnemyDatabase] enemies.json not found.");
      return false;
    }

    [Serializable]
    class Root
    {
      public EnemyDef[] definitions;
    }
  }
}
