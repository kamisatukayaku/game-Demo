namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>Centralized detached weapon intro timing.</summary>
  public static class DetachedWeaponSpawnSettings
  {
    public const float InvisibleDuration = 0.05f;
    public const float CoreScaleInDuration = 0.15f;
    public const float CoreScaleInTarget = 0.75f;
    public const float FlyDuration = 0.30f;
    public const float SettleDuration = 0.15f;
    public const float TotalIntroDuration = 0.65f;
    public const float StaggerMin = 0.06f;
    public const float StaggerMax = 0.12f;
    public const float OvershootFactor = 1.06f;
    public const float StandbyRadiusFactor = 0.55f;
    public const float BoomerangStandbyRadiusFactor = 0.42f;
  }
}
