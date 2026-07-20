using UnityEngine;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;

namespace Game.Modes.Roguelike.Archetypes.Warrior
{
  /// <summary>
  /// Warrior runtime state snapshot. Built from RunBuildState whenever stats change.
  /// Equivalent to Mage's SkillContext pattern.
  /// </summary>
  public struct WarriorContext
  {
    // ── Orbit base stats ──
    public int WeaponCount;
    public float RotationSpeed;
    public float Radius;
    public float Damage;
    public float WeaponSize;
    public float HitInterval;
    public float RangeExpansion;

    // ── Satellite line ──
    public float OrbitSplashRatio;
    public float OrbitSyncBonus;
    public bool SatelliteCatastrophe;

    // ── Titan line ──
    public bool TitanFlag;

    // ── Spirit Blade line ──
    public bool SpiritEnabled;
    public int SpiritLaunchCount;
    public float SpiritBladeSpeed;
    public float SpiritBladeReturnSpeed;
    public int SpiritBladePierce;
    public bool SpiritBladeHoming;
    public bool SpiritBladeRecast;
    public bool SpiritBladeTrail;
    public float SpiritBladeTrailCD; // 路径伤害冷却（秒）
    public bool SpiritInfinite;
    public int ProjectileBounce;

    // ── Melee shared ──
    public float MeleeKnockbackChance;
    public float MeleeExplosionRadius;

    /// <summary>Build context snapshot from current RunBuildState.</summary>
    public static WarriorContext FromBuild()
    {
      var data = WarriorProgressionDatabase.Base;
      return new WarriorContext
      {
        WeaponCount = ComputeWeaponCount(data),
        RotationSpeed = Mathf.Max(10f, data.rotationSpeed + RunBuildState.GetWarriorRotationSpeed()),
        Radius = Mathf.Clamp(data.radius + RunBuildState.GetWarriorRadius(), 0.5f, 8f),
        Damage = Mathf.Max(0.5f, data.damage + RunBuildState.GetWarriorDamage()),
        WeaponSize = Mathf.Max(0.1f, data.weaponSize + RunBuildState.GetWarriorWeaponSize()),
        HitInterval = data.hitInterval,
        RangeExpansion = RunBuildState.GetWarriorRangeExpansion(),

        OrbitSplashRatio = RunBuildState.GetWarriorOrbitSplashRatio(),
        OrbitSyncBonus = RunBuildState.GetWarriorOrbitSyncBonus(),
        SatelliteCatastrophe = RunBuildState.GetWarriorSatelliteCatastrophe(),

        TitanFlag = RunBuildState.GetWarriorTitanFlag(),

        SpiritEnabled = RunBuildState.GetWarriorSpiritEnabled(),
        SpiritLaunchCount = Mathf.Max(0, Mathf.RoundToInt(RunBuildState.GetWarriorSpiritBladeCount())),
        SpiritBladeSpeed = 8f + RunBuildState.GetWarriorSpiritBladeSpeed() * 8f,
        SpiritBladeReturnSpeed = 12f + RunBuildState.GetWarriorSpiritBladeReturnSpeed() * 12f,
        SpiritBladePierce = Mathf.RoundToInt(RunBuildState.GetWarriorSpiritBladePierce()),
        SpiritBladeHoming = RunBuildState.GetWarriorSpiritBladeHoming() > 0f,
        SpiritBladeRecast = RunBuildState.GetWarriorSpiritBladeRecast() > 0f,
        SpiritBladeTrail = RunBuildState.GetWarriorSpiritBladeTrail() > 0f,
        SpiritBladeTrailCD = 0.15f, // 轨迹伤害内置冷却
        SpiritInfinite = RunBuildState.GetWarriorSpiritInfinite(),
        ProjectileBounce = Mathf.Max(0, Mathf.RoundToInt(RunBuildState.GetStat(StatKeys.WarriorProjectileBounce))),

        MeleeKnockbackChance = RunBuildState.GetMeleeKnockbackChance(),
        MeleeExplosionRadius = RunBuildState.GetMeleeExplosionRadius(),
      };
    }

    /// <summary>Final damage including orbit resonance bonus (satellite line).</summary>
    public float EffectiveDamage => TitanFlag
      ? Damage * 6f
      : Damage * (1f + WeaponCount * OrbitSyncBonus);

    /// <summary>Final weapon count after capstone multiplication.</summary>
    public int EffectiveWeaponCount => SatelliteCatastrophe
      ? WeaponCount * 2
      : TitanFlag ? 1 : WeaponCount;

    /// <summary>Final weapon size after capstone multiplier.</summary>
    public float EffectiveWeaponSize => TitanFlag ? WeaponSize * 2f : WeaponSize;

    public float EffectiveKnockbackMultiplier => TitanFlag ? 3f : 1f;

    static int ComputeWeaponCount(WarriorProgressionDatabase.WarriorBaseDef data)
    {
      if (RunBuildState.WeaponTheme == UnifiedBuildBootstrap.WeaponTheme)
      {
        if (!RunBuildState.HasTag("orbit"))
          return 0;
        // Unified orbit blades are earned one-by-one via warrior_weapon_count upgrades.
        return Mathf.Max(0, Mathf.RoundToInt(RunBuildState.GetWarriorWeaponCount()));
      }

      var count = data.weaponCount + Mathf.RoundToInt(RunBuildState.GetWarriorWeaponCount());
      return Mathf.Max(1, count);
    }
  }
}
