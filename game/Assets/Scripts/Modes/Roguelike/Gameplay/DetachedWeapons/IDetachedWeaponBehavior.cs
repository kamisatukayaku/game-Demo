namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  public interface IDetachedWeaponBehavior
  {
    DetachedWeaponAttackMode Mode { get; }
    void Initialize(in DetachedWeaponRuntimeContext context);
    void Tick(float deltaTime);
    void Shutdown();
  }
}
