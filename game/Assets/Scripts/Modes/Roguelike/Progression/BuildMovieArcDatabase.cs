using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>C8: Per-build 20min movie arc data.</summary>
  public static class BuildMovieArcDatabase
  {
    [Serializable]
    public class BeatDef
    {
      public int level;
      public string title;
      public string subtitle;
    }

    [Serializable]
    public class ArcDef
    {
      public string build_id;
      public string display_name;
      public string[] capstone_tags;
      public BeatDef[] beats;
      public int capstone_wave = 20;
      public string capstone_title;
      public string capstone_subtitle;
    }

    [Serializable]
    public class WeightBand
    {
      public int min_wave;
      public int max_wave;
      public float mult = 1f;
    }

    [Serializable]
    class Root
    {
      public ArcDef[] arcs;
      public WeightBand[] capstone_weight_by_wave;
    }

    static readonly Dictionary<string, ArcDef> s_byBuild = new();
    static WeightBand[] s_weightBands = Array.Empty<WeightBand>();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_byBuild.Clear();

      JsonDataLoader.TryParse("progression/build_movie_arcs", json =>
      {
        var root = JsonUtility.FromJson<Root>(json);
        s_weightBands = root?.capstone_weight_by_wave ?? Array.Empty<WeightBand>();
        foreach (var arc in root?.arcs ?? Array.Empty<ArcDef>())
        {
          if (arc != null && !string.IsNullOrEmpty(arc.build_id))
            s_byBuild[arc.build_id] = arc;
        }
      });
    }

    public static ArcDef GetForBuild(string buildId)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(buildId) && s_byBuild.TryGetValue(buildId, out var arc) ? arc : null;
    }

    public static float CapstoneWeightMult(int wave)
    {
      EnsureLoaded();
      foreach (var band in s_weightBands)
      {
        if (band != null && wave >= band.min_wave && wave <= band.max_wave)
          return band.mult > 0f ? band.mult : 1f;
      }
      return 1f;
    }
  }
}
