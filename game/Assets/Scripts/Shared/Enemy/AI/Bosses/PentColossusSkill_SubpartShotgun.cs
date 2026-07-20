using UnityEngine;
using Game.Shared.Core;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 2 — 子部件霰弹 + 减速。
  /// 从每个子部件各发出一组射程较长的霰弹，仅存在子部件时可释放，命中造成减速。
  /// Priority: 2, Cooldown: 5s
  /// </summary>
  public class PentColossusSkill_SubpartShotgun : BossSkillBase
  {
    const int   PELLETS_PER_PART = 5;
    const float SPREAD_DEG       = 30f;
    const float SHOTGUN_RANGE    = 16f;
    const float SHOTGUN_SPEED    = 7f;
    const string SLOW_BUFF_ID    = "boss_pent_shotgun_slow";
    const float SLOW_AMOUNT      = 0.3f;
    const float SLOW_DURATION    = 2f;

    float _elapsed;
    bool  _fired;

    public PentColossusSkill_SubpartShotgun()
    {
      Id       = WildBossPentColossus.SKILL_SHOTGUN;
      Priority = 2;
      Cooldown = 5f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      return colossus != null && colossus.HasLivingParts;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed = 0f;
      _fired   = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;

      if (!_fired && _elapsed >= 0.15f) // 短暂前摇
      {
        _fired = true;
        var colossus = boss as WildBossPentColossus;
        if (colossus != null)
          FireShotgunFromParts(colossus);
      }

      // 子弹飞行 + 简短收尾
      return _elapsed >= 0.5f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireShotgunFromParts(WildBossPentColossus colossus)
    {
      var positions = colossus.GetLivingPartPositions();

      foreach (var pos in positions)
      {
        var toPlayer = colossus.GetPlayerPos() - new Vector2(pos.x, pos.y);
        if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector2.right;
        var dir = toPlayer.normalized;

        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < PELLETS_PER_PART; i++)
        {
          float t = PELLETS_PER_PART <= 1 ? 0f : (i / (float)(PELLETS_PER_PART - 1) - 0.5f);
          float rad = (baseAngle + t * SPREAD_DEG) * Mathf.Deg2Rad;
          var pelletDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

          colossus.SpawnProjectileFrom(pos, pelletDir,
            damageMult: 0.35f, speedOverride: SHOTGUN_SPEED, rangeOverride: SHOTGUN_RANGE);
        }
      }

      // 对玩家施加减速
      colossus.ApplySlowToPlayer(SLOW_BUFF_ID, SLOW_AMOUNT, SLOW_DURATION);
    }
  }
}
