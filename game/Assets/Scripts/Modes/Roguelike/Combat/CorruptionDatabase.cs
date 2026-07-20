using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B2: Corruption 配表加载与查询。</summary>
  public static class CorruptionDatabase
  {
    [Serializable]
    public sealed class CorruptionDef
    {
      public string id;
      public string display_name;
      public string description;
      public string positive_stat;
      public float positive_value;
      public string negative_stat;
      public float negative_value;
    }

    [Serializable]
    sealed class CorruptionRoot
    {
      public CorruptionDef[] corruptions;
    }

    static readonly Dictionary<string, CorruptionDef> s_byId = new();
    static CorruptionDef[] s_all = Array.Empty<CorruptionDef>();
    static bool s_loaded;

    public static IReadOnlyList<CorruptionDef> All
    {
      get
      {
        EnsureLoaded();
        return s_all;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_byId.Clear();
      if (!JsonDataLoader.TryParse("progression/corruptions", json =>
      {
        try
        {
          var root = JsonUtility.FromJson<CorruptionRoot>(json);
          s_all = root?.corruptions ?? Array.Empty<CorruptionDef>();
          foreach (var entry in s_all)
          {
            if (entry != null && !string.IsNullOrEmpty(entry.id))
              s_byId[entry.id] = entry;
          }
        }
        catch (Exception e)
        {
          Debug.LogError($"[CorruptionDatabase] Parse failed: {e.Message}");
          s_all = Array.Empty<CorruptionDef>();
        }
      }))
      {
        s_all = Array.Empty<CorruptionDef>();
      }
    }

    public static CorruptionDef Get(string id)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(id) && s_byId.TryGetValue(id, out var entry) ? entry : null;
    }
  }
}
