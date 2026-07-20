using UnityEngine;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Player;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  /// <summary>
  /// Warrior controller — thin MonoBehaviour that owns the OrbitWeaponSystem.
  /// All orbit / damage / spirit blade logic is delegated.
  /// Bleed and Reflect lines have been removed per Phase 1 rework.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class WarriorController : MonoBehaviour
  {
    PlayerAutoAttack _autoAttack;
    bool _autoAttackWasEnabled;
    OrbitWeaponSystem _orbitSystem;

    public static WarriorController Ensure(GameObject player)
    {
      var c = player.GetComponent<WarriorController>();
      return c != null ? c : player.AddComponent<WarriorController>();
    }

    void Awake()
    {
      _autoAttack = GetComponent<PlayerAutoAttack>();
    }

    void OnEnable()
    {
      if (_autoAttack != null && ShouldDisablePrimaryAutoAttack())
      {
        _autoAttackWasEnabled = _autoAttack.enabled;
        _autoAttack.enabled = false;
      }
      RunBuildState.Changed += RefreshFromBuild;
      RefreshFromBuild();
    }

    void OnDisable()
    {
      RunBuildState.Changed -= RefreshFromBuild;
      if (_autoAttack != null && ShouldDisablePrimaryAutoAttack())
        _autoAttack.enabled = _autoAttackWasEnabled;
      _orbitSystem?.Shutdown();
      _orbitSystem = null;
    }

    /// <summary>Legacy warrior theme replaces primary attack with orbit. Unified keeps both.</summary>
    static bool ShouldDisablePrimaryAutoAttack() =>
      RunBuildState.WeaponTheme != UnifiedBuildBootstrap.WeaponTheme;

    void Update()
    {
      if (!WarriorProgressionDatabase.IsValid)
        return;
      _orbitSystem?.Tick(Time.deltaTime);
    }

    public void RefreshFromBuild()
    {
      if (!WarriorProgressionDatabase.IsValid)
        return;

      var ctx = WarriorContext.FromBuild();

      if (_orbitSystem == null)
      {
        _orbitSystem = new OrbitWeaponSystem(gameObject, ctx);
      }
      else
      {
        _orbitSystem.Refresh(ctx);
      }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
      _orbitSystem?.DrawDebugGizmos();
    }
#endif
  }
}
