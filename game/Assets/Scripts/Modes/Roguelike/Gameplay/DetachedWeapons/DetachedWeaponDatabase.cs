using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  public static class DetachedWeaponDatabase
  {
    static readonly Dictionary<string, DetachedWeaponDefinition> Definitions = new();
    static readonly Dictionary<string, DetachedWeaponAttackModeDefinition> Modes = new();
    static bool s_loaded;

    public static IReadOnlyDictionary<string, DetachedWeaponDefinition> All
    {
      get
      {
        EnsureLoaded();
        return Definitions;
      }
    }

    public static IReadOnlyDictionary<string, DetachedWeaponAttackModeDefinition> AttackModes
    {
      get
      {
        EnsureLoaded();
        return Modes;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("weapons/detached_weapons", Parse))
        Debug.LogError("[DetachedWeapons] data/roguelike/weapons/detached_weapons.json is missing or invalid.");
    }

    public static DetachedWeaponDefinition Get(string id)
    {
      EnsureLoaded();
      return !string.IsNullOrEmpty(id) && Definitions.TryGetValue(id, out var definition)
        ? definition
        : null;
    }

    public static bool TryParseMode(string id, out DetachedWeaponAttackMode mode)
    {
      mode = default;
      if (string.IsNullOrEmpty(id))
        return false;
      return Enum.TryParse(id, true, out mode);
    }

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<DetachedWeaponRoot>(json);
      Definitions.Clear();
      Modes.Clear();

      if (root?.attack_modes != null)
      {
        foreach (var mode in root.attack_modes)
          if (mode != null && !string.IsNullOrEmpty(mode.id))
            Modes[mode.id] = mode;
      }

      if (root?.weapons != null)
      {
        foreach (var weapon in root.weapons)
          if (weapon != null && !string.IsNullOrEmpty(weapon.id))
            Definitions[weapon.id] = weapon;
      }
    }

    [Serializable]
    sealed class DetachedWeaponRoot
    {
      public int schema_version;
      public DetachedWeaponAttackModeDefinition[] attack_modes;
      public DetachedWeaponDefinition[] weapons;
    }
  }
}
