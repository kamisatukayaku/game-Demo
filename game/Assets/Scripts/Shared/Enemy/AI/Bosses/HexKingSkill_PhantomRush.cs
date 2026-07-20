using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 4 — 幻影瞬移冲撞。
  /// 高速远离玩家 → 召唤 6 个幻影子部件（围绕玩家固定相对位置）→
  /// 每隔短时间瞬移到随机幻影处并对玩家发起无蓄力冲撞 → 幻影销毁。
  /// 持续至无幻影存活或护盾被击破。
  /// Priority: 1, Cooldown: 20s
  /// </summary>
  public class HexKingSkill_PhantomRush : BossSkillBase
  {
    const float RETREAT_SPEED      = 10f;
    const float RETREAT_DISTANCE   = 8f;     // 退到这个距离后开始召唤幻影
    const int   PHANTOM_COUNT      = 6;
    const float PHANTOM_HP_RATIO   = 0.10f;  // 每个幻影血量 = Boss 最大 × 10%
    const float PHANTOM_RADIUS     = 4f;     // 幻影围绕玩家的半径

    const float RUSH_INTERVAL      = 0.35f;
    const float RUSH_SPEED         = 22f;
    const float RUSH_HIT_RADIUS    = 1.5f;
    const float RUSH_DAMAGE_MULT   = 0.65f;
    const float RUSH_DIST_MULT     = 1.75f;
    const float RUSH_DUR_MIN       = 0.5f;
    const float RUSH_DUR_MAX       = 2f;

    enum Phase { Retreating, Summoning, Rushing }
    Phase  _phase;
    float  _phaseTimer;
    float  _rushTimer;
    float  _rushDurTimer;
    Vector2 _rushDir;
    float[] _phantomAngles;
    bool   _hadShieldAtStart;
    bool   _rushHitApplied;

    public HexKingSkill_PhantomRush()
    {
      Id       = WildBossHexKing.SKILL_PHANTOM;
      Priority = 1;
      Cooldown = 20f;
      Category = BossSkillCategory.Ultimate;
    }

    public override bool CanTrigger(BossCore boss) => boss is WildBossHexKing;

    public override void OnEnter(BossCore boss)
    {
      _phase            = Phase.Retreating;
      _phaseTimer       = 0f;
      _rushTimer        = 0f;
      _rushDurTimer     = 0f;
      _phantomAngles    = null;
      var king          = boss as WildBossHexKing;
      _hadShieldAtStart = king != null && king.HasShield;
      _rushHitApplied   = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var king = boss as WildBossHexKing;
      if (king == null) return State.Completed;

      // ══ 全局中断检测：技能开始时存在护盾，但中途破裂 ══
      if (_hadShieldAtStart && !king.HasShield)
        return State.Completed;

      _phaseTimer += dt;

      switch (_phase)
      {
        case Phase.Retreating:
        {
          var away = (king.Position - king.GetPlayerPos()).normalized;
          king.MoveInDir(away, RETREAT_SPEED, dt);

          if (king.DistToPlayer() >= RETREAT_DISTANCE)
          {
            _phase = Phase.Summoning;
            _phaseTimer = 0f;

            float partHp = Mathf.Max(1f, (boss.Core?.Health?.MaxHp ?? 100f) * PHANTOM_HP_RATIO);
            _phantomAngles = king.CreatePhantoms(PHANTOM_COUNT, partHp, PHANTOM_RADIUS);
          }
          break;
        }

        case Phase.Summoning:
        {
          if (_phantomAngles != null)
            king.SyncPhantomPositions(_phantomAngles, PHANTOM_RADIUS);

          if (_phaseTimer >= 0.3f)
          {
            _phase     = Phase.Rushing;
            _rushTimer = RUSH_INTERVAL; // 立即开始第一次冲撞
          }
          break;
        }

        case Phase.Rushing:
        {
          if (_phantomAngles != null)
            king.SyncPhantomPositions(_phantomAngles, PHANTOM_RADIUS);

          _rushTimer += dt;

          // 正在冲撞中
          if (_rushDurTimer > 0f)
          {
            _rushDurTimer -= dt;
            king.MoveInDir(_rushDir, RUSH_SPEED, dt);
            TryRushHit(king);

            if (_rushDurTimer <= 0f && king.LivingPhantomCount <= 0)
              return State.Completed;
          }
          // 空闲 → 触发下一次冲撞
          else if (_rushTimer >= RUSH_INTERVAL)
          {
            _rushTimer -= RUSH_INTERVAL;

            if (king.PopRandomPhantom(out Vector3 phantomPos))
            {
              boss.transform.position = phantomPos;
              _rushDir      = king.DirToPlayer();
              _rushDurTimer = Mathf.Clamp(king.DistToPlayer() * RUSH_DIST_MULT / RUSH_SPEED, RUSH_DUR_MIN, RUSH_DUR_MAX);
              _rushHitApplied = false;
            }
            else
            {
              return State.Completed;
            }
          }
          break;
        }
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss)
    {
      var king = boss as WildBossHexKing;
      king?.DestroyAllPhantoms();
    }

    void TryRushHit(WildBossHexKing king)
    {
      if (_rushHitApplied)
        return;

      float dist = Vector2.Distance(king.Position, king.GetPlayerPos());
      if (dist <= RUSH_HIT_RADIUS)
      {
        king.MeleeHit(RUSH_DAMAGE_MULT);
        _rushHitApplied = true;
      }
    }
  }
}
