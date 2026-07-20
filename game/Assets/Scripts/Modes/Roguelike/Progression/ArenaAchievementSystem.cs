using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Data;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>C2: Demo achievements + daily contracts persisted in PlayerPrefs.</summary>
  public static class ArenaAchievementSystem
  {
    [Serializable]
    public class AchievementDef
    {
      public string id;
      public string title;
      public string description;
      public string metric;
      public int target;
    }

    [Serializable]
    public class DailyContractDef
    {
      public string id;
      public string title;
      public string description;
      public string metric;
      public int target;
      public int reward_shards;
    }

    [Serializable]
    class Root
    {
      public AchievementDef[] achievements;
      public DailyContractDef[] daily_contracts;
    }

    static AchievementDef[] s_achievements = Array.Empty<AchievementDef>();
    static DailyContractDef[] s_dailyContracts = Array.Empty<DailyContractDef>();
    static bool s_loaded;

    const string UnlockedPrefix = "arena_ach_";
    const string DailyDayKey = "arena_daily_day";
    const string DailyContractKey = "arena_daily_contract";
    const string DailyDoneKey = "arena_daily_done";
    const string BuildsWonKey = "arena_builds_won";

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      JsonDataLoader.TryParse("progression/arena_achievements", json =>
      {
        var root = JsonUtility.FromJson<Root>(json);
        s_achievements = root?.achievements ?? Array.Empty<AchievementDef>();
        s_dailyContracts = root?.daily_contracts ?? Array.Empty<DailyContractDef>();
      });
    }

    public static IReadOnlyList<AchievementDef> AllAchievements
    {
      get
      {
        EnsureLoaded();
        return s_achievements;
      }
    }

    public static DailyContractDef ActiveDailyContract
    {
      get
      {
        EnsureLoaded();
        RefreshDailyContractIfNeeded();
        var id = PlayerPrefs.GetString(DailyContractKey, string.Empty);
        foreach (var contract in s_dailyContracts)
        {
          if (contract != null && contract.id == id)
            return contract;
        }

        return s_dailyContracts.Length > 0 ? s_dailyContracts[0] : null;
      }
    }

    public static bool IsUnlocked(string achievementId) =>
      !string.IsNullOrEmpty(achievementId) && PlayerPrefs.GetInt(UnlockedPrefix + achievementId, 0) == 1;

    public static bool IsDailyComplete() => PlayerPrefs.GetInt(DailyDoneKey, 0) == 1;

    public static void EvaluateRun(bool victory, int wave, int kills, int level, string buildId, string difficultyId)
    {
      EnsureLoaded();
      RefreshDailyContractIfNeeded();

      foreach (var ach in s_achievements)
      {
        if (ach == null || IsUnlocked(ach.id))
          continue;

        if (MeetsMetric(ach.metric, ach.target, victory, wave, kills, level, buildId, difficultyId))
          Unlock(ach.id);
      }

      if (victory && !string.IsNullOrEmpty(buildId))
        MarkBuildVictory(buildId);

      EvaluateDaily(victory, wave, kills, level, buildId);
      PlayerPrefs.Save();
    }

    static void EvaluateDaily(bool victory, int wave, int kills, int level, string buildId)
    {
      if (IsDailyComplete())
        return;

      var contract = ActiveDailyContract;
      if (contract == null)
        return;

      if (!MeetsMetric(contract.metric, contract.target, victory, wave, kills, level, buildId, ArenaDifficultyRuntime.DifficultyId))
        return;

      PlayerPrefs.SetInt(DailyDoneKey, 1);
      var bonus = Mathf.Max(1, contract.reward_shards);
      ArenaMetaProgress.AddShards(bonus);
    }

    static bool MeetsMetric(
      string metric,
      int target,
      bool victory,
      int wave,
      int kills,
      int level,
      string buildId,
      string difficultyId)
    {
      switch (metric)
      {
        case "wave_reached":
          return wave >= target;
        case "kills":
          return kills >= target;
        case "level":
          return level >= target;
        case "victory_w15":
        case "victory_w20":
          return victory && wave >= target;
        case "hard_victory":
          return victory && difficultyId == "hard";
        case "builds_won":
          return CountBuildsWon() >= target;
        case "total_shards":
          return ArenaMetaProgress.TotalShards >= target;
        case "build_victory":
          return victory;
        default:
          return false;
      }
    }

    static void Unlock(string id)
    {
      PlayerPrefs.SetInt(UnlockedPrefix + id, 1);
    }

    static void MarkBuildVictory(string buildId)
    {
      var key = BuildsWonKey + "_" + buildId;
      if (PlayerPrefs.GetInt(key, 0) == 1)
        return;
      PlayerPrefs.SetInt(key, 1);
    }

    static int CountBuildsWon()
    {
      var count = 0;
      foreach (var build in new[] { ArenaBuildBootstrap.Mage, ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact })
      {
        if (PlayerPrefs.GetInt(BuildsWonKey + "_" + build, 0) == 1)
          count++;
      }
      return count;
    }

    static void RefreshDailyContractIfNeeded()
    {
      var today = DateTime.UtcNow.ToString("yyyyMMdd");
      if (PlayerPrefs.GetString(DailyDayKey, string.Empty) == today)
        return;

      PlayerPrefs.SetString(DailyDayKey, today);
      PlayerPrefs.SetInt(DailyDoneKey, 0);
      if (s_dailyContracts == null || s_dailyContracts.Length == 0)
        return;

      var idx = Mathf.Abs(today.GetHashCode()) % s_dailyContracts.Length;
      PlayerPrefs.SetString(DailyContractKey, s_dailyContracts[idx].id);
    }

    public static int UnlockedCount
    {
      get
      {
        EnsureLoaded();
        var count = 0;
        foreach (var ach in s_achievements)
        {
          if (ach != null && IsUnlocked(ach.id))
            count++;
        }
        return count;
      }
    }
  }
}
