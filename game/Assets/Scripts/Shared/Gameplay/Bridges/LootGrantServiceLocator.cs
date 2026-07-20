using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Gameplay.Bridges
{
  public static class LootGrantServiceLocator
  {
    static ILootGrantService s_service = NullLootGrantService.Instance;

    public static ILootGrantService Service => s_service;

    public static void Register(ILootGrantService service)
    {
      s_service = service ?? NullLootGrantService.Instance;
    }

    public static void Clear()
    {
      s_service = NullLootGrantService.Instance;
    }
  }

  sealed class NullLootGrantService : ILootGrantService
  {
    public static readonly NullLootGrantService Instance = new();
    NullLootGrantService() { }
    public void GrantLootPoolAtPosition(UnityEngine.Vector3 position, string poolId) { }
  }
}