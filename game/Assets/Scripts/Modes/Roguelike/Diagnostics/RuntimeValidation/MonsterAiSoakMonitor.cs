using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Shared.Combat.Events;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>60-second AI soak metrics for a single enemy instance.</summary>
  public sealed class MonsterAiSoakMonitor : MonoBehaviour
  {
    string _enemyId;
    string _role;
    float _startTime;
    float _lastMoveTime;
    float _longestIdle;
    float _totalMoveDistance;
    Vector2 _lastPos;
    int _attackCount;
    int _stateChanges;
    int _sprintCount;
    int _sprintRecoveries;
    int _boundaryStuckCount;
    int _buffApplyCount;
    int _buffRemoveCount;
    int _childSpawnCount;
    int _postDeathBehaviorCount;
    EnemyCore _core;
    bool _wasSprinting;
    bool _exploded;
    bool _finished;
    bool _subscribed;

    public static MonsterAiSoakMonitor Attach(GameObject enemy, string role)
    {
      var monitor = enemy.GetComponent<MonsterAiSoakMonitor>();
      if (monitor == null)
        monitor = enemy.AddComponent<MonsterAiSoakMonitor>();
      monitor.Initialize(enemy, role);
      return monitor;
    }

    void Initialize(GameObject enemy, string role)
    {
      _core = enemy.GetComponent<EnemyCore>();
      _enemyId = _core != null ? _core.EnemyId : enemy.name;
      _role = role;
      _startTime = Time.time;
      _lastMoveTime = _startTime;
      _lastPos = GameplayPlane.Position2D(enemy.transform);
      _wasSprinting = _core != null && _core.IsSprinting;
      CombatEventBus.PostDamage += OnPostDamage;
      _subscribed = true;
    }

    void OnDestroy()
    {
      if (_subscribed)
        CombatEventBus.PostDamage -= OnPostDamage;
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (_finished || _core == null || args.Attacker != _core.gameObject)
        return;
      _attackCount++;
    }

    void Update()
    {
      if (_finished || _core == null)
        return;

      var pos = GameplayPlane.Position2D(transform);
      var delta = Vector2.Distance(pos, _lastPos);
      if (delta > 0.05f)
      {
        _totalMoveDistance += delta;
        _lastMoveTime = Time.time;
        _lastPos = pos;
      }
      else
      {
        var idle = Time.time - _lastMoveTime;
        if (idle > _longestIdle)
          _longestIdle = idle;
        if (idle >= RuntimeValidationSettings.AiSoakStallThresholdSeconds)
          _boundaryStuckCount++;
      }

      if (_core.IsSprinting && !_wasSprinting)
        _sprintCount++;
      if (!_core.IsSprinting && _wasSprinting)
        _sprintRecoveries++;
      _wasSprinting = _core.IsSprinting;

      if (CircleArenaController.IsActive)
      {
        var center = CircleArenaController.Center;
        var dist = Vector2.Distance(pos, center);
        if (dist > CircleArenaController.EffectiveRadius * 0.98f && delta < 0.01f)
          _boundaryStuckCount++;
      }
    }

    public bool IsComplete(float durationSeconds) => Time.time - _startTime >= durationSeconds;

    public SoakResult BuildResult(bool playerDead)
    {
      _finished = true;
      var duration = Time.time - _startTime;
      var avgAttackInterval = _attackCount > 1 ? duration / _attackCount : 0f;
      return new SoakResult(
        _enemyId,
        _role,
        duration,
        _totalMoveDistance,
        _longestIdle,
        _attackCount,
        avgAttackInterval,
        _stateChanges,
        _sprintCount,
        _sprintRecoveries,
        _boundaryStuckCount,
        _buffApplyCount,
        _buffRemoveCount,
        _childSpawnCount,
        _postDeathBehaviorCount,
        playerDead,
        PassesAcceptance(_role, _attackCount, _totalMoveDistance, _longestIdle, _boundaryStuckCount));
    }

    static bool PassesAcceptance(string role, int attacks, float moveDistance, float longestIdle, int boundaryStuck)
    {
      if (boundaryStuck > 3)
        return false;

      switch (role)
      {
        case "chaser":
          return moveDistance > 5f && longestIdle < RuntimeValidationSettings.AiSoakStallThresholdSeconds;
        case "runner":
        case "runner_wisp":
          return moveDistance > 8f;
        case "shooter":
          return attacks >= 2;
        case "tank":
        case "splitter":
        case "supporter":
        case "bomber":
        case "disruptor":
          return longestIdle < RuntimeValidationSettings.AiSoakStallThresholdSeconds * 1.5f;
        default:
          return attacks >= 1 || moveDistance > 3f;
      }
    }

    public string ToCsvRow()
    {
      var result = BuildResult(false);
      var sb = new StringBuilder();
      sb.Append(result.EnemyId).Append(',')
        .Append(result.Role).Append(',')
        .Append(result.DurationSec.ToString("F1")).Append(',')
        .Append(result.MoveDistance.ToString("F2")).Append(',')
        .Append(result.LongestIdleSec.ToString("F2")).Append(',')
        .Append(result.AttackCount).Append(',')
        .Append(result.AvgAttackInterval.ToString("F2")).Append(',')
        .Append(result.StateChanges).Append(',')
        .Append(result.SprintCount).Append(',')
        .Append(result.SprintRecoveries).Append(',')
        .Append(result.BoundaryStuckCount).Append(',')
        .Append(result.Pass ? "PASS" : "FAIL").Append('\n');
      return sb.ToString();
    }

    public readonly struct SoakResult
    {
      public readonly string EnemyId;
      public readonly string Role;
      public readonly float DurationSec;
      public readonly float MoveDistance;
      public readonly float LongestIdleSec;
      public readonly int AttackCount;
      public readonly float AvgAttackInterval;
      public readonly int StateChanges;
      public readonly int SprintCount;
      public readonly int SprintRecoveries;
      public readonly int BoundaryStuckCount;
      public readonly int BuffApplyCount;
      public readonly int BuffRemoveCount;
      public readonly int ChildSpawnCount;
      public readonly int PostDeathBehaviorCount;
      public readonly bool PlayerDead;
      public readonly bool Pass;

      public SoakResult(
        string enemyId,
        string role,
        float durationSec,
        float moveDistance,
        float longestIdleSec,
        int attackCount,
        float avgAttackInterval,
        int stateChanges,
        int sprintCount,
        int sprintRecoveries,
        int boundaryStuckCount,
        int buffApplyCount,
        int buffRemoveCount,
        int childSpawnCount,
        int postDeathBehaviorCount,
        bool playerDead,
        bool pass)
      {
        EnemyId = enemyId;
        Role = role;
        DurationSec = durationSec;
        MoveDistance = moveDistance;
        LongestIdleSec = longestIdleSec;
        AttackCount = attackCount;
        AvgAttackInterval = avgAttackInterval;
        StateChanges = stateChanges;
        SprintCount = sprintCount;
        SprintRecoveries = sprintRecoveries;
        BoundaryStuckCount = boundaryStuckCount;
        BuffApplyCount = buffApplyCount;
        BuffRemoveCount = buffRemoveCount;
        ChildSpawnCount = childSpawnCount;
        PostDeathBehaviorCount = postDeathBehaviorCount;
        PlayerDead = playerDead;
        Pass = pass;
      }
    }
  }
}
