using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 9 — 随机射程子弹 + 到期环形弹幕（仅三阶段）。
  /// 向周围多个随机方向各发射一枚射程随机的子弹。
  /// 子弹到达射程时（预计算时间），在该位置发射一轮环形弹幕。
  /// Priority: 4, Cooldown: 4s
  /// </summary>
  public class PrismNexusSkill_RandomBombs : BossSkillBase
  {
    const int   BULLET_COUNT      = 8;
    const float BULLET_SPEED      = 6f;
    const float RANGE_MIN         = 5f;
    const float RANGE_MAX         = 14f;
    const float DMG_MULT_BULLET   = 0.3f;
    const float DMG_MULT_RING     = 0.4f;
    const int   RING_BULLETS      = 12;
    const float RING_SPEED        = 4f;
    const float RING_RANGE        = 5.5f;

    /// <summary>到期弹幕事件：发射点 + 方向 + 剩余时间 + 总飞行时间</summary>
    struct BombEvent
    {
      public Vector2 Origin;
      public Vector2 Direction;
      public float   Remaining;   // 距到期剩余秒数
      public float   TravelTime;  // 总飞行时间（不变）
      public bool    Triggered;
    }

    List<BombEvent> _bombs;
    float _elapsed;
    bool  _launched;

    public PrismNexusSkill_RandomBombs()
    {
      Id = FinalBossPrismNexus.SK_BOMBS; Priority = 4; Cooldown = 4f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.GamePhase >= 2;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed  = 0f;
      _launched = false;
      _bombs    = new List<BombEvent>();
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var nexus = boss as FinalBossPrismNexus;
      if (nexus == null) return State.Completed;

      _elapsed += dt;

      // 发射子弹（仅一次）
      if (!_launched && _elapsed >= 0.15f)
      {
        _launched = true;
        LaunchBombs(nexus);
      }

      // 处理到期弹幕
      bool anyActive = false;
      for (int i = 0; i < _bombs.Count; i++)
      {
        var b = _bombs[i];
        if (b.Triggered) continue;

        b.Remaining -= dt;
        _bombs[i] = b;

        if (b.Remaining <= 0f)
        {
          b.Triggered = true;
          _bombs[i] = b;
          // 到期位置 = 发射点 + 方向 × 弹速 × 总飞行时间
          Vector2 detonatePos = b.Origin + b.Direction * (BULLET_SPEED * b.TravelTime);
          FireRingBarrage(nexus, detonatePos);
        }
        else
        {
          anyActive = true;
        }
      }

      // 所有弹幕已到期 + 环形弹幕有足够时间消散
      if (!anyActive && _elapsed >= 2.5f)
        return State.Completed;

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void LaunchBombs(FinalBossPrismNexus nexus)
    {
      for (int i = 0; i < BULLET_COUNT; i++)
      {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float range = Random.Range(RANGE_MIN, RANGE_MAX);
        float travelTime = range / BULLET_SPEED;

        // 发射子弹
        nexus.SpawnProj(dir, DMG_MULT_BULLET, BULLET_SPEED, range);

        // 记录到期事件
        _bombs.Add(new BombEvent
        {
          Origin     = nexus.Position,
          Direction  = dir,
          Remaining  = travelTime,
          TravelTime = travelTime,
          Triggered  = false
        });
      }
    }

    void FireRingBarrage(FinalBossPrismNexus nexus, Vector2 center)
    {
      for (int i = 0; i < RING_BULLETS; i++)
      {
        float angle = (360f / RING_BULLETS) * i;
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        nexus.SpawnProjFrom(center, dir, DMG_MULT_RING, RING_SPEED, RING_RANGE);
      }
    }
  }
}
