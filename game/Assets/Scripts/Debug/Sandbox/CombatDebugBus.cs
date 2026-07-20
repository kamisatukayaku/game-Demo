using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DevTools.Sandbox
{
  public static class CombatDebugBus
  {
    const int MaxHistory = 300;

    static readonly List<DebugEvent> s_history = new();

    public static event Action<DebugEvent> OnEvent;

    public static IReadOnlyList<DebugEvent> History => s_history;

    public static void Emit(string category, string featureId, string description)
    {
      if (string.IsNullOrEmpty(featureId))
        featureId = "unknown";

      var evt = new DebugEvent
      {
        Time = Time.time,
        Category = category ?? "general",
        FeatureId = featureId,
        Description = description ?? featureId
      };

      s_history.Add(evt);
      if (s_history.Count > MaxHistory)
        s_history.RemoveAt(0);

      FeatureExecutionTracker.MarkExecuted(featureId);
      OnEvent?.Invoke(evt);
    }

    public static void Clear() => s_history.Clear();
  }
}
