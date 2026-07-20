using Game.Modes.Roguelike.Build.Integration;
using Game.Modes.Roguelike.Build.Stats;
using Game.Shared.Stats;
using Game.Shared.Gameplay.Bridges;

namespace Game.Modes.Roguelike.Bootstrap.Registrations
{
  static class RoguelikeStatsRegistration
  {
    internal static void Install()
    {
      RunSessionConfiguratorLocator.Register(RoguelikeRunSessionConfigurator.Instance);
      CombatStatProviderLocator.Register(RoguelikeCombatStatProvider.Instance);
      BuildStatWriterLocator.Register(RoguelikeBuildStatWriter.Instance);
    }
  }
}
