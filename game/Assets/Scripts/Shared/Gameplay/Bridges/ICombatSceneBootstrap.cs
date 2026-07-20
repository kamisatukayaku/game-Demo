using UnityEngine;
using Game.Shared.Gameplay.Bridges;

namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>MainScene 战斗系统初始化（?Roguelike / World 模式实现）?/summary>
  public interface ICombatSceneBootstrap
  {
    void InitializeCombatSystems();
    void ApplyPlayerComponents(GameObject playerGo);
  }

  public static class CombatSceneBootstrapLocator
  {
    static ICombatSceneBootstrap s_bootstrap;

    public static ICombatSceneBootstrap Bootstrap => s_bootstrap;

    public static void Register(ICombatSceneBootstrap bootstrap) => s_bootstrap = bootstrap;

    public static void Clear() => s_bootstrap = null;
  }
}