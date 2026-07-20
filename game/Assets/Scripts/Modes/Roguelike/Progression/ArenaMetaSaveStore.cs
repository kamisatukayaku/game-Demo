using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Versioned PlayerPrefs persistence for arena meta progress.</summary>
  public static class ArenaMetaSaveStore
  {
    const string SaveKey = "arena_meta_save_v1";
    const string CorruptBackupKey = "arena_meta_save_corrupt_backup";
    const string UnknownVersionBackupKey = "arena_meta_save_unknown_version_backup";

    const string LegacyShardsKey = "arena_meta_shards";
    const string LegacyTotalRunsKey = "arena_total_runs";
    const string LegacyTotalVictoriesKey = "arena_total_victories";
    const string LegacyBestWaveKey = "arena_best_wave";
    const string LegacyUnlockedBuildPrefix = "arena_codex_build_";
    const string LegacyUnlockedEvoPrefix = "arena_codex_evo_";
    const string LegacyBuildRunsPrefix = "arena_build_runs_";

    static ArenaMetaSaveData s_cached;
    static bool s_loaded;

    public static ArenaMetaSaveData Data
    {
      get
      {
        EnsureLoaded();
        return s_cached;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_cached = TryLoadVersioned() ?? MigrateLegacy() ?? ArenaMetaSaveData.CreateDefault();
    }

    public static void Save()
    {
      EnsureLoaded();
      s_cached.schema_version = ArenaMetaSaveData.CurrentSchemaVersion;
      try
      {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(s_cached));
        PlayerPrefs.Save();
      }
      catch (Exception e)
      {
        Debug.LogError($"[ArenaMetaSaveStore] Save failed: {e.Message}");
      }
    }

    public static void ReloadForTests()
    {
      s_loaded = false;
      s_cached = null;
      EnsureLoaded();
    }

    static ArenaMetaSaveData TryLoadVersioned()
    {
      if (!PlayerPrefs.HasKey(SaveKey))
        return null;

      var json = PlayerPrefs.GetString(SaveKey, string.Empty);
      if (string.IsNullOrWhiteSpace(json))
        return null;

      try
      {
        var data = JsonUtility.FromJson<ArenaMetaSaveData>(json);
        if (data == null)
          throw new InvalidOperationException("Null DTO");

        if (data.schema_version > ArenaMetaSaveData.CurrentSchemaVersion)
        {
          PlayerPrefs.SetString(UnknownVersionBackupKey, json);
          Debug.LogWarning(
            $"[ArenaMetaSaveStore] Unknown schema_version {data.schema_version}; using safe defaults.");
          return ArenaMetaSaveData.CreateDefault();
        }

        return data;
      }
      catch (Exception e)
      {
        PlayerPrefs.SetString(CorruptBackupKey, json);
        Debug.LogError($"[ArenaMetaSaveStore] Corrupt save; backup stored. {e.Message}");
        return ArenaMetaSaveData.CreateDefault();
      }
    }

    static ArenaMetaSaveData MigrateLegacy()
    {
      if (!PlayerPrefs.HasKey(LegacyShardsKey)
          && !PlayerPrefs.HasKey(LegacyTotalRunsKey)
          && !PlayerPrefs.HasKey(LegacyBestWaveKey))
        return null;

      var data = ArenaMetaSaveData.CreateDefault();
      data.shards = PlayerPrefs.GetInt(LegacyShardsKey, 0);
      data.total_runs = PlayerPrefs.GetInt(LegacyTotalRunsKey, 0);
      data.total_victories = PlayerPrefs.GetInt(LegacyTotalVictoriesKey, 0);
      data.best_wave = PlayerPrefs.GetInt(LegacyBestWaveKey, 0);

      var builds = new List<string>();
      foreach (var build in new[] { ArenaBuildBootstrap.Mage, ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact })
      {
        if (PlayerPrefs.GetInt(LegacyUnlockedBuildPrefix + build, 0) == 1)
          builds.Add(build);
      }
      data.unlocked_build_ids = builds.ToArray();

      var evos = new List<string>();
      foreach (var key in EnumerateLegacyKeys(LegacyUnlockedEvoPrefix))
      {
        if (PlayerPrefs.GetInt(key, 0) == 1)
          evos.Add(key.Substring(LegacyUnlockedEvoPrefix.Length));
      }
      data.unlocked_evolution_ids = evos.ToArray();

      var runIds = new List<string>();
      var runCounts = new List<int>();
      foreach (var build in new[] { ArenaBuildBootstrap.Mage, ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact })
      {
        var count = PlayerPrefs.GetInt(LegacyBuildRunsPrefix + build, 0);
        if (count > 0)
        {
          runIds.Add(build);
          runCounts.Add(count);
        }
      }
      data.build_run_ids = runIds.ToArray();
      data.build_run_counts = runCounts.ToArray();

      s_cached = data;
      Save();
      Debug.Log("[ArenaMetaSaveStore] Migrated legacy PlayerPrefs to versioned save.");
      return data;
    }

    static IEnumerable<string> EnumerateLegacyKeys(string prefix)
    {
      foreach (var build in new[] { ArenaBuildBootstrap.Mage, ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact })
        yield return LegacyUnlockedBuildPrefix + build;

      // PlayerPrefs has no enumeration API; known evo ids are migrated when explicitly unlocked.
    }

    public static bool IsBuildUnlocked(string buildId)
    {
      if (string.IsNullOrEmpty(buildId))
        return false;
      foreach (var id in Data.unlocked_build_ids)
      {
        if (id == buildId)
          return true;
      }
      return false;
    }

    public static void UnlockBuild(string buildId)
    {
      if (string.IsNullOrEmpty(buildId) || IsBuildUnlocked(buildId))
        return;

      var list = new List<string>(Data.unlocked_build_ids ?? Array.Empty<string>()) { buildId };
      Data.unlocked_build_ids = list.ToArray();
      Save();
    }

    public static bool IsEvolutionUnlocked(string evolutionId)
    {
      if (string.IsNullOrEmpty(evolutionId))
        return false;
      foreach (var id in Data.unlocked_evolution_ids)
      {
        if (id == evolutionId)
          return true;
      }
      return false;
    }

    public static void UnlockEvolution(string evolutionId)
    {
      if (string.IsNullOrEmpty(evolutionId) || IsEvolutionUnlocked(evolutionId))
        return;

      var list = new List<string>(Data.unlocked_evolution_ids ?? Array.Empty<string>()) { evolutionId };
      Data.unlocked_evolution_ids = list.ToArray();
      Save();
    }

    public static int GetBuildRunCount(string buildId)
    {
      if (string.IsNullOrEmpty(buildId))
        return 0;

      var ids = Data.build_run_ids ?? Array.Empty<string>();
      var counts = Data.build_run_counts ?? Array.Empty<int>();
      for (var i = 0; i < ids.Length && i < counts.Length; i++)
      {
        if (ids[i] == buildId)
          return counts[i];
      }
      return 0;
    }

    public static void IncrementBuildRunCount(string buildId)
    {
      if (string.IsNullOrEmpty(buildId))
        return;

      var ids = new List<string>(Data.build_run_ids ?? Array.Empty<string>());
      var counts = new List<int>(Data.build_run_counts ?? Array.Empty<int>());
      for (var i = 0; i < ids.Count; i++)
      {
        if (ids[i] != buildId)
          continue;
        counts[i]++;
        Data.build_run_ids = ids.ToArray();
        Data.build_run_counts = counts.ToArray();
        Save();
        return;
      }

      ids.Add(buildId);
      counts.Add(1);
      Data.build_run_ids = ids.ToArray();
      Data.build_run_counts = counts.ToArray();
      Save();
    }
  }
}
