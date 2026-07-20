using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  public readonly struct EnemyKilledEvent : IGameEvent
  {
    public readonly GameObject Enemy;
    public readonly GameObject Killer;
    public readonly Vector3 Position;
    public readonly string EnemyId;
    public readonly string LootTableId;
    public readonly bool IsBoss;

    public EnemyKilledEvent(
      GameObject enemy,
      GameObject killer,
      Vector3 position,
      string enemyId,
      string lootTableId,
      bool isBoss = false)
    {
      Enemy = enemy;
      Killer = killer;
      Position = position;
      EnemyId = enemyId ?? "unknown";
      LootTableId = string.IsNullOrEmpty(lootTableId) ? "common_mob" : lootTableId;
      IsBoss = isBoss;
    }
  }

  public readonly struct BossSpawnedEvent : IGameEvent
  {
    public readonly GameObject Boss;
    public readonly Vector3 Position;
    public readonly string BossId;

    public BossSpawnedEvent(GameObject boss, Vector3 position, string bossId)
    {
      Boss = boss;
      Position = position;
      BossId = bossId ?? "boss";
    }
  }

  public readonly struct BossKilledEvent : IGameEvent
  {
    public readonly GameObject Boss;
    public readonly GameObject Killer;
    public readonly Vector3 Position;
    public readonly string BossId;

    public BossKilledEvent(GameObject boss, GameObject killer, Vector3 position, string bossId)
    {
      Boss = boss;
      Killer = killer;
      Position = position;
      BossId = bossId ?? "boss";
    }
  }

  public readonly struct BossPhaseChangedEvent : IGameEvent
  {
    public readonly GameObject Boss;
    public readonly string BossId;
    public readonly int FromPhase;
    public readonly int ToPhase;
    public readonly string Subtitle;

    public BossPhaseChangedEvent(GameObject boss, string bossId, int fromPhase, int toPhase, string subtitle)
    {
      Boss = boss;
      BossId = bossId ?? "boss";
      FromPhase = fromPhase;
      ToPhase = toPhase;
      Subtitle = subtitle ?? string.Empty;
    }
  }

}
