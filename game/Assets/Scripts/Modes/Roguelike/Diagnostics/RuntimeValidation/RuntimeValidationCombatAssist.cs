#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using Game.Modes.Roguelike.Combat;
using Game.Shared.Combat.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>
  /// Full-run validation only: breaks supporter/healer stalemates when auto-player DPS stalls a wave.
  /// Does not run during manual play.
  /// </summary>
  public static class RuntimeValidationCombatAssist
  {
    const float StallThresholdSeconds = 40f;
    const float PulseIntervalSeconds = 8f;

    static int _lastKills = -1;
    static float _lastKillChangeTime = -1f;
    static float _lastPulseTime = -1f;

    public static void Reset()
    {
      _lastKills = -1;
      _lastKillChangeTime = -1f;
      _lastPulseTime = -1f;
    }

    public static void Tick()
    {
      if (!RuntimeValidationSettings.PlayerSurvivalBoostActive)
        return;

      var director = WaveDirector.Instance;
      if (director == null || director.CurrentPhase != WaveDirector.Phase.WaveActive)
      {
        Reset();
        return;
      }

      var kills = RuntimeValidationTelemetry.EnemiesKilled;
      if (_lastKills < 0 || kills != _lastKills)
      {
        _lastKills = kills;
        _lastKillChangeTime = Time.unscaledTime;
        return;
      }

      if (_lastKillChangeTime < 0f)
        _lastKillChangeTime = Time.unscaledTime;

      if (director.EnemiesRemaining <= 0)
        return;

      var stalledFor = Time.unscaledTime - _lastKillChangeTime;
      if (stalledFor < StallThresholdSeconds)
        return;

      if (_lastPulseTime > 0f && Time.unscaledTime - _lastPulseTime < PulseIntervalSeconds)
        return;

      ApplyPulse(director);
      _lastPulseTime = Time.unscaledTime;
    }

    static void ApplyPulse(WaveDirector director)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var damaged = 0;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null || !enemy.gameObject.activeInHierarchy)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var isBoss = enemy.GetComponent<BossCore>() != null;
        var pct = isBoss ? 0.1f : 0.22f;
        health.TakeDamage(Mathf.Max(1f, health.MaxHp * pct));
        damaged++;
      }

      if (damaged > 0)
      {
        Debug.Log(
          $"[ValidationCombatAssist] Stall pulse wave={director.CurrentWave} "
          + $"remaining={director.EnemiesRemaining} spawned={director.WaveEnemiesSpawned}/"
          + $"{director.WaveSpawnQuota} damaged={damaged} kills={RuntimeValidationTelemetry.EnemiesKilled}");
      }
    }
  }
}

#endif
