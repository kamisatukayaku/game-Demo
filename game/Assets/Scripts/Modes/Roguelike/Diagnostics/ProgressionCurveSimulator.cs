using System;
using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics
{
  /// <summary>
  /// Deterministic wave XP projection for level-curve validation (no Play Mode required).
  /// </summary>
  public static class ProgressionCurveSimulator
  {
    public enum XpScenario
    {
      Minimum,
      Average,
      Maximum
    }

    public readonly struct WaveProjection
    {
      public readonly int Wave;
      public readonly int EnemyCount;
      public readonly int CumulativeXp;
      public readonly int Level;

      public WaveProjection(int wave, int enemyCount, int cumulativeXp, int level)
      {
        Wave = wave;
        EnemyCount = enemyCount;
        CumulativeXp = cumulativeXp;
        Level = level;
      }
    }

    public readonly struct ScenarioReport
    {
      public readonly XpScenario Scenario;
      public readonly int XpPerMob;
      public readonly WaveProjection[] Milestones;

      public ScenarioReport(XpScenario scenario, int xpPerMob, WaveProjection[] milestones)
      {
        Scenario = scenario;
        XpPerMob = xpPerMob;
        Milestones = milestones;
      }
    }

    const int MaxEnemiesPerWaveBase = 24;
    const float GrowthPerWave = 1.14f;
    const int MinEnemiesPerWave = 28;
    const int MaxCapEnemies = 350;
    static readonly int[] BossWaves = { 5, 8, 10, 13, 15, 18 };
    const int BossXpAverage = 100;

    public static int ProjectWaveEnemyCount(int wave)
    {
      var baseCount = Mathf.FloorToInt(MaxEnemiesPerWaveBase * Mathf.Pow(GrowthPerWave, wave - 1));
      var count = Mathf.Max(MinEnemiesPerWave, baseCount) + Mathf.FloorToInt(wave * 1.6f);
      return Mathf.Min(count, MaxCapEnemies);
    }

    public static int XpPerMobForScenario(XpScenario scenario) => scenario switch
    {
      XpScenario.Minimum => 10,
      XpScenario.Average => 14,
      XpScenario.Maximum => 18,
      _ => 14
    };

    public static ScenarioReport SimulateScenario(XpScenario scenario, int xpBase, float xpGrowth, int maxLevel = 25)
    {
      var xpPerMob = XpPerMobForScenario(scenario);
      var cumulative = 0;
      var milestones = new List<WaveProjection>();
      var milestoneWaves = new[] { 1, 5, 10, 20 };

      for (var wave = 1; wave <= 20; wave++)
      {
        cumulative += ProjectWaveEnemyCount(wave) * xpPerMob;
        if (Array.IndexOf(BossWaves, wave) >= 0)
          cumulative += BossXpAverage;

        foreach (var milestone in milestoneWaves)
        {
          if (wave != milestone)
            continue;

          milestones.Add(new WaveProjection(
            wave,
            ProjectWaveEnemyCount(wave),
            cumulative,
            LevelAtXp(cumulative, xpBase, xpGrowth, maxLevel)));
        }
      }

      return new ScenarioReport(scenario, xpPerMob, milestones.ToArray());
    }

    public static int LevelAtXp(int totalXp, int xpBase, float xpGrowth, int maxLevel)
    {
      var level = 1;
      while (level < maxLevel)
      {
        var need = XpRequiredForLevel(level + 1, xpBase, xpGrowth);
        if (totalXp >= need)
          level++;
        else
          break;
      }

      return level;
    }

    public static int XpRequiredForLevel(int level, int xpBase, float xpGrowth)
    {
      if (level <= 1)
        return 0;

      var total = 0;
      for (var lv = 1; lv < level; lv++)
        total += XpStepForLevel(lv, xpBase, xpGrowth);
      return total;
    }

    public static int XpStepForLevel(int currentLevel, int xpBase, float xpGrowth)
    {
      var levelIndex = Mathf.Max(0, currentLevel - 1);
      var baseXp = Mathf.Max(1, xpBase);
      var growth = Mathf.Max(1f, xpGrowth);
      return Mathf.Max(1, Mathf.RoundToInt(baseXp * Mathf.Pow(growth, levelIndex)));
    }

    public static bool ValidateTargets(int xpBase, float xpGrowth, out string failureReport)
    {
      var sb = new StringBuilder();
      var ok = true;

      var avg = SimulateScenario(XpScenario.Average, xpBase, xpGrowth);
      foreach (var m in avg.Milestones)
      {
        switch (m.Wave)
        {
          case 1 when m.Level < 2 || m.Level > 3:
            ok = false;
            sb.AppendLine($"W1 avg level {m.Level} outside [2,3].");
            break;
          case 5 when m.Level < 8 || m.Level > 11:
            ok = false;
            sb.AppendLine($"W5 avg level {m.Level} outside [8,11].");
            break;
          case 10 when m.Level < 15 || m.Level > 19:
            ok = false;
            sb.AppendLine($"W10 avg level {m.Level} outside [15,19].");
            break;
          case 20 when m.Level < 24 || m.Level > 25:
            ok = false;
            sb.AppendLine($"W20 avg level {m.Level} outside [24,25].");
            break;
        }
      }

      var maxW1 = SimulateScenario(XpScenario.Maximum, xpBase, xpGrowth)
        .Milestones[0].Level;
      if (maxW1 > 4)
      {
        ok = false;
        sb.AppendLine($"W1 max-xp level {maxW1} exceeds 4.");
      }

      var minW20 = SimulateScenario(XpScenario.Minimum, xpBase, xpGrowth)
        .Milestones[^1].Level;
      if (minW20 < 22)
      {
        ok = false;
        sb.AppendLine($"W20 min-xp level {minW20} below 22.");
      }

      failureReport = sb.ToString();
      return ok;
    }

    public static string FormatReport(int xpBase, float xpGrowth)
    {
      var sb = new StringBuilder();
      sb.AppendLine($"ProgressionCurve xp_base={xpBase} xp_growth={xpGrowth:F3}");
      foreach (XpScenario scenario in Enum.GetValues(typeof(XpScenario)))
      {
        var report = SimulateScenario(scenario, xpBase, xpGrowth);
        sb.AppendLine($"[{scenario}] xp/mob={report.XpPerMob}");
        foreach (var m in report.Milestones)
          sb.AppendLine($"  W{m.Wave}: enemies={m.EnemyCount} cumXp={m.CumulativeXp} level={m.Level}");
      }

      return sb.ToString();
    }
  }
}
