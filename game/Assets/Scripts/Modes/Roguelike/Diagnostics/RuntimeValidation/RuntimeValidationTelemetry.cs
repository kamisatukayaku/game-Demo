using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Combat;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Aggregates objective runtime metrics for validation reports.</summary>
  public static class RuntimeValidationTelemetry
  {
    public static int SceneLoadCount { get; private set; }
    public static int WavePhaseTransitions { get; private set; }
    public static int EnemiesSpawned { get; private set; }
    public static int EnemiesKilled { get; private set; }
    public static int BossesSpawned { get; private set; }
    public static int BossesKilled { get; private set; }
    public static int LevelUpPauseCount { get; private set; }
    public static int LevelUpResumeCount { get; private set; }
    public static int PlayerDeathCount { get; private set; }
    public static int RunRestartCount { get; private set; }
    public static int ExceptionCount { get; private set; }
    public static int ErrorLogCount { get; private set; }
    public static int PeakActiveEnemies { get; private set; }
    public static int OrphanedObjectsAfterRestart { get; private set; }

    static WaveDirector.Phase _lastPhase = WaveDirector.Phase.NotStarted;
    static int _lastWave;
    static readonly Dictionary<string, int> _eventCounts = new();

    public static void Reset()
    {
      SceneLoadCount = 0;
      WavePhaseTransitions = 0;
      EnemiesSpawned = 0;
      EnemiesKilled = 0;
      BossesSpawned = 0;
      BossesKilled = 0;
      LevelUpPauseCount = 0;
      LevelUpResumeCount = 0;
      PlayerDeathCount = 0;
      RunRestartCount = 0;
      ExceptionCount = 0;
      ErrorLogCount = 0;
      PeakActiveEnemies = 0;
      OrphanedObjectsAfterRestart = 0;
      _lastPhase = WaveDirector.Phase.NotStarted;
      _lastWave = 0;
      _eventCounts.Clear();
    }

    public static void RecordSceneLoad() => SceneLoadCount++;

    public static void RecordWaveDirectorState(WaveDirector.Phase phase, int wave)
    {
      if (phase != _lastPhase || wave != _lastWave)
      {
        WavePhaseTransitions++;
        _lastPhase = phase;
        _lastWave = wave;
      }
    }

    public static void RecordEnemySpawn(bool isBoss = false)
    {
      EnemiesSpawned++;
      if (isBoss)
        BossesSpawned++;
    }

    public static void RecordEnemyKill(bool isBoss = false)
    {
      EnemiesKilled++;
      if (isBoss)
        BossesKilled++;
    }

    public static void RecordLiveEnemyCount(int count) =>
      PeakActiveEnemies = Mathf.Max(PeakActiveEnemies, count);

    public static void RecordLevelUpPause() => LevelUpPauseCount++;

    public static void RecordLevelUpResume() => LevelUpResumeCount++;

    public static void RecordPlayerDeath() => PlayerDeathCount++;

    public static void RecordRunRestart() => RunRestartCount++;

    public static void RecordException() => ExceptionCount++;

    public static void RecordErrorLog() => ErrorLogCount++;

    public static void RecordOrphanedObjects(int count) => OrphanedObjectsAfterRestart = count;

    public static void IncrementEvent(string eventId)
    {
      if (string.IsNullOrEmpty(eventId))
        return;
      _eventCounts.TryGetValue(eventId, out var count);
      _eventCounts[eventId] = count + 1;
    }

    public static int GetEventCount(string eventId) =>
      _eventCounts.TryGetValue(eventId, out var count) ? count : 0;

    public static string FormatSummary()
    {
      var sb = new StringBuilder();
      sb.AppendLine($"scene_loads={SceneLoadCount}");
      sb.AppendLine($"wave_phase_transitions={WavePhaseTransitions}");
      sb.AppendLine($"enemies_spawned={EnemiesSpawned} killed={EnemiesKilled} peak_live={PeakActiveEnemies}");
      sb.AppendLine($"bosses_spawned={BossesSpawned} killed={BossesKilled}");
      sb.AppendLine($"levelup_pause={LevelUpPauseCount} resume={LevelUpResumeCount}");
      sb.AppendLine($"player_deaths={PlayerDeathCount} run_restarts={RunRestartCount}");
      sb.AppendLine($"exceptions={ExceptionCount} error_logs={ErrorLogCount}");
      sb.AppendLine($"orphans_after_restart={OrphanedObjectsAfterRestart}");
      return sb.ToString();
    }
  }
}
