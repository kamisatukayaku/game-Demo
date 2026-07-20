using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Modes.Roguelike.Tutorial
{
  public enum GroundZoneType
  {
    Beneficial,
    Hazard,
    Objective,
    Control,
    Neutral
  }

  public static class GroundZoneDefinitionDatabase
  {
    const string Stem = "tutorial/ground_zones";
    const string UnknownZoneId = "unknown_zone";

    [Serializable]
    class Root
    {
      public ZoneDef[] zones;
    }

    [Serializable]
    public class ZoneDef
    {
      public string id;
      public string displayName;
      public string description;
      public string proximityHint;
      public string enteredHint;
      public string type;
      public string color;
      public string symbol;
      public bool showOnFirstEncounter = true;
      public bool isHazardous;
    }

    static readonly Dictionary<string, ZoneDef> s_zones = new();
    static ZoneDef s_unknown;
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_zones.Clear();

      if (!JsonDataLoader.TryParse(Stem, json =>
          {
            var root = JsonUtility.FromJson<Root>(json);
            if (root?.zones == null)
              return;
            foreach (var zone in root.zones)
            {
              if (zone == null || string.IsNullOrEmpty(zone.id))
                continue;
              s_zones[zone.id] = zone;
              if (zone.id == UnknownZoneId)
                s_unknown = zone;
            }
          }))
      {
        ApplyDefaults();
      }

      if (s_unknown == null)
        s_unknown = CreateUnknownDefault();
    }

    static void ApplyDefaults()
    {
      s_zones["unknown_zone"] = CreateUnknownDefault();
      s_unknown = s_zones["unknown_zone"];
    }

    static ZoneDef CreateUnknownDefault() => new()
    {
      id = UnknownZoneId,
      displayName = "未知区域",
      description = "靠近查看效果",
      proximityHint = "未知区域：靠近查看效果",
      enteredHint = "未知区域：靠近查看效果",
      type = "Neutral",
      color = "#A0B4C8",
      symbol = "Dot",
      showOnFirstEncounter = true,
      isHazardous = false
    };

    public static ZoneDef Get(string zoneId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(zoneId))
        return s_unknown ?? CreateUnknownDefault();
      return s_zones.TryGetValue(zoneId, out var def) ? def : s_unknown ?? CreateUnknownDefault();
    }

    public static IEnumerable<string> AllZoneIds
    {
      get
      {
        EnsureLoaded();
        return s_zones.Keys;
      }
    }

    public static GroundZoneType ParseType(string raw)
    {
      if (string.IsNullOrEmpty(raw))
        return GroundZoneType.Neutral;
      return raw switch
      {
        "Beneficial" => GroundZoneType.Beneficial,
        "Hazard" => GroundZoneType.Hazard,
        "Objective" => GroundZoneType.Objective,
        "Control" => GroundZoneType.Control,
        _ => GroundZoneType.Neutral
      };
    }

    public static Color ParseColor(string hex, Color fallback)
    {
      if (string.IsNullOrEmpty(hex))
        return fallback;
      if (!hex.StartsWith("#", StringComparison.Ordinal))
        hex = "#" + hex;
      return ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
    }

    public static int GetPromptPriority(ZoneDef def)
    {
      if (def == null)
        return 2;
      return ParseType(def.type) == GroundZoneType.Hazard ? 0 : 1;
    }
  }
}
