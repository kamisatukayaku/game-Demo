using System;
using System.Collections.Generic;
using Game.Shared.Combat.Damage;

namespace Game.Shared.Projectile
{
  /// <summary>Tracks live straight projectiles without scene scans.</summary>
  public static class ActiveProjectileRegistry
  {
    static readonly HashSet<StraightProjectile> s_active = new();

    public static event Action<StraightProjectile, DamageRequest> Spawned;
    public static event Action<StraightProjectile> Despawned;

    public static void Register(StraightProjectile projectile, in DamageRequest request)
    {
      if (projectile == null || !s_active.Add(projectile))
        return;

      Spawned?.Invoke(projectile, request);
    }

    public static void Unregister(StraightProjectile projectile)
    {
      if (projectile == null || !s_active.Remove(projectile))
        return;

      Despawned?.Invoke(projectile);
    }

    public static void CopyActive(List<StraightProjectile> buffer)
    {
      buffer.Clear();
      foreach (var projectile in s_active)
      {
        if (projectile != null && projectile.gameObject.activeInHierarchy)
          buffer.Add(projectile);
      }
    }

    public static void ResetAll()
    {
      s_active.Clear();
    }

    static readonly List<StraightProjectile> s_despawnScratch = new();

    public static void DespawnAllActive()
    {
      CopyActive(s_despawnScratch);
      foreach (var projectile in s_despawnScratch)
      {
        if (projectile != null)
          projectile.ForceDespawnForRunReset();
      }

      s_active.Clear();
    }
  }
}
