using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 1 — 二连冲撞。蓄力后可自由改变方向，随后发起两次冲撞。
  /// Priority: 2, Cooldown: 6s
  /// </summary>
  public class HexKingSkill_DoubleDash : BossSkillBase
  {
    const float WINDUP_DURATION   = 1.0f;
    const float WINDUP_SHORT      = 1.0f;
    const float TURN_RATE_DEG    = 80f;
    const float DASH_SPEED        = 20f;
    const float DASH_HIT_RADIUS   = 1.5f;
    const float DASH_DAMAGE_MULT  = 0.75f;
    const float PAUSE_BETWEEN     = 0.35f;
    const float DASH_DIST_MULT    = 1.75f;
    const float DASH_DUR_MIN      = 0.5f;
    const float DASH_DUR_MAX      = 2f;
    const float RECOVERY_DURATION = 1.0f;

    enum Phase { Windup1, Dashing1, Pause, Windup2, Dashing2, Recovery }
    Phase  _phase;
    float  _phaseTimer;
    float  _dashDuration;
    Vector2 _dashDir;
    Vector2 _dashStart;
    bool   _dash1HitApplied;
    bool   _dash2HitApplied;

    public HexKingSkill_DoubleDash()
    {
      Id       = WildBossHexKing.SKILL_DOUBLE_DASH;
      Priority = 2;
      Cooldown = 7f;
    }

    public override bool CanTrigger(BossCore boss) => boss is WildBossHexKing;

    public override void OnEnter(BossCore boss)
    {
      _phase            = Phase.Windup1;
      _phaseTimer       = 0f;
      _dashDuration     = 0f;
      _dash1HitApplied  = false;
      _dash2HitApplied  = false;
      StartChargeSpin(boss);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var king = boss as WildBossHexKing;
      if (king == null) return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Windup1:
          _dashDir = king.DirToPlayer();
          if (_phaseTimer >= WINDUP_DURATION)
          {
            _phase            = Phase.Dashing1;
            _phaseTimer         = 0f;
            _dashStart          = king.Position;
            _dash1HitApplied    = false;
            _dashDuration       = Mathf.Clamp(king.DistToPlayer() * DASH_DIST_MULT / DASH_SPEED, DASH_DUR_MIN, DASH_DUR_MAX);
            StartChargeSpin(boss);
          }
          break;

        case Phase.Dashing1:
          king.MoveInDir(_dashDir, DASH_SPEED, dt);
          TryDashHit(king, ref _dash1HitApplied);
          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _phase      = Phase.Pause;
            _phaseTimer = 0f;
          }
          break;

        case Phase.Pause:
          if (_phaseTimer >= PAUSE_BETWEEN)
          {
            _phase      = Phase.Windup2;
            _phaseTimer = 0f;
            StartChargeSpin(boss);
          }
          break;

        case Phase.Windup2:
          _dashDir = king.DirToPlayer();
          if (_phaseTimer >= WINDUP_SHORT)
          {
            _phase            = Phase.Dashing2;
            _phaseTimer         = 0f;
            _dashStart          = king.Position;
            _dash2HitApplied    = false;
            _dashDuration       = Mathf.Clamp(king.DistToPlayer() * DASH_DIST_MULT / DASH_SPEED, DASH_DUR_MIN, DASH_DUR_MAX);
            StartChargeSpin(boss);
          }
          break;

        case Phase.Dashing2:
          king.MoveInDir(_dashDir, DASH_SPEED, dt);
          TryDashHit(king, ref _dash2HitApplied);
          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _phase      = Phase.Recovery;
            _phaseTimer = 0f;
          }
          break;

        case Phase.Recovery:
          if (_phaseTimer >= RECOVERY_DURATION) return State.Completed;
          break;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { StopChargeSpin(boss); }

    static void TryDashHit(WildBossHexKing king, ref bool hitApplied)
    {
      if (hitApplied)
        return;

      var playerPos = king.GetPlayerPos();
      float dist = Vector2.Distance(king.Position, playerPos);
      if (dist <= DASH_HIT_RADIUS)
      {
        king.MeleeHit(DASH_DAMAGE_MULT, "physical");
        hitApplied = true;
      }
    }
  }
}
