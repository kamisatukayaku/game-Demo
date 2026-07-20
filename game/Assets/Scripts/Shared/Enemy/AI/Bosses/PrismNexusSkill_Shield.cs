using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 8 — 护盾子部件（仅三阶段）。
  /// 护盾拥有 5% Boss 最大血量。被击破时中断当前技能并短时间封锁所有技能。
  /// Priority: 0, Cooldown: 8s（较短冷却）
  /// </summary>
  public class PrismNexusSkill_Shield : BossSkillBase
  {
    const float HP_RATIO      = 0.05f;
    const float CAST_DURATION = 0.25f;

    float _elapsed;
    bool  _executed;

    public PrismNexusSkill_Shield()
    {
      Id = FinalBossPrismNexus.SK_SHIELD; Priority = 0; Cooldown = 8f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.GamePhase >= 2 && !nexus.HasShield;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed  = 0f;
      _executed = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;
      if (!_executed && _elapsed >= CAST_DURATION)
      {
        _executed = true;
        var nexus = boss as FinalBossPrismNexus;
        nexus?.SummonShield(HP_RATIO);
      }
      return _elapsed >= CAST_DURATION + 0.2f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
