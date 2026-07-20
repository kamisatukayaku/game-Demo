using System.Collections.Generic;

namespace Game.DevTools.Sandbox
{
  public static class FeatureExecutionTracker
  {
    static readonly HashSet<string> s_executed = new();

    public static void MarkExecuted(string featureId)
    {
      if (string.IsNullOrEmpty(featureId))
        return;
      s_executed.Add(featureId);
    }

    public static bool WasExecuted(string featureId) =>
      !string.IsNullOrEmpty(featureId) && s_executed.Contains(featureId);

    public static void Clear() => s_executed.Clear();
  }
}
