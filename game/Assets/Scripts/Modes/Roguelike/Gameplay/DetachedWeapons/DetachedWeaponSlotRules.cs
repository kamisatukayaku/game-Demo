using System;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  /// <summary>Tracks detached-weapon slot capacity vs active evolution routes for offer filtering and runtime layout.</summary>
  public static class DetachedWeaponSlotRules
  {
    public const int MaxSlots = 6;

    public static int GetTotalSlotCount()
    {
      var partBonus = Mathf.RoundToInt(RunBuildState.GetStat("detached_part_count"));
      var count = DetachedWeaponSpawnRules.ShouldSpawnInitialContactWeapon() ? 1 : 0;
      count += partBonus;
      return Mathf.Clamp(count, 0, MaxSlots);
    }

    public static int GetEvolvedRouteCount()
    {
      var count = 0;
      foreach (var id in EvolutionWeaponIds.All)
      {
        if (RunBuildState.GetStat($"detached_{id}_tier") > 0.05f)
          count++;
      }
      return count;
    }

    public static int GetUnevolvedSlotCount() =>
      Mathf.Max(0, GetTotalSlotCount() - GetEvolvedRouteCount());

    public static bool IsWeaponEvolutionOffer(LevelUpChoiceDatabase.UpgradeDef def) =>
      def?.id != null && def.id.StartsWith("evo_", StringComparison.Ordinal);

    public static bool IsEvolutionOfferBlocked(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (!IsWeaponEvolutionOffer(def))
        return false;

      if (GetTotalSlotCount() <= 0)
        return true;

      var routeId = ExtractEvolutionRouteId(def);
      var currentTier = string.IsNullOrEmpty(routeId)
        ? 0
        : Mathf.RoundToInt(RunBuildState.GetStat($"detached_{routeId}_tier"));

      if (currentTier > 0)
        return false;

      return GetUnevolvedSlotCount() <= 0;
    }

    public static string ExtractEvolutionRouteId(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return null;

      if (!string.IsNullOrEmpty(def.mechanic_id))
        return def.mechanic_id;

      if (def.id == null || !def.id.StartsWith("evo_", StringComparison.Ordinal))
        return null;

      var parts = def.id.Split('_');
      return parts.Length >= 2 ? parts[1] : null;
    }
  }
}
