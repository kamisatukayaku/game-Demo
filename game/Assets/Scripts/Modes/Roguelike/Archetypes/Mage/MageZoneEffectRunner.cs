using System.Collections.Generic;
using UnityEngine;

using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Combat.Damage;
using Game.Shared.Projectile;
using Game.Shared.Enemy.AI;
using Game.Shared.Runtime.Physics;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  public sealed class MageZoneEffectRunner
  {
    static readonly List<StraightProjectile> s_projectileBuffer = new();

    public void UpdatePullMotion(Vector2 center, float radius, SkillContext ctx, float deltaTime, float overlapAmp = 1f)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || deltaTime <= 0f)
        return;

      var pullBase = (3.2f + ctx.SkillVacuumStrength * 5.5f) * overlapAmp;
      if (ctx.SkillVacuum > 0.5f)
        pullBase += 1.4f;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;

        var pos = GameplayPlane.Position2D(enemy.transform);
        var toCenter = center - pos;
        var dist = toCenter.magnitude;
        if (dist > radius || dist < 0.08f)
          continue;

        var t = 1f - Mathf.Clamp01(dist / radius);
        var pullSpeed = pullBase * Mathf.Lerp(0.4f, 0.95f, t);
        var delta = toCenter.normalized * Mathf.Min(dist, pullSpeed * deltaTime);
        var physics = enemy.GetComponent<EntityPhysicsBody>();
        if (physics != null)
          physics.QueuePlanarMove(delta);
        else
          GameplayPlane.SetPosition2D(enemy.transform, pos + delta);
      }
    }

    public void TickPull(
      Transform caster,
      Vector2 center,
      float radius,
      float baseDamage,
      SkillContext ctx,
      ISkillSystem skills,
      float tickInterval,
      ref float rampTime,
      float overlapAmp = 1f)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      rampTime += tickInterval;
      if (ctx.GravityProjectilePull > 0f)
        PullNearbyProjectiles(center, radius, ctx.GravityProjectilePull * overlapAmp, tickInterval);

      var ramp = ctx.SkillVacuumRampDamage > 0.5f ? 1f + rampTime * 0.15f : 1f;
      var vuln = ctx.SkillVulnerableBonus;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;

        var pos = GameplayPlane.Position2D(enemy.transform);
        var dist = Vector2.Distance(pos, center);
        if (dist > radius || dist < 0.05f)
          continue;

        if (Random.value < 0.18f)
          GameEventBus.Publish(new TriggerActivatedEvent(
            "MageGravityTether",
            new Vector3(center.x, center.y, 0f),
            enemy.gameObject,
            1f));

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var tickDmg = baseDamage * 0.02f * (1f + ctx.GravityDamageMult) * ramp;
        if (ctx.SkillPctHpDamage > 0f)
          tickDmg += health.MaxHp * ctx.SkillPctHpDamage * tickInterval;
        if (vuln > 0f)
          tickDmg *= 1f + vuln;

        if (tickDmg > 0.01f)
        {
          var req = skills.BuildSkillDamageRequest(tickDmg, caster != null ? caster.gameObject : null);
          DamagePipeline.Apply(req, health);
        }
      }
    }

    static void PullNearbyProjectiles(Vector2 center, float radius, float strength, float deltaTime)
    {
      ActiveProjectileRegistry.CopyActive(s_projectileBuffer);
      var pullRadiusSq = radius * radius;
      for (var i = 0; i < s_projectileBuffer.Count; i++)
      {
        var projectile = s_projectileBuffer[i];
        if (projectile == null)
          continue;

        var pos = GameplayPlane.Position2D(projectile.transform);
        if ((pos - center).sqrMagnitude > pullRadiusSq)
          continue;

        projectile.ApplyGravityPull(
          new Vector3(center.x, center.y, projectile.transform.position.z),
          strength,
          deltaTime);
      }
    }

    public void Collapse(
      Transform caster,
      Vector2 center,
      float radius,
      float baseDamage,
      SkillContext ctx,
      ISkillSystem skills)
    {
      var collapse = ctx.SkillCollapseExplosion > 0.5f || ctx.SkillZoneCollapse > 0.5f;
      if (!collapse && ctx.SkillVacuum <= 0.5f)
        return;

      var blastRadius = radius * (ctx.SkillZoneCollapse > 0.5f ? 1.45f : 0.85f);
      blastRadius *= 1f + ctx.SkillCollapseRadiusBonus;
      blastRadius *= 1f + ctx.SkillBurstRadiusBonus;

      var ratio = ctx.SkillExplosionRatio > 0f ? ctx.SkillExplosionRatio : 0.65f;
      var damage = baseDamage * ratio * 2.2f * (1f + ctx.GravityDamageMult * 0.7f);

      GameEventBus.Publish(new TriggerActivatedEvent(
        "Explosion",
        new Vector3(center.x, center.y, 0f),
        caster != null ? caster.gameObject : null,
        blastRadius,
        alternate: true));

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(center, blastRadius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var req = skills.BuildSkillDamageRequest(damage, caster != null ? caster.gameObject : null);
        DamagePipeline.Apply(req, health);
      }
    }
  }
}
