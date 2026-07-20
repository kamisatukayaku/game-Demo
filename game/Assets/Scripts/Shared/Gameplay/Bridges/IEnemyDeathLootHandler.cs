using UnityEngine;
using Game.Shared.Gameplay.Bridges;

namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>怪物死亡掉落（Roguelike Loot 层实现）?/summary>
  public interface IEnemyDeathLootHandler
  {
    void HandleEnemyDeath(Vector3 position, string lootTableId, bool arenaMode);
  }

  public static class EnemyDeathLootHandlerLocator
  {
    static IEnemyDeathLootHandler s_handler;

    public static IEnemyDeathLootHandler Handler => s_handler;

    public static void Register(IEnemyDeathLootHandler handler) => s_handler = handler;

    public static void Clear() => s_handler = null;
  }
}