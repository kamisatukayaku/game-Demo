namespace Game.DevTools.Sandbox
{
  /// <summary>Archetype Runtime 统一调试埋点（Emit + MarkExecuted）?/summary>
  public static class SandboxRuntimeHooks
  {
    public static void Mage(string featureId, string description) =>
      CombatDebugBus.Emit("mage", featureId, description);

    public static void Range(string featureId, string description) =>
      CombatDebugBus.Emit("range", featureId, description);
  }
}
