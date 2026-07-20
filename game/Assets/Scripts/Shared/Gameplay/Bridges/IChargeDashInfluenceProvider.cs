using UnityEngine;

namespace Game.Shared.Gameplay.Bridges
{
  public interface IChargeDashInfluenceProvider
  {
    Vector2 GetDashOffset(GameObject enemy, Vector2 currentPosition, Vector2 dashDirection, float deltaTime);
  }

  public static class ChargeDashInfluenceLocator
  {
    static IChargeDashInfluenceProvider s_provider = NullChargeDashInfluenceProvider.Instance;

    public static IChargeDashInfluenceProvider Provider => s_provider;

    public static void Register(IChargeDashInfluenceProvider provider)
    {
      s_provider = provider ?? NullChargeDashInfluenceProvider.Instance;
    }

    public static void Clear()
    {
      s_provider = NullChargeDashInfluenceProvider.Instance;
    }
  }

  sealed class NullChargeDashInfluenceProvider : IChargeDashInfluenceProvider
  {
    public static readonly NullChargeDashInfluenceProvider Instance = new();

    NullChargeDashInfluenceProvider() { }

    public Vector2 GetDashOffset(GameObject enemy, Vector2 currentPosition, Vector2 dashDirection, float deltaTime) =>
      Vector2.zero;
  }
}
