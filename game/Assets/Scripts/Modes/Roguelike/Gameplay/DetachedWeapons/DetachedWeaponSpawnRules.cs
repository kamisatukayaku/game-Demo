using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Build.Runtime;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  /// <summary>Single source for whether the run should spawn detached weapons at start or after upgrades.</summary>
  public static class DetachedWeaponSpawnRules
  {
    public static bool ShouldSpawnInitialContactWeapon() =>
      ArenaBuildBootstrap.SelectedBuildId == ArenaBuildBootstrap.Contact;

    public static bool HasDetachedWeaponEntitlement()
    {
      if (ShouldSpawnInitialContactWeapon())
        return true;

      if (RunBuildState.GetStat("detached_contact_level") > 0.05f)
        return true;

      return RunBuildState.GetStat("detached_part_count") > 0.05f;
    }
  }

  static class EvolutionWeaponIds
  {
    public static readonly string[] All = { "laser", "missile", "explosion", "pulse", "boomerang", "trail" };
  }
}
