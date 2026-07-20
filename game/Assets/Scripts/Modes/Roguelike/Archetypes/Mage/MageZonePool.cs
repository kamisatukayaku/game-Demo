using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Gameplay.Bridges;
using Game.Shared.Player;
using Game.Shared.Projectile;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  public static class MageZonePool
  {
    const int Capacity = 6;

    static readonly Stack<MageZone> s_pool = new();
    static Transform s_root;
    static bool s_ready;

    public static MageZone Acquire()
    {
      EnsurePool();
      if (s_pool.Count > 0)
        return s_pool.Pop();

      var go = new GameObject("MageZone");
      go.transform.SetParent(s_root, false);
      return go.AddComponent<MageZone>();
    }

    public static void Release(MageZone zone)
    {
      if (zone == null)
        return;

      EnsurePool();
      zone.Shutdown();
      zone.transform.SetParent(s_root, false);
      if (s_pool.Count < Capacity)
        s_pool.Push(zone);
      else
        Object.Destroy(zone.gameObject);
    }

    public static void ResetAll()
    {
      MageZone.ResetActiveZones();
      MageTidalBoundaryZone.ResetAll();
      ActiveProjectileRegistry.ResetAll();

      while (s_pool.Count > 0)
      {
        var zone = s_pool.Pop();
        if (zone != null)
          Object.Destroy(zone.gameObject);
      }

      if (s_root != null)
        Object.Destroy(s_root.gameObject);

      s_pool.Clear();
      s_root = null;
      s_ready = false;
      ChargeDashInfluenceLocator.Clear();
    }

    static void EnsurePool()
    {
      if (s_ready && s_root != null)
        return;

      s_ready = true;
      s_root = new GameObject("MageZonePool").transform;
    }
  }
}
