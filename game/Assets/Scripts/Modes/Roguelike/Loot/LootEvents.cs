using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Loot
{
  public readonly struct EquipmentPickedEvent : IGameEvent
  {
    public readonly GameObject Player;
    public readonly string EquipmentId;
    public readonly Vector3 Position;

    public EquipmentPickedEvent(GameObject player, string equipmentId, Vector3 position)
    {
      Player = player;
      EquipmentId = equipmentId;
      Position = position;
    }
  }

}
