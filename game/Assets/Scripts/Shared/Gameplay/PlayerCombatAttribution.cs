using UnityEngine;

namespace Game.Shared.Gameplay
{
  /// <summary>判断伤害/击杀是否应归因于玩家（含弹体、环绕武器、外置武器等子物体）。</summary>
  public static class PlayerCombatAttribution
  {
    public static bool IsPlayerOrOwned(GameObject source)
    {
      if (source == null)
        return false;

      var current = source.transform;
      while (current != null)
      {
        if (current.CompareTag("Player") || current.name == "Player")
          return true;
        current = current.parent;
      }

      var n = source.name;
      if (string.IsNullOrEmpty(n))
        return false;

      return n.StartsWith("PlayerProjectile", System.StringComparison.Ordinal)
        || n.StartsWith("Proj_Player", System.StringComparison.OrdinalIgnoreCase)
        || n.StartsWith("DetachedWeapon", System.StringComparison.Ordinal)
        || n.StartsWith("OrbitWeapon", System.StringComparison.Ordinal);
    }
  }
}
