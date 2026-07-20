using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Modes.Roguelike.Build.Runtime;

using Game.Shared.Data;
namespace Game.Modes.Roguelike.Progression
{
  public static class MetaTalentDatabase
  {
    static readonly Dictionary<string, MetaTalentDef> s_talents = new();
    static bool s_loaded;
    static int s_maxPicks = 3;

    public static int MaxPicks
    {
      get
      {
        EnsureLoaded();
        return s_maxPicks;
      }
    }

    public static IReadOnlyDictionary<string, MetaTalentDef> Talents
    {
      get
      {
        EnsureLoaded();
        return s_talents;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("progression/meta_talents", Parse))
        Debug.LogWarning("[MetaTalentDatabase] meta_talents.json not found.");
    }

    public static MetaTalentDef Get(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id))
        return null;
      s_talents.TryGetValue(id, out var def);
      return def;
    }

    public static void ApplyToRunBuild(IEnumerable<string> talentIds)
    {
      EnsureLoaded();
      if (talentIds == null)
        return;

      foreach (var id in talentIds)
      {
        var t = Get(id);
        if (t?.modifiers == null)
          continue;

        foreach (var m in t.modifiers)
        {
          if (m == null || string.IsNullOrEmpty(m.stat))
            continue;

          if (m.op == "add")
            RunBuildState.AddStat(m.stat, m.value);
        }
      }
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<MetaTalentRoot>(json);
        s_talents.Clear();
        s_maxPicks = root != null && root.max_picks > 0 ? root.max_picks : 3;

        if (root?.talents == null)
          return;

        foreach (var t in root.talents)
        {
          if (t != null && !string.IsNullOrEmpty(t.id))
            s_talents[t.id] = t;
        }

        Debug.Log($"[MetaTalentDatabase] Loaded {s_talents.Count} talents, max_picks={s_maxPicks}");
      }
      catch (Exception e)
      {
        Debug.LogError($"[MetaTalentDatabase] Parse failed: {e.Message}");
      }
    }

    [Serializable]
    class MetaTalentRoot
    {
      public int max_picks;
      public MetaTalentDef[] talents;
    }

    [Serializable]
    public class StatMod
    {
      public string stat;
      public string op;
      public float value;
    }

    [Serializable]
    public class MetaTalentDef
    {
      public string id;
      public string display_name;
      public string description;
      public string branch;
      public StatMod[] modifiers;
    }
  }
}
