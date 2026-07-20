using UnityEngine;

using Game.Modes.Roguelike.Loot;
using Game.Shared.Gameplay.Bridges;
namespace Game.Modes.Roguelike.Gameplay
{
  sealed class RoguelikeLootGrantService : ILootGrantService
  {
    public static readonly RoguelikeLootGrantService Instance = new();
    RoguelikeLootGrantService() { }

    public void GrantLootPoolAtPosition(Vector3 position, string poolId)
    {
      if (string.IsNullOrEmpty(poolId))
        return;

      var drops = LootService.Roll(poolId);
      if (drops != null && drops.Count > 0)
        LootService.GrantToPlayerOrSpawnPickup(position, drops);
    }
  }
}