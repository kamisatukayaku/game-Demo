using System;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Gameplay.Events;
using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  [DisallowMultipleComponent]
  public sealed class DetachedWeaponSystem : MonoBehaviour
  {
    public static event Action<GameObject> WeaponSpawned;

    readonly System.Collections.Generic.List<DetachedWeaponController> _weapons = new();
    readonly System.Collections.Generic.List<string> _configuredSignatures = new();

    static readonly string[] EvolutionIds =
    {
      "laser",
      "missile",
      "explosion",
      "pulse",
      "boomerang",
      "trail"
    };

    public static DetachedWeaponSystem Ensure(GameObject player)
    {
      if (player == null)
        return null;
      return player.GetComponent<DetachedWeaponSystem>() ?? player.AddComponent<DetachedWeaponSystem>();
    }

    public void ClearAllWeapons()
    {
      for (var i = _weapons.Count - 1; i >= 0; i--)
      {
        if (_weapons[i] != null)
          Destroy(_weapons[i].gameObject);
      }
      _weapons.Clear();
      _configuredSignatures.Clear();
    }

    void OnEnable()
    {
      RunBuildState.Changed += Refresh;
      Refresh();
    }

    void OnDisable() => RunBuildState.Changed -= Refresh;

    void Refresh()
    {
      if (!DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())
      {
        ClearAllWeapons();
        return;
      }

      var slotCount = DetachedWeaponSlotRules.GetTotalSlotCount();
      if (slotCount <= 0)
      {
        ClearAllWeapons();
        return;
      }

      var evolutions = CollectActiveEvolutions();
      EnsureWeaponCount(slotCount);

      var contactLevel = Mathf.RoundToInt(RunBuildState.GetStat("detached_contact_level"));
      if (DetachedWeaponSpawnRules.ShouldSpawnInitialContactWeapon())
        contactLevel = Mathf.Max(1, contactLevel);
      else if (RunBuildState.GetStat("detached_part_count") > 0.05f)
        contactLevel = Mathf.Max(1, contactLevel);

      var evolvedSlots = Mathf.Min(evolutions.Count, slotCount);
      for (var i = 0; i < _weapons.Count; i++)
      {
        if (i < evolvedSlots)
          Configure(i, evolutions[i].WeaponId, evolutions[i].Tier);
        else
          Configure(i, "contact_weapon", contactLevel);
      }
    }

    void EnsureWeaponCount(int count)
    {
      while (_weapons.Count < count)
      {
        var go = new GameObject($"DetachedWeapon_{_weapons.Count + 1}");
        go.transform.SetParent(null, true);
        go.transform.position = ComputeSlotPosition(_weapons.Count, count, "contact_weapon");
        go.transform.localScale = Vector3.one;
        go.AddComponent<DetachedWeaponVisualState>();
        _weapons.Add(go.AddComponent<DetachedWeaponController>());
        _configuredSignatures.Add(null);
        WeaponSpawned?.Invoke(go);
        GameEventBus.Publish(new DetachedWeaponAcquiredEvent(go, string.Empty));
      }

      while (_weapons.Count > count)
      {
        var index = _weapons.Count - 1;
        if (_weapons[index] != null)
          Destroy(_weapons[index].gameObject);
        _weapons.RemoveAt(index);
        _configuredSignatures.RemoveAt(index);
      }
    }

    void Configure(int index, string weaponId, int tier)
    {
      var weapon = _weapons[index];
      if (weapon == null)
        return;

      var signature = $"{weaponId}:{Mathf.Max(0, tier)}:{index}:{_weapons.Count}";
      weapon.transform.localScale = Vector3.one;
      var marker = weapon.GetComponent<DetachedWeaponSlotMarker>() ?? weapon.gameObject.AddComponent<DetachedWeaponSlotMarker>();
      marker.Set(index, _weapons.Count, weaponId);

      if (_configuredSignatures[index] == signature)
        return;

      weapon.transform.position = ComputeSlotPosition(index, _weapons.Count, weaponId);
      if (weapon.Configure(gameObject, weaponId))
      {
        _configuredSignatures[index] = signature;
        weapon.GetComponent<DetachedWeaponVisualState>()?.SetVisual(
          DetachedWeaponDatabase.Get(weaponId)?.visual_id);
        DetachedWeaponPresentationSystem.PlayWeaponIntro(
          weapon.gameObject, transform, index, _weapons.Count, weaponId);
      }
    }

    Vector3 ComputeSlotPosition(int index, int count, string weaponId)
    {
      var safeCount = Mathf.Max(1, count);
      var angle = (index / (float)safeCount) * Mathf.PI * 2f;
      var def = DetachedWeaponDatabase.Get(weaponId) ?? DetachedWeaponDatabase.Get("contact_weapon");
      var radius = def != null && def.orbit_radius > 0.01f ? def.orbit_radius : 2.2f;
      radius = Mathf.Max(1.35f, radius);
      return transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
    }

    static System.Collections.Generic.List<EvolvedWeaponSlot> CollectActiveEvolutions()
    {
      var result = new System.Collections.Generic.List<EvolvedWeaponSlot>();
      foreach (var id in EvolutionIds)
      {
        var tier = Mathf.RoundToInt(RunBuildState.GetStat($"detached_{id}_tier"));
        if (tier <= 0)
          continue;

        result.Add(new EvolvedWeaponSlot
        {
          WeaponId = $"{id}_weapon",
          Tier = Mathf.Clamp(tier, 1, 5)
        });
      }

      return result;
    }

    struct EvolvedWeaponSlot
    {
      public string WeaponId;
      public int Tier;
    }
  }
}
