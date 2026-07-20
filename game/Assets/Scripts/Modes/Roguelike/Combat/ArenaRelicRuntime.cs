using System;
using System.Collections.Generic;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>A14: Runtime state for in-run Boss Relic picks.</summary>
  public static class ArenaRelicRuntime
  {
    static readonly List<RelicDef> s_pool = new();
    static readonly HashSet<string> s_picked = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      s_pool.Clear();
      JsonDataLoader.TryParse("progression/arena_relics", json =>
      {
        try
        {
          var root = JsonUtility.FromJson<RelicRoot>(json);
          if (root?.relics != null)
            s_pool.AddRange(root.relics);
        }
        catch (Exception e)
        {
          Debug.LogError($"[ArenaRelicRuntime] Parse failed: {e.Message}");
        }
      });
    }

    public static RelicDef[] RollOffer(int count = 3)
    {
      EnsureLoaded();
      var available = new List<RelicDef>();
      foreach (var relic in s_pool)
      {
        if (relic != null && !s_picked.Contains(relic.id))
          available.Add(relic);
      }

      count = Mathf.Min(count, available.Count);
      var result = new RelicDef[count];
      for (var i = 0; i < count; i++)
      {
        var idx = UnityEngine.Random.Range(0, available.Count);
        result[i] = available[idx];
        available.RemoveAt(idx);
      }

      return result;
    }

    public static void ApplyRelic(RelicDef relic)
    {
      if (relic == null || string.IsNullOrEmpty(relic.id))
        return;

      s_picked.Add(relic.id);
      ArenaRelicEffects.Apply(relic);
    }

    [Serializable]
    sealed class RelicRoot
    {
      public RelicDef[] relics;
    }

    [Serializable]
    public sealed class RelicDef
    {
      public string id;
      public string display_name;
      public string description;
      public string stat;
      public float value;
    }
  }

  static class ArenaRelicEffects
  {
    public static void Apply(ArenaRelicRuntime.RelicDef relic)
    {
      if (relic == null)
        return;

      switch (relic.stat)
      {
        case "build_phase_reduce":
          WaveDirector.Instance?.ApplyBuildPhaseReduction(relic.value);
          break;
        case "heal_on_kill_pct":
          Game.Modes.Roguelike.Build.Runtime.RunBuildState.AddStat("heal_on_kill_pct", relic.value);
          break;
        case "exp_gain_mult":
          Game.Modes.Roguelike.Build.Runtime.RunBuildState.AddStat("exp_gain_mult", relic.value);
          break;
        case "edge_hazard_mult":
          CircleArenaController.SetEdgeHazardMult(relic.value);
          break;
        case "dash_cooldown_reduce":
          PlayerDashCooldownReducer.Apply(relic.value);
          break;
        case "ranged_overload_bonus":
          RangedOverloadRuntime.ApplyRelicBonus(relic.value);
          break;
        case "boss_damage_mult":
        case "elite_damage_mult":
        case "detached_part_damage_mult":
        case "weapon_attack_speed_mult":
        case "primary_projectile_damage_mult":
        case "all_damage_mult":
          Game.Modes.Roguelike.Build.Runtime.RunBuildState.AddStat(relic.stat, relic.value);
          break;
      }
    }
  }

  static class PlayerDashCooldownReducer
  {
    public static float Bonus { get; private set; }
    public static void Apply(float value) => Bonus += value;
    public static void Reset() => Bonus = 0f;
  }
}
