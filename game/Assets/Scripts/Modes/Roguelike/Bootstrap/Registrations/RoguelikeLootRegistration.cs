using Game.Modes.Roguelike.Gameplay;
using Game.Shared.Gameplay.Bridges;

namespace Game.Modes.Roguelike.Bootstrap.Registrations
{
  static class RoguelikeLootRegistration
  {
    internal static void Install()
    {
      LootGrantServiceLocator.Register(RoguelikeLootGrantService.Instance);
    }
  }
}
