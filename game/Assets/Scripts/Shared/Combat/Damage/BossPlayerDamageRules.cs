namespace Game.Shared.Combat.Damage
{
  /// <summary>
  /// Caps boss single-hit damage so fights stay mechanic-driven, not instant kills.
  /// </summary>
  public static class BossPlayerDamageRules
  {
    public const float DefaultMaxHpFraction = 0.30f;
    public const float HeavyHitMaxHpFraction = 0.35f;
    public const float LightHitMaxHpFraction = 0.22f;

    public static float CapSingleHit(
      float rawDamage,
      global::Game.Shared.Combat.Health.Health target,
      float maxHpFraction = DefaultMaxHpFraction)
    {
      if (target == null || target.MaxHp <= 0f)
        return rawDamage;

      var cap = target.MaxHp * maxHpFraction;
      return rawDamage > cap ? cap : rawDamage;
    }
  }
}
