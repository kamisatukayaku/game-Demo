using System.Collections.Generic;
using System;
using UnityEngine;

namespace Game.Shared.Enemy.Database
{
  /// <summary>读取 data/ai_profiles.json，供怪物 AI 移动与交战参数使用?/summary>
  public static class EnemyAiProfileDatabase
  {
    public class AiProfile
    {
      public string id;
      public float aggro_range_base;
      public float attack_range_base;
      public float attack_cooldown_base;
      public float leash_mult = 1f;
      public float windup_base;
      public float priority_player = 1f;
      public float priority_tower;
      public float priority_camp;
    }

    static readonly Dictionary<string, AiProfile> s_profiles = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_profiles.Clear();
      TryLoadJson(Parse);
    }

    public static AiProfile Get(string profileId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(profileId))
        return null;

      s_profiles.TryGetValue(profileId, out var profile);
      return profile;
    }

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.definitions == null)
        return;

      foreach (var entry in root.definitions)
      {
        if (entry == null || string.IsNullOrEmpty(entry.id))
          continue;

        var profile = new AiProfile
        {
          id = entry.id,
          aggro_range_base = entry.aggro_range_base,
          attack_range_base = entry.attack_range_base,
          attack_cooldown_base = entry.attack_cooldown_base,
          leash_mult = entry.leash_mult > 0f ? entry.leash_mult : 1f,
          windup_base = entry.windup_base
        };

        if (entry.priority_weights != null)
        {
          profile.priority_player = entry.priority_weights.player;
          profile.priority_tower = entry.priority_weights.tower;
          profile.priority_camp = entry.priority_weights.camp;
        }

        s_profiles[entry.id] = profile;
      }
    }

    static bool TryLoadJson(Action<string> parser)
    {
      var path = System.IO.Path.Combine(Application.dataPath, "../../data/ai_profiles.json");
      if (System.IO.File.Exists(path))
      {
        parser(System.IO.File.ReadAllText(path));
        return true;
      }

      var textAsset = Resources.Load<TextAsset>("Data/ai_profiles");
      if (textAsset != null)
      {
        parser(textAsset.text);
        return true;
      }

      return false;
    }

    [Serializable]
    class Root
    {
      public AiProfileJson[] definitions;
    }

    [Serializable]
    class AiProfileJson
    {
      public string id;
      public float aggro_range_base;
      public float attack_range_base;
      public float attack_cooldown_base;
      public float leash_mult;
      public float windup_base;
      public PriorityWeights priority_weights;
    }

    [Serializable]
    class PriorityWeights
    {
      public float player;
      public float tower;
      public float camp;
    }
  }
}