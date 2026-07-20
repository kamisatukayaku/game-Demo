using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Combat
{
  /// <summary>
  /// 装备品质权重工具：按波次计算品质权重?Roll?
  /// </summary>
  public static class TierWeightUtils
  {
    public struct TierWeights
    {
      public int common;
      public int rare;
      public int epic;
      public int legendary;
    }

    static readonly TierWeights[] DefaultCurve =
    {
      new() { common = 78, rare = 20, epic = 2, legendary = 0 },
      new() { common = 55, rare = 35, epic = 9, legendary = 1 },
      new() { common = 35, rare = 42, epic = 20, legendary = 3 },
      new() { common = 18, rare = 40, epic = 32, legendary = 10 },
    };

    static readonly int[] DefaultCurveWaves = { 1, 3, 6, 10 };

    static List<TierWeights> _curve = new();
    static int[] _curveWaves = DefaultCurveWaves;

    public static void SetCurve(TierWeights[] weights, int[] waves)
    {
      if (weights == null || weights.Length == 0 || waves == null || waves.Length != weights.Length)
      {
        _curve.Clear();
        _curveWaves = DefaultCurveWaves;
        return;
      }

      _curve = new List<TierWeights>(weights);
      _curveWaves = waves;
    }

    public static string RollTier(int waveNumber)
    {
      var w = GetWeightsForWave(waveNumber);
      int total = w.common + w.rare + w.epic + w.legendary;
      if (total <= 0)
        return "common";

      int roll = Random.Range(0, total);
      int c = w.common;
      if (roll < c) return "common";
      roll -= c;
      c = w.rare;
      if (roll < c) return "rare";
      roll -= c;
      c = w.epic;
      if (roll < c) return "epic";
      return "legendary";
    }

    public static TierWeights GetWeightsForWave(int waveNumber)
    {
      if (_curve.Count == 0)
        return Interpolate(DefaultCurve, DefaultCurveWaves, waveNumber);

      return Interpolate(_curve.ToArray(), _curveWaves, waveNumber);
    }

    static TierWeights Interpolate(TierWeights[] points, int[] waves, int waveNumber)
    {
      if (points.Length == 0)
        return DefaultCurve[0];

      if (waveNumber <= waves[0])
        return points[0];

      for (int i = 1; i < waves.Length; i++)
      {
        if (waveNumber > waves[i])
          continue;

        var t = Mathf.InverseLerp(waves[i - 1], waves[i], waveNumber);
        return Lerp(points[i - 1], points[i], t);
      }

      return points[points.Length - 1];
    }

    static TierWeights Lerp(TierWeights a, TierWeights b, float t)
    {
      return new TierWeights
      {
        common = Mathf.RoundToInt(Mathf.Lerp(a.common, b.common, t)),
        rare = Mathf.RoundToInt(Mathf.Lerp(a.rare, b.rare, t)),
        epic = Mathf.RoundToInt(Mathf.Lerp(a.epic, b.epic, t)),
        legendary = Mathf.RoundToInt(Mathf.Lerp(a.legendary, b.legendary, t))
      };
    }

    public static string NormalizeTier(string tier)
    {
      if (string.IsNullOrEmpty(tier))
        return "common";

      tier = tier.ToLowerInvariant();
      return tier switch
      {
        "rare" => "rare",
        "epic" => "epic",
        "legendary" => "legendary",
        _ => "common"
      };
    }
  }
}