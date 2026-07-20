using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 短距冲撞 — 配合牢笼压缩空间。Priority: 1, Cooldown: 6s
  /// </summary>
  public class SquareJailerSkill_Charge : BossSkillBase
  {
    const float WINDUP_DURATION = 1.0f;
    const float DASH_SPEED = 16f;
    const float DASH_HIT_RADIUS = 1.35f;
    const float DASH_DAMAGE_MULT = 0.9f;
    const float DASH_DIST_MULT = 1.4f;
    const float DASH_DUR_MIN = 0.4f;
    const float DASH_DUR_MAX = 1.4f;

    enum Phase { Windup, Dashing, Recovery }
    Phase _phase;
    float _phaseTimer;
    float _dashDuration;
    Vector2 _dashDir;
    bool _dashHitApplied;

    public SquareJailerSkill_Charge()
    {
      Id = "jailer_charge";
      Priority = 1;
      Cooldown = 6f;
      Category = BossSkillCategory.Mobility;
    }

    public override bool CanTrigger(BossCore boss) => boss is MiniBossSquareJailer;

    public override void OnEnter(BossCore boss)
    {
      _phase = Phase.Windup;
      _phaseTimer = 0f;
      _dashHitApplied = false;
      StartChargeSpin(boss);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var jailer = boss as MiniBossSquareJailer;
      if (jailer == null) return State.Completed;

      _phaseTimer += dt;
      switch (_phase)
      {
        case Phase.Windup:
          _dashDir = jailer.DirToPlayer();
          if (_phaseTimer >= WINDUP_DURATION)
          {
            _phase = Phase.Dashing;
            _phaseTimer = 0f;
            _dashHitApplied = false;
            _dashDuration = Mathf.Clamp(
              Vector2.Distance(jailer.Position, jailer.GetPlayerPos()) * DASH_DIST_MULT / DASH_SPEED,
              DASH_DUR_MIN,
              DASH_DUR_MAX);
            StartChargeSpin(boss);
          }
          break;

        case Phase.Dashing:
          jailer.MoveInDir(_dashDir, DASH_SPEED, dt);
          if (!_dashHitApplied
              && Vector2.Distance(jailer.Position, jailer.GetPlayerPos()) <= DASH_HIT_RADIUS)
          {
            jailer.MeleeHit(DASH_DAMAGE_MULT);
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
