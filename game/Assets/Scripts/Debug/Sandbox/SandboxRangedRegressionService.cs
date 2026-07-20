using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Shared.Vfx;
using UnityEngine;

namespace Game.DevTools.Sandbox
{
  public static class SandboxRangedRegressionService
  {
    public static void ApplyPreset(SandboxSceneController scene, string presetId)
    {
      if (scene?.Player == null)
        return;

      ClearRangedStats();

      if (presetId == "base")
      {
        RunBuildState.NotifyChanged();
        scene.Spawner?.ClearAll();
        return;
      }

      if (presetId == "high_as")
      {
        BuildStatRepository.SetStat(StatKeys.WeaponAttackSpeedMult, 1.2f);
        RunBuildState.NotifyChanged();
        scene.Spawner?.ClearAll();
        SandboxDetachedWeaponRegressionService.SpawnTargets(scene, 6);
        return;
      }

      if (presetId == "swarm")
      {
        SetSpreadTier(3);
        SetPierceTier(2);
        SetExplosionTier(3);
        SetLightningTier(3);
        RunBuildState.NotifyChanged();
        scene.Spawner?.ClearAll();
        scene.Spawner?.SpawnSwarm();
        return;
      }

      if (presetId.StartsWith("sp") && TryParseTier(presetId, "sp", out var spTier))
        SetSpreadTier(spTier);
      else if (presetId.StartsWith("pc") && TryParseTier(presetId, "pc", out var pcTier))
        SetPierceTier(pcTier);
      else if (presetId.StartsWith("ex") && TryParseTier(presetId, "ex", out var exTier))
        SetExplosionTier(exTier);
      else if (presetId.StartsWith("lt") && TryParseTier(presetId, "lt", out var ltTier))
        SetLightningTier(ltTier);
      else switch (presetId)
      {
        case "spread_pierce":
          SetSpreadTier(5);
          SetPierceTier(5);
          break;
        case "explosion_lightning":
          SetExplosionTier(5);
          SetLightningTier(5);
          break;
        case "full_max":
          SetSpreadTier(5);
          SetPierceTier(5);
          SetExplosionTier(5);
          SetLightningTier(5);
          break;
      }

      RunBuildState.NotifyChanged();
      scene.Spawner?.ClearAll();
      SandboxDetachedWeaponRegressionService.SpawnTargets(scene, presetId == "full_max" ? 12 : 8);
    }

    public static void ResetVfxPools() => RangedVfxSandboxReset.ResetAll();

    static bool TryParseTier(string presetId, string prefix, out int tier)
    {
      tier = 0;
      if (presetId.Length <= prefix.Length)
        return false;
      return int.TryParse(presetId.Substring(prefix.Length), out tier) && tier >= 1 && tier <= 5;
    }

    static void ClearRangedStats()
    {
      RangedOverloadRuntime.ResetForNewRun();
      BuildStatRepository.SetStat(StatKeys.WeaponExtraProjectile, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectilePierce, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileExplosionRadius, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileExplosionRatio, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileChainCount, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileChainDamageRatio, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileChainJumpRange, 0);
      BuildStatRepository.SetStat(StatKeys.WeaponAttackSpeedMult, 0);
      BuildStatRepository.SetStat(StatKeys.PierceNoFalloff, 0);
      BuildStatRepository.SetStat(StatKeys.ExplosionDamageMult, 0);
      BuildStatRepository.SetStat(StatKeys.ProjectileSpeedMult, 0);
      BuildStatRepository.SetStat("projectile_size_mult", 0);
      BuildStatRepository.SetStat("primary_projectile_damage_mult", 0);
      BuildStatRepository.SetStat("spread_angle", 0);
      BuildStatRepository.SetStat("volley_stagger", 0);
      BuildStatRepository.SetStat("auxiliary_explosive_tier", 0);
      BuildStatRepository.SetStat("auxiliary_lightning_tier", 0);
      BuildStatRepository.SetStat("auxiliary_explosive_interval", 0);
      BuildStatRepository.SetStat("auxiliary_lightning_interval", 0);
      BuildStatRepository.SetStat("explosion_secondary_wave", 0);
      BuildStatRepository.SetStat("explosion_fragment_burst", 0);
      BuildStatRepository.SetStat("explosion_chain_detonate", 0);
      BuildStatRepository.SetStat("explosion_saturation", 0);
      BuildStatRepository.SetStat("lightning_fork_jumps", 0);
      BuildStatRepository.SetStat("lightning_conduct_mark", 0);
      BuildStatRepository.SetStat("lightning_network", 0);
      BuildStatRepository.SetStat("pierce_trail_feedback", 0);
    }

    static void SetSpreadTier(int tier)
    {
      BuildStatRepository.SetStat(StatKeys.WeaponExtraProjectile, tier);
      BuildStatRepository.SetStat("spread_angle", tier switch
      {
        >= 5 => 34f,
        >= 3 => 26f,
        >= 2 => 18f,
        _ => 10f
      });
      BuildStatRepository.SetStat("primary_projectile_damage_mult", tier switch
      {
        >= 5 => -0.31f,
        >= 4 => -0.26f,
        >= 3 => -0.20f,
        >= 2 => -0.12f,
        _ => -0.12f
      });
      if (tier >= 4)
        BuildStatRepository.SetStat("volley_stagger", 1);
    }

    static void SetPierceTier(int tier)
    {
      var pierce = tier switch { >= 5 => 5, >= 3 => 3, _ => tier };
      BuildStatRepository.SetStat(StatKeys.ProjectilePierce, pierce);
      if (tier >= 2)
        BuildStatRepository.SetStat(StatKeys.PierceNoFalloff, 1);
      if (tier >= 3)
        BuildStatRepository.SetStat("projectile_size_mult", 0.08f);
      if (tier >= 4)
        BuildStatRepository.SetStat("pierce_trail_feedback", 1);
      if (tier >= 5)
        BuildStatRepository.SetStat(StatKeys.ProjectileSpeedMult, 0.1f);
    }

    static void SetExplosionTier(int tier)
    {
      BuildStatRepository.SetStat("auxiliary_explosive_tier", tier);
      BuildStatRepository.SetStat(StatKeys.ProjectileExplosionRadius, 1.2f + tier * 0.35f);
      BuildStatRepository.SetStat(StatKeys.ProjectileExplosionRatio, 0.45f);
      BuildStatRepository.SetStat("auxiliary_explosive_interval", Mathf.Max(0.5f, 2.4f - tier * 0.08f));
      if (tier >= 2)
        BuildStatRepository.SetStat("explosion_secondary_wave", 1);
      if (tier >= 3)
        BuildStatRepository.SetStat("explosion_fragment_burst", 1);
      if (tier >= 4)
      {
        BuildStatRepository.SetStat("explosion_chain_detonate", 1);
        BuildStatRepository.SetStat(StatKeys.ExplosionDamageMult, 0.12f);
      }
      if (tier >= 5)
        BuildStatRepository.SetStat("explosion_saturation", 1);
    }

    static void SetLightningTier(int tier)
    {
      BuildStatRepository.SetStat("auxiliary_lightning_tier", tier);
      BuildStatRepository.SetStat(StatKeys.ProjectileChainCount, tier);
      BuildStatRepository.SetStat(StatKeys.ProjectileChainDamageRatio, (tier - 1) * 0.15f);
      BuildStatRepository.SetStat("auxiliary_lightning_interval", Mathf.Max(0.5f, 2.6f - tier * 0.1f));
      if (tier >= 3)
        BuildStatRepository.SetStat("lightning_fork_jumps", 1);
      if (tier >= 4)
      {
        BuildStatRepository.SetStat(StatKeys.ProjectileChainJumpRange, 1.2f);
        BuildStatRepository.SetStat("lightning_conduct_mark", 1);
      }
      if (tier >= 5)
        BuildStatRepository.SetStat("lightning_network", 1);
    }
  }
}
