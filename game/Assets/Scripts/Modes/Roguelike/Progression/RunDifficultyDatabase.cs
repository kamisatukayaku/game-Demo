using System.Collections.Generic;
using System;
using UnityEngine;

using Game.Shared.Data;
namespace Game.Modes.Roguelike.Progression
{
  public static class RunDifficultyDatabase
  {
    static readonly Dictionary<string, DifficultyDef> s_defs = new();
    static bool s_loaded;

    public static IReadOnlyDictionary<string, DifficultyDef> Difficulties
    {
      get
      {
        EnsureLoaded();
        return s_defs;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("core/run_difficulty", Parse))
        Debug.LogWarning("[RunDifficultyDatabase] run_difficulty.json not found.");
    }

    public static DifficultyDef Get(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id))
        return null;

      s_defs.TryGetValue(id, out var def);
      return def;
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<DifficultyRoot>(json);
        s_defs.Clear();

        if (root?.difficulties == null)
          return;

        foreach (var d in root.difficulties)
        {
          if (d != null && !string.IsNullOrEmpty(d.id))
            s_defs[d.id] = d;
        }

        Debug.Log($"[RunDifficultyDatabase] Loaded {s_defs.Count} difficulties.");
      }
      catch (Exception e)
      {
        Debug.LogError($"[RunDifficultyDatabase] Parse failed: {e.Message}");
      }
    }

    [Serializable]
    class DifficultyRoot
    {
      public DifficultyDef[] difficulties;
    }

    [Serializable]
    public class DifficultyDef
    {
      public string id;
      public string display_name;
      public string victory_mode;
      public int wave_survive_count;
      public int wild_boss_spawn_count;
      public float world_level_rate_mult;
      public float player_stat_mult;
      public float xp_mult = 1f;
      public float spawn_count_mult = 1f;
      public float reward_mult = 1f;
      public float enemy_hp_mult = 1f;
      public float enemy_damage_mult = 1f;
      public float build_phase_seconds = 2f;
      public float boss_minion_spawn_mult = 0.35f;
    }
  }
}
