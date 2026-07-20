using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>向玩家授予指?equipment_id（背包或地面拾取物）?/summary>
  public interface IEquipmentGrantService
  {
    bool TryGrantEquipment(string equipmentId);
  }
}