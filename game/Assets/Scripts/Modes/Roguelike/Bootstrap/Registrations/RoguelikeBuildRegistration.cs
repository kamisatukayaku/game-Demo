namespace Game.Modes.Roguelike.Bootstrap.Registrations
{
  static class RoguelikeBuildRegistration
  {
    internal static void Install()
    {
      // Build 模块运行时由 RunBuildApplier / MechanicManager 在玩家上挂载，无需全局 Locator 注册。
    }
  }
}
