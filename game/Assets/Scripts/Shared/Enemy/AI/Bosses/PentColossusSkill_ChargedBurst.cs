using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 5 — 蓄力减伤 → 多轮环形高伤弹幕。
  /// 蓄力期间具有减伤，受到足够伤害将打断蓄力；
  /// 蓄力完成后发射多轮环形弹幕（每轮相位略有偏移），射程很长。
  /// Priority: 5, Cooldown: 18s
  /// </summary>
  public class PentColossusSkill_ChargedBurst : BossSkillBase
  {
    const float CHARGE_DURATION      = 1.5f;
    const float DAMAGE_THRESHOLD     = 30f;   // 累积受到此伤害后打断蓄力
    const float DAMAGE_REDUCTION     = 0.6f;  // 减伤 60%（只受 40% 伤害）
    const int   BURST_ROUNDS         = 4;     // 发射轮数
    const float ROUND_INTERVAL       = 0.2f;  // 每轮间隔
    const int   BULLETS_PER_ROUND    = 16;    // 每轮子弹数
    const float BULLET_RANGE         = 22f;   // 长射程
    const float BULLET_SPEED         = 5f;
    const float BULLET_DAMAGE_MULT   = 0.7f;
    const float PHASE_OFFSET_PER_ROUND = 11.25f; // 每轮相位偏移 (360/16/2)

    enum Phase { Charging, Bursting }
    Phase  _phase;
    float  _phaseTimer;
    float  _damageAccumulated;
    int    _burstRound;
    float  _burstTimer;

    // 用于减伤回调
    System.Action<float> _damageHandler;

    public PentColossusSkill_ChargedBurst()
    {
      Id       = WildBossPentColossus.SKILL_BURST;
      Priority = 5;
      Cooldown = 18f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      return colossus != null;
    }

    public override void OnEnter(BossCore boss)
    {
      _phase             = Phase.Charging;
      _phaseTimer        = 0f;
      _damageAccumulated = 0f;
      _burstRound        = 0;
      _burstTimer        = 0f;

      var colossus = boss as WildBossPentColossus;

      // 订阅伤害事件用于减伤 + 打断检测
      var health = boss.Core?.Health;
      if (health != null)
      {
        _damageHandler = amount =>
        {
          // 减伤：回血抵消大部分伤害
          float reduced = amount * DAMAGE_REDUCTION;
          if (reduced > 0f)
            health.Heal(reduced); // 回血 = 减伤量

          _damageAccumulated += amount;
        };
        health.Damaged += _damageHandler;
      }
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var colossus = boss as WildBossPentColossus;
      if (colossus == null) return State.Completed;

      _phaseTimer += dt;

      if (_phase == Phase.Charging)
      {
        // 检查打断条件
        if (_damageAccumulated >= DAMAGE_THRESHOLD)
          return State.Completed;

        if (_phaseTimer >= CHARGE_DURATION)
        {
          _phase      = Phase.Bursting;
          _burstRound = 0;
          _burstTimer = ROUND_INTERVAL; // 立即发射第一轮
        }
      }
      else if (_phase == Phase.Bursting)
      {
        _burstTimer += dt;
        while (_burstTimer >= ROUND_INTERVAL && _burstRound < BURST_ROUNDS)
        {
          _burstTimer -= ROUND_INTERVAL;
          FireBurstRound(colossus, _burstRound);
          _burstRound++;
        }

        if (_burstRound >= BURST_ROUNDS)
          return State.Completed;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss)
    {
      // 取消减伤回调
      var health = boss.Core?.Health;
      if (health != null && _damageHandler != null)
        health.Damaged -= _damageHandler;
    }

    void FireBurstRound(WildBossPentColossus colossus, int roundIndex)
    {
      var pos = colossus.AttackOrigin;
      float phaseOffset = roundIndex * PHASE_OFFSET_PER_ROUND;

      for (int i = 0; i < BULLETS_PER_ROUND; i++)
      {
        float angle = (360f / BULLETS_PER_ROUND) * i + phaseOffset;
        float rad = angle * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        colossus.SpawnProjectileFrom(pos, dir,
          damageMult: BULLET_DAMAGE_MULT,
          speedOverride: BULLET_SPEED,
          rangeOverride: BULLET_RANGE);
      }
    }
  }
}
