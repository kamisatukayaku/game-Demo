using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>C6: Three-step narrative event chain data.</summary>
  public static class NarrativeEventDatabase
  {
    [Serializable]
    public class StepDef
    {
      public int wave;
      public string phase;
      public string title;
      public string body;
      public string banner_color = "#FFFFFF";
      public float spawn_interval_mult = 1f;
      public int xp_bonus;
    }

    [Serializable]
    public class ChainDef
    {
      public string id;
      public string display_name;
      public StepDef[] steps;
    }

    [Serializable]
    class Root
    {
      public ChainDef[] chains;
    }

    static ChainDef s_activeChain;
    static bool s_loaded;

    public static ChainDef ActiveChain
    {
      get
      {
        EnsureLoaded();
        return s_activeChain;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      JsonDataLoader.TryParse("combat/narrative_events", json =>
      {
        var root = JsonUtility.FromJson<Root>(json);
        var chains = root?.chains;
        if (chains == null || chains.Length == 0)
          return;
        s_activeChain = chains[UnityEngine.Random.Range(0, chains.Length)];
      });
    }

    public static StepDef GetStepForWave(int wave)
    {
      EnsureLoaded();
      if (s_activeChain?.steps == null)
        return null;

      foreach (var step in s_activeChain.steps)
      {
        if (step != null && step.wave == wave)
          return step;
      }

      return null;
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
