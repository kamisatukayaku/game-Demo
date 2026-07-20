using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Shared.Combat.Buff
{
  /// <summary>
  /// Buff/Debuff 定义数据库。读?data/buffs.json?
  /// </summary>
  public static class BuffDatabase
  {
    public enum Polarity { buff, debuff }

    public enum StackPolicy { refresh_duration, add_duration, independent, replace }

    public enum DurationPolicy { timed, permanent, until_leave_zone }

    public enum EffectType { stat_mod, tick_damage, tick_heal, tick_ei, grant_rule, block_rule, shield, apply_buff_on_trigger, custom }

    [Serializable]
    public class BuffDef
    {
      public string id;
      public string display_name;
      public Polarity polarity;
      public string category;
      public string[] tags;
      public float duration;
      public DurationPolicy duration_policy;
      public int max_stacks;
      public StackPolicy stack_policy;
      public string exclusive_group;
      public int priority;
      public BuffEffect[] effects;
    }

    [Serializable]
    public class BuffEffect
    {
      /// <summary>JSON 原始 type 字符串（Unity JsonUtility 不支持枚举字符串解析，必须用 string）?/summary>
      public string type;
      public string stat;

      public EffectType ParsedType
      {
        get
        {
          if (string.IsNullOrEmpty(type)) return EffectType.stat_mod;
          switch (type)
          {
            case "stat_mod": return EffectType.stat_mod;
            case "tick_damage": return EffectType.tick_damage;
            case "tick_heal": return EffectType.tick_heal;
            case "tick_ei": return EffectType.tick_ei;
            case "grant_rule": return EffectType.grant_rule;
            case "block_rule": return EffectType.block_rule;
            case "shield": return EffectType.shield;
            case "apply_buff_on_trigger": return EffectType.apply_buff_on_trigger;
            case "custom": return EffectType.custom;
            default: return EffectType.stat_mod;
          }
        }
      }
      public string op;
      public float value;
      public bool per_stack;
      public string damage_type;
      public float interval;
      public string rule_flag;
      public string buff_id;
      public string trigger;
      public float chance;
    }

    static readonly Dictionary<string, BuffDef> s_defs = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded) return;
      s_loaded = true;
      s_defs.Clear();
      if (!JsonDataLoader.TryParse("combat/buffs", Parse))
        JsonDataLoader.TryParse("buffs", Parse);
    }

    /// <summary>Editor tests only — merges definitions without persisting to production load path.</summary>
    public static void MergeTestDefinitions(string json)
    {
      EnsureLoaded();
      Parse(json);
    }

    /// <summary>Clears cache and reloads production definitions only.</summary>
    public static void ReloadProduction()
    {
      s_loaded = false;
      s_defs.Clear();
      EnsureLoaded();
    }

    public static BuffDef Get(string buffId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(buffId)) return null;
      s_defs.TryGetValue(buffId, out var def);
      return def;
    }

    public static bool Exists(string buffId) => Get(buffId) != null;

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.definitions == null) return;
      foreach (var entry in root.definitions)
      {
        if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
        s_defs[entry.id] = entry;
      }
    }

    [Serializable]
    class Root { public BuffDef[] definitions; }
  }
}