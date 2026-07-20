using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Gameplay.Events
{
  public readonly struct DamageEvent : IGameEvent
  {
    public readonly GameObject Attacker;
    public readonly GameObject Target;
    public readonly Vector3 Position;
    public readonly float Amount;
    public readonly string DamageType;
    public readonly string DamageSource;

    public DamageEvent(GameObject attacker, GameObject target, Vector3 position, float amount, string damageType, string damageSource = null)
    {
      Attacker = attacker;
      Target = target;
      Position = position;
      Amount = amount;
      DamageType = damageType;
      DamageSource = damageSource;
    }
  }

  public readonly struct EnemyDeathEvent : IGameEvent
  {
    public readonly GameObject Enemy;
    public readonly Vector3 Position;
    public readonly string EnemyId;

    public EnemyDeathEvent(GameObject enemy, Vector3 position, string enemyId)
    {
      Enemy = enemy;
      Position = position;
      EnemyId = enemyId;
    }
  }

  public readonly struct ProjectileSpawnEvent : IGameEvent
  {
    public readonly GameObject Projectile;
    public readonly Vector3 Position;
    public readonly string ProjectileId;
    public readonly GameObject Target;

    public ProjectileSpawnEvent(GameObject projectile, Vector3 position, string projectileId, GameObject target = null)
    {
      Projectile = projectile;
      Position = position;
      ProjectileId = projectileId;
      Target = target;
    }
  }

  public readonly struct ProjectileHitEvent : IGameEvent
  {
    public readonly GameObject Projectile;
    public readonly GameObject Target;
    public readonly Vector3 Position;
    public readonly string ProjectileId;

    public ProjectileHitEvent(GameObject projectile, GameObject target, Vector3 position, string projectileId)
    {
      Projectile = projectile;
      Target = target;
      Position = position;
      ProjectileId = projectileId;
    }
  }

  public readonly struct TrailSegmentEvent : IGameEvent
  {
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public readonly float Width;
    public readonly float Lifetime;
    public readonly bool Branch;
    public readonly bool Network;

    public TrailSegmentEvent(
      Vector3 start,
      Vector3 end,
      float width,
      float lifetime,
      bool branch,
      bool network)
    {
      Start = start;
      End = end;
      Width = width;
      Lifetime = lifetime;
      Branch = branch;
      Network = network;
    }
  }

  public readonly struct UpgradeAppliedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly Vector3 Position;
    public readonly string UpgradeId;

    public UpgradeAppliedEvent(GameObject player, Vector3 position, string upgradeId)
    {
      Player = player;
      Position = position;
      UpgradeId = upgradeId;
    }
  }

  public readonly struct TriggerActivatedEvent : IGameEvent
  {
    public readonly GameObject Source;
    public readonly Vector3 Position;
    public readonly string TriggerId;
    public readonly float Scale;
    public readonly float Value;
    public readonly bool Alternate;

    public TriggerActivatedEvent(
      string triggerId,
      Vector3 position,
      GameObject source = null,
      float scale = 1f,
      float value = 0f,
      bool alternate = false)
    {
      Source = source;
      Position = position;
      TriggerId = triggerId;
      Scale = scale;
      Value = value;
      Alternate = alternate;
    }
  }
}
