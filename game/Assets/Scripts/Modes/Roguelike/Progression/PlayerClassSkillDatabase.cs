using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Player class active-skill configuration.</summary>
  public static class PlayerClassSkillDatabase
  {
    static readonly Dictionary<string, ClassSkillSet> s_sets = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("skills/player_class_skills", Parse))
        SeedDefaults();
    }

    public static ClassSkillSet Get(string classId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(classId))
        return null;

      s_sets.TryGetValue(classId, out var set);
      return set;
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<Root>(json);
        s_sets.Clear();
        if (root?.classes == null)
        {
          SeedDefaults();
          return;
        }

        foreach (var entry in root.classes)
        {
          if (entry == null || string.IsNullOrEmpty(entry.id))
            continue;
          NormalizeLegacySlots(entry);
          s_sets[entry.id] = entry;
        }
      }
      catch (Exception e)
      {
        Debug.LogError($"[PlayerClassSkillDatabase] Parse failed: {e.Message}");
        SeedDefaults();
      }
    }

    static void SeedDefaults()
    {
      s_sets.Clear();
      s_sets["mage"] = BuildMageSet();
    }

    static ClassSkillSet BuildMageSet()
    {
      return new ClassSkillSet
      {
        id = "mage",
        slots = new[]
        {
          new SkillSlotDef
          {
            slot = 1,
            id = "skill_mage_gravity_well",
            display_name = "引力井",
            kind = "gravity_well",
            attack_profile_id = "skill_mage_gravity_well",
            cooldown = 7f,
            base_radius = 3.2f,
            duration = 1.6f
          },
          new SkillSlotDef
          {
            slot = 2,
            id = "skill_mage_tidal_pulse",
            display_name = "潮汐脉冲",
            kind = "tidal_pulse",
            attack_profile_id = "skill_mage_tidal_pulse",
            cooldown = 9f,
            base_radius = 3f,
            duration = 3.5f
          }
        }
      };
    }

    static void NormalizeLegacySlots(ClassSkillSet set)
    {
      if (set?.slots == null)
        return;

      foreach (var slot in set.slots)
      {
        if (slot == null)
          continue;

        if (slot.kind == "frost_ward")
          slot.kind = "tidal_pulse";

        if (slot.id == "skill_mage_frost_ward")
        {
          slot.id = "skill_mage_tidal_pulse";
          slot.attack_profile_id = "skill_mage_tidal_pulse";
        }
      }
    }

    [Serializable]
    class Root
    {
      public ClassSkillSet[] classes;
    }

    [Serializable]
    public class ClassSkillSet
    {
      public string id;
      public SkillSlotDef[] slots;
    }

    [Serializable]
    public class SkillSlotDef
    {
      public int slot;
      public string id;
      public string display_name;
      public string kind;
      public string aura_kind;
      public string attack_profile_id;
      public float cooldown = 1f;
      public float duration = 5f;
      public float base_radius = 2.5f;
    }
  }
}
