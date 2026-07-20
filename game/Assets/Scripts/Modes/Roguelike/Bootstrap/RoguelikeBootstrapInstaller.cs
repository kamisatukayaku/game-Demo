using UnityEngine;
using Game.Modes.Roguelike.Presentation.VFX;

namespace Game.Modes.Roguelike.Bootstrap
{
  static class RoguelikeBootstrapInstaller
  {
    internal static void Install()
    {
      Registrations.RoguelikeStatsRegistration.Install();
      Registrations.RoguelikeGameplayRegistration.Install();
      Registrations.RoguelikeLootRegistration.Install();
      Registrations.RoguelikeBuildRegistration.Install();
      VFXEventListener.EnsureExists();
    }
  }

  static class RoguelikeBootstrapEntry
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
      RoguelikeBootstrapInstaller.Install();
    }
  }
}
