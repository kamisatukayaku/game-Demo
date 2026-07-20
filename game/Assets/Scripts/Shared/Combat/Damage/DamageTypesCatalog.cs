using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Shared.Combat.Damage
{
  /// <summary>读取 data/damage_types.json?/summary>
  public static class DamageTypesCatalog
  {
    public class DamageTypeDef
    {
      public string id;
      public string display_name;
      public bool bypass_armor;
      public float armor_coefficient = 1f;
      // Legacy JSON field; ignored at runtime.
      public bool affected_by_pollution;
    }

    static readonly Dictionary<string, DamageTypeDef> s_defs = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_defs.Clear();

      if (!JsonDataLoader.TryParse("combat/damage_types", Parse)
          && !JsonDataLoader.TryParse("damage_types", Parse))
      {
        RegisterDefaults();
      }
    }

    public static DamageTypeDef Get(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id))
        return null;

      s_defs.TryGetValue(id, out var def);
      return def;
    }

    public static float GetArmorCoefficient(string damageTypeId)
    {
      var def = Get(damageTypeId);
      if (def == null)
        return 1f;

      if (def.bypass_armor)
        return 0f;

      return def.armor_coefficient > 0f ? def.armor_coefficient : 1f;
    }

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.definitions == null)
      {
        RegisterDefaults();
        return;
      }

      foreach (var entry in root.definitions)
      {
        if (entry == null || string.IsNullOrEmpty(entry.id))
          continue;

        if (entry.armor_coefficient <= 0f && !entry.bypass_armor)
          entry.armor_coefficient = 1f;

        s_defs[entry.id] = entry;
      }

      // Map legacy damage types from old saves / profiles.
      MapLegacy("eco", "kinetic");
      MapLegacy("tech", "energy");
      MapLegacy("pollution", "kinetic");
    }

    static void MapLegacy(string legacyId, string targetId)
    {
      if (s_defs.ContainsKey(legacyId) || !s_defs.TryGetValue(targetId, out var target))
        return;

      s_defs[legacyId] = new DamageTypeDef
      {
        id = legacyId,
        display_name = legacyId,
        bypass_armor = target.bypass_armor,
        armor_coefficient = target.armor_coefficient
      };
    }

    static void RegisterDefaults()
    {
      s_defs["physical"] = new DamageTypeDef { id = "physical", armor_coefficient = 1f };
      s_defs["kinetic"] = new DamageTypeDef { id = "kinetic", armor_coefficient = 0.85f };
      s_defs["energy"] = new DamageTypeDef { id = "energy", armor_coefficient = 0.7f };
      s_defs["impact"] = new DamageTypeDef { id = "impact", armor_coefficient = 1.1f };
      s_defs["true"] = new DamageTypeDef { id = "true", bypass_armor = true, armor_coefficient = 0f };
    }

    [Serializable]
    class Root
    {
      public DamageTypeDef[] definitions;
    }
  }
}