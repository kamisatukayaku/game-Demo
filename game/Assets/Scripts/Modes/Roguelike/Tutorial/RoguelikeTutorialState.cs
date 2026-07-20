using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Persistent Roguelike tutorial progress via PlayerPrefs (Roguelike.Tutorial.*).</summary>
  public static class RoguelikeTutorialState
  {
    public const string KeyPrefix = "Roguelike.Tutorial.";

    public static bool IsStepComplete(string stepId) =>
      !string.IsNullOrEmpty(stepId)
      && PlayerPrefs.GetInt(KeyPrefix + "Step." + stepId, 0) == 1;

    public static void MarkStepComplete(string stepId)
    {
      if (string.IsNullOrEmpty(stepId))
        return;
      PlayerPrefs.SetInt(KeyPrefix + "Step." + stepId, 1);
      PlayerPrefs.Save();
    }

    public static bool IsZoneIntroComplete(string zoneId) =>
      !string.IsNullOrEmpty(zoneId)
      && PlayerPrefs.GetInt(KeyPrefix + "Zone." + zoneId, 0) == 1;

    public static void MarkZoneIntroComplete(string zoneId)
    {
      if (string.IsNullOrEmpty(zoneId))
        return;
      PlayerPrefs.SetInt(KeyPrefix + "Zone." + zoneId, 1);
      PlayerPrefs.Save();
    }

    public static bool IsZoneProximityComplete(string zoneId) =>
      !string.IsNullOrEmpty(zoneId)
      && PlayerPrefs.GetInt(KeyPrefix + "ZoneProx." + zoneId, 0) == 1;

    public static void MarkZoneProximityComplete(string zoneId)
    {
      if (string.IsNullOrEmpty(zoneId))
        return;
      PlayerPrefs.SetInt(KeyPrefix + "ZoneProx." + zoneId, 1);
      PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
      var keys = CollectRoguelikeTutorialKeys();
      foreach (var key in keys)
        PlayerPrefs.DeleteKey(key);
      PlayerPrefs.Save();
    }

    public static IReadOnlyList<string> CollectRoguelikeTutorialKeys()
    {
      var result = new List<string>();
#if UNITY_EDITOR
      // PlayerPrefs has no enumeration API; track known suffixes in tests and reset via explicit list.
#endif
      return result;
    }

    public static void ResetAllKnown(IEnumerable<string> stepIds, IEnumerable<string> zoneIds)
    {
      if (stepIds != null)
      {
        foreach (var id in stepIds)
        {
          if (!string.IsNullOrEmpty(id))
            PlayerPrefs.DeleteKey(KeyPrefix + "Step." + id);
        }
      }

      if (zoneIds != null)
      {
        foreach (var id in zoneIds)
        {
          if (string.IsNullOrEmpty(id))
            continue;
          PlayerPrefs.DeleteKey(KeyPrefix + "Zone." + id);
          PlayerPrefs.DeleteKey(KeyPrefix + "ZoneProx." + id);
        }
      }

      PlayerPrefs.Save();
    }

    public static bool ShouldSkipPersistence => SandboxBypassPersistence;

    public static bool SandboxBypassPersistence { get; set; }
  }
}
