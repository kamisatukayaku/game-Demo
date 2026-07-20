using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 5 — 部件弱追踪弹。
  /// 每个子部件发射一枚失锁追踪弹（角度偏差过大 → 脱锁变直线）。
  /// P1: 每发弹速随机波动。
  /// 三阶段无效。
  /// Priority: 5, Cooldown: 7s
  /// </summary>
  public class PrismNexusSkill_HomingBullet : BossSkillBase
  {
    const float BASE_SPEED        = 5f;
    const float TURN_RATE         = 60f;
    const float LOCK_LOSS_ANGLE   = 45f;
    const float DMG_MULT          = 0.5f;
    const float SPEED_VARIANCE    = 3f;  // P1 弹速波动范围
    const float CAST_TIME         = 0.2f;

    float _elapsed;
    bool  _fired;
    bool  _isP1;

    public PrismNexusSkill_HomingBullet()
    {
      Id = FinalBossPrismNexus.SK_HOMING; Priority = 5; Cooldown = 7f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.HasParts && nexus.GamePhase < 2;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed = 0f; _fired = false;
      var nexus = boss as FinalBossPrismNexus;
      _isP1 = nexus != null && nexus.GamePhase >= 1;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;
      if (!_fired && _elapsed >= CAST_TIME)
      {
        _fired = true;
        var nexus = boss as FinalBossPrismNexus;
        if (nexus != null) Fire(nexus);
      }
      return _elapsed >= CAST_TIME + 0.4f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void Fire(FinalBossPrismNexus nexus)
    {
      foreach (var part in nexus.Parts)
      {
        if (part == null || part.IsDestroyed) continue;
        float speed = _isP1
          ? BASE_SPEED + Random.Range(-SPEED_VARIANCE, SPEED_VARIANCE)
          : BASE_SPEED;
        if (speed < 1.5f) speed = 1.5f;

        nexus.SpawnLockLossHoming(part.transform.position, DMG_MULT, speed,
          TURN_RATE, LOCK_LOSS_ANGLE);
      }
    }
  }
}
