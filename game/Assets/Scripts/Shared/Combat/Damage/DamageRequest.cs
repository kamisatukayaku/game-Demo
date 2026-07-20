using UnityEngine;

namespace Game.Shared.Combat.Damage
{
  /// <summary>
  /// 伤害请求：攻击方提交基础数据，由 <see cref="DamagePipeline"/> 结算?
  /// </summary>
  public struct DamageRequest
  {
    public float Base;
    public string DamageTypeId;
    public string DamageSourceId;
    public DamageKind Kind;
    public GameObject Attacker;

    /// <summary>步骤 5：攻击方属性倍率（装备攻减" 等）?/summary>
    public float AttackerStatMult;

    /// <summary>步骤 5：攻击方固定加成?/summary>
    public float AttackerFlatBonus;

    public float CritChance;
    public float CritDamageMult;

    /// <summary>步骤 5：环?EI 等规则倍率（默?1）?/summary>
    public float EnvironmentMult;

    /// <summary>投射物命中半径（0 = 使用默认?0.5f）?/summary>
    public float HitRadius;

    /// <summary>攻击 Profile ID（用?on_hit_buffs 等后处理）?/summary>
    public string AttackProfileId;

    public static DamageRequest Direct(
      float baseDamage,
      string damageTypeId,
      string damageSourceId,
      GameObject attacker = null,
      DamageKind kind = DamageKind.Direct)
    {
      return new DamageRequest
      {
        Base = baseDamage,
        DamageTypeId = string.IsNullOrEmpty(damageTypeId) ? "physical" : damageTypeId,
        DamageSourceId = string.IsNullOrEmpty(damageSourceId) ? "environment" : damageSourceId,
        Kind = kind,
        Attacker = attacker,
        AttackerStatMult = 1f,
        AttackerFlatBonus = 0f,
        CritChance = 0f,
        CritDamageMult = 1.5f,
        EnvironmentMult = 1f
      };
    }

    public DamageRequest WithAttackerBonuses(
      float statMult,
      float flatBonus,
      float critChance = 0f,
      float critDamageMult = 1.5f)
    {
      AttackerStatMult = statMult;
      AttackerFlatBonus = flatBonus;
      CritChance = critChance;
      CritDamageMult = critDamageMult > 0f ? critDamageMult : 1.5f;
      return this;
    }
  }
}