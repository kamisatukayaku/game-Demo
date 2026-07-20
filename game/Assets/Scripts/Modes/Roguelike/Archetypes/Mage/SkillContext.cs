namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>法师/技能流运行时参数快照（Roguelike MageContextBuilder 填充，Mage Runtime 只读）</summary>
  public struct SkillContext
  {
    public bool IsMageTheme;

    public float SkillDamageMult;
    public float SkillRangeMult;
    public float SkillCooldownReduce;

    public float SkillExplosionRadius;
    public float SkillExplosionRatio;
    public float SkillBurstRadiusBonus;
    public float SkillPulseDamage;

    public float SkillVacuum;
    public float SkillVacuumDuration;
    public float SkillVacuumStrength;
    public int SkillVacuumSplit;
    public float SkillVacuumOverlapAmp;
    public float SkillVacuumTrail;
    public float SkillPctHpDamage;
    public float SkillVacuumRampDamage;
    public float SkillVulnerableBonus;

    public float SkillCollapseExplosion;
    public float SkillZoneCollapse;
    public float SkillCollapseRadiusBonus;

    public float SkillBurnDps;
    public float SkillBurnDuration;
    public float SkillSlowAmount;
    public float SkillSlowChance;

    public float SkillCdResetChance;
    public float SkillTimeStopChance;
    public float SkillTimeRewind;
    public float SkillTimeDilationField;

    public float SkillElementBurst;
    public float SkillElementMelt;
    public float SkillElementOverload;

    public int SkillChainCount;
    public float SkillChainDamageRatio;
    public int SkillPierce;
    public int SkillSplitOnHit;
    public float SkillHoming;
    public float SkillHomingTurn;
    public int SkillEchoCount;
    public float SkillEchoGuarantee;
    public float SkillMirrorCast;
    public int SkillVolleyOnCast;
    public int SkillExtraProjectile;

    public float SkillCritChance;
    public float SkillCritDamage;

    public float TimeDilationRadius;
    public float ArcaneDamageMult;
    public float ArcaneCooldownReduce;
    public float ArcaneProjectileSpeed;
    public float FireDamageMult;
    public float FireDurationBonus;
    public float FireDamageAmp;
    public float FireRadiusBonus;
    public float GravityRadiusBonus;
    public float GravityDamageMult;
    public float GravityProjectilePull;
    public bool MovingGravityWell;
    public bool GravityDashPull;
    public float GravityFireBonus;
    public float FireKillExplosion;
    public float FireHeatPerTarget;
    public float FrostShieldBonus;
    public float FrostDurationBonus;
    public float FrostThorns;
    public float FrostReflectChance;
    public float FrostPulseDamage;
    public float FrostPulseRadius;
    public float FrostPulseCooldown;
    public float FrostReflectDamageMult;
    public float FrostDamageReduction;
    public float FrostShatterDamage;
    public float FrostRestoreRatio;

    public bool TidalSuccessivePush;
    public float TidalDamageMult;
    public bool TidalBoundary;
    public bool TidalInterruptDash;
    public bool TidalDeflectProjectiles;
    public bool TidalSafeZone;
  }
}
