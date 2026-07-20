using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Data;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  public static class WarriorProgressionDatabase
  {
    static readonly List<LevelUpChoiceDatabase.UpgradeDef> s_upgrades = new();
    static WarriorBaseDef s_base;
    static bool s_loaded;
    static bool s_valid;

    public static bool IsValid
    {
      get
      {
        EnsureLoaded();
        return s_valid;
      }
    }

    public static WarriorBaseDef Base
    {
      get
      {
        EnsureLoaded();
        return s_base;
      }
    }

    public static IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> Upgrades
    {
      get
      {
        EnsureLoaded();
        return s_upgrades;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_valid = JsonDataLoader.TryParse("upgrades/warrior_progression", Parse);
      if (!s_valid)
        Debug.LogError("[Warrior] data/roguelike/upgrades/warrior_progression.json is missing or invalid.");
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<WarriorProgressionRoot>(json);
        if (root?.warrior?.baseConfig == null || root.warrior.upgrades == null)
          throw new InvalidOperationException("Missing warrior.base or warrior.upgrades.");

        s_base = root.warrior.baseConfig;
        s_upgrades.Clear();

        foreach (var source in root.warrior.upgrades)
        {
          if (source == null || string.IsNullOrEmpty(source.id))
            continue;

          var modifierList = new List<LevelUpChoiceDatabase.StatModifier>();

          // Support multi-modifier upgrades via "modifiers" array
          if (source.modifiers != null && source.modifiers.Length > 0)
          {
            foreach (var mod in source.modifiers)
            {
              var stat = ResolveStat(mod.type);
              if (!string.IsNullOrEmpty(stat))
                modifierList.Add(new LevelUpChoiceDatabase.StatModifier
                {
                  stat = stat,
                  op = "add",
                  value = mod.value
                });
            }
          }
          else if (!string.IsNullOrEmpty(source.type))
          {
            // Legacy single type:value
            var stat = ResolveStat(source.type);
            if (!string.IsNullOrEmpty(stat))
              modifierList.Add(new LevelUpChoiceDatabase.StatModifier
              {
                stat = stat,
                op = "add",
                value = source.value
              });
          }

          if (modifierList.Count == 0)
          {
            Debug.LogWarning($"[Warrior] Ignoring upgrade '{source.id}' — no valid modifiers.");
            continue;
          }

          s_upgrades.Add(new LevelUpChoiceDatabase.UpgradeDef
          {
            route = "equipment",
            weapon_theme = "warrior",
            id = source.id,
            display_name = source.displayName,
            description = source.description,
            repeatable = source.repeatable,
            max_stacks = source.maxStacks,
            tags = source.tags,
            requires_ids = source.requiresIds,
            category = source.category,
            offer_weight = source.offerWeight,
            classes = new[] { "warrior" },
            modifiers = modifierList.ToArray()
          });
        }

        s_valid = true;
        Debug.Log($"[Warrior] Loaded {s_upgrades.Count} upgrades from upgrades/warrior_progression.json.");
      }
      catch (Exception e)
      {
        s_valid = false;
        Debug.LogError($"[Warrior] upgrades/warrior_progression.json parse failed: {e.Message}");
      }
    }

    static string ResolveStat(string type) => type switch
    {
      "weaponCount" => StatKeys.WarriorWeaponCount,
      "rotationSpeed" => StatKeys.WarriorRotationSpeed,
      "radius" => StatKeys.WarriorRadius,
      "damage" => StatKeys.WarriorDamage,
      "weaponSize" => StatKeys.WarriorWeaponSize,
      "orbitSplashRatio" => StatKeys.WarriorOrbitSplashRatio,
      "orbitSyncBonus" => StatKeys.WarriorOrbitSyncBonus,
      "spiritbladeCount" => StatKeys.WarriorSpiritBladeCount,
      "spiritbladeHoming" => StatKeys.WarriorSpiritBladeHoming,
      "spiritbladePierce" => StatKeys.WarriorSpiritBladePierce,
      "spiritbladeRecast" => StatKeys.WarriorSpiritBladeRecast,
      "spiritbladeReturnSpeed" => StatKeys.WarriorSpiritBladeReturnSpeed,
      "spiritbladeSpeed" => StatKeys.WarriorSpiritBladeSpeed,
      "spiritEnabled" => StatKeys.WarriorSpiritEnabled,
      "spiritInfinite" => StatKeys.WarriorSpiritInfinite,
      "satelliteCatastrophe" => StatKeys.WarriorSatelliteCatastrophe,
      "titanFlag" => StatKeys.WarriorTitanFlag,
      "spiritTrail" => StatKeys.WarriorSpiritBladeTrail,
      "meleeExplosionRadius" => StatKeys.MeleeExplosionRadius,
      "meleeKnockbackChance" => StatKeys.MeleeKnockbackChance,
      "projectileBounce" => StatKeys.WarriorProjectileBounce,
      _ => null
    };

    [Serializable]
    class WarriorProgressionRoot
    {
      public WarriorDef warrior;
    }

    [Serializable]
    class WarriorDef
    {
      [SerializeField] WarriorBaseDef @base;
      public WarriorUpgradeDef[] upgrades;
      public WarriorBaseDef baseConfig => @base;
    }

    [Serializable]
    public class WarriorBaseDef
    {
      public int weaponCount;
      public float rotationSpeed;
      public float radius;
      public float damage;
      public float weaponSize;
      public float hitInterval;
    }

    [Serializable]
    class WarriorUpgradeDef
    {
      public string id;
      public string displayName;
      public string description;
      public string type;
      public float value;
      public bool repeatable;
      public int maxStacks;
      public string[] tags;
      public string[] requiresIds;
      public string category;
      public float offerWeight;
      public WarriorModifierDef[] modifiers;
    }

    [Serializable]
    class WarriorModifierDef
    {
      public string type;
      public float value;
    }
  }
}
