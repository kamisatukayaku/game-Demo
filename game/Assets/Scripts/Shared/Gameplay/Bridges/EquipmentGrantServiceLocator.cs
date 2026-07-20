using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Gameplay.Bridges
{
  public static class EquipmentGrantServiceLocator
  {
    static IEquipmentGrantService s_service = NullEquipmentGrantService.Instance;

    public static IEquipmentGrantService Service => s_service;

    public static void Register(IEquipmentGrantService service)
    {
      s_service = service ?? NullEquipmentGrantService.Instance;
    }

    public static void Clear()
    {
      s_service = NullEquipmentGrantService.Instance;
    }
  }

  sealed class NullEquipmentGrantService : IEquipmentGrantService
  {
    public static readonly NullEquipmentGrantService Instance = new();
    NullEquipmentGrantService() { }
    public bool TryGrantEquipment(string equipmentId) => false;
  }
}