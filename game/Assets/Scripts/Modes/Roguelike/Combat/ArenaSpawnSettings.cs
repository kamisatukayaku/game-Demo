using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Centralized arena spawn space and rhythm parameters.
  /// </summary>
  public static class ArenaSpawnSettings
  {
    public const int MaxPickAttempts = 14;

    public const int GroupSizeEarly = 2;
    public const int GroupSizeMid = 3;
    public const int GroupSizeLate = 4;
    public const float GroupInterval = 0.55f;
    public const float UnitSpawnJitter = 0.14f;

    public const float SpawnArcWidthEarly = 0.95f;
    public const float SpawnArcWidthMid = 1.05f;
    public const float SpawnArcWidthLate = 1.15f;

    public const float MinPlayerDistance = 13f;
    public const float MaxEngagementDistance = 46f;
    public const float SpawnBandInnerFactor = 1.06f;
    public const float SpawnBandOuterFactor = 1.58f;
    public const float VisibleCenterRejectFactor = 0.88f;
    public const float MinSpawnSeparation = 2.6f;
    public const float BoundaryWallMargin = 2.2f;

    public const int MaxHordePending = 6;
    public const int MaxConcurrentEnemiesBonus = 0;

    public static int GetGroupSize(int wave)
    {
      if (wave <= 6) return GroupSizeEarly;
      if (wave <= 14) return GroupSizeMid;
      return GroupSizeLate;
    }

    public static float GetSpawnArcWidth(int wave)
    {
      if (wave <= 6) return SpawnArcWidthEarly;
      if (wave <= 14) return SpawnArcWidthMid;
      return SpawnArcWidthLate;
    }

    public static int GetAttackSectorCount(int wave, float waveProgress01)
    {
      if (wave <= 5) return 1;
      if (wave <= 14) return 2;
      return waveProgress01 > 0.72f && Random.value < 0.35f ? 3 : 2;
    }
  }
}
