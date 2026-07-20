using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  public static class MonsterEcosystemDatabase
  {
    static readonly Dictionary<string, MonsterRoleProfile> Profiles = new();
    static MonsterEcosystemRoot s_root;
    static bool s_loaded;

    public static MonsterEcosystemRoot Root { get { EnsureLoaded(); return s_root; } }

    public static MonsterRoleProfile Get(string enemyId)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(enemyId) && Profiles.TryGetValue(enemyId, out var profile)
        ? profile
        : null;
    }

    public static void EnsureLoaded()
    {
      if (s_loaded) return;
      s_loaded = true;
      if (!JsonDataLoader.TryParse("enemies/ecosystem", Parse))
        Debug.LogError("[MonsterEcosystem] data/roguelike/enemies/ecosystem.json is missing or invalid.");
    }

    static void Parse(string json)
    {
      s_root = JsonUtility.FromJson<MonsterEcosystemRoot>(json);
      Profiles.Clear();
      foreach (var profile in s_root?.profiles ?? Array.Empty<MonsterRoleProfile>())
        if (profile != null && !string.IsNullOrEmpty(profile.enemy_id))
          Profiles[profile.enemy_id] = profile;
    }
  }

  [Serializable]
  public sealed class MonsterEcosystemRoot
  {
    public MonsterRoleProfile[] profiles;
    public EliteAffixProfile[] elite_affixes;
    public WaveComposition[] compositions;
  }

  [Serializable]
  public sealed class MonsterRoleProfile
  {
    public string enemy_id;
    public string role;
    public bool elite_eligible = true;
    public float effect_radius = 4f;
    public float interval = 1f;
    public float heal_percent = 0.04f;
    public float shield_amount = 5f;
    public float trigger_radius = 2.2f;
    public float windup = 0.8f;
    public float ability_damage = 12f;
    public float knockback_multiplier = 1f;
    public string split_child_id;
    public int split_count;
    public int split_generations;
  }

  [Serializable]
  public sealed class EliteAffixProfile
  {
    public string id;
    public float hp_mult = 1f;
    public float speed_mult = 1f;
    public float scale_mult = 1f;
    public float shield_amount;
    public int split_count;
    public float death_explosion_radius;
    public float death_explosion_damage;
    public float gravity_radius;
  }

  [Serializable]
  public sealed class WaveComposition
  {
    public string id;
    public int min_wave;
    public int max_wave;
    public int weight = 1;
    public string[] enemy_ids;
  }
}
