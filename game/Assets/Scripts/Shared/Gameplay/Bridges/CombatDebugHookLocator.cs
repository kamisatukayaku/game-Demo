using System;

namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>Optional diagnostics bridge. Runtime gameplay remains independent from debug tooling.</summary>
  public static class CombatDebugHookLocator
  {
    public static Action<string, string> MageHook { private get; set; }
    public static Action<string, string> RangeHook { private get; set; }

    public static void Mage(string featureId, string description)
    {
      MageHook?.Invoke(featureId, description);
    }

    public static void Range(string featureId, string description)
    {
      RangeHook?.Invoke(featureId, description);
    }

    public static void Clear()
    {
      MageHook = null;
      RangeHook = null;
    }
  }
}
