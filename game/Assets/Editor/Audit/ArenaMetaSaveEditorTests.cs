using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
  public static class ArenaMetaSaveEditorTests
  {
    const string TestPrefix = "arena_meta_test_";

    [MenuItem("Tools/Audit/Run Arena Meta Save Tests")]
    public static void RunAll()
    {
      var fails = 0;
      fails += Run(Test_NewSaveDefaults);
      fails += Run(Test_LegacyMigration);
      fails += Run(Test_CurrentFormatRoundTrip);
      fails += Run(Test_CorruptDataUsesDefaultsAndBackup);
      fails += Run(Test_UnknownFutureVersion);
      fails += Run(Test_RepeatedSaveLoad);

      if (fails > 0)
        throw new InvalidOperationException($"ArenaMetaSaveEditorTests failed: {fails} case(s).");

      Debug.Log("[ArenaMetaSaveEditorTests] All passed.");
    }

    public static void RunBatchAndQuit()
    {
      RunAll();
      EditorApplication.Exit(0);
    }

    static int Run(Action test)
    {
      try
      {
        test();
        return 0;
      }
      catch (Exception e)
      {
        Debug.LogError($"[ArenaMetaSaveEditorTests] {test.Method.Name} failed: {e.Message}");
        return 1;
      }
      finally
      {
        ClearTestKeys();
        Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      }
    }

    static void ClearTestKeys()
    {
      PlayerPrefs.DeleteKey("arena_meta_save_v1");
      PlayerPrefs.DeleteKey("arena_meta_save_corrupt_backup");
      PlayerPrefs.DeleteKey("arena_meta_save_unknown_version_backup");
      PlayerPrefs.DeleteKey("arena_meta_shards");
      PlayerPrefs.DeleteKey("arena_total_runs");
      PlayerPrefs.DeleteKey("arena_total_victories");
      PlayerPrefs.DeleteKey("arena_best_wave");
      PlayerPrefs.DeleteKey("arena_codex_build_mage");
      PlayerPrefs.DeleteKey("arena_codex_build_shooter");
      PlayerPrefs.DeleteKey("arena_build_runs_mage");
      PlayerPrefs.Save();
    }

    static void Test_NewSaveDefaults()
    {
      ClearTestKeys();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      var data = Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data;
      if (data.shards != 0 || data.total_runs != 0 || data.best_wave != 0)
        throw new InvalidOperationException("Expected default zeros.");
    }

    static void Test_LegacyMigration()
    {
      ClearTestKeys();
      PlayerPrefs.SetInt("arena_meta_shards", 42);
      PlayerPrefs.SetInt("arena_total_runs", 3);
      PlayerPrefs.SetInt("arena_total_victories", 1);
      PlayerPrefs.SetInt("arena_best_wave", 12);
      PlayerPrefs.SetInt("arena_codex_build_mage", 1);
      PlayerPrefs.SetInt("arena_build_runs_mage", 5);
      PlayerPrefs.Save();

      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      var data = Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data;
      if (data.shards != 42 || data.total_runs != 3 || data.best_wave != 12)
        throw new InvalidOperationException("Legacy migration values mismatch.");
      if (!Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.IsBuildUnlocked("mage"))
        throw new InvalidOperationException("Legacy build unlock not migrated.");
      if (Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.GetBuildRunCount("mage") != 5)
        throw new InvalidOperationException("Legacy build run count not migrated.");
      if (!PlayerPrefs.HasKey("arena_meta_save_v1"))
        throw new InvalidOperationException("Expected versioned save after migration.");
    }

    static void Test_CurrentFormatRoundTrip()
    {
      ClearTestKeys();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      var data = Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data;
      data.shards = 99;
      data.total_victories = 2;
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Save();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      if (Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data.shards != 99)
        throw new InvalidOperationException("Round-trip shards mismatch.");
    }

    static void Test_CorruptDataUsesDefaultsAndBackup()
    {
      ClearTestKeys();
      PlayerPrefs.SetString("arena_meta_save_v1", "{not-json");
      PlayerPrefs.Save();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      if (Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data.shards != 0)
        throw new InvalidOperationException("Corrupt save should use defaults.");
      if (!PlayerPrefs.HasKey("arena_meta_save_corrupt_backup"))
        throw new InvalidOperationException("Expected corrupt backup key.");
    }

    static void Test_UnknownFutureVersion()
    {
      ClearTestKeys();
      PlayerPrefs.SetString("arena_meta_save_v1", "{\"schema_version\":999,\"shards\":50}");
      PlayerPrefs.Save();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      if (Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.Data.shards != 0)
        throw new InvalidOperationException("Unknown version should use safe defaults.");
      if (!PlayerPrefs.HasKey("arena_meta_save_unknown_version_backup"))
        throw new InvalidOperationException("Expected unknown-version backup.");
    }

    static void Test_RepeatedSaveLoad()
    {
      ClearTestKeys();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.UnlockBuild("shooter");
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.IncrementBuildRunCount("shooter");
      Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.ReloadForTests();
      if (!Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.IsBuildUnlocked("shooter"))
        throw new InvalidOperationException("Unlock not persisted.");
      if (Game.Modes.Roguelike.Progression.ArenaMetaSaveStore.GetBuildRunCount("shooter") != 1)
        throw new InvalidOperationException("Build run count not persisted.");
    }
  }
}
