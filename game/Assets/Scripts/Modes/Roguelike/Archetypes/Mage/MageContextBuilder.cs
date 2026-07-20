using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>RunBuildState ?SkillContext（Mage 运行时快照）唯一构建入口?/summary>
  public static class MageContextBuilder
  {
    public static SkillContext Build()
    {
      return new SkillContext
      {
        IsMageTheme = RunBuildState.WeaponTheme == "mage",

        SkillDamageMult = RunBuildCombatHooks.GetSkillDamageMult(),
        SkillRangeMult = RunBuildState.GetSkillRangeMult(),
        SkillCooldownReduce = RunBuildState.GetSkillCooldownReduce(),

        SkillExplosionRadius = RunBuildState.GetSkillExplosionRadius(),
        SkillExplosionRatio = RunBuildState.GetSkillExplosionRatio(),
        SkillBurstRadiusBonus = RunBuildState.GetSkillBurstRadiusBonus(),
        SkillPulseDamage = RunBuildState.GetSkillPulseDamage(),

        SkillVacuum = RunBuildState.GetSkillVacuum(),
        SkillVacuumDuration = RunBuildState.GetSkillVacuumDuration(),
        SkillVacuumStrength = RunBuildState.GetSkillVacuumStrength(),
        SkillVacuumSplit = RunBuildState.GetSkillVacuumSplit(),
        SkillVacuumOverlapAmp = RunBuildState.GetSkillVacuumOverlapAmp(),
        SkillVacuumTrail = RunBuildState.GetSkillVacuumTrail(),
        SkillPctHpDamage = RunBuildState.GetSkillPctHpDamage(),
        SkillVacuumRampDamage = RunBuildState.GetSkillVacuumRampDamage(),
        SkillVulnerableBonus = RunBuildState.GetSkillVulnerableBonus(),

        SkillCollapseExplosion = RunBuildState.GetSkillCollapseExplosion(),
        SkillZoneCollapse = RunBuildState.GetSkillZoneCollapse(),
        SkillCollapseRadiusBonus = RunBuildState.GetSkillCollapseRadiusBonus(),

        SkillBurnDps = RunBuildState.GetSkillBurnDps(),
        SkillBurnDuration = RunBuildState.GetSkillBurnDuration(),
        SkillSlowAmount = RunBuildState.GetSkillSlowAmount(),
        SkillSlowChance = RunBuildState.GetSkillSlowChance(),

        SkillCdResetChance = RunBuildState.GetSkillCdResetChance(),
        SkillTimeStopChance = RunBuildState.GetSkillTimeStopChance(),
        SkillTimeRewind = RunBuildState.GetSkillTimeRewind(),
        SkillTimeDilationField = RunBuildState.GetSkillTimeDilationField(),

        SkillElementBurst = RunBuildState.GetSkillElementBurst(),
        SkillElementMelt = RunBuildState.GetSkillElementMelt(),
        SkillElementOverload = RunBuildState.GetSkillElementOverload(),

        SkillChainCount = RunBuildState.GetSkillChainCount(),
        SkillChainDamageRatio = RunBuildState.GetStat(StatKeys.SkillChainDamageRatio),
        SkillPierce = RunBuildState.GetSkillPierce(),
        SkillSplitOnHit = RunBuildState.GetSkillSplitOnHit(),
        SkillHoming = RunBuildState.GetSkillHoming(),
        SkillHomingTurn = RunBuildState.GetSkillHomingTurn(),
        SkillEchoCount = RunBuildState.GetSkillEchoCount(),
        SkillEchoGuarantee = RunBuildState.GetSkillEchoGuarantee(),
        SkillMirrorCast = RunBuildState.GetSkillMirrorCast(),
        SkillVolleyOnCast = RunBuildState.GetSkillVolleyOnCast(),
        SkillExtraProjectile = RunBuildState.GetSkillExtraProjectiles(),

        SkillCritChance = RunBuildState.GetSkillCritChance(),
        SkillCritDamage = RunBuildState.GetSkillCritDamage(),

        TimeDilationRadius = 4.5f,
        ArcaneDamageMult = RunBuildState.GetMageArcaneDamageMult(),
        ArcaneCooldownReduce = RunBuildState.GetMageArcaneCooldownReduce(),
        ArcaneProjectileSpeed = RunBuildState.GetMageArcaneProjectileSpeed(),
        FireDamageMult = RunBuildState.GetMageFireDamageMult(),
        FireDurationBonus = RunBuildState.GetMageFireDuration(),
        FireDamageAmp = RunBuildState.GetMageFireAmp(),
        FireRadiusBonus = RunBuildState.GetMageFireRadius(),
        GravityRadiusBonus = RunBuildState.GetMageGravityRadius(),
        GravityDamageMult = RunBuildState.GetMageGravityDamageMult(),
        GravityProjectilePull = RunBuildState.GetMageGravityProjectilePull(),
        MovingGravityWell = RunBuildState.GetMageGravityMoving(),
        GravityDashPull = RunBuildState.GetMageGravityDashPull(),
        GravityFireBonus = RunBuildState.GetMageGravityFireBonus(),
        FireKillExplosion = RunBuildState.GetMageFireKillExplosion(),
        FireHeatPerTarget = RunBuildState.GetMageFireHeatPerTarget(),
        FrostShieldBonus = RunBuildState.GetMageFrostShield(),
        FrostDurationBonus = RunBuildState.GetMageFrostDuration(),
        FrostThorns = RunBuildState.GetMageFrostThorns(),
        FrostReflectChance = RunBuildState.GetMageFrostReflectChance(),
        FrostPulseDamage = RunBuildState.GetMageFrostPulseDamage(),
        FrostPulseRadius = RunBuildState.GetMageFrostPulseRadius(),
        FrostPulseCooldown = RunBuildState.GetMageFrostPulseCooldown(),
        FrostReflectDamageMult = RunBuildState.GetMageFrostReflectDamageMult(),
        FrostDamageReduction = RunBuildState.GetMageFrostDamageReduction(),
        FrostShatterDamage = RunBuildState.GetMageFrostShatterDamage(),
        FrostRestoreRatio = RunBuildState.GetMageFrostRestoreRatio(),

        TidalSuccessivePush = RunBuildState.GetSkillTidalSuccessivePush(),
        TidalDamageMult = RunBuildState.GetSkillTidalDamageMult(),
        TidalBoundary = RunBuildState.GetSkillTidalBoundary(),
        TidalInterruptDash = RunBuildState.GetSkillTidalInterruptDash(),
        TidalDeflectProjectiles = RunBuildState.GetSkillTidalDeflectProjectiles(),
        TidalSafeZone = RunBuildState.GetSkillTidalSafeZone()
      };
    }
  }
}
