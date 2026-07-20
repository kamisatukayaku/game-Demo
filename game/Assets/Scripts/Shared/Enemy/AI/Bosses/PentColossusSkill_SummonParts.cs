using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 6 — 消耗生命召唤子部件。
  /// 不存在子部件时可释放，消耗 20% 当前生命，召唤 2 个新子部件。
  /// 部件血量 = 消耗前生命的 20%。
  /// Priority: 0（最高优先级）, Cooldown: 25s
  /// </summary>
  public class PentColossusSkill_SummonParts : BossSkillBase
  {
    const float HP_COST_RATIO   = 0.20f; // 消耗当前生命 20%
    const float PART_HP_FACTOR  = 0.20f; // 部件血量 = 消耗前生命 × 20%
    const float CAST_DURATION   = 0.5f;

    float _elapsed;
    bool  _executed;

    public PentColossusSkill_SummonParts()
    {
      Id       = WildBossPentColossus.SKILL_SUMMON;
      Priority = 0; // 最高优先级
      Cooldown = 25f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      // 仅在不存活部件时可释放
      if (colossus == null || colossus.HasLivingParts) return false;

      // 需要足够的生命来支付代价（至少留 1 点血）
      var health = boss.Core?.Health;
      if (health == null) return false;
      float hpCost = health.CurrentHp * HP_COST_RATIO;
      return health.CurrentHp - hpCost >= 1f;
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
        var colossus = boss as WildBossPentColossus;
        if (colossus != null) ExecuteSummon(colossus);
      }

      return _elapsed >= CAST_DURATION + 0.3f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void ExecuteSummon(WildBossPentColossus colossus)
    {
      var health = colossus.Core?.Health;
      if (health == null) return;

      // 记录消耗前的生命值
      float hpBeforeCost = health.CurrentHp;
      float hpCost = hpBeforeCost * HP_COST_RATIO;

      float maxCost = Mathf.Max(0f, hpBeforeCost - 1f);
      hpCost = Mathf.Min(hpCost, maxCost);
      if (hpCost <= 0f) return;

      // ── 召唤前淡入动画（在子部件位置）──
      const float PART_OFFSET_X = 2.5f;
      var previewSprite = Resources.Load<Sprite>("Sprites/Enemies/Bosses/pent_colossus_part");
      var bossPos = colossus.Position;
      if (previewSprite != null)
      {
        ShowSummonFadeIn(new Vector3(bossPos.x - PART_OFFSET_X, bossPos.y, 0f), previewSprite, Vector3.one * 0.8f, 0.5f);
        ShowSummonFadeIn(new Vector3(bossPos.x + PART_OFFSET_X, bossPos.y, 0f), previewSprite, Vector3.one * 0.8f, 0.5f);
      }

      colossus.SelfDamage(hpCost);
      float partHp = hpBeforeCost * PART_HP_FACTOR;
      colossus.SummonNewParts(partHp);
    }
  }
}
