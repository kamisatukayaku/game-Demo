using System.Collections.Generic;
using System;
using UnityEngine;

using Game.Shared.Data;
namespace Game.Modes.Roguelike.Progression
{
  public static class WeaponThemeDatabase
  {
    static readonly Dictionary<string, WeaponThemeDef> s_themes = new();
    static bool s_loaded;

    public static IReadOnlyDictionary<string, WeaponThemeDef> Themes
    {
      get
      {
        EnsureLoaded();
        return s_themes;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("themes/weapon_themes", Parse))
        Debug.LogWarning("[WeaponThemeDatabase] weapon_themes.json not found.");
    }

    public static WeaponThemeDef Get(string themeId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(themeId))
        return null;

      s_themes.TryGetValue(themeId, out var def);
      return def;
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<WeaponThemeRoot>(json);
        s_themes.Clear();
        if (root?.themes == null)
          return;

        foreach (var t in root.themes)
        {
          if (t != null && !string.IsNullOrEmpty(t.id))
            s_themes[t.id] = t;
        }

        Debug.Log($"[WeaponThemeDatabase] Loaded {s_themes.Count} themes.");
      }
      catch (Exception e)
      {
        Debug.LogError($"[WeaponThemeDatabase] Parse failed: {e.Message}");
      }
    }

    [Serializable]
    class WeaponThemeRoot
    {
      public WeaponThemeDef[] themes;
    }

    [Serializable]
    public class WeaponThemeDef
    {
      public string id;
      public string display_name;
      public string description;
      public string attack_profile_id;
      public ThemeBaseStats base_stats;
    }

    [Serializable]
    public class ThemeBaseStats
    {
      public float attack_mult = 1f;
      public float attack_speed_mult = 1f;
      public float range;
    }
  }
}
