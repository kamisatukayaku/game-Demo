using System.Collections.Generic;
using System.Text;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.AI;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Samples real boss skill damage timing and TTK in Play Mode.</summary>
  public sealed class BossRuntimeSampler
  {
    readonly List<BossSkillSample> _skillSamples = new();
    readonly List<BossTtkSample> _ttkSamples = new();

    float _telegraphStart;
    float _damageStart;
    string _activeSkillId;
    int _damageTicks;
    float _totalDamage;
    int _hits;
    bool _subscribed;

    public IReadOnlyList<BossSkillSample> SkillSamples => _skillSamples;
    public IReadOnlyList<BossTtkSample> TtkSamples => _ttkSamples;

    public void BeginListening()
    {
      if (_subscribed)
        return;
      CombatEventBus.PostDamage += OnPostDamage;
      _subscribed = true;
    }

    public void StopListening()
    {
      if (!_subscribed)
        return;
      CombatEventBus.PostDamage -= OnPostDamage;
      _subscribed = false;
    }

    public void TrackBoss(BossCore boss)
    {
      if (boss == null)
        return;

      var skillId = boss.ActiveSkillId;
      if (skillId != _activeSkillId)
      {
        if (!string.IsNullOrEmpty(_activeSkillId))
          FlushSkillSample(boss);

        _activeSkillId = skillId;
        _telegraphStart = Time.time;
        _damageStart = 0f;
        _damageTicks = 0;
        _totalDamage = 0f;
        _hits = 0;
      }
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      var player = GameObject.FindWithTag("Player");
      if (player == null || args.Target != player)
        return;

      var attacker = args.Attacker;
      if (attacker == null || attacker.GetComponentInParent<BossCore>() == null)
        return;

      if (_damageStart <= 0f)
        _damageStart = Time.time;

      _damageTicks++;
      _totalDamage += args.Result.FinalDamage;
      _hits++;
    }

    void FlushSkillSample(BossCore boss)
    {
      var telegraphDuration = _damageStart > 0f ? _damageStart - _telegraphStart : Time.time - _telegraphStart;
      var preDamage = _damageStart > 0f && _damageStart > _telegraphStart;
      _skillSamples.Add(new BossSkillSample(
        boss != null ? boss.Core?.EnemyId : "unknown",
        _activeSkillId,
        _telegraphStart,
        _damageStart,
        telegraphDuration,
        _totalDamage,
        _damageTicks,
        _hits,
        preDamage && _damageStart < _telegraphStart + 0.001f));
    }

    public void RecordTtk(
      string bossId,
      string starter,
      WaveBalanceRuntimeSampler.BuildTier tier,
      int seed,
      float bossMaxHp,
      float ttkSeconds,
      float playerDamageDealt,
      float playerDamageTaken,
      float lowestHpRatio,
      float targetDurationSeconds)
    {
      var deviationPct = targetDurationSeconds > 0f
        ? (ttkSeconds - targetDurationSeconds) / targetDurationSeconds * 100f
        : 0f;
      _ttkSamples.Add(new BossTtkSample(
        bossId,
        starter,
        tier.ToString(),
        seed,
        bossMaxHp,
        ttkSeconds,
        playerDamageDealt,
        playerDamageTaken,
        lowestHpRatio,
        targetDurationSeconds,
        deviationPct));
    }

    public string SkillCsvHeader() =>
      "boss_id,skill_id,telegraph_start,damage_start,telegraph_duration,total_damage,damage_ticks,hits,early_damage";

    public string SkillCsvRows(int targetFps)
    {
      var sb = new StringBuilder();
      foreach (var sample in _skillSamples)
      {
        sb.Append(sample.BossId).Append(',')
          .Append(sample.SkillId).Append(',')
          .Append(targetFps).Append(',')
          .Append(sample.TelegraphStart.ToString("F3")).Append(',')
          .Append(sample.DamageStart.ToString("F3")).Append(',')
          .Append(sample.TelegraphDuration.ToString("F3")).Append(',')
          .Append(sample.TotalDamage.ToString("F2")).Append(',')
          .Append(sample.DamageTicks).Append(',')
          .Append(sample.Hits).Append(',')
          .Append(sample.EarlyDamage ? "1" : "0").Append('\n');
      }

      return sb.ToString();
    }

    public string TtkCsvHeader() =>
      "boss_id,starter,build_tier,seed,boss_max_hp,ttk_sec,damage_dealt,damage_taken,lowest_hp_ratio,target_duration_sec,deviation_pct";

    public string TtkCsvRows()
    {
      var sb = new StringBuilder();
      foreach (var sample in _ttkSamples)
      {
        sb.Append(sample.BossId).Append(',')
          .Append(sample.Starter).Append(',')
          .Append(sample.BuildTier).Append(',')
          .Append(sample.Seed).Append(',')
          .Append(sample.BossMaxHp.ToString("F1")).Append(',')
          .Append(sample.TtkSeconds.ToString("F2")).Append(',')
          .Append(sample.PlayerDamageDealt.ToString("F1")).Append(',')
          .Append(sample.PlayerDamageTaken.ToString("F1")).Append(',')
          .Append(sample.LowestHpRatio.ToString("F3")).Append(',')
          .Append(sample.TargetDurationSeconds.ToString("F1")).Append(',')
          .Append(sample.DeviationPct.ToString("F1")).Append('\n');
      }

      return sb.ToString();
    }

    public readonly struct BossSkillSample
    {
      public readonly string BossId;
      public readonly string SkillId;
      public readonly float TelegraphStart;
      public readonly float DamageStart;
      public readonly float TelegraphDuration;
      public readonly float TotalDamage;
      public readonly int DamageTicks;
      public readonly int Hits;
      public readonly bool EarlyDamage;

      public BossSkillSample(
        string bossId,
        string skillId,
        float telegraphStart,
        float damageStart,
        float telegraphDuration,
        float totalDamage,
        int damageTicks,
        int hits,
        bool earlyDamage)
      {
        BossId = bossId;
        SkillId = skillId;
        TelegraphStart = telegraphStart;
        DamageStart = damageStart;
        TelegraphDuration = telegraphDuration;
        TotalDamage = totalDamage;
        DamageTicks = damageTicks;
        Hits = hits;
        EarlyDamage = earlyDamage;
      }
    }

    public readonly struct BossTtkSample
    {
      public readonly string BossId;
      public readonly string Starter;
      public readonly string BuildTier;
      public readonly int Seed;
      public readonly float BossMaxHp;
      public readonly float TtkSeconds;
      public readonly float PlayerDamageDealt;
      public readonly float PlayerDamageTaken;
      public readonly float LowestHpRatio;
      public readonly float TargetDurationSeconds;
      public readonly float DeviationPct;

      public BossTtkSample(
        string bossId,
        string starter,
        string buildTier,
        int seed,
        float bossMaxHp,
        float ttkSeconds,
        float playerDamageDealt,
        float playerDamageTaken,
        float lowestHpRatio,
        float targetDurationSeconds,
        float deviationPct)
      {
        BossId = bossId;
        Starter = starter;
        BuildTier = buildTier;
        Seed = seed;
        BossMaxHp = bossMaxHp;
        TtkSeconds = ttkSeconds;
        PlayerDamageDealt = playerDamageDealt;
        PlayerDamageTaken = playerDamageTaken;
        LowestHpRatio = lowestHpRatio;
        TargetDurationSeconds = targetDurationSeconds;
        DeviationPct = deviationPct;
      }
    }
  }
}
