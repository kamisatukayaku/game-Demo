using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>C1: Arena layout definitions (narrow_ring / island_chain / cross).</summary>
  public static class ArenaLayoutDatabase
  {
    [Serializable]
    public class IslandDef
    {
      public float angle_deg;
      public float radius = 7f;
      public float orbit = 18f;
    }

    [Serializable]
    public class LayoutEntry
    {
      public string id;
      public string display_name;
      public string description;
      public float base_radius = 28f;
      public string shape_hint = "circle";
      public string fill_color = "#335A7A";
      public string border_color = "#C8F5FF";
      public float corridor_width = 11f;
      public IslandDef[] islands;
    }

    [Serializable]
    class Root
    {
      public LayoutEntry[] layouts;
    }

    static readonly Dictionary<string, LayoutEntry> s_byId = new();
    static LayoutEntry[] s_all;
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_byId.Clear();
      s_all = Array.Empty<LayoutEntry>();

      JsonDataLoader.TryParse("combat/arena_layouts", json =>
      {
        var root = JsonUtility.FromJson<Root>(json);
        s_all = root?.layouts ?? Array.Empty<LayoutEntry>();
        foreach (var entry in s_all)
        {
          if (entry != null && !string.IsNullOrEmpty(entry.id))
            s_byId[entry.id] = entry;
        }
      });
    }

    public static LayoutEntry Get(string id)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(id) && s_byId.TryGetValue(id, out var entry) ? entry : null;
    }

    public static LayoutEntry PickForRun()
    {
      EnsureLoaded();
      if (s_all == null || s_all.Length == 0)
        return null;

      var expanse = Get("expanse");
      if (expanse != null)
        return expanse;

      return PickLegacyCompactLayout();
    }

    /// <summary>Pre-expanse arena layouts (excludes the default expanse map).</summary>
    public static LayoutEntry PickLegacyCompactLayout()
    {
      EnsureLoaded();
      if (s_all == null || s_all.Length == 0)
        return null;

      var island = Get("island_chain");
      if (island != null)
        return island;

      LayoutEntry largest = null;
      for (var i = 0; i < s_all.Length; i++)
      {
        var entry = s_all[i];
        if (entry == null || string.Equals(entry.id, "expanse", StringComparison.Ordinal))
          continue;

        if (largest == null || entry.base_radius > largest.base_radius)
          largest = entry;
      }

      return largest;
    }

    public static Color ParseColor(string hex, Color fallback)
    {
      if (string.IsNullOrEmpty(hex))
        return fallback;
      if (!hex.StartsWith("#", StringComparison.Ordinal))
        hex = "#" + hex;
      return ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
    }
  }
}
