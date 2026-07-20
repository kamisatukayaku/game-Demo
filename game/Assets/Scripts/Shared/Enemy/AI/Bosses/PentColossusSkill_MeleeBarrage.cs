using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 1 — 近战 + 子部件环形弹幕。
  /// 正常速度靠近玩家 → 近战攻击 → 若存在子部件，从部件位置各发出一组环形弹幕。
  /// Priority: 1, Cooldown: 2s
  /// </summary>
  public class PentColossusSkill_MeleeBarrage : BossSkillBase
  {
    const float WINDUP_DURATION  = 0.3f;
    const float RECOVERY_TIME    = 0.2f;
    const int   RING_BULLETS     = 8;     // 每个部件环形弹幕子弹数
    const float RING_RANGE       = 6.5f;  // 环形弹幕射程
    const float RING_SPEED       = 2.5f;  // 环形弹幕弹速

    enum Phase { Approaching, Windup, Strike, Recovery }
    Phase _phase;
    float _phaseTimer;

    public PentColossusSkill_MeleeBarrage()
    {
      Id       = WildBossPentColossus.SKILL_MELEE;
      Priority = 1;
      Cooldown = 2f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      return colossus != null;
    }

    public override void OnEnter(BossCore boss)
    {
      _phase      = Phase.Approaching;
      _phaseTimer = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var colossus = boss as WildBossPentColossus;
      if (colossus == null) return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Approaching:
          // 朝玩家靠近
          var playerPos = colossus.GetPlayerPos();
          var toPlayer = playerPos - colossus.Position;
          float dist = toPlayer.magnitude;

          if (dist <= 1.8f) // 进入近战范围
          {
            _phase = Phase.Windup;
            _phaseTimer = 0f;
          }
          else
          {
            colossus.MoveInDirection(toPlayer.normalized, colossus.MoveSpeed, dt);
          }
          break;

        case Phase.Windup:
          if (_phaseTimer >= WINDUP_DURATION)
          {
            _phase = Phase.Strike;
            _phaseTimer = 0f;

            // 执行近战攻击
            colossus.MeleeHit(damageMult: 1.2f);

            // 子部件环形弹幕
            FirePartRingBarrages(colossus);
          }
          break;

        case Phase.Strike:
          _phase = Phase.Recovery;
          _phaseTimer = 0f;
          break;

        case Phase.Recovery:
          if (_phaseTimer >= RECOVERY_TIME)
            return State.Completed;
          break;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FirePartRingBarrages(WildBossPentColossus colossus)
    {
      var positions = colossus.GetLivingPartPositions();
      foreach (var pos in positions)
      {
        for (int i = 0; i < RING_BULLETS; i++)
        {
          float angle = (360f / RING_BULLETS) * i + Random.Range(-10f, 10f);
          float rad = angle * Mathf.Deg2Rad;
          var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
          colossus.SpawnProjectileFrom(pos, dir,
            damageMult: 0.3f, speedOverride: RING_SPEED, rangeOverride: RING_RANGE);
        }
      }
    }
  }
}
