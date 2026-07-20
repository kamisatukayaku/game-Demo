using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  public static class BossRushScaling
  {
    public static WaveSpawnScaling Build(
      BossRushEncounterDef encounter,
      int encounterIndex)
    {
      encounter ??= new BossRushEncounterDef();
      var hpMult = Positive(encounter.hp_mult, 1f) * ArenaDifficultyRuntime.EnemyHpMult;
      var damageMult = Positive(encounter.damage_mult, 1f) * ArenaDifficultyRuntime.EnemyDamageMult;
      var speedMult = Positive(encounter.speed_mult, 1f);
      return new WaveSpawnScaling
      {
        waveNumber = encounterIndex,
        hpMult = hpMult,
        damageMult = damageMult,
        speedMult = speedMult,
        dashSpeedMult = speedMult
      };
    }

    static float Positive(float value, float fallback) => value > 0f ? value : fallback;
  }
}
