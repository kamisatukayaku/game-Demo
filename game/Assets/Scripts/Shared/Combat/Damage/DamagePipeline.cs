using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Events;
using HealthComp = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Gameplay.Events;
using Game.Shared.Player;
using Game.Shared.Stats;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Reflect;
namespace Game.Shared.Combat.Damage
{
  public interface IIncomingDamageModifier
  {
    float ModifyIncomingDamage(float damage);
  }

  public interface ISkillDamageMultiplierProvider
  {
    float SkillDamageMultiplier { get; }
  }
  /// <summary>
  /// 统一伤害结算：基础 ?Buff ?类型抗??护甲 ?额外加成 ?最终伤害?
  /// </summary>
  public static class DamagePipeline
  {
    public static bool DebugLog { get; set; }

    /// <summary>完整结算并扣血?/summary>
    public static DamageResult Apply(in DamageRequest request, HealthComp target)
    {
      if (target == null || target.IsDead || request.Base <= 0f)
        return new DamageResult { FinalDamage = 0f, Breakdown = new DamageBreakdown() };

      // 0. Fire PreDamage event (for interceptors)
      CombatEventBus.FirePreDamage(request.Attacker, target.gameObject, request);

      if (target.CompareTag("Player")
          && PlayerReflectGateHelper.TryFullyBlockIncoming(target.gameObject, request.Attacker, request.Base, out var reflectedDamage))
      {
        if (reflectedDamage > 0f && request.Attacker != null)
        {
          var attackerHealth = request.Attacker.GetComponent<HealthComp>();
          if (attackerHealth != null && !attackerHealth.IsDead)
          {
            var reflectReq = DamageRequest.Direct(
              CombatStatProviderLocator.Provider.ComputeReflectDamage(reflectedDamage),
              "physical",
              "reflect",
              target.gameObject);
            Apply(reflectReq, attackerHealth);
          }
        }

        var blockedResult = new DamageResult { FinalDamage = 0f, Breakdown = new DamageBreakdown() };
        CombatEventBus.FirePostDamage(request.Attacker, target.gameObject, request, blockedResult);
        return blockedResult;
      }

      // 0.5. Consume shield from target's BuffContainer
      var requestWithShield = request;
      foreach (var modifier in target.GetComponents<IIncomingDamageModifier>())
        requestWithShield.Base = Mathf.Max(0f, modifier.ModifyIncomingDamage(requestWithShield.Base));

      requestWithShield.Base = BossDamageMitigation.ApplyIfBossTarget(
        target.gameObject,
        requestWithShield.Base,
        requestWithShield.DamageSourceId);
      var buffContainer = target.GetComponent<BuffContainer>();
      if (buffContainer != null)
      {
        float remaining = buffContainer.ConsumeShield(requestWithShield.Base);
        if (remaining <= 0f)
        {
          var zeroResult = new DamageResult { FinalDamage = 0f, Breakdown = new DamageBreakdown() };
          CombatEventBus.FirePostDamage(request.Attacker, target.gameObject, requestWithShield, zeroResult);
          return zeroResult;
        }

        if (remaining < requestWithShield.Base)
        {
          requestWithShield.Base = remaining;
        }
      }

      // 1. Resolve and apply damage
      var result = Resolve(requestWithShield, target);
      var dealtDamage = result.FinalDamage;
      if (!target.IsDead && dealtDamage > 0f)
      {
        if (target.IsInvulnerable)
        {
          dealtDamage = 0f;
        }
        else
        {
          // Track last attacker for kill attribution
          target.LastAttacker = request.Attacker;
          target.SetLastDamageSource(request.DamageSourceId);
          target.TakeDamage(dealtDamage);
        }
      }

      if (dealtDamage != result.FinalDamage)
        result.FinalDamage = dealtDamage;

      // Fire PostDamage event
      CombatEventBus.FirePostDamage(request.Attacker, target.gameObject, requestWithShield, result);

      if (dealtDamage > 0f)
      {
        GameEventBus.Publish(new DamageDealtEvent(
          request.Attacker,
          target.gameObject,
          dealtDamage,
          requestWithShield.DamageSourceId,
          requestWithShield.DamageTypeId,
          result.WasCritical));

        if (result.WasCritical)
        {
          GameEventBus.Publish(new CriticalHitEvent(
            request.Attacker,
            target.gameObject,
            dealtDamage,
            requestWithShield.AttackProfileId));
        }
      }

      // Fire AttackHit + apply on_hit_buffs (trigger even if damage reduced to 0 by shield/armor)
      if (result.FinalDamage >= 0f && !target.IsDead && !string.IsNullOrEmpty(request.AttackProfileId))
      {
        var profile = AttackProfileDatabase.Get(request.AttackProfileId);
        var onHitBuffs = profile?.on_hit_buffs ?? new string[0];

        CombatEventBus.FireAttackHit(new CombatEventBus.AttackHitArgs
        {
          Attacker = request.Attacker,
          Target = target.gameObject,
          Damage = result.FinalDamage,
          AttackProfileId = request.AttackProfileId,
          OnHitBuffIds = onHitBuffs
        });

        // Apply on_hit_buffs to target
        if (onHitBuffs.Length > 0)
        {
          var targetContainer = target.GetComponent<BuffContainer>();
          if (targetContainer != null)
          {
            foreach (var buffId in onHitBuffs)
            {
              targetContainer.ApplyBuff(buffId, new BuffContainer.BuffApplyContext
              {
                sourceEntity = request.Attacker,
                sourceKind = request.DamageSourceId,
                abilityId = request.AttackProfileId,
                stacks = 1
              });
            }
          }
        }
      }

      return result;
    }

    /// <summary>仅计算，不扣血?/summary>
    public static DamageResult Resolve(in DamageRequest request, HealthComp target)
    {
      var breakdown = new DamageBreakdown();
      var result = new DamageResult { Breakdown = breakdown };

      if (target == null || target.IsDead || request.Base <= 0f)
        return result;

      DamageTypesCatalog.EnsureLoaded();

      // 1. 基础伤害
      var value = Mathf.Max(0f, request.Base);
      breakdown.AfterBase = value;

      // 2. Buff 修正
      var outgoing = BuffDamageModifiers.GetOutgoingMultiplier(request.Attacker);
      var incoming = BuffDamageModifiers.GetIncomingMultiplier(target);
      value *= outgoing * incoming;
      breakdown.AfterBuff = value;

      // 3. 伤害类型 / 抗怀"
      var receiver = target.GetComponent<DamageReceiver>();
      var resistMult = receiver != null
        ? receiver.GetResistanceMultiplier(request.DamageTypeId)
        : 1f;
      value *= resistMult;
      breakdown.AfterResistance = value;

      // 4. 减伤 / 护甲
      var typeDef = DamageTypesCatalog.Get(request.DamageTypeId);
      var bypassArmor = typeDef != null && typeDef.bypass_armor;
      if (!bypassArmor && receiver != null)
      {
        var buffContainer = target.GetComponent<BuffContainer>();
        float armorBonus = buffContainer != null ? buffContainer.GetStatAdd("armor") : 0f;
        value = receiver.ApplyArmorReduction(value, request.DamageTypeId, armorBonus);
      }

      breakdown.AfterArmor = value;

      // 5. 额外加成（属性、环境、暴击）
      var statMult = Mathf.Approximately(request.AttackerStatMult, 0f) ? 1f : request.AttackerStatMult;
      value *= statMult;

      var envMult = Mathf.Approximately(request.EnvironmentMult, 0f) ? 1f : request.EnvironmentMult;
      value *= envMult;

      if (request.CritChance > 0f && Random.value < request.CritChance)
      {
        result.WasCritical = true;
        var critMult = request.CritDamageMult > 0f ? request.CritDamageMult : 1.5f;
        value *= critMult;
      }

      value += request.AttackerFlatBonus;
      breakdown.AfterBonuses = value;

      if (target.CompareTag("Player"))
      {
        var provider = CombatStatProviderLocator.Provider;

        // 5a. 固定防御减法：伤害先减去防御值
        var defense = provider.Defense;
        if (defense > 0f)
        {
          value = Mathf.Max(0f, value - defense);
          breakdown.AfterDefense = value;
        }

        // 5b. 百分比伤害减免
        var reduction = provider.DamageReduction;
        if (reduction > 0f)
          value *= 1f - reduction;

        var allResist = Mathf.Clamp01(provider.AllResist);
        if (allResist > 0f)
          value *= 1f - allResist;
      }

      // 5.5. 敌人减伤（营地核心等外部驱动）
      var ec = target.GetComponent<EnemyCore>();
      if (ec != null && ec.DefenseRatio > 0f)
        value *= (1f - ec.DefenseRatio);

      // 6. 最终伤害"
      value = Mathf.Max(0f, value);
      value = Mathf.Round(value * 10f) / 10f;
      breakdown.Final = value;
      result.FinalDamage = value;
      result.Breakdown = breakdown;

      if (DebugLog)
      {
        Debug.Log(
          $"[DamagePipeline] {request.DamageSourceId}/{request.DamageTypeId} " +
          $"base={breakdown.AfterBase:F1} buff={breakdown.AfterBuff:F1} " +
          $"res={breakdown.AfterResistance:F1} armor={breakdown.AfterArmor:F1} " +
          $"bonus={breakdown.AfterBonuses:F1} final={breakdown.Final:F1}" +
          (result.WasCritical ? " CRIT" : ""));
      }

      return result;
    }
  }
}
