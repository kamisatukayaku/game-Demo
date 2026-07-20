using UnityEngine;
using Game.Shared.Gameplay.Bridges;

namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>?loot pool 结算并授予玩家（或生成地面拾取物）?/summary>
  public interface ILootGrantService
  {
    void GrantLootPoolAtPosition(Vector3 position, string poolId);
  }
}