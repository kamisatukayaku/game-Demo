using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Shared.Core;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  static class DetachedWeaponSpawnTargets
  {
    public static Vector3 Compute(Transform owner, string weaponId, int slotIndex, int totalSlots)
    {
      if (owner == null)
        return Vector3.zero;

      var def = DetachedWeaponDatabase.Get(weaponId);
      var ownerPos = GameplayPlane.Position2D(owner);
      var angleDeg = slotIndex * 360f / Mathf.Max(1, totalSlots);
      var angleRad = angleDeg * Mathf.Deg2Rad;
      var dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

      if (def == null || IsContact(def))
      {
        var radius = def != null ? def.orbit_radius : 2.2f;
        return GameplayPlane.ToWorld(ownerPos + dir * radius, owner.position.z);
      }

      if (IsBoomerang(def))
      {
        var radius = def.wander_radius * DetachedWeaponSpawnSettings.BoomerangStandbyRadiusFactor;
        return GameplayPlane.ToWorld(ownerPos + dir * radius, owner.position.z);
      }

      var standby = def.wander_radius * DetachedWeaponSpawnSettings.StandbyRadiusFactor;
      return GameplayPlane.ToWorld(ownerPos + dir * standby, owner.position.z);
    }

    static bool IsContact(DetachedWeaponDefinition def)
    {
      if (def.attack_modes == null || def.attack_modes.Length == 0)
        return def.id == "contact_weapon";
      foreach (var modeId in def.attack_modes)
      {
        if (DetachedWeaponDatabase.TryParseMode(modeId, out var mode) && mode == DetachedWeaponAttackMode.Contact)
          return true;
      }
      return false;
    }

    static bool IsBoomerang(DetachedWeaponDefinition def)
    {
      if (def.attack_modes == null)
        return false;
      foreach (var modeId in def.attack_modes)
      {
        if (DetachedWeaponDatabase.TryParseMode(modeId, out var mode) && mode == DetachedWeaponAttackMode.Boomerang)
          return true;
      }
      return false;
    }
  }
}
