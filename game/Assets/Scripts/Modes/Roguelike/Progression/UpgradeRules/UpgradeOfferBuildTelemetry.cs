namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Editor/simulation telemetry for offer building.</summary>
  public static class UpgradeOfferBuildTelemetry
  {
    public readonly struct PoolSnapshot
    {
      public readonly int Gameplay;
      public readonly int Player;
      public readonly int Detached;
      public readonly int Numeric;

      public PoolSnapshot(int gameplay, int player, int detached, int numeric)
      {
        Gameplay = gameplay;
        Player = player;
        Detached = detached;
        Numeric = numeric;
      }
    }

    public static int PityForcedCount { get; private set; }
    public static int FallbackFillCount { get; private set; }
    public static int EmptyOfferCount { get; private set; }
    public static int AuxiliaryInjectionCount { get; private set; }
    public static PoolSnapshot LastPoolSnapshot { get; private set; }

    public static void Reset()
    {
      PityForcedCount = 0;
      FallbackFillCount = 0;
      EmptyOfferCount = 0;
      AuxiliaryInjectionCount = 0;
      LastPoolSnapshot = default;
    }

    public static void RecordPityForced() => PityForcedCount++;
    public static void RecordFallbackFill() => FallbackFillCount++;
    public static void RecordEmptyOffer() => EmptyOfferCount++;
    public static void RecordAuxiliaryInjection() => AuxiliaryInjectionCount++;

    public static void RecordPoolSnapshot(
      int gameplay,
      int player,
      int detached,
      int numeric) =>
      LastPoolSnapshot = new PoolSnapshot(gameplay, player, detached, numeric);
  }
}
