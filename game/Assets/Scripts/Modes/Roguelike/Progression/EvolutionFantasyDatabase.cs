using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>B8: Loads behavior_verb and fantasy_text from detached-weapon evolution JSON.</summary>
  public static class EvolutionFantasyDatabase
  {
    static readonly Dictionary<string, EvolutionMeta> s_meta = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      s_meta.Clear();

      foreach (var id in new[] { "laser", "missile", "explosion", "pulse", "boomerang", "trail" })
      {
        JsonDataLoader.TryParse($"weapons/evolutions/{id}_evolution", json =>
        {
          try
          {
            var root = JsonUtility.FromJson<EvolutionRoot>(json);
            if (root?.evolution == null || string.IsNullOrEmpty(root.evolution.id))
              return;

            s_meta[root.evolution.id] = new EvolutionMeta
            {
              behaviorVerb = root.evolution.behavior_verb,
              fantasyText = root.evolution.fantasy_text
            };
          }
          catch (Exception e)
          {
            Debug.LogError($"[EvolutionFantasyDatabase] Parse {id} failed: {e.Message}");
          }
        });
      }
    }

    public static string GetBehaviorVerb(string evolutionId)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(evolutionId)
        && s_meta.TryGetValue(evolutionId, out var meta)
        ? meta.behaviorVerb
        : null;
    }

    public static string GetFantasyText(string evolutionId)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(evolutionId)
        && s_meta.TryGetValue(evolutionId, out var meta)
        ? meta.fantasyText
        : null;
    }

    public static bool HasBehaviorVerb(string evolutionId) =>
      !string.IsNullOrEmpty(GetBehaviorVerb(evolutionId));

    sealed class EvolutionMeta
    {
      public string behaviorVerb;
      public string fantasyText;
    }

    [Serializable]
    sealed class EvolutionRoot
    {
      public EvolutionDef evolution;
    }

    [Serializable]
    sealed class EvolutionDef
    {
      public string id;
      public string behavior_verb;
      public string fantasy_text;
    }
  }
}
