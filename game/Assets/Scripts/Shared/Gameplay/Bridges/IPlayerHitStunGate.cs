using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>玩家受击硬直查询（Roguelike UI 层可选实现）?/summary>
  public interface IPlayerHitStunGate
  {
    bool IsInHitStun { get; }
  }
}