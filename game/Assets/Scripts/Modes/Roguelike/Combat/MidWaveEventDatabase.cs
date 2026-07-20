using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B1: 波中事件配表。</summary>
  public static class MidWaveEventDatabase
  {
    [Serializable]
    public class SettingsDef
    {
      public int min_wave = 8;
      public float trigger_delay_min = 20f;
      public float trigger_delay_max = 30f;
    }

    [Serializable]
    public class EventEntry
    {
      public string id;
      public string display_name;
      public string description;
      public float warning_seconds = 2.5f;
      public float duration = 10f;
      public float radius = 3.5f;
      public float tick_interval = 0.85f;
      public float damage = 12f;
      public string color = "#FFFFFF";
    }

    [Serializable]
    class Root
    {
      public SettingsDef settings;
      public EventEntry[] events;
    }

    static readonly Dictionary<string, EventEntry> s_byId = new();
    static EventEntry[] s_all;
    static SettingsDef s_settings = new();
    static bool s_loaded;

    public static SettingsDef CurrentSettings
    {
      get
      {
        EnsureLoaded();
        return s_settings;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_byId.Clear();
      s_all = Array.Empty<EventEntry>();

      JsonDataLoader.TryParse("combat/mid_wave_events", json =>
      {
        var root = JsonUtility.FromJson<Root>(json);
        s_settings = root?.settings ?? new SettingsDef();
        s_all = root?.events ?? Array.Empty<EventEntry>();
        foreach (var entry in s_all)
        {
          if (entry != null && !string.IsNullOrEmpty(entry.id))
            s_byId[entry.id] = entry;
        }
      });
    }

    public static EventEntry Get(string id)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(id) && s_byId.TryGetValue(id, out var entry) ? entry : null;
    }

    public static EventEntry PickRandom()
    {
      EnsureLoaded();
      if (s_all == null || s_all.Length == 0)
        return null;
      return s_all[UnityEngine.Random.Range(0, s_all.Length)];
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
