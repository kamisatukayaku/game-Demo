using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Stats;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  public sealed class MageEffectRunner
  {
    const float DilationTick = 0.35f;

    float _dilationTimer;
    float _timeStopUntil;
    bool _reactionHandled;
    PlayerActiveSkillController _skills;

    public GameObject Owner { get; private set; }

    public void Bind(GameObject owner, PlayerActiveSkillController skills)
    {
      Owner = owner;
      _skills = skills;
    }

    public void Tick(SkillContext ctx, Transform owner, float deltaTime)
    {
      if (Time.time < _timeStopUntil)
        TickTimeStop(ctx, owner);

      _dilationTimer -= deltaTime;
      if (_dilationTimer <= 0f)
      {
        _dilationTimer = DilationTick;
        foreach (var behavior in Behaviors.MageBehaviorRegistry.All)
          behavior.Tick(this, ctx, owner, deltaTime);
      }
    }

    public void OnSkillCast(SkillContext ctx, Transform owner, int slotIndex)
    {
      foreach (var behavior in Behaviors.MageBehaviorRegistry.All)
        behavior.OnSkillCast(this, ctx, owner, slotIndex);
    }

    public void OnPostSkillDamage(SkillContext ctx, GameObject attacker, GameObject target, float damage)
    {
      _reactionHandled = false;
      foreach (var behavior in Behaviors.MageBehaviorRegistry.All)
      {
        if (_reactionHandled)
          break;
        behavior.OnPostSkillDamage(this, ctx, attacker, target, damage);
      }
    }

    public void OnKill(SkillContext ctx, GameObject killer)
    {
      foreach (var behavior in Behaviors.MageBehaviorRegistry.All)
        behavior.OnKill(this, ctx, killer);
    }

    public void MarkReactionHandled() => _reactionHandled = true;

    public void TriggerTimeStop()
    {
      _timeStopUntil = Time.time + 0.55f;
      CombatDebugHookLocator.Mage("time_stop", "Time Stop");
    }

    public void ResetCooldown(int slot) => _skills?.ResetCooldown(slot);

    public void ResetAllCooldowns()
    {
      _skills?.ResetAllCooldowns();
      CombatDebugHookLocator.Mage("cooldown_reset", "Cooldown Reset");
    }

    public void TickTimeDilationField(SkillContext ctx, Transform owner)
    {
      if (ctx.SkillTimeDilationField <= 0.5f)
        return;

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var center = GameplayPlane.Position2D(owner);
      foreach (var enemy in registry.GetInRange(center, ctx.TimeDilationRadius))
      {
        if (enemy == null)
          continue;
        var buffs = enemy.GetComponent<BuffContainer>();
        buffs?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
        {
          sourceEntity = owner.gameObject,
          sourceKind = "skill",
          abilityId = "skill_time_dilation",
          stacks = 1,
          customSlowAmount = 0.35f,
          customDuration = DilationTick + 0.1f
        });
      }
    }

    public void TickTimeStop(SkillContext ctx, Transform owner)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var center = GameplayPlane.Position2D(owner);
      foreach (var enemy in registry.GetInRange(center, 12f))
      {
        if (enemy == null)
          continue;
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
          rb.velocity = Vector2.zero;
        var buffs = enemy.GetComponent<BuffContainer>();
        buffs?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
        {
          sourceEntity = owner.gameObject,
          sourceKind = "skill",
          abilityId = "skill_time_stop",
          stacks = 1,
          customSlowAmount = 0.98f,
          customDuration = 0.12f
        });
      }
    }

    public void ApplyAreaSkillDamage(
      GameObject attacker,
      Transform target,
      float radius,
      float damage,
      bool spawnExplosion)
    {
      MarkReactionHandled();
      var center = GameplayPlane.Position2D(target);
      if (spawnExplosion)
        GameEventBus.Publish(new TriggerActivatedEvent(
          "Explosion",
          target.position,
          attacker,
          radius * 0.5f));

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var req = RunBuildCombatHooks.BuildSkillDamageRequest(damage, attacker);
        DamagePipeline.Apply(req, health);
      }
    }

    public void ChainFromTarget(GameObject attacker, GameObject primary, float damage, int jumps)
    {
      MarkReactionHandled();
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || primary == null || jumps <= 0)
        return;

      var visited = new HashSet<GameObject> { primary };
      var current = primary;
      for (var i = 0; i < jumps; i++)
      {
        var center = GameplayPlane.Position2D(current.transform);
        EnemyCore next = null;
        var bestDist = 5.5f;
        foreach (var enemy in registry.GetInRange(center, bestDist))
        {
          if (enemy == null || visited.Contains(enemy.gameObject))
            continue;
          var d = Vector2.Distance(center, GameplayPlane.Position2D(enemy.transform));
          if (d < bestDist)
          {
            bestDist = d;
            next = enemy;
          }
        }

        if (next == null)
          break;

        visited.Add(next.gameObject);
        var health = next.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
          var req = RunBuildCombatHooks.BuildSkillDamageRequest(
            damage * Mathf.Pow(0.88f, i), attacker);
          DamagePipeline.Apply(req, health);
        }

        current = next.gameObject;
      }
    }
  }
}
