using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  /// <summary>
  /// Per-orbit-weapon state machine for Spirit Blade mode.
  /// Each orbit weapon can be in one of three states:
  ///   Orbit → Launch → Return → Orbit
  /// </summary>
  public enum BladeState : byte { Orbit, Launch, Return }

  public sealed class OrbitWeaponController
  {
    public BladeState State = BladeState.Orbit;
    public Transform Visual;
    public int Index;
    public float LaunchTimer;
    public float ReturnTimer;

    // Tracks the launched projectile GameObject (if active)
    public OrbitWeaponProjectile ActiveProjectile;

    public bool CanLaunch => State == BladeState.Orbit && ActiveProjectile == null;
    public bool IsFlying => ActiveProjectile != null || State != BladeState.Orbit;
  }
}
