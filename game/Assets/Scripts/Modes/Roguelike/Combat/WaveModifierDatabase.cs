using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Progression;
using Game.Shared.Data;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>S8: 波次 Modifier 配表加载与查询。</summary>
  public static class WaveModifierDatabase
  {
    [Serializable]
    public class WaveModifierEntry
    {
      public string id;
      public string display_name;
      public string description;
      public string color;
      public bool hard_only;
    }

    [Serializable]
    class WaveModifierRoot
    {
      public WaveModifierEntry[] modifiers;
    }

    static readonly Dictionary<string, WaveModifierEntry> s_byId = new();
    static WaveModifierEntry[] s_all;
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_byId.Clear();
      if (!JsonDataLoader.TryParse("enemies/wave_modifiers", json =>
      {
        var root = JsonUtility.FromJson<WaveModifierRoot>(json);
        s_all = root?.modifiers ?? Array.Empty<WaveModifierEntry>();
        foreach (var entry in s_all)
        {
          if (entry != null && !string.IsNullOrEmpty(entry.id))
            s_byId[entry.id] = entry;
        }
      }))
      {
        s_all = Array.Empty<WaveModifierEntry>();
      }
    }

    public static WaveModifierEntry Get(string id)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(id) && s_byId.TryGetValue(id, out var entry) ? entry : GetOrStandard(id);
    }

    static WaveModifierEntry GetOrStandard(string id)
    {
      if (s_byId.TryGetValue("standard", out var standard))
        return standard;
      return s_all != null && s_all.Length > 0 ? s_all[0] : null;
    }

    public static WaveModifierEntry PickForWave(int waveNumber, int totalWaves)
    {
      EnsureLoaded();
      if (s_all == null || s_all.Length == 0)
        return null;

      if (ArenaDifficultyRuntime.IsHard && waveNumber >= 4 && waveNumber % 4 == 0)
        return PickHardModifier(waveNumber);

      if (waveNumber == 7 || waveNumber == 17)
        return Get("night");
      if (waveNumber == 9 || waveNumber == 19)
        return Get("frenzy");
      if (HuntContractRuntime.IsContractWave(waveNumber))
        return Get("hunt_contract");
      if (waveNumber % 3 == 0 && waveNumber > 0)
        return Get("elite_hunt");
      if (waveNumber % 5 == 0 && waveNumber < totalWaves)
        return Get("calm");
      return Get("standard");
    }

    static WaveModifierEntry PickHardModifier(int waveNumber)
    {
      var hardIds = new[] { "double_elite", "support_x2", "no_dash_wave" };
      var idx = (waveNumber / 4) % hardIds.Length;
      return Get(hardIds[idx]);
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
