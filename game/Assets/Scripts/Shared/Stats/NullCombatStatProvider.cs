namespace Game.Shared.Stats
{
  sealed class NullCombatStatProvider : ICombatStatProvider
  {
    public static readonly NullCombatStatProvider Instance = new();

    public float Defense => 0f;
    public float DamageReduction => 0f;
    public float AllResist => 0f;
    public float OverhealShield => 0f;
    public float CritChance => 0f;
    public float CritDamageMult => 1.5f;
    public float Lifesteal => 0f;
    public float HealOnHitPct => 0f;
    public float KnockbackResist => 0f;
    public float ComputeReflectDamage(float baseDamage) => baseDamage;
  }
}