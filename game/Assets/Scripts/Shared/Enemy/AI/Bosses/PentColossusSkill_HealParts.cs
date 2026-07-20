using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 4 — 完全治疗子部件 + 自损。
  /// 完全治疗所有存活的子部件，并扣除治疗量 25% 的自身血量（至多降为 1）。
  /// Priority: 4, Cooldown: 15s
  /// </summary>
  public class PentColossusSkill_HealParts : BossSkillBase
  {
    const float HEAL_CAST_TIME    = 0.4f;
    const float SELF_DAMAGE_RATIO = 0.25f;

    float _elapsed;
    bool  _executed;

    public PentColossusSkill_HealParts()
    {
      Id       = WildBossPentColossus.SKILL_HEAL;
      Priority = 4;
      Cooldown = 15f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      if (colossus == null || !colossus.HasLivingParts) return false;

      // 检查是否有部件需要治疗
      foreach (var part in colossus.GetLivingParts())
      {
        var h = part.GetComponent<Health>();
        if (h != null && h.CurrentHp < h.MaxHp) return true;
      }
      return false;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed  = 0f;
      _executed = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;

      if (!_executed && _elapsed >= HEAL_CAST_TIME)
      {
        _executed = true;
        var colossus = boss as WildBossPentColossus;
        if (colossus != null) ExecuteHeal(colossus);
      }

      return _elapsed >= HEAL_CAST_TIME + 0.3f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void ExecuteHeal(WildBossPentColossus colossus)
    {
      float totalHealed = 0f;

      foreach (var part in colossus.GetLivingParts())
      {
        var h = part.GetComponent<Health>();
        if (h == null) continue;

        float missing = h.MaxHp - h.CurrentHp;
        if (missing <= 0f) continue;

        // 满血治疗
        colossus.FullHealPart(part);
        totalHealed += missing;
      }

      if (totalHealed > 0f)
      {
        // 扣除治疗量 25% 的自身血量，至少保留 1 点
        float selfDamage = totalHealed * SELF_DAMAGE_RATIO;
        var bossHealth = colossus.Core?.Health;
        if (bossHealth != null)
        {
          float maxAllowed = Mathf.Max(0f, bossHealth.CurrentHp - 1f);
          selfDamage = Mathf.Min(selfDamage, maxAllowed);
          if (selfDamage > 0f)
            colossus.SelfDamage(selfDamage);
        }
      }
    }
  }
}
