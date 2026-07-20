using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 2 — 近身高伤近战。仅距离较近时可触发。
  /// Priority: 1, Cooldown: 3s
  /// </summary>
  public class PrismNexusSkill_Melee : BossSkillBase
  {
    const float TRIGGER_DIST   = 3f;
    const float APPROACH_SPEED = 3.5f;
    const float MELEE_RANGE    = 1.8f;
    const float DMG_MULT       = 2.0f;
    const float WINDUP_TIME    = 0.3f;
    const float RECOVERY       = 0.25f;

    enum Phase { Approaching, Windup, Strike, Recovery }
    Phase _phase;
    float _phaseTimer;

    public PrismNexusSkill_Melee()
    {
      Id = FinalBossPrismNexus.SK_MELEE; Priority = 1; Cooldown = 3f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.DistToPlayer() <= TRIGGER_DIST;
    }

    public override void OnEnter(BossCore boss)
    {
      _phase      = Phase.Approaching;
      _phaseTimer = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var nexus = boss as FinalBossPrismNexus;
      if (nexus == null) return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Approaching:
          nexus.MoveInDir(nexus.DirToPlayer(), APPROACH_SPEED, dt);
          if (nexus.DistToPlayer() <= MELEE_RANGE)
          { _phase = Phase.Windup; _phaseTimer = 0f; }
          break;

        case Phase.Windup:
          if (_phaseTimer >= WINDUP_TIME)
          { nexus.MeleeHit(DMG_MULT); _phase = Phase.Recovery; _phaseTimer = 0f; }
          break;

        case Phase.Recovery:
          if (_phaseTimer >= RECOVERY) return State.Completed;
          break;
      }
      return State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
