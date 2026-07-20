using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Gameplay.Events
{
  public readonly struct DashStartedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly Vector2 Start;
    public readonly Vector2 Direction;
    public readonly float RequestedDistance;
    public readonly float ActualDistance;
    public readonly float Duration;
    public readonly bool IsContactStrike;
    public readonly bool IsPursuitDash;

    public DashStartedEvent(
      GameObject player,
      Vector2 start,
      Vector2 direction,
      float requestedDistance,
      float actualDistance,
      float duration,
      bool isContactStrike,
      bool isPursuitDash)
    {
      Player = player;
      Start = start;
      Direction = direction;
      RequestedDistance = requestedDistance;
      ActualDistance = actualDistance;
      Duration = duration;
      IsContactStrike = isContactStrike;
      IsPursuitDash = isPursuitDash;
    }
  }

  public readonly struct DashEnemyHitEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly GameObject Enemy;
    public readonly Vector2 HitPosition;
    public readonly Vector2 Direction;
    public readonly float Damage;
    public readonly bool WasCritical;
    public readonly int HitIndex;
    public readonly bool IsAftershock;

    public DashEnemyHitEvent(
      GameObject player,
      GameObject enemy,
      Vector2 hitPosition,
      Vector2 direction,
      float damage,
      bool wasCritical,
      int hitIndex,
      bool isAftershock)
    {
      Player = player;
      Enemy = enemy;
      HitPosition = hitPosition;
      Direction = direction;
      Damage = damage;
      WasCritical = wasCritical;
      HitIndex = hitIndex;
      IsAftershock = isAftershock;
    }
  }

  public readonly struct DashEndedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly Vector2 End;
    public readonly Vector2 Direction;
    public readonly int HitCount;
    public readonly bool WasBlocked;
    public readonly bool TriggeredAftershock;
    public readonly bool IsContactStrike;
    public readonly bool GrantedPursuitCharge;

    public DashEndedEvent(
      GameObject player,
      Vector2 end,
      Vector2 direction,
      int hitCount,
      bool wasBlocked,
      bool triggeredAftershock,
      bool isContactStrike,
      bool grantedPursuitCharge)
    {
      Player = player;
      End = end;
      Direction = direction;
      HitCount = hitCount;
      WasBlocked = wasBlocked;
      TriggeredAftershock = triggeredAftershock;
      IsContactStrike = isContactStrike;
      GrantedPursuitCharge = grantedPursuitCharge;
    }
  }

  public readonly struct DashAftershockEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly Vector2 Position;
    public readonly float Radius;
    public readonly float DamageRatio;
    public readonly int HitCount;

    public DashAftershockEvent(
      GameObject player,
      Vector2 position,
      float radius,
      float damageRatio,
      int hitCount)
    {
      Player = player;
      Position = position;
      Radius = radius;
      DamageRatio = damageRatio;
      HitCount = hitCount;
    }
  }

  public readonly struct DashDamageTrailEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly Vector2 Start;
    public readonly Vector2 End;
    public readonly float Width;
    public readonly float Lifetime;

    public DashDamageTrailEvent(GameObject player, Vector2 start, Vector2 end, float width, float lifetime)
    {
      Player = player;
      Start = start;
      End = end;
      Width = width;
      Lifetime = lifetime;
    }
  }

  public readonly struct DashRefundCapReachedEvent : IGameEvent
  {
    public readonly GameObject Player;

    public DashRefundCapReachedEvent(GameObject player) => Player = player;
  }

  public readonly struct DashPursuitChargeGrantedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly float WindowSeconds;

    public DashPursuitChargeGrantedEvent(GameObject player, float windowSeconds)
    {
      Player = player;
      WindowSeconds = windowSeconds;
    }
  }
}
