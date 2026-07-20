using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 3 — 护盾子部件。召唤一个与自身位置完全同步的子部件作为护盾。
  /// 存在时自身免疫伤害（受伤后治疗等量数值）。
  /// 护盾破碎后自身流失 10% 最大生命，且一段时间内无法再次释放。
  /// 存在护盾或护盾破碎冷却中时 CanTrigger 返回 false。
  /// Priority: 0（最高优先）, Cooldown: 12s
  /// </summary>
  public class HexKingSkill_Shield : BossSkillBase
  {
    const float CAST_DURATION       = 0.3f;
    const float HP_RATIO            = 0.18f; // 护盾血量 = Boss 最大生命 × 18%

    float _elapsed;
    bool  _executed;

    public HexKingSkill_Shield()
    {
      Id       = WildBossHexKing.SKILL_SHIELD;
      Priority = 0;
      Cooldown = 16f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var king = boss as WildBossHexKing;
      if (king == null) return false;
      // 已有护盾或护盾破碎冷却中 → 不可释放
      return !king.HasShield && !king.ShieldOnCooldown;
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
        var king = boss as WildBossHexKing;
        king?.SummonShield(HP_RATIO);
      }

      return _elapsed >= CAST_DURATION + 0.3f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
