using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 四角柱牢笼 — 在玩家周围四角召唤柱体。Priority: 0, Cooldown: 12s
  /// </summary>
  public class SquareJailerSkill_CornerPillars : BossSkillBase
  {
    const float CAST_DURATION = 0.4f;
    const float PILLAR_RADIUS = 3.2f;

    float _elapsed;
    bool _executed;

    public SquareJailerSkill_CornerPillars()
    {
      Id = MiniBossSquareJailer.SKILL_CORNER_PILLARS;
      Priority = 0;
      Cooldown = 12f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var jailer = boss as MiniBossSquareJailer;
      return jailer != null && jailer.LivingPillarCount == 0;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed = 0f;
      _executed = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;
      if (!_executed && _elapsed >= CAST_DURATION)
      {
        _executed = true;
        (boss as MiniBossSquareJailer)?.SummonCornerPillars(PILLAR_RADIUS);
      }
      return _elapsed >= CAST_DURATION + 0.25f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
