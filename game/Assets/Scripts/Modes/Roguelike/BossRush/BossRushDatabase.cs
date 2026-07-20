using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  public static class BossRushDatabase
  {
    const string ConfigPath = "roguelike/boss_rush/boss_rush";

    static BossRushConfig s_config;
    static bool s_loaded;
    static string s_loadError;

    public static bool IsLoaded => s_loaded;
    public static string LoadError => s_loadError;
    public static IReadOnlyList<BossRushEncounterDef> Encounters => s_config?.encounters ?? Array.Empty<BossRushEncounterDef>();
    public static BossRushSettings Settings => s_config?.settings ?? BossRushSettings.Default;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_loadError = null;
      s_config = null;

      if (!JsonDataLoader.TryLoadText(ConfigPath, out var json))
      {
        s_loadError = $"Missing Boss Rush config: {ConfigPath}";
        Debug.LogError($"[BossRushDatabase] {s_loadError}");
        return;
      }

      try
      {
        s_config = JsonUtility.FromJson<BossRushConfig>(json);
        if (s_config?.encounters == null || s_config.encounters.Length == 0)
        {
          s_loadError = "Boss Rush config has no encounters.";
          s_config = null;
          return;
        }

        Array.Sort(s_config.encounters, (a, b) => a.index.CompareTo(b.index));
      }
      catch (Exception ex)
      {
        s_loadError = ex.Message;
        s_config = null;
        Debug.LogError($"[BossRushDatabase] Parse failed: {ex.Message}");
      }
    }

    public static BossRushEncounterDef GetEncounter(int index)
    {
      EnsureLoaded();
      if (s_config?.encounters == null)
        return null;

      foreach (var encounter in s_config.encounters)
      {
        if (encounter != null && encounter.index == index)
          return encounter;
      }

      return null;
    }

    public static void ResetForTests()
    {
      s_loaded = false;
      s_config = null;
      s_loadError = null;
    }

    [Serializable]
    sealed class BossRushConfig
    {
      public int schema_version;
      public BossRushSettings settings;
      public BossRushEncounterDef[] encounters;
    }
  }

  [Serializable]
  public class BossRushSettings
  {
    public float pre_fight_countdown = 3f;
    public float post_fight_delay = 1.5f;
    public int reward_choices = 3;
    public float base_heal_percent = 0.25f;
    public float minimum_heal_percent = 0.35f;
    public bool full_cleanse_between_fights = true;
    public bool clear_enemy_projectiles = true;
    public bool clear_boss_summons = true;
    public bool clear_hazards = true;
    public string arena_layout_id = "island_chain";
    public float boss_spawn_distance_min = 5.5f;
    public float boss_spawn_distance_max = 9.5f;
    public int opening_reward_count = 2;

    public static BossRushSettings Default => new();
  }

  [Serializable]
  public class BossRushEncounterDef
  {
    public int index = 1;
    public string boss_id;
    public string display_name;
    public float hp_mult = 1f;
    public float damage_mult = 1f;
    public float speed_mult = 1f;
    public float cooldown_mult = 1f;
    public float intro_grace;
    public int reward_count = 1;
    public float heal_percent = 0.25f;
    public string arena_modifier = "standard";
    public float target_duration_seconds = 45f;
    public bool allow_minions = true;
    public int max_minions = 8;
    public string music_layer;
    public float background_intensity = 0.5f;
    public string tip;

    public bool IsFinalBoss => !string.IsNullOrEmpty(boss_id)
                               && boss_id.StartsWith("final_boss_", StringComparison.Ordinal);

    public bool IsMiniBoss => !string.IsNullOrEmpty(boss_id)
                              && boss_id.StartsWith("mini_boss_", StringComparison.Ordinal);

    public float ResolveIntroGrace()
    {
      if (intro_grace > 0.01f)
        return intro_grace;
      if (IsFinalBoss)
        return 3f;
      if (boss_id != null && boss_id.StartsWith("wild_boss_", StringComparison.Ordinal))
        return 2.5f;
      return 1.5f;
    }
  }
}
