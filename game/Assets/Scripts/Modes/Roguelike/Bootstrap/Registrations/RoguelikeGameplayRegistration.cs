using Game.Modes.Roguelike.Gameplay;
using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.UI;
using Game.Shared.Gameplay.Input;

namespace Game.Modes.Roguelike.Bootstrap.Registrations
{
  static class RoguelikeGameplayRegistration
  {
    internal static void Install()
    {
      SkillSystemLocator.Register(RoguelikeSkillSystem.Instance);
      ArenaLayoutLocator.Register(RoguelikeArenaLayout.Instance);
      GameplayInputGateLocator.Register(RoguelikeGameplayInputGate.Instance);
      CombatSceneBootstrapLocator.Register(RoguelikeCombatSceneBootstrap.Instance);
      EnemyDeathLootHandlerLocator.Register(RoguelikeEnemyDeathLootHandler.Instance);
      RoguelikeTutorialResetBridge.ResetAll = RoguelikeTutorialDirector.ResetAllTutorialState;
    }
  }
}
