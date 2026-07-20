using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Ranged
{
  /// <summary>Snapshot for auxiliary explosive/lightning channels only.</summary>
  public struct RangedAuxiliaryContext
  {
    public int ExplosiveTier;
    public int LightningTier;
    public float ExplosiveInterval;
    public float LightningInterval;

    // Independent auxiliary growth hooks (0 until JSON defines values).
    public int ExplosiveProjectileCount;
    public float ExplosiveSpreadAngle;
    public int ExplosivePierce;
    public float ExplosivePierceDamageRetention;
    public float ExplosiveProjectileDamageMult;
    public int LightningProjectileCount;
    public float LightningSpreadAngle;
    public int LightningPierce;
    public float LightningPierceDamageRetention;
    public float LightningProjectileDamageMult;
  }

  public static class RangedAuxiliaryContextBuilder
  {
    public static RangedAuxiliaryContext Build()
    {
      return new RangedAuxiliaryContext
      {
        ExplosiveTier = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.AuxiliaryExplosiveTier)),
        LightningTier = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.AuxiliaryLightningTier)),
        ExplosiveInterval = ResolveInterval(
          RunBuildState.GetStat(RangedStatKeys.AuxiliaryExplosiveInterval), 2.4f),
        LightningInterval = ResolveInterval(
          RunBuildState.GetStat(RangedStatKeys.AuxiliaryLightningInterval), 2.6f),
        ExplosiveProjectileCount = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.ExplosiveProjectileCount)),
        ExplosiveSpreadAngle = RunBuildState.GetStat(RangedStatKeys.ExplosiveSpreadAngle),
        ExplosivePierce = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.ExplosivePierce)),
        ExplosivePierceDamageRetention = RunBuildState.GetStat(RangedStatKeys.ExplosivePierceDamageRetention),
        ExplosiveProjectileDamageMult = RunBuildState.GetStat(RangedStatKeys.ExplosiveProjectileDamageMult),
        LightningProjectileCount = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.LightningProjectileCount)),
        LightningSpreadAngle = RunBuildState.GetStat(RangedStatKeys.LightningSpreadAngle),
        LightningPierce = Mathf.RoundToInt(RunBuildState.GetStat(RangedStatKeys.LightningPierce)),
        LightningPierceDamageRetention = RunBuildState.GetStat(RangedStatKeys.LightningPierceDamageRetention),
        LightningProjectileDamageMult = RunBuildState.GetStat(RangedStatKeys.LightningProjectileDamageMult)
      };
    }

    static float ResolveInterval(float configured, float fallback) =>
      configured > 0.05f ? Mathf.Max(0.4f, configured) : fallback;
  }
}
