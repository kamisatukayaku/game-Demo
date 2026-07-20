using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Persistent arena meta currency stored in versioned PlayerPrefs.</summary>
  public static class ArenaMetaProgress
  {
    public static int TotalShards
    {
      get => ArenaMetaSaveStore.Data.shards;
      private set
      {
        ArenaMetaSaveStore.Data.shards = value;
        ArenaMetaSaveStore.Save();
      }
    }

    public static int TotalRuns => ArenaMetaSaveStore.Data.total_runs;
    public static int TotalVictories => ArenaMetaSaveStore.Data.total_victories;
    public static int BestWave => ArenaMetaSaveStore.Data.best_wave;
    public static float BestBossRushSeconds => ArenaMetaSaveStore.Data.best_boss_rush_seconds;

    public static void RecordBossRushVictory(float elapsedSeconds, int defeatedCount)
    {
      ArenaMetaSaveStore.EnsureLoaded();
      var data = ArenaMetaSaveStore.Data;
      data.total_runs += 1;
      data.total_victories += 1;
      data.boss_rush_clears += 1;
      if (data.best_boss_rush_seconds <= 0f || elapsedSeconds < data.best_boss_rush_seconds)
        data.best_boss_rush_seconds = elapsedSeconds;

      var shards = Mathf.Max(20, Mathf.RoundToInt(180f + defeatedCount * 40f - elapsedSeconds * 0.15f));
      data.shards += shards;
      ArenaMetaSaveStore.Save();
      UnlockBuild(ArenaBuildBootstrap.SelectedBuildId);
    }

    public static int AwardRun(bool victory, int waveReached, int kills, int level, float surviveSeconds)
    {
      ArenaMetaSaveStore.EnsureLoaded();
      var score = kills * 2f + waveReached * 55f + level * 28f + surviveSeconds * 0.35f;
      if (victory)
        score += 260f;

      var shards = Mathf.Max(5, Mathf.RoundToInt(ArenaDifficultyRuntime.ScaleRewardShards(score) * 0.1f));
      var data = ArenaMetaSaveStore.Data;
      data.shards += shards;
      data.total_runs += 1;
      if (victory)
        data.total_victories += 1;
      if (waveReached > data.best_wave)
        data.best_wave = waveReached;
      ArenaMetaSaveStore.Save();

      UnlockBuild(ArenaBuildBootstrap.SelectedBuildId);
      return shards;
    }

    public static void UnlockBuild(string buildId) => ArenaMetaSaveStore.UnlockBuild(buildId);

    public static void UnlockEvolution(string evolutionId) => ArenaMetaSaveStore.UnlockEvolution(evolutionId);

    public static bool IsBuildUnlocked(string buildId) => ArenaMetaSaveStore.IsBuildUnlocked(buildId);

    public static bool IsEvolutionUnlocked(string evolutionId) => ArenaMetaSaveStore.IsEvolutionUnlocked(evolutionId);

    static readonly string[] CodexBuildIds =
    {
      ArenaBuildBootstrap.Unified,
      ArenaBuildBootstrap.Mage,
      ArenaBuildBootstrap.Shooter,
      ArenaBuildBootstrap.Contact
    };

    public static int UnlockedBuildCount
    {
      get
      {
        var count = 0;
        foreach (var build in CodexBuildIds)
        {
          if (IsBuildUnlocked(build))
            count++;
        }
        return count;
      }
    }

    public static string GetCodexSummaryLine() =>
      $"构筑图鉴：{UnlockedBuildCount}/{CodexBuildIds.Length} 已解锁";

    public static string GetRecommendedBuildLabel()
    {
      if (!IsBuildUnlocked(ArenaBuildBootstrap.Unified))
        return $"下一局试 {ArenaBuildBootstrap.GetDisplayName(ArenaBuildBootstrap.Unified)} — {ArenaBuildBootstrap.GetTagline(ArenaBuildBootstrap.Unified)}";

      foreach (var build in new[] { ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact, ArenaBuildBootstrap.Mage })
      {
        if (!IsBuildUnlocked(build))
          return $"下一局试 {ArenaBuildBootstrap.GetDisplayName(build)} — {ArenaBuildBootstrap.GetTagline(build)}";
      }

      var leastPlayed = ArenaBuildBootstrap.Unified;
      var minRuns = int.MaxValue;
      foreach (var build in CodexBuildIds)
      {
        var runs = ArenaMetaSaveStore.GetBuildRunCount(build);
        if (runs < minRuns)
        {
          minRuns = runs;
          leastPlayed = build;
        }
      }

      return $"下一局试 {ArenaBuildBootstrap.GetDisplayName(leastPlayed)} — {ArenaBuildBootstrap.GetTagline(leastPlayed)}";
    }

    public static void RecordBuildRun(string buildId) => ArenaMetaSaveStore.IncrementBuildRunCount(buildId);

    public static void AddShards(int amount)
    {
      if (amount <= 0)
        return;
      ArenaMetaSaveStore.EnsureLoaded();
      ArenaMetaSaveStore.Data.shards += amount;
      ArenaMetaSaveStore.Save();
    }
  }
}
