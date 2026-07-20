using System.Collections.Generic;
using System;
using UnityEngine;

using Game.Shared.Combat.Damage;
using HealthComp = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Combat.Events;
namespace Game.Shared.Combat.Buff
{
  /// <summary>
  /// Buff/Debuff 运行时容器。挂载在拥有 HealthComp ?GameObject 上?
  /// 支持施加、叠层、Tick、属性修正汇总、护盾吸收?
  /// </summary>
  [DisallowMultipleComponent]
  public class BuffContainer : MonoBehaviour
  {
    [SerializeField] bool debugLog;

    public struct BuffApplyContext
    {
      public GameObject sourceEntity;
      public string sourceKind;
      public string abilityId;
      public int stacks;
      public float durationOverride;
      public string zoneId;
      // v2: 构筑效果自定义参敀"
      public float customSlowAmount;
      public float customDps;
      public float customDuration;
      public float customShieldAmount;
    }

    public class BuffInstance
    {
      public string defId;
      public int stacks;
      public float remaining;
      public float appliedAt;
      public BuffApplyContext context;
      public float tickTimer;
      /// <summary>护盾剩余量；-1 表示尚未初始化（?def 读取）?/summary>
      public float shieldRemaining = -1f;

      public bool IsExpired => remaining <= 0f;
      public BuffDatabase.BuffDef Def => BuffDatabase.Get(defId);
    }

    readonly List<BuffInstance> _activeBuffs = new();
    readonly List<BuffInstance> _toRemove = new();
    HealthComp _health;

    public IReadOnlyList<BuffInstance> ActiveBuffs => _activeBuffs;

    void Awake()
    {
      _health = GetComponent<HealthComp>();
    }

    /// <summary>懒加?HealthComp 引用，兼?Editor 模式初始化顺序问题?/summary>
    HealthComp GetOrFindHealth()
    {
      if (_health == null) _health = GetComponent<HealthComp>();
      return _health;
    }

    void Update()
    {
      Tick(Time.deltaTime);
    }

    /// <summary>施加一?Buff。按 stack_policy 处理叠层?/summary>
    public bool ApplyBuff(string buffId, BuffApplyContext context = default)
    {
      var def = BuffDatabase.Get(buffId);
      if (def == null)
      {
        if (debugLog)
          Debug.LogWarning($"[BuffContainer] Buff '{buffId}' not found in database.", this);
        return false;
      }

      var existing = FindInstance(buffId);

      if (existing != null)
      {
        switch (def.stack_policy)
        {
          case BuffDatabase.StackPolicy.refresh_duration:
            existing.remaining = GetEffectiveDuration(def, context);
            existing.stacks = Mathf.Min(existing.stacks + context.stacks, def.max_stacks);
            existing.context = context;
            InitShieldFromDef(existing, def);
            break;

          case BuffDatabase.StackPolicy.add_duration:
            existing.remaining += GetEffectiveDuration(def, context);
            existing.stacks = Mathf.Min(existing.stacks + context.stacks, def.max_stacks);
            existing.context = context;
            break;

          case BuffDatabase.StackPolicy.independent:
            AddNewInstance(def, context);
            break;

          case BuffDatabase.StackPolicy.replace:
            existing.stacks = Mathf.Min(context.stacks, def.max_stacks);
            existing.remaining = GetEffectiveDuration(def, context);
            existing.context = context;
            break;
        }
      }
      else
      {
        AddNewInstance(def, context);
      }

      return true;
    }

    /// <summary>移除指定 ID 的所?Buff 实例?/summary>
    public void RemoveBuff(string buffId)
    {
      for (int i = _activeBuffs.Count - 1; i >= 0; i--)
      {
        if (_activeBuffs[i].defId == buffId)
          RemoveInstanceAt(i);
      }
    }

    /// <summary>?tag 移除所有匹配的 Buff?/summary>
    public void RemoveBuffsByTag(string tag)
    {
      for (int i = _activeBuffs.Count - 1; i >= 0; i--)
      {
        var def = _activeBuffs[i].Def;
        if (def?.tags != null && Array.IndexOf(def.tags, tag) >= 0)
          RemoveInstanceAt(i);
      }
    }

    /// <summary>?zone_id 移除 until_leave_zone ?Buff?/summary>
    public void RemoveBuffsByZone(string zoneId)
    {
      for (int i = _activeBuffs.Count - 1; i >= 0; i--)
      {
        var inst = _activeBuffs[i];
        var def = inst.Def;
        if (def != null && def.duration_policy == BuffDatabase.DurationPolicy.until_leave_zone
            && inst.context.zoneId == zoneId)
          RemoveInstanceAt(i);
      }
    }

    /// <summary>每帧 Tick：减少持续时间，触发周期效果，清理过期?/summary>
    public void Tick(float dt)
    {
      var hp = GetOrFindHealth();
      if (hp == null || hp.IsDead) return;

      _toRemove.Clear();

      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def == null) { _toRemove.Add(inst); continue; }

        // Duration
        if (def.duration_policy == BuffDatabase.DurationPolicy.timed)
        {
          inst.remaining -= dt;
        }

        // Tick effects ?handle multiple intervals within a single Tick(dt)
        inst.tickTimer -= dt;
        int safety = 100; // prevent infinite loop if interval is 0 or negative
        while (inst.tickTimer < 0f && def.effects != null && safety-- > 0)
        {
          foreach (var effect in def.effects)
          {
            if (effect.ParsedType == BuffDatabase.EffectType.tick_damage)
              ApplyTickDamage(inst, effect);
            else if (effect.ParsedType == BuffDatabase.EffectType.tick_heal)
              ApplyTickHeal(inst, effect);
          }

          // Add interval back to tickTimer (instead of resetting to it)
          // so that multiple pending ticks are handled correctly.
          float minInterval = float.MaxValue;
          foreach (var e in def.effects)
          {
            if ((e.ParsedType == BuffDatabase.EffectType.tick_damage
                 || e.ParsedType == BuffDatabase.EffectType.tick_heal)
                && e.interval > 0f)
              minInterval = Mathf.Min(minInterval, e.interval);
          }
          if (minInterval < float.MaxValue)
            inst.tickTimer += minInterval;
          else
          {
            inst.tickTimer = 999f;
            break;
          }
        }

        if (def.duration_policy == BuffDatabase.DurationPolicy.timed && inst.remaining <= 0f)
          _toRemove.Add(inst);
      }

      foreach (var inst in _toRemove)
      {
        CombatEventBus.FireBuffExpired(gameObject, inst.defId);
        _activeBuffs.Remove(inst);
      }
    }

    /// <summary>汇总所?Buff 对指定属性的修正值?
    /// 聚合顺序：override（最?priority）→ add（求和）?multiply（连乘）?/summary>
    public float GetStatModifier(string stat)
    {
      if (string.IsNullOrEmpty(stat)) return 1f;

      float overrideValue = float.NaN;
      int overridePriority = int.MinValue;
      float addSum = 0f;
      float multProduct = 1f;

      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def?.effects == null) continue;

        foreach (var effect in def.effects)
        {
          if (effect.ParsedType != BuffDatabase.EffectType.stat_mod) continue;
          if (!string.Equals(effect.stat, stat, StringComparison.OrdinalIgnoreCase)) continue;

          float effectiveValue = effect.per_stack ? effect.value * inst.stacks : effect.value;
          if (string.Equals(stat, "move_speed", StringComparison.OrdinalIgnoreCase)
              && inst.defId == "buff_slow_debuff"
              && inst.context.customSlowAmount > 0f)
          {
            effectiveValue = -inst.context.customSlowAmount;
          }

          if (string.Equals(stat, "attack", StringComparison.OrdinalIgnoreCase)
              && inst.defId == "buff_aura_weaken"
              && inst.context.customSlowAmount > 0f)
          {
            effectiveValue = -inst.context.customSlowAmount;
          }

          switch (effect.op)
          {
            case "override":
              if (def.priority > overridePriority)
              {
                overrideValue = effectiveValue;
                overridePriority = def.priority;
              }
              break;
            case "add":
              addSum += effectiveValue;
              break;
            case "multiply":
            case "mul":
              multProduct *= effect.op == "mul"
                ? Mathf.Max(0.05f, 1f + effectiveValue)
                : effectiveValue;
              break;
          }
        }
      }

      if (!float.IsNaN(overrideValue)) return overrideValue;
      return (1f + addSum) * multProduct;
    }

    /// <summary>获取原始 add 加算值（用于 armor 等）?/summary>
    public float GetStatAdd(string stat)
    {
      if (string.IsNullOrEmpty(stat)) return 0f;
      float sum = 0f;
      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def?.effects == null) continue;
        foreach (var effect in def.effects)
        {
          if (effect.ParsedType != BuffDatabase.EffectType.stat_mod) continue;
          if (!string.Equals(effect.stat, stat, StringComparison.OrdinalIgnoreCase)) continue;
          if (effect.op != "add") continue;
          sum += effect.per_stack ? effect.value * inst.stacks : effect.value;
        }
      }
      return sum;
    }

    /// <summary>检查是否拥有指?rule flag?/summary>
    public bool HasRuleFlag(string flag)
    {
      if (string.IsNullOrEmpty(flag)) return false;
      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def?.effects == null) continue;
        foreach (var effect in def.effects)
        {
          if (effect.ParsedType == BuffDatabase.EffectType.grant_rule
              && effect.rule_flag == flag)
            return true;
        }
      }
      return false;
    }

    public bool HasBuff(string buffId)
    {
      if (string.IsNullOrEmpty(buffId))
        return false;
      return FindInstance(buffId) != null;
    }

    public bool HasShieldEffect()
    {
      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def?.effects == null)
          continue;

        foreach (var effect in def.effects)
        {
          if (effect.ParsedType != BuffDatabase.EffectType.shield)
            continue;

          var amount = inst.shieldRemaining >= 0f
            ? inst.shieldRemaining
            : (effect.per_stack ? effect.value * inst.stacks : effect.value);
          if (amount > 0f)
            return true;
        }
      }

      return false;
    }

    /// <summary>检查是否受到减速效果（任意 stat_mod 降低 move_speed）?/summary>
    public bool HasSlowEffect()
    {
      foreach (var inst in _activeBuffs)
      {
        var def = inst.Def;
        if (def?.effects == null) continue;
        foreach (var effect in def.effects)
        {
          if (effect.ParsedType == BuffDatabase.EffectType.stat_mod
              && effect.stat == "move_speed"
              && (effect.op == "mul" && effect.value < 0f || effect.op == "add" && effect.value < 0f))
            return true;
        }
      }
      return false;
    }

    /// <summary>消费护盾，返回护盾吸收后剩余的伤害?/summary>
    public float ConsumeShield(float damage)
    {
      float remainingDamage = damage;
      for (int i = _activeBuffs.Count - 1; i >= 0; i--)
      {
        var inst = _activeBuffs[i];
        var def = inst.Def;
        if (def?.effects == null) continue;

        foreach (var effect in def.effects)
        {
          if (effect.ParsedType != BuffDatabase.EffectType.shield) continue;

          float shieldAmount = GetShieldAmount(inst, effect);
          if (shieldAmount <= 0f) continue;

          if (shieldAmount >= remainingDamage)
          {
            inst.shieldRemaining = shieldAmount - remainingDamage;
            remainingDamage = 0f;
            if (inst.shieldRemaining <= 0f)
              RemoveInstanceAt(i);
            return 0f;
          }

          remainingDamage -= shieldAmount;
          RemoveInstanceAt(i);
          // 移除后原 i+1 位置的元素移到了 i，for 循环?i-- 会跳过它
          // 补偿 i++，让循环重新检查当前位置的元素
          if (i < _activeBuffs.Count)
            i++;
          break;
        }

        if (remainingDamage <= 0f) break;
      }

      return remainingDamage;
    }

    // ── Private ──────────────────────────────────────

    void AddNewInstance(BuffDatabase.BuffDef def, BuffApplyContext context)
    {
      var instance = new BuffInstance
      {
        defId = def.id,
        stacks = Mathf.Max(1, context.stacks),
        remaining = GetEffectiveDuration(def, context),
        appliedAt = Time.time,
        context = context,
        tickTimer = 0f // tick immediately on next frame
      };

      InitShieldFromDef(instance, def);
      _activeBuffs.Add(instance);
      CombatEventBus.FireBuffApplied(gameObject, def.id, context);
    }

    static void InitShieldFromDef(BuffInstance instance, BuffDatabase.BuffDef def)
    {
      if (def?.effects == null) return;
      foreach (var effect in def.effects)
      {
        if (effect.ParsedType != BuffDatabase.EffectType.shield) continue;
        instance.shieldRemaining = instance.context.customShieldAmount > 0f
          ? instance.context.customShieldAmount
          : (effect.per_stack ? effect.value * instance.stacks : effect.value);
        return;
      }
    }

    static float GetShieldAmount(BuffInstance inst, BuffDatabase.BuffEffect effect)
    {
      if (inst.shieldRemaining >= 0f)
        return inst.shieldRemaining;
      return effect.per_stack ? effect.value * inst.stacks : effect.value;
    }

    void RemoveInstanceAt(int index)
    {
      var inst = _activeBuffs[index];
      CombatEventBus.FireBuffRemoved(gameObject, inst.defId);
      _activeBuffs.RemoveAt(index);
    }

    BuffInstance FindInstance(string buffId)
    {
      foreach (var inst in _activeBuffs)
        if (inst.defId == buffId) return inst;
      return null;
    }

    float GetEffectiveDuration(BuffDatabase.BuffDef def, BuffApplyContext context)
    {
      if (def.duration_policy == BuffDatabase.DurationPolicy.permanent
          || def.duration_policy == BuffDatabase.DurationPolicy.until_leave_zone)
        return float.MaxValue;

      if (context.durationOverride > 0f)
        return context.durationOverride;
      if (context.customDuration > 0f)
        return context.customDuration;
      return def.duration;
    }

    void ApplyTickDamage(BuffInstance inst, BuffDatabase.BuffEffect effect)
    {
      var hp = GetOrFindHealth();
      if (hp == null || hp.IsDead)
        return;

      float dmg = effect.per_stack ? effect.value * inst.stacks : effect.value;
      if (inst.context.customDps > 0f)
        dmg = inst.context.customDps * (effect.per_stack ? inst.stacks : 1f);
      var request = DamageRequest.Direct(
        dmg,
        effect.damage_type ?? "physical",
        "buff",
        inst.context.sourceEntity,
        DamageKind.Dot);

      DamagePipeline.Apply(request, hp);
    }

    void ApplyTickHeal(BuffInstance inst, BuffDatabase.BuffEffect effect)
    {
      var hp = GetOrFindHealth();
      if (hp == null || hp.IsDead)
        return;

      if (HasRuleFlag("heal_suppress"))
        return;

      float healAmount = effect.per_stack ? effect.value * inst.stacks : effect.value;
      healAmount *= GetStatModifier("heal_received");
      hp.Heal(healAmount);
    }
  }
}
