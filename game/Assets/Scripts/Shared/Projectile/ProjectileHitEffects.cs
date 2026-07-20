using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Vfx;
using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Projectile
{
  /// <summary>
  /// 远程弹体命中后的爆炸 / 连锁 / 黑洞吸引等构筑效果?
  /// </summary>
  public static class ProjectileHitEffects
  {
    public struct Config
    {
      public float explosionRadius;
      public float explosionDamageRatio;
      public float explosionDamageMult;
      public bool explosionVacuum;
      public int chainCount;
      public float chainDamageRatio;
      public float chainJumpRange;
      public float chainFalloffPerJump;
      public float slowChance;
      public float slowAmount;
      public float burnDps;
      public float burnDuration;
      public bool explosionSecondaryWave;
      public bool explosionFragmentBurst;
      public bool explosionChainDetonate;
      public bool explosionSaturation;
      public int lightningForkJumps;
      public bool lightningConductMark;
      public bool lightningNetwork;
    }

    public static void ApplyOnHit(
      GameObject attacker,
      Health primaryTarget,
      float directDamage,
      in Config config,
      bool allowSecondaryEffects = true)
    {
      if (primaryTarget == null || directDamage <= 0f)
        return;

      if (!allowSecondaryEffects)
      {
        ApplyStatusEffects(attacker, primaryTarget, config);
        return;
      }

      if (config.explosionRadius > 0.01f)
        ApplyExplosion(attacker, primaryTarget, directDamage, config);

      if (config.chainCount > 0)
        ApplyChain(attacker, primaryTarget, directDamage, config);

      ApplyStatusEffects(attacker, primaryTarget, config);
    }

    public static void ApplyExplosion(
      GameObject attacker,
      Health primaryTarget,
      float directDamage,
      in Config config)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || primaryTarget == null)
        return;

      var hitPos = primaryTarget.transform.position;
      var center = GameplayPlane.Position2D(primaryTarget.transform);
      var radius = config.explosionRadius;
      var ratio = config.explosionDamageRatio > 0f ? config.explosionDamageRatio : 0.5f;
      var mult = config.explosionDamageMult > 0f ? config.explosionDamageMult : 1f;
      var splashDamage = directDamage * ratio * mult;

      RangedExplosionVfx.Spawn(hitPos, radius);

      var hitCount = 0;
      var detonateVisited = new HashSet<Health> { primaryTarget };
      var chainKills = new List<Vector2>();

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null || enemy.gameObject == primaryTarget.gameObject)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        hitCount++;
        detonateVisited.Add(health);
        var req = DamageRequest.Direct(splashDamage, "physical", "weapon", attacker);
        DamagePipeline.Apply(req, health);
        if (health.IsDead)
          chainKills.Add(GameplayPlane.Position2D(enemy.transform));
      }

      if (config.explosionChainDetonate && chainKills.Count > 0)
        ApplyChainDetonations(attacker, registry, chainKills, detonateVisited, radius, splashDamage * 0.42f, maxDepth: 2);

      if (config.explosionFragmentBurst && hitCount >= 2)
        RangedExplosionVfx.Spawn(hitPos, radius * 0.55f);

      if (config.explosionSecondaryWave)
        ScheduleSecondaryWave(attacker, hitPos, center, radius, splashDamage * 0.55f);

      if (config.explosionSaturation && hitCount > 0)
        ApplySaturationBursts(attacker, registry, center, radius, splashDamage * 0.35f, primaryTarget);

      if (config.explosionVacuum)
        ExplosionGravityPull.Spawn(center, radius, pullStrength: 1.35f);

      CombatDebugHookLocator.Range("explosion", "Explosion");
    }

    static void ScheduleSecondaryWave(GameObject attacker, Vector3 hitPos, Vector2 center, float radius, float waveDamage)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      RangedExplosionVfx.Spawn(hitPos, radius * 0.72f);
      foreach (var enemy in registry.GetInRange(center, radius * 0.72f))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var req = DamageRequest.Direct(waveDamage, "physical", "weapon", attacker);
        DamagePipeline.Apply(req, health);
      }
    }

    static void ApplyChainDetonations(
      GameObject attacker,
      EnemyRegistry registry,
      List<Vector2> origins,
      HashSet<Health> visited,
      float baseRadius,
      float burstDamage,
      int maxDepth)
    {
      const int maxBursts = 4;
      var queue = new Queue<(Vector2 pos, int depth)>();
      for (var i = 0; i < origins.Count; i++)
        queue.Enqueue((origins[i], 0));

      var burstCount = 0;
      while (queue.Count > 0 && burstCount < maxBursts)
      {
        var (origin, depth) = queue.Dequeue();
        if (depth >= maxDepth)
          continue;

        if (!TryFindDetonationTarget(registry, visited, origin, baseRadius * 0.85f, out var nextEnemy))
          continue;

        var nextHealth = nextEnemy.GetComponent<Health>();
        if (nextHealth == null || nextHealth.IsDead)
          continue;

        var nextPos = GameplayPlane.Position2D(nextEnemy.transform);
        var burstRadius = baseRadius * 0.38f;
        RangedExplosionDetonateLinkVfx.Spawn(
          new Vector3(origin.x, origin.y, -0.03f),
          new Vector3(nextPos.x, nextPos.y, -0.03f));
        RangedExplosionVfx.Spawn(nextEnemy.transform.position, burstRadius);

        visited.Add(nextHealth);
        var req = DamageRequest.Direct(burstDamage, "physical", "weapon", attacker);
        DamagePipeline.Apply(req, nextHealth);
        burstCount++;

        if (nextHealth.IsDead)
          queue.Enqueue((nextPos, depth + 1));
      }
    }

    static bool TryFindDetonationTarget(
      EnemyRegistry registry,
      HashSet<Health> visited,
      Vector2 origin,
      float range,
      out EnemyCore nextEnemy)
    {
      nextEnemy = null;
      var bestDistSq = range * range;
      foreach (var enemy in registry.GetInRange(origin, range))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || visited.Contains(health))
          continue;
        var distSq = (GameplayPlane.Position2D(enemy.transform) - origin).sqrMagnitude;
        if (distSq > bestDistSq)
          continue;
        bestDistSq = distSq;
        nextEnemy = enemy;
      }
      return nextEnemy != null;
    }

    static void ApplySaturationBursts(
      GameObject attacker,
      EnemyRegistry registry,
      Vector2 center,
      float radius,
      float burstDamage,
      Health primaryTarget)
    {
      var burstCount = 0;
      foreach (var enemy in registry.GetInRange(center, radius * 1.15f))
      {
        if (burstCount >= 3)
          break;
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || health == primaryTarget)
          continue;
        burstCount++;
        RangedExplosionVfx.Spawn(enemy.transform.position, radius * 0.42f);
        var req = DamageRequest.Direct(burstDamage, "physical", "weapon", attacker);
        DamagePipeline.Apply(req, health);
      }
    }

    public static void ApplyChain(
      GameObject attacker,
      Health firstTarget,
      float directDamage,
      in Config config)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || firstTarget == null || config.chainCount <= 0)
        return;

      var visited = new HashSet<Health> { firstTarget };
      var networkNodes = new List<Vector2> { GameplayPlane.Position2D(firstTarget.transform) };
      var currentPos = GameplayPlane.Position2D(firstTarget.transform);
      var jumpRange = config.chainJumpRange > 0f ? config.chainJumpRange : 5.5f;
      var ratio = 0.5f * (1f + Mathf.Max(0f, config.chainDamageRatio));
      var falloff = config.chainFalloffPerJump > 0f ? config.chainFalloffPerJump : 0.92f;
      var chainDamage = directDamage * ratio;

      for (var jump = 0; jump < config.chainCount; jump++)
      {
        var forkCount = config.lightningForkJumps > 0 && jump % 2 == 0 ? 2 : 1;
        for (var fork = 0; fork < forkCount; fork++)
        {
          if (!TryFindNextChainTarget(registry, visited, currentPos, jumpRange, config.lightningConductMark, out var nextEnemy))
            break;

          var nextHealth = nextEnemy.GetComponent<Health>();
          if (nextHealth == null || nextHealth.IsDead)
            break;

          var nextPos = GameplayPlane.Position2D(nextEnemy.transform);
          var isFinalJump = jump == config.chainCount - 1 && fork == forkCount - 1;
          RangedChainLightningVfx.Spawn(
            new Vector3(currentPos.x, currentPos.y, -0.03f),
            new Vector3(nextPos.x, nextPos.y, -0.03f),
            isFinalJump);

          var req = DamageRequest.Direct(chainDamage, "energy", "weapon", attacker);
          DamagePipeline.Apply(req, nextHealth);
          RangedElectricBurstVfx.Spawn(nextHealth.transform.position, isFinalJump);

          if (config.lightningConductMark)
            ProjectileConductMark.Apply(nextHealth);

          visited.Add(nextHealth);
          networkNodes.Add(nextPos);
          currentPos = nextPos;
        }

        chainDamage *= falloff;
      }

      if (config.lightningNetwork && networkNodes.Count >= 2)
        SpawnLightningNetwork(networkNodes);

      CombatDebugHookLocator.Range("chain", "Chain Projectile");
    }

    static void SpawnLightningNetwork(List<Vector2> nodes)
    {
      const int maxLinks = 5;
      var linkCount = 0;
      for (var i = 0; i < nodes.Count - 1 && linkCount < maxLinks; i++)
      {
        var from = nodes[i];
        var to = nodes[i + 1];
        RangedChainLightningVfx.Spawn(
          new Vector3(from.x, from.y, -0.03f),
          new Vector3(to.x, to.y, -0.03f),
          i == nodes.Count - 2);
        linkCount++;
      }
    }

    static bool TryFindNextChainTarget(
      EnemyRegistry registry,
      HashSet<Health> visited,
      Vector2 currentPos,
      float jumpRange,
      bool preferConductive,
      out EnemyCore nextEnemy)
    {
      ProjectileConductMark.Prune();
      if (preferConductive && TryFindConductiveTarget(registry, visited, currentPos, jumpRange, out nextEnemy))
        return true;

      return TryFindNearestChainTarget(registry, visited, currentPos, jumpRange, out nextEnemy);
    }

    static bool TryFindConductiveTarget(
      EnemyRegistry registry,
      HashSet<Health> visited,
      Vector2 currentPos,
      float jumpRange,
      out EnemyCore nextEnemy)
    {
      nextEnemy = null;
      var bestDistSq = jumpRange * jumpRange;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || visited.Contains(health) || !ProjectileConductMark.IsActive(health))
          continue;
        var enemyPos = GameplayPlane.Position2D(enemy.transform);
        var distSq = (enemyPos - currentPos).sqrMagnitude;
        if (distSq > bestDistSq)
          continue;
        bestDistSq = distSq;
        nextEnemy = enemy;
      }
      return nextEnemy != null;
    }

    static bool TryFindNearestChainTarget(
      EnemyRegistry registry,
      HashSet<Health> visited,
      Vector2 currentPos,
      float jumpRange,
      out EnemyCore nextEnemy)
    {
      nextEnemy = null;
      var bestDistSq = jumpRange * jumpRange;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead || visited.Contains(health))
          continue;

        var enemyPos = GameplayPlane.Position2D(enemy.transform);
        var distSq = (enemyPos - currentPos).sqrMagnitude;
        if (distSq > bestDistSq)
          continue;

        bestDistSq = distSq;
        nextEnemy = enemy;
      }

      return nextEnemy != null;
    }

    static void ApplyStatusEffects(GameObject attacker, Health target, in Config config)
    {
      if (target == null)
        return;

      if (config.slowChance > 0f && Random.value < config.slowChance)
      {
        var buff = target.GetComponent<BuffContainer>();
        buff?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
        {
          sourceEntity = attacker,
          sourceKind = "weapon",
          abilityId = "projectile_slow",
          stacks = 1,
          customSlowAmount = config.slowAmount > 0f ? config.slowAmount : 0.3f,
          customDuration = 2f
        });
        RangedSlowPulseVfx.Spawn(target.transform.position, 1f);
      }

      if (config.burnDps > 0f)
      {
        var buff = target.GetComponent<BuffContainer>();
        buff?.ApplyBuff("buff_burn", new BuffContainer.BuffApplyContext
        {
          sourceEntity = attacker,
          sourceKind = "weapon",
          abilityId = "projectile_burn",
          stacks = 1,
          customDps = config.burnDps,
          customDuration = config.burnDuration > 0f ? config.burnDuration : 3f
        });
      }
    }

    public static Config FromBuildMods(in Player.PlayerAttackDirector.ProjectileBuildModifiers mods)
    {
      return new Config
      {
        explosionRadius = mods.explosionRadius,
        explosionDamageRatio = mods.explosionDamageRatio,
        explosionDamageMult = mods.explosionDamageMult,
        explosionVacuum = mods.explosionVacuum,
        chainCount = mods.chainCount,
        chainDamageRatio = mods.chainDamageRatio,
        chainJumpRange = mods.chainJumpRange,
        chainFalloffPerJump = mods.chainFalloffPerJump,
        slowChance = mods.slowChance,
        slowAmount = mods.slowAmount,
        burnDps = mods.burnDps,
        burnDuration = mods.burnDuration,
        explosionSecondaryWave = mods.explosionSecondaryWave,
        explosionFragmentBurst = mods.explosionFragmentBurst,
        explosionChainDetonate = mods.explosionChainDetonate,
        explosionSaturation = mods.explosionSaturation,
        lightningForkJumps = mods.lightningForkJumps,
        lightningConductMark = mods.lightningConductMark,
        lightningNetwork = mods.lightningNetwork
      };
    }
  }
}
