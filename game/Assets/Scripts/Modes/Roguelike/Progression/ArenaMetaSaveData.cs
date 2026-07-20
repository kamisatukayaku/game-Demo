using System;
using System.Collections.Generic;

namespace Game.Modes.Roguelike.Progression
{
  [Serializable]
  public class ArenaMetaSaveData
  {
    public const int CurrentSchemaVersion = 2;

    public int schema_version = CurrentSchemaVersion;
    public int shards;
    public int total_runs;
    public int total_victories;
    public int best_wave;
    public float best_boss_rush_seconds;
    public int boss_rush_clears;
    public string[] unlocked_build_ids = Array.Empty<string>();
    public string[] unlocked_evolution_ids = Array.Empty<string>();
    public string[] build_run_ids = Array.Empty<string>();
    public int[] build_run_counts = Array.Empty<int>();

    public static ArenaMetaSaveData CreateDefault() =>
      new()
      {
        schema_version = CurrentSchemaVersion,
        shards = 0,
        total_runs = 0,
        total_victories = 0,
        best_wave = 0,
        unlocked_build_ids = Array.Empty<string>(),
        unlocked_evolution_ids = Array.Empty<string>(),
        build_run_ids = Array.Empty<string>(),
        build_run_counts = Array.Empty<int>()
      };
  }
}
