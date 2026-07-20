using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 6 — 快速位移至玩家周围中远距离随机位置。
  /// Priority: 3, Cooldown: 5s
  /// </summary>
  public class PrismNexusSkill_Reposition : BossSkillBase
  {
    const float MOVE_SPEED   = 10f;
    const float ARRIVE_DIST  = 0.5f;
    const float MAX_DURATION = 1.8f;

    Vector2 _targetPos;
    float   _elapsed;

    public PrismNexusSkill_Reposition()
    {
      Id = FinalBossPrismNexus.SK_REPOS; Priority = 3; Cooldown = 5f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked;
    }

    public override void OnEnter(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      _targetPos = nexus != null ? nexus.RandomMidFarPos(5f, 9f) : Vector2.zero;
      _elapsed   = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var nexus = boss as FinalBossPrismNexus;
      if (nexus == null) return State.Completed;

      _elapsed += dt;
      var toTarget = _targetPos - nexus.Position;
      float dist = toTarget.magnitude;

      if (dist <= ARRIVE_DIST || _elapsed >= MAX_DURATION)
        return State.Completed;

      nexus.MoveInDir(toTarget.normalized, MOVE_SPEED, dt);
      return State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
