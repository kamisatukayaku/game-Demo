using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.AI;
using Game.Shared.Combat.Damage;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Gameplay.Events;
using Game.Shared.Player;
using Game.Shared.Projectile;
namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>法师专属范围技能：引力井、冰霜护体、火焰新星等?/summary>
  public static class PlayerSkillExecutor
  {
    const float FlameNovaDelay = 3f;
    const float TidalPulseBaseDurationScale = 0.45f;

    public static void ExecuteGravityWell(
      Transform caster,
      Vector2 aimDir,
      float baseRadius,
      float duration,
      float baseDamage)
    {
      var ctx = MageSystemLocator.Context;
      var skills = MageSystemLocator.System;
      if (caster == null)
        return;

      SkillGravityWellZone.Spawn(caster, aimDir, baseRadius, duration, baseDamage);

      // 即时脉冲（skill_pulse_damage?
      var pulse = ctx.SkillPulseDamage;
      if (pulse > 0f)
      {
        var rangeMult = ctx.SkillRangeMult;
        var center = GameplayPlane.Position2D(caster) + aimDir.normalized * 4.5f * rangeMult;
        var registry = CombatRoot.EnemyRegistry;
        if (registry != null)
        {
          foreach (var enemy in registry.GetInRange(center, baseRadius * rangeMult))
          {
            if (enemy == null)
              continue;
            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead)
              continue;
            var req = skills.BuildSkillDamageRequest(pulse, caster.gameObject);
            DamagePipeline.Apply(req, health);
          }
        }
      }
    }

    public static void ExecuteFireNova(
      Transform caster,
      Vector2 aimDir,
      float baseRadius,
      float baseDamage,
      Vector2? targetOverride = null)
    {
      var ctx = MageSystemLocator.Context;
      var skills = MageSystemLocator.System;
      if (caster == null)
        return;

      var rangeMult = ctx.SkillRangeMult;
      var radius = Mathf.Max(1.2f, (baseRadius + ctx.FireRadiusBonus + ctx.SkillExplosionRadius * 0.5f) * rangeMult);
      radius *= 1f + ctx.SkillBurstRadiusBonus;
      var center = targetOverride ?? ResolveMouseTargetOrAimPoint(caster, aimDir, 4.5f * rangeMult);
      var ratio = ctx.SkillExplosionRatio > 0f ? ctx.SkillExplosionRatio : 0.55f;
      var damage = baseDamage * ratio * ctx.FireDamageMult;
      MageFireNovaWindow.Activate(
        caster.gameObject,
        3f + ctx.FireDurationBonus,
        0.2f + ctx.FireDamageAmp);

      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageFlameNovaWarning",
        new Vector3(center.x, center.y, 0f),
        caster.gameObject,
        radius,
        FlameNovaDelay + 0.1f,
        alternate: ctx.SkillVacuum > 0.5f));

      MageDelayedSkillHost.Run(caster.gameObject)
        .StartCoroutine(ResolveFireNovaAfterDelay(caster, center, radius, damage, ctx, skills));
    }

    static IEnumerator ResolveFireNovaAfterDelay(
      Transform caster,
      Vector2 center,
      float radius,
      float damage,
      SkillContext ctx,
      ISkillSystem skills)
    {
      yield return new WaitForSeconds(FlameNovaDelay);

      if (caster == null)
        yield break;

      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageFlameNova",
        new Vector3(center.x, center.y, 0f),
        caster.gameObject,
        radius,
        alternate: ctx.SkillVacuum > 0.5f));

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        yield break;

      var gatheredTargets = 0;
      if (ctx.FireHeatPerTarget > 0f)
      {
        foreach (var enemy in registry.GetInRange(center, radius))
          if (enemy != null && MageZone.Contains(GameplayPlane.Position2D(enemy.transform)))
            gatheredTargets++;
      }
      var heatMultiplier = 1f + Mathf.Min(12, gatheredTargets) * ctx.FireHeatPerTarget;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var targetDamage = damage;
        if (MageZone.Contains(GameplayPlane.Position2D(enemy.transform)))
          targetDamage *= (1f + ctx.GravityFireBonus) * heatMultiplier;
        var req = skills.BuildSkillDamageRequest(targetDamage, caster.gameObject);
        DamagePipeline.Apply(req, health);
        GameEventBus.Publish(new TriggerActivatedEvent(
          "MageFlameNovaHit",
          health.transform.position,
          caster.gameObject,
          1f));

        if (ctx.SkillBurnDps > 0f)
        {
          var buffs = enemy.GetComponent<BuffContainer>();
          buffs?.ApplyBuff("buff_burn", new BuffContainer.BuffApplyContext
          {
            sourceEntity = caster.gameObject,
            sourceKind = "skill",
            abilityId = "mage_fire_nova",
            stacks = 1,
            customDps = ctx.SkillBurnDps,
            customDuration = ctx.SkillBurnDuration
          });
        }
      }
    }

    static Vector2 ResolveMouseTargetOrAimPoint(Transform caster, Vector2 aimDir, float fallbackDistance)
    {
      var origin = GameplayPlane.Position2D(caster);
      var aim = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : Vector2.right;
      var fallback = origin + aim * Mathf.Max(0.1f, fallbackDistance);

      var aimController = PlayerAimController.Instance;
      if (aimController == null)
        return fallback;

      var mousePoint = aimController.AimWorldPoint;
      if ((mousePoint - origin).sqrMagnitude < 0.0004f)
        return fallback;

      return mousePoint;
    }

    public static void ExecuteTidalPulse(Transform caster, float baseRadius, float duration) =>
      ExecuteFrostWard(caster, baseRadius, duration);

    public static void ExecuteFrostWard(Transform caster, float baseRadius, float duration)
    {
      var ctx = MageSystemLocator.Context;
      var skills = MageSystemLocator.System;
      if (caster == null)
        return;

      var radius = Mathf.Clamp(
        (baseRadius + ctx.FrostPulseRadius + ctx.FrostDurationBonus * 0.12f
          + RunBuildState.GetStat("skill_tidal_radius_add")) * ctx.SkillRangeMult,
        6f,
        12f);
      var pulseDuration = Mathf.Clamp(duration * TidalPulseBaseDurationScale + ctx.FrostShieldBonus * 0.9f, 1.2f, 2f);
      var ringInterval = Mathf.Clamp(pulseDuration / 3f - ctx.FrostPulseCooldown * 0.025f, 0.28f, 0.52f);
      var ringTravelTime = Mathf.Clamp(1.05f - ctx.FrostPulseCooldown * 0.045f, 0.75f, 1.2f);
      var ringThickness = Mathf.Clamp(
        0.5f + ctx.FrostRestoreRatio * 0.25f + Mathf.Max(0f, ctx.FrostReflectDamageMult - 1f) * 0.08f,
        0.42f,
        0.9f);
      var damage = Mathf.Max(
        4f,
        5f + ctx.FrostPulseDamage * 0.7f + ctx.FrostShatterDamage * 0.22f + ctx.FrostThorns * 3f)
        * (1f + ctx.TidalDamageMult);
      var slowAmount = Mathf.Clamp01(
        Mathf.Max(0.25f, ctx.SkillSlowAmount)
        + ctx.FrostDamageReduction * 0.75f
        + ctx.FrostReflectChance * 0.12f);
      var slowDuration = Mathf.Max(0.65f, 1.15f + ctx.FrostDurationBonus * 0.18f);
      var freezeChance = Mathf.Clamp01(ctx.SkillSlowChance + ctx.FrostReflectChance * 0.25f);

      MageDelayedSkillHost.Run(caster.gameObject)
        .StartCoroutine(ResolveTidalPulse(
          caster,
          radius,
          damage,
          slowAmount,
          slowDuration,
          freezeChance,
          pulseDuration,
          ringInterval,
          ringTravelTime,
          ringThickness,
          skills,
          ctx));
    }

    static IEnumerator ResolveTidalPulse(
      Transform caster,
      float radius,
      float damage,
      float slowAmount,
      float slowDuration,
      float freezeChance,
      float pulseDuration,
      float ringInterval,
      float ringTravelTime,
      float ringThickness,
      ISkillSystem skills,
      SkillContext ctx)
    {
      const int ringCount = 3;
      var globalHits = ctx.TidalSuccessivePush ? null : new HashSet<Health>();
      for (var ringIndex = 0; ringIndex < ringCount; ringIndex++)
      {
        if (caster == null)
          yield break;

        var center = GameplayPlane.Position2D(caster);
        GameEventBus.Publish(new TriggerActivatedEvent(
          "MageTidalWave",
          new Vector3(center.x, center.y, 0f),
          caster.gameObject,
          radius,
          ringIndex + 1));

        MageDelayedSkillHost.Run(caster.gameObject)
          .StartCoroutine(ResolveTidalPulseRing(
            caster,
            center,
            radius,
            ringTravelTime,
            ringThickness,
            damage,
            slowAmount,
            slowDuration,
            freezeChance,
            skills,
            globalHits,
            ctx));

        if (ringIndex < ringCount - 1)
          yield return new WaitForSeconds(ringInterval);
      }

      if (ctx.TidalBoundary)
      {
        MageTidalBoundaryZone.Spawn(
          GameplayPlane.Position2D(caster),
          radius * 0.92f,
          1.35f,
          3.2f + RunBuildState.GetStat("skill_tidal_knockback_add"));
      }
    }

    static IEnumerator ResolveTidalPulseRing(
      Transform caster,
      Vector2 center,
      float maxRadius,
      float travelTime,
      float thickness,
      float damage,
      float slowAmount,
      float slowDuration,
      float freezeChance,
      ISkillSystem skills,
      HashSet<Health> sharedHits,
      SkillContext ctx)
    {
      var ringHits = sharedHits ?? new HashSet<Health>();
      var deflected = ctx.TidalDeflectProjectiles ? new HashSet<StraightProjectile>() : null;
      var elapsed = 0f;
      var halfWidth = Mathf.Max(0.12f, thickness * 0.5f);
      while (elapsed < travelTime)
      {
        if (caster == null)
          yield break;

        elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(elapsed / travelTime);
        var currentRadius = Mathf.Lerp(0f, maxRadius, 1f - Mathf.Pow(1f - t, 2.1f));
        var registry = CombatRoot.EnemyRegistry;
        if (registry == null)
        {
          yield return null;
          continue;
        }

        if (ctx.TidalDeflectProjectiles)
          DeflectEnemyProjectilesAtRing(center, currentRadius, halfWidth, deflected);

        if (ctx.TidalSafeZone && currentRadius > maxRadius * 0.35f)
          PushInnerSafeZone(center, currentRadius * 0.42f);

        foreach (var enemy in registry.GetInRange(center, currentRadius + halfWidth))
        {
          if (enemy == null)
            continue;

          var dist = Vector2.Distance(center, GameplayPlane.Position2D(enemy.transform));
          if (Mathf.Abs(dist - currentRadius) > halfWidth)
            continue;

          var health = enemy.GetComponent<Health>();
          if (health != null && !health.IsDead && ringHits.Add(health))
          {
            var req = skills.BuildSkillDamageRequest(damage, caster.gameObject);
            DamagePipeline.Apply(req, health);

            if (ctx.TidalInterruptDash)
              enemy.GetComponent<EnemyMovement>()?.ApplyHitStun();

            var buffs = enemy.GetComponent<BuffContainer>();
            buffs?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
            {
              sourceEntity = caster.gameObject,
              sourceKind = "skill",
              abilityId = "mage_tidal_pulse",
              stacks = 1,
              customSlowAmount = slowAmount,
              customDuration = slowDuration
            });

            if (freezeChance > 0f && Random.value < freezeChance)
              enemy.GetComponent<EnemyMovement>()?.ApplyHitStun();

            var movement = enemy.GetComponent<EnemyMovement>();
            if (movement != null)
            {
              var outward = GameplayPlane.Position2D(enemy.transform) - center;
              if (outward.sqrMagnitude > 0.001f)
                movement.ApplyKnockback(outward.normalized
                  * (4.5f + RunBuildState.GetStat("skill_tidal_knockback_add")));
            }

            GameEventBus.Publish(new TriggerActivatedEvent(
              "MageTidalHit",
              enemy.transform.position,
              enemy.gameObject,
              1f));
          }
        }

        yield return null;
      }
    }

    static void DeflectEnemyProjectilesAtRing(
      Vector2 center,
      float currentRadius,
      float halfWidth,
      HashSet<StraightProjectile> deflected)
    {
      if (deflected == null)
        return;

      var buffer = new List<StraightProjectile>();
      ActiveProjectileRegistry.CopyActive(buffer);
      for (var i = 0; i < buffer.Count; i++)
      {
        var projectile = buffer[i];
        if (projectile == null || !projectile.IsMonsterProjectile || deflected.Contains(projectile))
          continue;

        var dist = Vector2.Distance(center, GameplayPlane.Position2D(projectile.transform));
        if (Mathf.Abs(dist - currentRadius) > halfWidth)
          continue;

        projectile.DeflectAwayFrom(center);
        deflected.Add(projectile);
      }
    }

    static void PushInnerSafeZone(Vector2 center, float innerRadius)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(center, innerRadius))
      {
        if (enemy == null)
          continue;

        var pos = GameplayPlane.Position2D(enemy.transform);
        if (Vector2.Distance(pos, center) > innerRadius)
          continue;

        var outward = pos - center;
        if (outward.sqrMagnitude < 0.0001f)
          outward = Vector2.up;
        enemy.GetComponent<EnemyMovement>()?.ApplyKnockback(outward.normalized * 2.8f);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFireNovaWindow : MonoBehaviour, ISkillDamageMultiplierProvider
  {
    float _until;
    float _damageAmp;

    public float SkillDamageMultiplier => Time.time < _until ? 1f + _damageAmp : 1f;

    public static void Activate(GameObject owner, float duration, float damageAmp)
    {
      if (owner == null)
        return;
      var state = owner.GetComponent<MageFireNovaWindow>() ?? owner.AddComponent<MageFireNovaWindow>();
      state._until = Mathf.Max(state._until, Time.time + Mathf.Max(0.1f, duration));
      state._damageAmp = Mathf.Max(state._damageAmp, damageAmp);
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageDelayedSkillHost : MonoBehaviour
  {
    public static MageDelayedSkillHost Run(GameObject owner)
    {
      if (owner == null)
        return null;
      return owner.GetComponent<MageDelayedSkillHost>() ?? owner.AddComponent<MageDelayedSkillHost>();
    }
  }
}
