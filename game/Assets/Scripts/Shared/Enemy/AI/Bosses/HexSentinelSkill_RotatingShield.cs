using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 旋转护盾 — 召唤 3 个环绕部件。Priority: 0, Cooldown: 10s
  /// </summary>
  public class HexSentinelSkill_RotatingShield : BossSkillBase
  {
    const float CAST_DURATION = 0.35f;
    const int SHIELD_COUNT = 3;
    const float ORBIT_RADIUS = 2.4f;
    const float ORBIT_SPEED = 95f;

    float _elapsed;
    bool _executed;

    public HexSentinelSkill_RotatingShield()
    {
      Id = MiniBossHexSentinel.SKILL_ROTATING_SHIELD;
      Priority = 0;
      Cooldown = 10f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var sentinel = boss as MiniBossHexSentinel;
      return sentinel != null && sentinel.LivingShieldCount == 0;
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
        (boss as MiniBossHexSentinel)?.SpawnRotatingShields(SHIELD_COUNT, ORBIT_RADIUS, ORBIT_SPEED);
      }
      return _elapsed >= CAST_DURATION + 0.2f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
