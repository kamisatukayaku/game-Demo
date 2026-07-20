using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;

using UnityEngine;



namespace Game.Modes.Roguelike.Archetypes.Ranged

{

  sealed class RangedArchetype : Core.IArchetype

  {

    RangedWeaponVisual _visual;

    RangedOverloadController _overload;

    RangedAuxiliaryAttackController _auxiliary;

    bool _active;



    public string Id => "ranged";



    public bool IsActive => _active;



    public void Initialize(GameObject player)

    {

      if (player == null)

        return;



      RangedOverloadController.Ensure(player);

      RangedAuxiliaryAttackController.Ensure(player);

      _visual = RangedWeaponVisual.Ensure(player);

      _overload = player.GetComponent<RangedOverloadController>();

      _auxiliary = player.GetComponent<RangedAuxiliaryAttackController>();



      if (_visual != null)

        _visual.enabled = true;

      if (_overload != null)

        _overload.enabled = true;

      if (_auxiliary != null)

      {

        _auxiliary.ResetChannelState();

        _auxiliary.enabled = true;

      }



      _active = _visual != null;

    }



    public void Shutdown()

    {

      if (_visual != null)

        _visual.enabled = false;

      if (_overload != null)

        _overload.enabled = false;

      if (_auxiliary != null)

      {

        _auxiliary.enabled = false;

        _auxiliary.ResetChannelState();

      }



      _active = false;

    }



    public void OnLevelUp() { }



    public void OnEnemyKilled() { }



    public void Tick(float deltaTime) { }



    internal static bool ShouldActivate() =>
      RunBuildState.WeaponTheme == "ranged"
      || (RunBuildState.WeaponTheme == UnifiedBuildBootstrap.WeaponTheme
          && RunBuildState.HasTag("projectile"));

  }

}


