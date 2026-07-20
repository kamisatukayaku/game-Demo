using UnityEngine;

using Game.Modes.Roguelike.Loot;
using Game.Shared.Gameplay.Bridges;
namespace Game.Modes.Roguelike.Gameplay
{
  sealed class RoguelikeEnemyDeathLootHandler : IEnemyDeathLootHandler
  {
    public static readonly RoguelikeEnemyDeathLootHandler Instance = new();

    public void HandleEnemyDeath(Vector3 position, string lootTableId, bool arenaMode)
    {
      var drops = LootService.Roll(lootTableId);
      if (arenaMode)
        GrantArenaLoot(position, drops);
      else
        LootService.GrantToPlayerOrSpawnPickup(position, drops);
    }

    static void GrantArenaLoot(Vector3 position, System.Collections.Generic.List<LootService.LootDrop> drops)
    {
      if (drops == null)
        return;

      foreach (var drop in drops)
      {
        if (drop.xp > 0)
          LootService.SpawnXpPickup(position, drop.xp);
      }
    }
  }
}
