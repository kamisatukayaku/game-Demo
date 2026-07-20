using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Build.Runtime;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B2: Runtime state for in-run Corruption picks.</summary>
  public static class CorruptionRuntime
  {
    static readonly HashSet<string> s_picked = new();

    public static void ResetRun() => s_picked.Clear();

    public static CorruptionDatabase.CorruptionDef[] RollOffer(int count = 3)
    {
      CorruptionDatabase.EnsureLoaded();
      var available = new List<CorruptionDatabase.CorruptionDef>();
      foreach (var corruption in CorruptionDatabase.All)
      {
        if (corruption != null && !s_picked.Contains(corruption.id))
          available.Add(corruption);
      }

      count = Mathf.Min(count, available.Count);
      var result = new CorruptionDatabase.CorruptionDef[count];
      for (var i = 0; i < count; i++)
      {
        var idx = Random.Range(0, available.Count);
        result[i] = available[idx];
        available.RemoveAt(idx);
      }

      return result;
    }

    public static void ApplyCorruption(CorruptionDatabase.CorruptionDef corruption)
    {
      if (corruption == null || string.IsNullOrEmpty(corruption.id))
        return;

      s_picked.Add(corruption.id);
      if (!string.IsNullOrEmpty(corruption.positive_stat))
        RunBuildState.AddStat(corruption.positive_stat, corruption.positive_value);
      if (!string.IsNullOrEmpty(corruption.negative_stat))
        RunBuildState.AddStat(corruption.negative_stat, corruption.negative_value);
    }
  }
}
