using UnityEngine;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  sealed class WarriorArchetype : Core.IArchetype
  {
    WarriorController _controller;
    bool _active;

    public string Id => "warrior";
    public bool IsActive => _active;

    public void Initialize(GameObject player)
    {
      if (player == null || !WarriorProgressionDatabase.IsValid)
        return;

      _controller = WarriorController.Ensure(player);
      _controller.enabled = true;
      _controller.RefreshFromBuild();
      _active = true;
    }

    public void Shutdown()
    {
      if (_controller != null)
        _controller.enabled = false;
      _active = false;
    }

    public void OnLevelUp() => _controller?.RefreshFromBuild();
    public void OnEnemyKilled() { }
    public void Tick(float deltaTime) { }

    internal static bool ShouldActivate() =>
      RunBuildState.WeaponTheme == "warrior"
      || (RunBuildState.WeaponTheme == UnifiedBuildBootstrap.WeaponTheme
          && (RunBuildState.HasTag("orbit")
              || Game.Modes.Roguelike.Build.Progression.BuildProgressionState.HasMechanic("dash_melee")));
  }
}
