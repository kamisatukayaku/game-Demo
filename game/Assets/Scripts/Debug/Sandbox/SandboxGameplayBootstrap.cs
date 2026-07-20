using Game.Modes.Roguelike.Debugging;
using Game.Modes.Roguelike.Gameplay;
using Game.Shared.Gameplay.Bridges;

namespace Game.DevTools.Sandbox
{
  /// <summary>Build Sandbox 运行时服务：注册 Roguelike 流派 System Locator。</summary>
  public static class SandboxGameplayBootstrap
  {
    static bool s_installed;

    public static void EnsureInstalled()
    {
      if (s_installed)
        return;

      RoguelikeDebugBridge.InstallGameplayServices();
      CombatSceneBootstrapLocator.Register(RoguelikeCombatSceneBootstrap.Instance);
      s_installed = true;
    }
  }
}
