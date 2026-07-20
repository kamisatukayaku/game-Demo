using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Modes.Roguelike.Bootstrap.Registrations;
using Game.Modes.Roguelike.Gameplay;

namespace Game.Modes.Roguelike.Debugging
{
  /// <summary>Minimal public surface used by editor and Sandbox diagnostics.</summary>
  public static class RoguelikeDebugBridge
  {
    public static bool IsMageActive => MageArchetype.ShouldActivate();
    public static bool IsRangedActive => RangedArchetype.ShouldActivate();

    public static void InstallGameplayServices()
    {
      RoguelikeGameplayRegistration.Install();
    }

    public static void RefreshBuildContexts()
    {
      RoguelikeSkillSystem.Instance.RefreshFromBuild();
    }
  }
}
