using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  /// <summary>Gameplay 事件调试：最?100 条记?+ 频率统计?/summary>
  public static class GameEventDebugger
  {
    const int HistoryCapacity = 100;

    static readonly Queue<string> s_recent = new();
    static readonly Dictionary<string, int> s_counts = new(StringComparer.Ordinal);
    static bool s_enabled = true;

    public static bool Enabled
    {
      get => s_enabled;
      set => s_enabled = value;
    }

    internal static void RecordPublished(Type eventType)
    {
      if (!s_enabled || eventType == null)
        return;

      var name = eventType.Name;
      if (s_counts.TryGetValue(name, out var count))
        s_counts[name] = count + 1;
      else
        s_counts[name] = 1;

      s_recent.Enqueue(name);
      while (s_recent.Count > HistoryCapacity)
        s_recent.Dequeue();
    }

    public static IReadOnlyDictionary<string, int> GetFrequencyCounts() => s_counts;

    public static string[] GetRecentEventNames() => s_recent.ToArray();

    public static string BuildReport()
    {
      var sb = new StringBuilder(512);
      sb.AppendLine("=== GameEventBus Debug ===");
      sb.AppendLine($"Recent ({s_recent.Count}/{HistoryCapacity}):");

      var recent = GetRecentEventNames();
      for (var i = recent.Length - 1; i >= 0; i--)
        sb.AppendLine($"  {recent[i]}");

      sb.AppendLine();
      sb.AppendLine("Frequency:");
      foreach (var kv in s_counts)
        sb.AppendLine($"  {kv.Key} x {kv.Value}");

      return sb.ToString();
    }

    public static void LogReport()
    {
      Debug.Log(BuildReport());
    }

    public static void Clear()
    {
      s_recent.Clear();
      s_counts.Clear();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Architecture/Log Game Event Stats")]
    public static void LogStatsFromMenu() => LogReport();
#endif
  }
}