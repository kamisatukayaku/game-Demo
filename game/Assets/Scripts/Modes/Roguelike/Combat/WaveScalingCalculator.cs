using UnityEngine;

using Game.Shared.Enemy.AI;
namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Roguelike 式波次强度：全局指数曲线 × 单波 scaling 倍率?
  /// </summary>
  public static class WaveScalingCalculator
  {
    public static WaveScalingCurves DefaultCurves => new()
    {
      hp_growth_per_wave = 1.14f,
      damage_growth_per_wave = 1.12f,
      speed_growth_per_wave = 1.022f,
      dash_speed_growth_per_wave = 1.05f,
      hp_growth_cap = 5.5f,
      damage_growth_cap = 3.2f,
      speed_growth_cap = 1.38f,
      dash_speed_growth_cap = 2.2f
    };

    public static WaveSpawnScaling Compute(int waveNumber, WaveDirector.WaveDefinition waveDef, WaveScalingCurves curves)
    {
      if (waveNumber < 1)
        waveNumber = 1;

      if (curves == null)
        curves = DefaultCurves;

      var waveIndex = waveNumber - 1;
      var globalHp = Mathf.Min(Mathf.Pow(curves.hp_growth_per_wave, waveIndex), curves.hp_growth_cap);
      var globalDmg = Mathf.Min(Mathf.Pow(curves.damage_growth_per_wave, waveIndex), curves.damage_growth_cap);
      var perWaveHp = waveDef != null ? waveDef.hpMult : 1f;
      var perWaveDmg = waveDef != null ? waveDef.damageMult : 1f;

      return new WaveSpawnScaling
      {
        waveNumber = waveNumber,
        hpMult = globalHp * perWaveHp,
        damageMult = globalDmg * perWaveDmg,
        speedMult = 1f,
        dashSpeedMult = 1f
      };
    }
  }

  [System.Serializable]
  public class WaveScalingCurves
  {
    public float hp_growth_per_wave = 1.11f;
    public float damage_growth_per_wave = 1.09f;
    public float speed_growth_per_wave = 1.018f;
    public float dash_speed_growth_per_wave = 1.04f;
    public float hp_growth_cap = 3.8f;
    public float damage_growth_cap = 2.4f;
    public float speed_growth_cap = 1.28f;
    public float dash_speed_growth_cap = 1.8f;
  }
}
