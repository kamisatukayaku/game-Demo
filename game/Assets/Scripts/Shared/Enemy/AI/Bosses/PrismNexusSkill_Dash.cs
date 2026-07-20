using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 1 — 阶段相关连冲 + 传送。
  /// P0: 二连冲撞，蓄力低角速度转向
  /// P1: 三连冲撞，蓄力低角速度转向
  /// P2: 三连冲撞（间隔缩短）+ 第三冲结束后瞬移至玩家中距离 + 额外冲撞
  /// Priority: 2, Cooldown: 6s
  /// </summary>
  public class PrismNexusSkill_Dash : BossSkillBase
  {
    const float WINDUP_FIRST    = 1.0f;
    const float WINDUP_SHORT    = 1.0f;
    const float TURN_RATE_DEG   = 80f;
    const float DASH_SPEED      = 24f;
    const float HIT_RADIUS      = 1.6f;
    const float DMG_MULT        = 0.75f;
    const float DASH_DIST_MULT  = 1.75f;
    const float DASH_DUR_MIN    = 0.5f;
    const float DASH_DUR_MAX    = 2f;
    const float TELEPORT_DIST   = 5f;

    enum Phase { Windup, Dashing, TeleportDash, Recovery }
    Phase  _phase;
    int    _dashIndex;
    float  _phaseTimer;
    float  _dashDuration;
    float  _dashInterval;
    Vector2 _dashDir;
    bool   _dashHitApplied;

    public PrismNexusSkill_Dash()
    {
      Id = FinalBossPrismNexus.SK_DASH; Priority = 2; Cooldown = 6f;
      Category = BossSkillCategory.Mobility;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked;
    }

    public override void OnEnter(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      _phase        = Phase.Windup;
      _dashIndex    = 0;
      _phaseTimer   = 0f;
      _dashDuration = 0f;
      _dashInterval = nexus != null ? nexus.DashInterval : 0.25f;
      _dashDir      = boss is FinalBossPrismNexus n ? n.DirToPlayer() : Vector2.right;
      _dashHitApplied = false;
      StartChargeSpin(boss);
    }

    float CalcDashDuration(FinalBossPrismNexus nexus) =>
      Mathf.Clamp(nexus.DistToPlayer() * DASH_DIST_MULT / DASH_SPEED, DASH_DUR_MIN, DASH_DUR_MAX);

    float CurrentWindupDuration => _dashIndex == 0 ? WINDUP_FIRST : WINDUP_SHORT;

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var nexus = boss as FinalBossPrismNexus;
      if (nexus == null) return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Windup:
        {
          var target = nexus.DirToPlayer();
          float cur = Mathf.Atan2(_dashDir.y, _dashDir.x) * Mathf.Rad2Deg;
          float tgt = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
          float a   = Mathf.MoveTowardsAngle(cur, tgt, TURN_RATE_DEG * dt);
          _dashDir  = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

          if (_phaseTimer >= CurrentWindupDuration)
          {
            _phase        = Phase.Dashing;
            _phaseTimer   = 0f;
            _dashHitApplied = false;
            _dashDuration = CalcDashDuration(nexus);
            StartChargeSpin(boss);
          }
          break;
        }

        case Phase.Dashing:
        {
          nexus.MoveInDir(_dashDir, DASH_SPEED, dt);
          if (!_dashHitApplied
              && Vector2.Distance(nexus.Position, nexus.GetPlayerPos()) <= HIT_RADIUS)
          {
            nexus.MeleeHit(DMG_MULT);
            _dashHitApplied = true;
          }

          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _dashIndex++;
            if (_dashIndex < nexus.DashCount)
            {
              _phase      = Phase.Windup;
              _phaseTimer = 0f;
              StartChargeSpin(boss);
            }
            else if (nexus.DashHasTeleport)
            {
              TeleportToPlayerFront(nexus);
              _phase        = Phase.TeleportDash;
              _phaseTimer   = 0f;
              _dashHitApplied = false;
              _dashDir      = nexus.DirToPlayer();
              _dashDuration = CalcDashDuration(nexus);
              StartChargeSpin(boss);
            }
            else
            {
              _phase      = Phase.Recovery;
              _phaseTimer = 0f;
            }
          }
          break;
        }

        case Phase.TeleportDash:
        {
          nexus.MoveInDir(_dashDir, DASH_SPEED, dt);
          if (!_dashHitApplied
              && Vector2.Distance(nexus.Position, nexus.GetPlayerPos()) <= HIT_RADIUS)
          {
            nexus.MeleeHit(DMG_MULT * 1.2f);
            _dashHitApplied = true;
          }

          if (_phaseTimer >= _dashDuration)
          {
            StopChargeSpin(boss);
            _phase      = Phase.Recovery;
            _phaseTimer = 0f;
          }
          break;
        }

        case Phase.Recovery:
          if (_phaseTimer >= 0.25f) return State.Completed;
          break;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { StopChargeSpin(boss); }

    void TeleportToPlayerFront(FinalBossPrismNexus nexus)
    {
      var playerPos = nexus.GetPlayerPos();
      var vel = nexus.GetPlayerVelocity();

      // 若玩家在移动，传送至移动方向前方；否则传送到玩家-Boss 连线方向
      Vector2 target;
      if (vel.sqrMagnitude > 0.5f)
        target = playerPos + vel.normalized * TELEPORT_DIST;
      else
        target = playerPos + (playerPos - nexus.Position).normalized * TELEPORT_DIST;

      nexus.transform.position = new Vector3(target.x, target.y, nexus.transform.position.z);

      var minDist = 4.2f;
      if (Vector2.Distance(nexus.Position, playerPos) < minDist)
      {
        var away = (nexus.Position - playerPos).normalized;
        if (away.sqrMagnitude < 0.01f)
          away = nexus.DirToPlayer() * -1f;
        var adjusted = playerPos + away * minDist;
        nexus.transform.position = new Vector3(adjusted.x, adjusted.y, nexus.transform.position.z);
      }
    }
  }
}
