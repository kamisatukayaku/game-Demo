using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Tutorial
{
  public readonly struct PlayerMovedEvent : IGameEvent
  {
    public readonly Vector2 Delta;
    public readonly float Distance;

    public PlayerMovedEvent(Vector2 delta, float distance)
    {
      Delta = delta;
      Distance = distance;
    }
  }

  public readonly struct PlayerDashedEvent : IGameEvent
  {
    public readonly Vector3 Position;

    public PlayerDashedEvent(Vector3 position) => Position = position;
  }

  public readonly struct AutoAttackHitEvent : IGameEvent
  {
    public readonly GameObject Target;
    public readonly Vector3 Position;

    public AutoAttackHitEvent(GameObject target, Vector3 position)
    {
      Target = target;
      Position = position;
    }
  }

  public readonly struct XpPickupCollectedEvent : IGameEvent
  {
    public readonly Vector3 Position;
    public readonly float Amount;

    public XpPickupCollectedEvent(Vector3 position, float amount)
    {
      Position = position;
      Amount = amount;
    }
  }

  public readonly struct GroundZoneSpawnedEvent : IGameEvent
  {
    public readonly string ZoneId;
    public readonly Vector2 Center;
    public readonly float Radius;
    public readonly float Duration;

    public GroundZoneSpawnedEvent(string zoneId, Vector2 center, float radius, float duration)
    {
      ZoneId = zoneId;
      Center = center;
      Radius = radius;
      Duration = duration;
    }
  }

  public readonly struct GroundZoneEnteredEvent : IGameEvent
  {
    public readonly string ZoneId;
    public readonly Vector2 Center;
    public readonly float Radius;

    public GroundZoneEnteredEvent(string zoneId, Vector2 center, float radius)
    {
      ZoneId = zoneId;
      Center = center;
      Radius = radius;
    }
  }

  public readonly struct GroundZoneExitedEvent : IGameEvent
  {
    public readonly string ZoneId;

    public GroundZoneExitedEvent(string zoneId) => ZoneId = zoneId;
  }

  public readonly struct DetachedWeaponAcquiredEvent : IGameEvent
  {
    public readonly GameObject Weapon;
    public readonly string UpgradeId;

    public DetachedWeaponAcquiredEvent(GameObject weapon, string upgradeId)
    {
      Weapon = weapon;
      UpgradeId = upgradeId;
    }
  }

  public readonly struct DetachedWeaponImpactEvent : IGameEvent
  {
    public readonly GameObject Weapon;
    public readonly Vector3 Position;

    public DetachedWeaponImpactEvent(GameObject weapon, Vector3 position)
    {
      Weapon = weapon;
      Position = position;
    }
  }

  public readonly struct TutorialUiBlockingEvent : IGameEvent
  {
    public readonly bool Blocking;
    public readonly string Reason;

    public TutorialUiBlockingEvent(bool blocking, string reason)
    {
      Blocking = blocking;
      Reason = reason;
    }
  }
}
