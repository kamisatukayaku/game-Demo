using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.AI;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Records boss→player damage for FPS / telegraph / frame-damage validation.</summary>
  public sealed class BossFpsDamageRecorder
  {
    public readonly struct DamageEvent
    {
      public readonly int Frame;
      public readonly float Time;
      public readonly float UnscaledTime;
      public readonly string SkillId;
      public readonly string AttackInstanceKey;
      public readonly float Damage;
      public readonly bool IsTickGroup;

      public DamageEvent(
        int frame,
        float time,
        float unscaledTime,
        string skillId,
        string attackInstanceKey,
        float damage,
        bool isTickGroup)
      {
        Frame = frame;
        Time = time;
        UnscaledTime = unscaledTime;
        SkillId = skillId;
        AttackInstanceKey = attackInstanceKey;
        Damage = damage;
        IsTickGroup = isTickGroup;
      }
    }

    public readonly struct SkillWindow
    {
      public readonly string SkillId;
      public readonly float EnterTime;
      public readonly int EnterFrame;
      public readonly int AttackInstanceId;

      public SkillWindow(string skillId, float enterTime, int enterFrame, int attackInstanceId)
      {
        SkillId = skillId;
        EnterTime = enterTime;
        EnterFrame = enterFrame;
        AttackInstanceId = attackInstanceId;
      }
    }

    readonly List<DamageEvent> _events = new();
    readonly List<SkillWindow> _skills = new();
    readonly Dictionary<int, List<float>> _frameDamage = new();

    string _activeSkillId;
    int _activeAttackInstanceId;
    float _skillEnterTime;
    int _skillEnterFrame;
    bool _subscribed;
    BossCore _boss;

    public IReadOnlyList<DamageEvent> Events => _events;
    public IReadOnlyList<SkillWindow> Skills => _skills;

    static readonly Dictionary<string, float> SkillWindupSeconds = new()
    {
      { "trail_dash", 1.1f },
      { "double_dash", 1.25f },
      { "phantom_rush", 0f },
      { "shield", 0f }
    };

    public void Begin(BossCore boss)
    {
      _boss = boss;
      if (_subscribed)
        return;
      CombatEventBus.PostDamage += OnPostDamage;
      _subscribed = true;
    }

    public void Stop()
    {
      if (!_subscribed)
        return;
      CombatEventBus.PostDamage -= OnPostDamage;
      _subscribed = false;
      FlushActiveSkill();
    }

    public void Tick()
    {
      if (_boss == null)
        return;

      var skillId = _boss.ActiveSkillId;
      if (skillId == _activeSkillId)
        return;

      FlushActiveSkill();
      if (string.IsNullOrEmpty(skillId))
        return;

      _activeSkillId = skillId;
      _activeAttackInstanceId = _boss.ActiveAttackInstanceId;
      _skillEnterTime = Time.time;
      _skillEnterFrame = Time.frameCount;
      _skills.Add(new SkillWindow(skillId, _skillEnterTime, _skillEnterFrame, _activeAttackInstanceId));
    }

    void FlushActiveSkill()
    {
      if (string.IsNullOrEmpty(_activeSkillId))
        return;
      _activeSkillId = null;
      _activeAttackInstanceId = 0;
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      var player = GameObject.FindWithTag("Player");
      if (player == null || args.Target != player)
        return;

      var attacker = args.Attacker;
      if (attacker == null)
        return;

      var boss = attacker.GetComponentInParent<BossCore>();
      if (boss == null || (_boss != null && boss != _boss))
        return;

      var skillId = boss.ActiveSkillId ?? _activeSkillId ?? "unknown";
      var instanceKey = boss.ActiveAttackInstanceId.ToString();
      var isTick = args.Request.DamageSourceId != null
                   && args.Request.DamageSourceId.Contains("tick");

      var evt = new DamageEvent(
        Time.frameCount,
        Time.time,
        Time.unscaledTime,
        skillId,
        instanceKey,
        args.Result.FinalDamage,
        isTick);

      _events.Add(evt);
      if (!_frameDamage.TryGetValue(evt.Frame, out var list))
      {
        list = new List<float>();
        _frameDamage[evt.Frame] = list;
      }

      list.Add(evt.Damage);
    }

    public FpsRunResult BuildRunResult(int targetFps, float sampleSeconds)
    {
      var earlyTelegraphViolations = CountEarlyTelegraphViolations();
      var frameDamageViolations = CountFrameDamageViolations();
      return new FpsRunResult(
        targetFps,
        sampleSeconds,
        _events.Count,
        _skills.Count,
        TotalDamage(),
        earlyTelegraphViolations,
        frameDamageViolations);
    }

    int CountEarlyTelegraphViolations()
    {
      var violations = 0;
      foreach (var evt in _events)
      {
        var window = FindSkillWindow(evt.SkillId, evt.Time);
        if (window == null)
          continue;
        if (!SkillWindupSeconds.TryGetValue(window.Value.SkillId, out var windup) || windup <= 0f)
          continue;
        if (evt.Time < window.Value.EnterTime + windup - 0.02f)
          violations++;
      }

      return violations;
    }

    int CountFrameDamageViolations()
    {
      var violations = 0;
      foreach (var pair in _frameDamage)
      {
        if (pair.Value.Count > 1)
          violations += pair.Value.Count - 1;
      }

      return violations;
    }

    SkillWindow? FindSkillWindow(string skillId, float time)
    {
      SkillWindow? best = null;
      foreach (var window in _skills)
      {
        if (window.SkillId != skillId)
          continue;
        if (time >= window.EnterTime && (!best.HasValue || window.EnterTime > best.Value.EnterTime))
          best = window;
      }

      return best;
    }

    float TotalDamage()
    {
      var sum = 0f;
      foreach (var evt in _events)
        sum += evt.Damage;
      return sum;
    }

    public string ToCsvRows(string bossId)
    {
      var sb = new StringBuilder();
      foreach (var evt in _events)
      {
        sb.Append(bossId).Append(',')
          .Append(evt.SkillId).Append(',')
          .Append(evt.Frame).Append(',')
          .Append(evt.Time.ToString("F4")).Append(',')
          .Append(evt.Damage.ToString("F2")).Append(',')
          .Append(evt.AttackInstanceKey).Append('\n');
      }

      return sb.ToString();
    }

    public readonly struct FpsRunResult
    {
      public readonly int TargetFps;
      public readonly float SampleSeconds;
      public readonly int DamageEvents;
      public readonly int SkillExecutions;
      public readonly float TotalDamage;
      public readonly int EarlyTelegraphViolations;
      public readonly int FrameDamageViolations;

      public FpsRunResult(
        int targetFps,
        float sampleSeconds,
        int damageEvents,
        int skillExecutions,
        float totalDamage,
        int earlyTelegraphViolations,
        int frameDamageViolations)
      {
        TargetFps = targetFps;
        SampleSeconds = sampleSeconds;
        DamageEvents = damageEvents;
        SkillExecutions = skillExecutions;
        TotalDamage = totalDamage;
        EarlyTelegraphViolations = earlyTelegraphViolations;
        FrameDamageViolations = frameDamageViolations;
      }
    }

    public static string ValidateCrossFps(IReadOnlyList<FpsRunResult> runs, float tickDamageTolerance)
    {
      if (runs == null || runs.Count < 2)
        return null;

      var failures = new List<string>();
      foreach (var run in runs)
      {
        if (run.EarlyTelegraphViolations > 0)
          failures.Add($"FPS {run.TargetFps}: early telegraph damage={run.EarlyTelegraphViolations}");
        if (run.FrameDamageViolations > 0)
          failures.Add($"FPS {run.TargetFps}: frame damage violations={run.FrameDamageViolations}");
        if (run.DamageEvents == 0 && run.SkillExecutions == 0)
          failures.Add($"FPS {run.TargetFps}: no boss skill activity recorded");
      }

      var damages = runs.Select(r => r.TotalDamage).ToArray();
      var max = damages.Max();
      var min = damages.Min();
      if (max - min > tickDamageTolerance)
        failures.Add($"Total damage spread {min:F1}-{max:F1} exceeds tick tolerance {tickDamageTolerance:F1}");

      return failures.Count == 0 ? null : string.Join("; ", failures);
    }
  }
}
