using Game.Shared.Core;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Arena combat camera tuning. Independent of arena map radius.
  /// </summary>
  public static class ArenaCameraSettings
  {
    /// <summary>Local combat view half-height — matches pre-expansion gameplay feel.</summary>
    public const float CombatOrthographicSize = WorldGridConstants.GameplayOrthographicSize;

    public const float FollowSmoothTime = 0.12f;
    public const float LookAheadDistance = 2.2f;
    public const float LookAheadSmoothness = 0.35f;
    public const float MaximumLookAhead = 4.5f;
    public const float ArenaEdgeFramingDistance = 18f;
    public const float EdgeFramingPullStrength = 0.48f;
  }
}
