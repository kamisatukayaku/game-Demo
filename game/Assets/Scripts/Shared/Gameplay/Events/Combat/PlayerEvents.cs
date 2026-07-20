using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  public readonly struct PlayerDamagedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly GameObject Attacker;
    public readonly float Damage;
    public readonly float CurrentHp;
    public readonly float MaxHp;

    public PlayerDamagedEvent(
      GameObject player,
      GameObject attacker,
      float damage,
      float currentHp,
      float maxHp)
    {
      Player = player;
      Attacker = attacker;
      Damage = damage;
      CurrentHp = currentHp;
      MaxHp = maxHp;
    }
  }

  public readonly struct PlayerHealedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly float Amount;
    public readonly float CurrentHp;
    public readonly float MaxHp;

    public PlayerHealedEvent(GameObject player, float amount, float currentHp, float maxHp)
    {
      Player = player;
      Amount = amount;
      CurrentHp = currentHp;
      MaxHp = maxHp;
    }
  }

  public readonly struct PlayerDeathEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly GameObject Killer;
    public readonly Vector3 Position;

    public PlayerDeathEvent(GameObject player, GameObject killer, Vector3 position)
    {
      Player = player;
      Killer = killer;
      Position = position;
    }
  }

}
