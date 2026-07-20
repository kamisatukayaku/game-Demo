using System;

namespace Game.Shared.UI
{
  /// <summary>Optional hook for settings UI to reset Roguelike tutorial prefs without referencing Roguelike assemblies.</summary>
  public static class RoguelikeTutorialResetBridge
  {
    public static Action ResetAll;

    public static void TryResetAll() => ResetAll?.Invoke();
  }
}
