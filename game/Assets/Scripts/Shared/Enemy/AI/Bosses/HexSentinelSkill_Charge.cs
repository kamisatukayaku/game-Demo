using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 单次冲撞。Priority: 1, Cooldown: 5s
  /// </summary>
  public class HexSentinelSkill_Charge : BossSkillBase
  {
    const float WINDUP_DURATION = 1.0f;
    const float DASH_SPEED = 18f;
    const float DASH_HIT_RADIUS = 1.4f;
    const float DASH_DAMAGE_MULT = 0.85f;
    const float DASH_DIST_MULT = 1.6f;
    const float DASH_DUR_MIN = 0.45f;
    const float DASH_DUR_MAX = 1.6f;
    enum Phase { Windup, Dashing, Recovery }
    Phase _phase;
    float _phaseTimer;
    float _dashDuration;
    Vector2 _dashDir;
    bool _dashHitApplied;

    public HexSentinelSkill_Charge()
    {
      Id = MiniBossHexSentinel.SKILL_CHARGE;
      Priority = 1;
      Cooldown = 5f;
      Category = BossSkillCategory.Mobility;
    }

    public override bool CanTrigger(BossCore boss) => boss is MiniBossHexSentinel;

    public override void OnEnter(BossCore boss)
    {
      _phase = Phase.Windup;
      _phaseTimer = 0f;
      _dashHitApplied = false;
      StartChargeSpin(boss);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var sentinel = boss as MiniBossHexSentinel;
      if (sentinel == null) return State.Completed;

      _phaseTimer += dt;
      switch (_phase)
      {
        case Phase.Windup:
          _dashDir = sentinel.DirToPlayer();
          if (_phaseTimer >= WINDUP_DURATION)
          {
            _phase = Phase.Dashing;
            _phaseTimer = 0f;
            _dashHitApplied = false;
            _dashDuration = Mathf.Clamp(
              sentinel.DistToPlayer() * DASH_DIST_MULT / DASH_SPEED,
              DASH_DUR_MIN,
              DASH_DUR_MAX);
            StartChargeSpin(boss);
          }
          break;

        case Phase.Dashing:
          sentinel.MoveInDir(_dashDir, DASH_SPEED, dt);
          if (!_dashHitApplied
              && Vector2.Distance(sentinel.Position, sentinel.GetPlayerPos()) <= DASH_HIT_RADIUS)
          {
            sentinel.MeleeHit(DASH_DAMAGE_MULT);
            _dashHitApplied = true;
          }
          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _phase = Phase.Recovery;
            _phaseTimer = 0f;
          }
          break;

        case Phase.Recovery:
          if (_phaseTimer >= 0.15f) return State.Completed;
          break;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) => StopChargeSpin(boss);
  }
}
