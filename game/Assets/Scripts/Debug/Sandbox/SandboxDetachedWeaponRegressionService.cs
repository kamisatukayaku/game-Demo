using UnityEngine;
using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Combat.Damage;
using Game.Shared.Player;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.DevTools.Sandbox
{
  /// <summary>Sandbox-only helpers for detached weapon visual regression.</summary>
  public static class SandboxDetachedWeaponRegressionService
  {
    static readonly string[] EvolutionKeys =
    {
      "laser", "missile", "explosion", "pulse", "boomerang", "trail"
    };

    public static bool SpawnWeapon(SandboxSceneController scene, string weaponKey, int tier)
    {
      if (scene?.Player == null)
        return false;

      tier = Mathf.Clamp(tier, 1, 5);
      BuildStatRepository.SetStat("detached_part_count", 1);
      BuildStatRepository.SetStat("detached_contact_level", weaponKey == "contact" ? tier : 3);

      foreach (var key in EvolutionKeys)
        BuildStatRepository.SetStat($"detached_{key}_tier", key == weaponKey ? tier : 0);

      RunBuildState.NotifyChanged();
      SyncDetachedRuntime(scene);
      return true;
    }

    public static void ClearAllWeapons(SandboxSceneController scene)
    {
      var system = scene?.Player?.GetComponent<DetachedWeaponSystem>();
      system?.ClearAllWeapons();
      DetachedWeaponPresentationSystem.ResetPoolsForSandbox();
    }

    public static void ResetPresentationPools() =>
      DetachedWeaponPresentationSystem.ResetPoolsForSandbox();

    public static void SpawnSixWeapons(SandboxSceneController scene)
    {
      if (scene?.Player == null)
        return;
      ClearAllWeapons(scene);
      BuildStatRepository.SetStat("detached_part_count", 6);
      BuildStatRepository.SetStat("detached_contact_level", 3);
      foreach (var key in EvolutionKeys)
        BuildStatRepository.SetStat($"detached_{key}_tier", 2);
      RunBuildState.NotifyChanged();
      SyncDetachedRuntime(scene);
      SpawnTargets(scene, 3);
    }

    public static void SpawnHighTierMix(SandboxSceneController scene)
    {
      if (scene?.Player == null)
        return;
      ClearAllWeapons(scene);
      BuildStatRepository.SetStat("detached_part_count", 6);
      BuildStatRepository.SetStat("detached_contact_level", 5);
      BuildStatRepository.SetStat("detached_laser_tier", 5);
      BuildStatRepository.SetStat("detached_missile_tier", 5);
      BuildStatRepository.SetStat("detached_explosion_tier", 5);
      BuildStatRepository.SetStat("detached_pulse_tier", 5);
      BuildStatRepository.SetStat("detached_trail_tier", 5);
      BuildStatRepository.SetStat("detached_boomerang_tier", 5);
      RunBuildState.NotifyChanged();
      SyncDetachedRuntime(scene);
      SpawnTargets(scene, 4);
    }

    public static void RecycleSpawnTest(SandboxSceneController scene)
    {
      SpawnWeapon(scene, "laser", 3);
      ClearAllWeapons(scene);
      SpawnWeapon(scene, "missile", 3);
      ResetPresentationPools();
    }

    public static string GetIntroStatusLabel()
    {
      var running = DetachedWeaponPresentationSystem.CountActiveIntros();
      return running > 0 ? $"Intro running: {running}" : "Intro idle";
    }

    public static void SpawnTargets(SandboxSceneController scene, int count)
    {
      if (scene?.Spawner == null)
        return;
      scene.Spawner.ClearAll();
      for (var i = 0; i < count; i++)
      {
        var angle = i / (float)Mathf.Max(1, count) * Mathf.PI * 2f;
        scene.Spawner.Spawn("mob_hex_01", new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f);
      }
    }

    public static void KillCurrentTarget(SandboxSceneController scene)
    {
      var target = ResolveAutoCombatTarget(scene);
      if (target == null)
        return;
      var health = target.GetComponent<Health>();
      if (health != null && !health.IsDead)
        DamagePipeline.Apply(DamageRequest.Direct(99999f, "physical", "sandbox", null), health);
    }

    public static string GetLockTargetLabel(SandboxSceneController scene)
    {
      var target = ResolveAutoCombatTarget(scene);
      return target != null ? target.name : "(none)";
    }

    public static string GetPoolStats() => DetachedWeaponPresentationSystem.GetPoolDebugSummary();

    public static int CountDetachedWeaponVisuals() =>
      DetachedWeaponPresentationSystem.CountWeaponVisuals();

    static void SyncDetachedRuntime(SandboxSceneController scene)
    {
      if (scene?.Player == null)
        return;

      DetachedWeaponPresentationSystem.EnsureExists();
      DetachedWeaponSystem.Ensure(scene.Player);
      DetachedWeaponPresentationSystem.RefreshExistingWeapons();
    }

    public static void ForceAttack(SandboxSceneController scene)
    {
      var player = scene?.Player;
      if (player == null)
        return;
      var director = player.GetComponent<PlayerAttackDirector>();
      var target = ResolveAutoCombatTarget(scene);
      if (director == null || target == null)
        return;
      var dir = GameplayPlane.Position2D(target.transform) - GameplayPlane.Position2D(player.transform);
      if (dir.sqrMagnitude < 0.0001f)
        dir = Vector2.right;
      director.SandboxExecuteAttack(dir.normalized);
    }

    static GameObject ResolveAutoCombatTarget(SandboxSceneController scene)
    {
      var auto = scene?.AutoCombat;
      if (auto == null)
        return null;

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || scene.Player == null)
        return null;

      Transform best = null;
      var bestDist = float.MaxValue;
      var origin = scene.Player.transform.position;
      foreach (var core in registry.AllEnemies)
      {
        if (core == null || !core.gameObject.activeInHierarchy)
          continue;
        var health = core.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var dist = Vector3.Distance(origin, core.transform.position);
        if (dist < bestDist)
        {
          bestDist = dist;
          best = core.transform;
        }
      }

      return best != null ? best.gameObject : null;
    }

    public static string RunRouteChecks()
    {
      EvolutionBuildGatesDatabase.EnsureLoaded();
      EvolutionBuildGatesDatabase.BypassDetachedRouteFilterForDebug = false;

      var lines = new List<string>();
      var failed = 0;

      void Check(string name, bool ok)
      {
        lines.Add(ok ? $"[OK] {name}" : $"[FAIL] {name}");
        if (!ok)
          failed++;
      }

      Check("Mage allows laser", !IsRouteBlocked("mage", "laser"));
      Check("Mage allows pulse", !IsRouteBlocked("mage", "pulse"));
      Check("Mage blocks missile", IsRouteBlocked("mage", "missile"));
      Check("Mage blocks explosion", IsRouteBlocked("mage", "explosion"));
      Check("Shooter allows missile", !IsRouteBlocked("shooter", "missile"));
      Check("Shooter allows explosion", !IsRouteBlocked("shooter", "explosion"));
      Check("Shooter blocks laser", IsRouteBlocked("shooter", "laser"));
      Check("Contact allows boomerang", !IsRouteBlocked("contact", "boomerang"));
      Check("Contact allows trail", !IsRouteBlocked("contact", "trail"));
      Check("Contact blocks missile", IsRouteBlocked("contact", "missile"));
      EvolutionBuildGatesDatabase.BypassDetachedRouteFilterForDebug = true;
      Check("Sandbox bypass", !IsRouteBlocked("mage", "missile"));
      EvolutionBuildGatesDatabase.BypassDetachedRouteFilterForDebug = false;

      lines.Insert(0, failed == 0
        ? $"Route validation passed ({lines.Count - 1} checks)"
        : $"Route validation failed ({failed}/{lines.Count - 1} checks)");
      return string.Join("\n", lines);
    }

    static bool IsRouteBlocked(string starterId, string evolutionId) =>
      EvolutionBuildGatesDatabase.IsDetachedEvolutionBlocked(
        MakeEvolutionUpgrade(evolutionId),
        starterId,
        new Dictionary<string, int>());

    static LevelUpChoiceDatabase.UpgradeDef MakeEvolutionUpgrade(string evolutionId) =>
      new LevelUpChoiceDatabase.UpgradeDef
      {
        id = $"evo_{evolutionId}_01_test",
        mechanic_id = evolutionId,
        tags = new[] { "mechanic", "detached_weapon", evolutionId }
      };
  }
}
