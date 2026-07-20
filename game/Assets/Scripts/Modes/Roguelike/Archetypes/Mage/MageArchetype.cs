using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  sealed class MageArchetype : Core.IArchetype
  {
    MageController _controller;
    bool _active;

    public string Id => "mage";

    public bool IsActive => _active;

    public void Initialize(GameObject player)
    {
      if (player == null)
        return;

      _controller = MageController.Ensure(player);
      if (_controller != null)
        _controller.enabled = true;
      _active = _controller != null;
    }

    public void Shutdown()
    {
      if (_controller != null)
        _controller.enabled = false;
      _active = false;
    }

    public void OnLevelUp() { }

    public void OnEnemyKilled() { }

    public void Tick(float deltaTime) { }

    internal static bool ShouldActivate() =>
      RunBuildState.WeaponTheme == "mage"
      || (RunBuildState.WeaponTheme == UnifiedBuildBootstrap.WeaponTheme
          && (RunBuildState.GetSkillGravityWellUnlocked() || RunBuildState.GetSkillTidalPulseUnlocked()));
  }
}
