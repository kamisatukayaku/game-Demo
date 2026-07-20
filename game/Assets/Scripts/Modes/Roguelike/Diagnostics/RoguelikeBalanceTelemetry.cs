#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics
{
  /// <summary>Collects combat balance metrics during Play Mode sampling runs.</summary>
  public static class RoguelikeBalanceTelemetry
  {
    public struct WaveSample
    {
      public int Seed;
      public string StarterBias;
      public int Wave;
      public int Level;
      public float DurationSeconds;
      public float Dps;
      public float DamageTaken;
      public float LowestHpRatio;
      public int Deaths;
      public int PeakEnemies;
      public float P95FrameMs;
      public string FailureReason;
    }

    static readonly List<WaveSample> s_waveSamples = new();

    public static IReadOnlyList<WaveSample> WaveSamples => s_waveSamples;

    public static void Reset() => s_waveSamples.Clear();

    public static void RecordWave(in WaveSample sample)
    {
      s_waveSamples.Add(sample);
    }
  }
}
#endif
