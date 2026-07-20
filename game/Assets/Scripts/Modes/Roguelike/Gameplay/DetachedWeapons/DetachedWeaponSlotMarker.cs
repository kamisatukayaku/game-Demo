using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  [DisallowMultipleComponent]
  public sealed class DetachedWeaponSlotMarker : MonoBehaviour
  {
    public int SlotIndex { get; private set; }
    public int TotalSlots { get; private set; }
    public float OrbitAngleDegrees { get; private set; }

    public void Set(int slotIndex, int totalSlots, string weaponId)
    {
      SlotIndex = slotIndex;
      TotalSlots = Mathf.Max(1, totalSlots);
      OrbitAngleDegrees = ComputeOrbitAngle(slotIndex, TotalSlots, weaponId);
    }

    static float ComputeOrbitAngle(int slotIndex, int totalSlots, string weaponId)
    {
      var def = DetachedWeaponDatabase.Get(weaponId);
      var isContact = def != null && HasMode(def, DetachedWeaponAttackMode.Contact);
      if (!isContact)
        return slotIndex * 360f / totalSlots;

      return slotIndex * 360f / totalSlots;
    }

    static bool HasMode(DetachedWeaponDefinition def, DetachedWeaponAttackMode mode)
    {
      if (def.attack_modes == null)
        return mode == DetachedWeaponAttackMode.Contact;
      foreach (var modeId in def.attack_modes)
      {
        if (DetachedWeaponDatabase.TryParseMode(modeId, out var parsed) && parsed == mode)
          return true;
      }
      return false;
    }
  }
}
