using Game.Modes.Roguelike.Build.Runtime;

using Game.Modes.Roguelike.Progression;

using Game.Shared.Combat.Health;

using Game.Shared.Core;

using Game.Shared.Gameplay.Input;

using Game.Shared.Player;

using UnityEngine;



namespace Game.Modes.Roguelike.Archetypes.Ranged

{

  /// <summary>

  /// Independent explosive and lightning auxiliary shot channels.

  /// Does not block primary auto-attack cooldown.

  /// </summary>

  [DisallowMultipleComponent]

  public sealed class RangedAuxiliaryAttackController : MonoBehaviour

  {

    float _explosiveCooldown;

    float _lightningCooldown;

    PlayerAttackDirector _director;

    Health _health;



    public static RangedAuxiliaryAttackController Ensure(GameObject player)

    {

      if (player == null)

        return null;

      return player.GetComponent<RangedAuxiliaryAttackController>()

        ?? player.AddComponent<RangedAuxiliaryAttackController>();

    }



    public static void ResetForNewRun()

    {

      if (s_instance == null)

        return;

      s_instance.ResetChannelState();

    }



    static RangedAuxiliaryAttackController s_instance;



    void Awake()

    {

      s_instance = this;

      _director = GetComponent<PlayerAttackDirector>();

      _health = GetComponent<Health>();

    }



    void OnDestroy()

    {

      if (s_instance == this)

        s_instance = null;

    }



    void OnDisable() => ResetChannelState();



    public void ResetChannelState()

    {

      _explosiveCooldown = 0f;

      _lightningCooldown = 0f;

    }



    void Update()

    {

      if (!CanFireAuxiliary())

        return;



      var ctx = RangedAuxiliaryContextBuilder.Build();

      if (ctx.ExplosiveTier <= 0 && ctx.LightningTier <= 0)

        return;



      if (!_director.TryResolveAutoAim(out var aimDir))

        return;



      _explosiveCooldown -= Time.deltaTime;

      _lightningCooldown -= Time.deltaTime;



      if (ctx.ExplosiveTier > 0 && _explosiveCooldown <= 0f)

      {

        _director.FireAuxiliaryExplosive(aimDir);

        _explosiveCooldown = ctx.ExplosiveInterval;

      }



      if (ctx.LightningTier > 0 && _lightningCooldown <= 0f)

      {

        _director.FireAuxiliaryLightning(aimDir);

        _lightningCooldown = ctx.LightningInterval;

      }

    }



    bool CanFireAuxiliary()

    {

      if (_director == null || !enabled)
        return false;

      if (!RangedArchetype.ShouldActivate())
        return false;



      if (_health != null && _health.IsDead)

        return false;



      if (LevelUpController.IsWaiting || CombatTimePause.IsPaused)

        return false;



      return !GameplayInputGateLocator.BlocksPlayerInput;

    }

  }

}


