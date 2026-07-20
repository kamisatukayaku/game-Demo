namespace Game.Shared.Stats
{
  /// <summary>战斗层可读的玩家/构筑数值（Shared 不依?Roguelike 实现）?/summary>
  public interface ICombatStatProvider
  {
    /// <summary>玩家防御值（flat subtraction，伤害先减防御再算减免）。</summary>
    float Defense { get; }
    float DamageReduction { get; }
    float AllResist { get; }
    float OverhealShield { get; }
    float CritChance { get; }
    float CritDamageMult { get; }
    float Lifesteal { get; }
    float HealOnHitPct { get; }
    float KnockbackResist { get; }

    /// <summary>反弹伤害换算（玩家护盾反弹等）?/summary>
    float ComputeReflectDamage(float baseDamage);
  }

}