using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Unified Ring Arena baseline: no class selection, shared starting kit.</summary>
  public static class UnifiedBuildBootstrap
  {
    public const string BuildId = "unified";
    public const string WeaponTheme = "unified";

    /// <summary>
    /// Baseline kit: move + dash only. Shooting, orbit blades, detached weapons, and dash damage
    /// unlock via Foundation tier-0 picks.
    /// </summary>
    public static void ApplyBaseline()
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      RunBuildState.Reset(WeaponTheme);
      BuildProgressionState.GrantTag("unified_run");
      ArenaDifficultyRuntime.ApplyPlayerModifiers();
    }
  }
}
