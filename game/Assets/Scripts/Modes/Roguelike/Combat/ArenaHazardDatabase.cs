using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B6: arena hazard definitions and rotation schedule.</summary>
  public static class ArenaHazardDatabase
  {
    [Serializable]
    public class HazardDef
    {
      public string id;
      public string display_name;
      public string description;
      public float edge_damage_mult = 1f;
      public float tick_interval = 0.35f;
      public float rotation_deg_per_sec = 40f;
      public float line_half_width = 1.4f;
      public float damage_per_tick = 8f;
      public float pull_strength = 6f;
      public float pull_radius = 0f;
      public float core_radius = 4.5f;
      public float core_damage_per_tick = 5f;
    }

    [Serializable]
    class HazardRoot
    {
      public int[] rotation_waves;
      public HazardDef[] hazards;
    }

    static readonly List<HazardDef> s_hazards = new();
    static int[] s_rotationWaves = { 5, 10, 15 };
    static bool s_loaded;

    public static IReadOnlyList<HazardDef> Hazards => s_hazards;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_hazards.Clear();

      if (!JsonDataLoader.TryParse("combat/arena_hazards", json =>
          {
            try
            {
              var root = JsonUtility.FromJson<HazardRoot>(json);
              if (root?.hazards != null)
                s_hazards.AddRange(root.hazards);
              if (root?.rotation_waves != null && root.rotation_waves.Length > 0)
                s_rotationWaves = root.rotation_waves;
            }
            catch (Exception e)
            {
              Debug.LogError($"[ArenaHazardDatabase] Parse failed: {e.Message}");
            }
          }))
      {
        s_hazards.Add(new HazardDef { id = "toxic_edge", display_name = "毒边", edge_damage_mult = 1.35f });
        s_hazards.Add(new HazardDef { id = "laser_sweep", display_name = "激光扫射", rotation_deg_per_sec = 42f });
        s_hazards.Add(new HazardDef { id = "gravity_well", display_name = "重力井", pull_strength = 6.5f });
      }
    }

    public static HazardDef Get(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id))
        return null;

      foreach (var hazard in s_hazards)
      {
        if (hazard != null && hazard.id == id)
          return hazard;
      }

      return null;
    }

    public static string PickForWave(int waveNumber)
    {
      EnsureLoaded();
      if (s_hazards.Count == 0 || waveNumber < s_rotationWaves[0])
        return null;

      var cycle = 0;
      for (var i = 0; i < s_rotationWaves.Length; i++)
      {
        if (waveNumber >= s_rotationWaves[i])
          cycle = i + 1;
      }

      if (cycle <= 0)
        return null;

      return s_hazards[(cycle - 1) % s_hazards.Count].id;
    }
  }
}
