using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 2 — 单次冲撞 + 路径弹幕。
  /// 蓄力期间仅能以低角速度改变方向，冲撞路径上持续向两侧发射短射程弹幕。
  /// Priority: 3, Cooldown: 5s
  /// </summary>
  public class HexKingSkill_TrailDash : BossSkillBase
  {
    const float WINDUP_DURATION     = 1.0f;
    const float TURN_RATE_DEG       = 60f;
    const float DASH_SPEED          = 17f;
    const float DASH_HIT_RADIUS     = 1.5f;
    const float DASH_DAMAGE_MULT    = 0.7f;
    const float DASH_DIST_MULT      = 1.75f;
    const float DASH_DUR_MIN        = 0.5f;
    const float DASH_DUR_MAX        = 2f;
    const float TRAIL_INTERVAL      = 0.06f;
    const int   TRAIL_PER_SIDE      = 3;
    const float TRAIL_SPREAD_DEG    = 40f;
    const float TRAIL_RANGE         = 9f;
    const float TRAIL_SPEED         = 3f;

    enum Phase { Windup, Dashing, Recovery }
    Phase  _phase;
    float  _phaseTimer;
    float  _dashDuration;
    Vector2 _dashDir;
    float  _trailTimer;
    Vector2 _prevPos;
    bool   _dashHitApplied;

    public HexKingSkill_TrailDash()
    {
      Id       = WildBossHexKing.SKILL_TRAIL_DASH;
      Priority = 3;
      Cooldown = 5f;
    }

    public override bool CanTrigger(BossCore boss) => boss is WildBossHexKing;

    public override void OnEnter(BossCore boss)
    {
      _phase        = Phase.Windup;
      _phaseTimer   = 0f;
      _dashDuration = 0f;
      _trailTimer   = 0f;
      _dashHitApplied   = false;
      var king      = boss as WildBossHexKing;
      _dashDir      = king != null ? king.DirToPlayer() : Vector2.right;
      StartChargeSpin(boss);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var king = boss as WildBossHexKing;
      if (king == null) return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Windup:
        {
          var targetDir = king.DirToPlayer();
          float cur = Mathf.Atan2(_dashDir.y, _dashDir.x) * Mathf.Rad2Deg;
          float tgt = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
          float newA = Mathf.MoveTowardsAngle(cur, tgt, TURN_RATE_DEG * dt);
          float rad  = newA * Mathf.Deg2Rad;
          _dashDir   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

          if (_phaseTimer >= WINDUP_DURATION)
          {
            _phase            = Phase.Dashing;
            _phaseTimer       = 0f;
            _trailTimer       = 0f;
            _prevPos          = king.Position;
            _dashHitApplied   = false;
            _dashDuration = Mathf.Clamp(king.DistToPlayer() * DASH_DIST_MULT / DASH_SPEED, DASH_DUR_MIN, DASH_DUR_MAX);
            StartChargeSpin(boss);
          }
          break;
        }

        case Phase.Dashing:
        {
          king.MoveInDir(_dashDir, DASH_SPEED, dt);
          TryDashHit(king);

          _trailTimer += dt;
          float traveled = Vector2.Distance(king.Position, _prevPos);
          if (traveled > 0.01f)
          {
            var moveDir = (king.Position - _prevPos).normalized;
            while (_trailTimer >= TRAIL_INTERVAL)
            {
              _trailTimer -= TRAIL_INTERVAL;
              FireTrailProjectiles(king, king.Position, moveDir);
            }
          }
          _prevPos = king.Position;

          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _phase      = Phase.Recovery;
            _phaseTimer = 0f;
          }
          break;
        }

        case Phase.Recovery:
          if (_phaseTimer >= 0.85f) return State.Completed;
          break;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { StopChargeSpin(boss); }

    void FireTrailProjectiles(WildBossHexKing king, Vector2 pos, Vector2 moveDir)
    {
      float moveAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;

      // 左侧弹幕：垂直于移动方向偏左
      float leftAngle = moveAngle + 90f;
      for (int i = 0; i < TRAIL_PER_SIDE; i++)
      {
        float t = TRAIL_PER_SIDE <= 1 ? 0f : (i / (float)(TRAIL_PER_SIDE - 1) - 0.5f);
        float rad = (leftAngle + t * TRAIL_SPREAD_DEG) * Mathf.Deg2Rad;
        king.SpawnProjFrom(pos, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)),
          dmgMult: 0.3f, spd: TRAIL_SPEED, range: TRAIL_RANGE);
      }

      // 右侧弹幕：垂直于移动方向偏右
      float rightAngle = moveAngle - 90f;
      for (int i = 0; i < TRAIL_PER_SIDE; i++)
      {
        float t = TRAIL_PER_SIDE <= 1 ? 0f : (i / (float)(TRAIL_PER_SIDE - 1) - 0.5f);
        float rad = (rightAngle + t * TRAIL_SPREAD_DEG) * Mathf.Deg2Rad;
        king.SpawnProjFrom(pos, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)),
          dmgMult: 0.3f, spd: TRAIL_SPEED, range: TRAIL_RANGE);
      }
    }

    void TryDashHit(WildBossHexKing king)
    {
      if (_dashHitApplied)
        return;

      float dist = Vector2.Distance(king.Position, king.GetPlayerPos());
      if (dist <= DASH_HIT_RADIUS)
      {
        king.MeleeHit(DASH_DAMAGE_MULT);
        _dashHitApplied = true;
      }
    }
  }
}
