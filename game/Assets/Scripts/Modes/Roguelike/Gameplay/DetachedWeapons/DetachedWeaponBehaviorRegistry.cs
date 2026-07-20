using System;
using System.Collections.Generic;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  public static class DetachedWeaponBehaviorRegistry
  {
    static readonly Dictionary<DetachedWeaponAttackMode, Func<IDetachedWeaponBehavior>> Factories = new();

    static DetachedWeaponBehaviorRegistry()
    {
      Register(DetachedWeaponAttackMode.Contact, () => new ContactWeaponBehavior());
      Register(DetachedWeaponAttackMode.LaserShot, () => new LaserShotBehavior());
      Register(DetachedWeaponAttackMode.Missile, () => new MissileBehavior());
      Register(DetachedWeaponAttackMode.Explosion, () => new ExplosionBehavior());
      Register(DetachedWeaponAttackMode.Pulse, () => new PulseBehavior());
      Register(DetachedWeaponAttackMode.Boomerang, () => new BoomerangBehavior());
      Register(DetachedWeaponAttackMode.Trail, () => new TrailBehavior());
    }

    public static void Register(
      DetachedWeaponAttackMode mode,
      Func<IDetachedWeaponBehavior> factory)
    {
      if (factory != null)
        Factories[mode] = factory;
    }

    public static bool TryCreate(
      DetachedWeaponAttackMode mode,
      out IDetachedWeaponBehavior behavior)
    {
      behavior = null;
      if (!Factories.TryGetValue(mode, out var factory))
        return false;
      behavior = factory.Invoke();
      return behavior != null;
    }
  }
}
