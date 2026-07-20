namespace Game.Modes.Roguelike.Archetypes.Ranged
{
  /// <summary>Cross-run overload relic bonus and controller lifecycle.</summary>
  public static class RangedOverloadRuntime
  {
    public static float RelicBonus { get; private set; }

    public static void ApplyRelicBonus(float value) => RelicBonus += value;

    public static void ResetForNewRun()
    {
      RelicBonus = 0f;
      RangedOverloadController.ResetForNewRun();
      RangedAuxiliaryAttackController.ResetForNewRun();
    }
  }
}
