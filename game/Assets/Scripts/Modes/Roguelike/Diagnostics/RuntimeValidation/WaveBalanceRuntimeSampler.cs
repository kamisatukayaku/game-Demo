using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Collects per-wave runtime balance metrics during Play Mode runs.</summary>
  public sealed class WaveBalanceRuntimeSampler
  {
    readonly List<WaveSample> _samples = new();
    float _waveStartTime;
    int _waveStartLevel;
    float _waveStartXp;
    int _waveKills;
    float _waveDamageDealt;
    float _waveDamageTaken;
    float _waveLowestHpRatio = 1f;
    int _waveDashCount;
    int _wavePeakEnemies;
    readonly List<string> _upgradeIds = new();

    public IReadOnlyList<WaveSample> Samples => _samples;
    public IReadOnlyList<string> UpgradeIds => _upgradeIds;

    public void BeginWave(int wave)
    {
      _waveStartTime = Time.time;
      _waveStartLevel = ExperienceSystem.Level;
      _waveStartXp = ExperienceSystem.TotalXp;
      _waveKills = CombatEventSubscriber.Instance != null ? CombatEventSubscriber.Instance.TotalKills : 0;
      _waveDamageDealt = RuntimeValidationTelemetry.GetEventCount("player_damage_dealt");
      _waveDamageTaken = RuntimeValidationTelemetry.GetEventCount("player_damage_taken");
      _waveLowestHpRatio = 1f;
      _waveDashCount = RuntimeValidationTelemetry.GetEventCount("player_dash");
      _wavePeakEnemies = 0;
      RuntimeValidationTelemetry.IncrementEvent($"wave_{wave}_begin");
    }

    public void Tick(int liveEnemies)
    {
      _wavePeakEnemies = Mathf.Max(_wavePeakEnemies, liveEnemies);
      var player = GameObject.FindWithTag("Player");
      if (player != null)
      {
        var health = player.GetComponent<Game.Shared.Combat.Health.Health>();
        if (health != null && health.MaxHp > 0f)
          _waveLowestHpRatio = Mathf.Min(_waveLowestHpRatio, health.CurrentHp / health.MaxHp);
      }
    }

    public void RecordUpgrade(string upgradeId)
    {
      if (!string.IsNullOrEmpty(upgradeId))
        _upgradeIds.Add(upgradeId);
    }

    public void EndWave(int wave, float durationOverride = -1f)
    {
      var duration = durationOverride >= 0f ? durationOverride : Time.time - _waveStartTime;
      var killsNow = CombatEventSubscriber.Instance != null ? CombatEventSubscriber.Instance.TotalKills : 0;
      var damageDealt = RuntimeValidationTelemetry.GetEventCount("player_damage_dealt") - _waveDamageDealt;
      var damageTaken = RuntimeValidationTelemetry.GetEventCount("player_damage_taken") - _waveDamageTaken;
      var dashCount = RuntimeValidationTelemetry.GetEventCount("player_dash") - _waveDashCount;

      _samples.Add(new WaveSample(
        wave,
        _waveStartLevel,
        ExperienceSystem.Level,
        ExperienceSystem.TotalXp - _waveStartXp,
        ExperienceSystem.Level - _waveStartLevel,
        duration,
        damageDealt,
        damageTaken,
        killsNow - _waveKills,
        _waveLowestHpRatio,
        dashCount,
        _wavePeakEnemies));

      RuntimeValidationTelemetry.IncrementEvent($"wave_{wave}_end");
    }

    public static void ApplyBuildTier(string buildId, BuildTier tier, int seed)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      LevelUpChoiceDatabase.Reseed(seed);
      ArenaBuildBootstrap.ConfigureForSimulation(buildId);
      RunBuildState.Reset(ArenaBuildBootstrap.GetThemeForBuild(buildId));

      var picks = tier switch
      {
        BuildTier.Low => GetLowBuildPicks(buildId),
        BuildTier.Mid => GetMidBuildPicks(buildId),
        BuildTier.High => GetHighBuildPicks(buildId),
        _ => System.Array.Empty<string>()
      };

      foreach (var id in picks)
      {
        var upgrade = LevelUpChoiceDatabase.FindById(id);
        if (upgrade != null)
          RunBuildState.ApplyChoice(upgrade);
      }
    }

    static string[] GetLowBuildPicks(string buildId)
    {
      if (buildId == ArenaBuildBootstrap.Shooter)
        return new[] { "num_common_damage_01", "num_player_max_hp_01" };
      if (buildId == ArenaBuildBootstrap.Contact)
        return new[] { "num_common_damage_01", "num_warrior_orbit_speed_01" };
      return new[] { "num_common_damage_01", "num_mage_gravity_well_01" };
    }

    static string[] GetMidBuildPicks(string buildId)
    {
      var list = new List<string>
      {
        "num_common_damage_01", "num_common_damage_02",
        "num_common_attack_speed_01", "num_player_max_hp_01", "num_player_move_speed_01"
      };
      if (buildId == ArenaBuildBootstrap.Shooter)
        list.Add("num_ranged_pierce_01");
      else if (buildId == ArenaBuildBootstrap.Contact)
        list.Add("num_warrior_orbit_speed_01");
      else
        list.Add("num_mage_gravity_well_01");
      return list.ToArray();
    }

    static string[] GetHighBuildPicks(string buildId)
    {
      var list = new List<string>();
      for (var level = 1; level <= 4; level++)
      {
        list.Add($"num_common_damage_{level:00}");
        list.Add($"num_common_attack_speed_{level:00}");
      }

      list.Add("num_player_max_hp_01");
      list.Add("num_player_max_hp_02");
      list.Add("num_player_move_speed_01");
      if (buildId == ArenaBuildBootstrap.Shooter)
      {
        list.Add("num_ranger_detached_slot_01");
        list.Add("num_ranged_pierce_01");
      }
      else if (buildId == ArenaBuildBootstrap.Contact)
      {
        list.Add("num_warrior_orbit_speed_01");
        list.Add("num_warrior_orbit_speed_02");
      }
      else
      {
        list.Add("num_mage_gravity_well_01");
        list.Add("num_mage_gravity_well_02");
      }

      return list.ToArray();
    }

    public string ToCsvHeader() =>
      "wave,start_level,end_level,xp_gained,level_ups,duration_sec,damage_dealt,damage_taken,kills,lowest_hp_ratio,dash_count,peak_enemies";

    public string ToCsvRows(string starter, int seed)
    {
      var sb = new StringBuilder();
      foreach (var sample in _samples)
      {
        sb.Append(starter).Append(',')
          .Append(seed).Append(',')
          .Append(sample.Wave).Append(',')
          .Append(sample.StartLevel).Append(',')
          .Append(sample.EndLevel).Append(',')
          .Append(sample.XpGained.ToString("F1")).Append(',')
          .Append(sample.LevelUps).Append(',')
          .Append(sample.DurationSec.ToString("F2")).Append(',')
          .Append(sample.DamageDealt.ToString("F1")).Append(',')
          .Append(sample.DamageTaken.ToString("F1")).Append(',')
          .Append(sample.Kills).Append(',')
          .Append(sample.LowestHpRatio.ToString("F3")).Append(',')
          .Append(sample.DashCount).Append(',')
          .Append(sample.PeakEnemies).Append('\n');
      }

      return sb.ToString();
    }

    public enum BuildTier
    {
      None,
      Low,
      Mid,
      High
    }

    public readonly struct WaveSample
    {
      public readonly int Wave;
      public readonly int StartLevel;
      public readonly int EndLevel;
      public readonly float XpGained;
      public readonly int LevelUps;
      public readonly float DurationSec;
      public readonly float DamageDealt;
      public readonly float DamageTaken;
      public readonly int Kills;
      public readonly float LowestHpRatio;
      public readonly int DashCount;
      public readonly int PeakEnemies;

      public WaveSample(
        int wave,
        int startLevel,
        int endLevel,
        float xpGained,
        int levelUps,
        float durationSec,
        float damageDealt,
        float damageTaken,
        int kills,
        float lowestHpRatio,
        int dashCount,
        int peakEnemies)
      {
        Wave = wave;
        StartLevel = startLevel;
        EndLevel = endLevel;
        XpGained = xpGained;
        LevelUps = levelUps;
        DurationSec = durationSec;
        DamageDealt = damageDealt;
        DamageTaken = damageTaken;
        Kills = kills;
        LowestHpRatio = lowestHpRatio;
        DashCount = dashCount;
        PeakEnemies = peakEnemies;
      }
    }
  }
}
