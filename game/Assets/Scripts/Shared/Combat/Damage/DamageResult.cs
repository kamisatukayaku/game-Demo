namespace Game.Shared.Combat.Damage
{
  public struct DamageResult
  {
    public float FinalDamage;
    public bool WasCritical;
    public DamageBreakdown Breakdown;

    public bool DidDamage => FinalDamage > 0f;
  }

  /// <summary>各结算步骤后的数值（调试用）?/summary>
  public struct DamageBreakdown
  {
    public float AfterBase;
    public float AfterBuff;
    public float AfterResistance;
    public float AfterArmor;
    public float AfterDefense;
    public float AfterBonuses;
    public float Final;
  }
}