namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Roguelike arena combat tuning constants.
  /// Default move speed 8 → ~10s center-to-edge at DefaultArenaRadius.
  /// </summary>
  public static class ArenaCombatScale
  {
    public const float DefaultPlayerMoveSpeed = 8f;
    public const float TargetEdgeTravelSeconds = 10f;
    public const float DefaultArenaRadius = DefaultPlayerMoveSpeed * TargetEdgeTravelSeconds;
    public const float MinHordeNearbyRadius = 14f;
  }
}
