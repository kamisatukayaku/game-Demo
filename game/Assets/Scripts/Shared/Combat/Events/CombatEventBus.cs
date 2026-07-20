using System;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using HealthComp = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Combat.Events
{
  /// <summary>
  /// 全局战斗事件总线。解?Buff、武器、装备等系统之间的通信?
  /// 所有事件为静?delegate，订阅方?OnEnable/OnDisable ?Awake/OnDestroy 中管理?
  /// </summary>
  public static class CombatEventBus
  {
    // ── 伤害事件 ─────────────────────────────────────

    /// <summary>伤害即将结算（步?0）。可用于护盾消费等拦截操作?/summary>
    public struct PreDamageArgs
    {
      public GameObject Attacker;
      public GameObject Target;
      public DamageRequest Request;
    }

    /// <summary>伤害结算完成（步?6 之后）?/summary>
    public struct PostDamageArgs
    {
      public GameObject Attacker;
      public GameObject Target;
      public DamageRequest Request;
      public DamageResult Result;
    }

    public delegate void PreDamageDelegate(in PreDamageArgs args);
    public delegate void PostDamageDelegate(in PostDamageArgs args);

    public static event PreDamageDelegate PreDamage;
    public static event PostDamageDelegate PostDamage;

    // ── Buff 事件 ────────────────────────────────────

    public struct BuffEvent
    {
      public GameObject Target;
      public string BuffId;
      public BuffContainer.BuffApplyContext Context;
    }

    public delegate void BuffDelegate(BuffEvent args);

    public static event Action<GameObject, string> BuffAppliedRaw;
    public static event Action<GameObject, string> BuffRemovedRaw;
    public static event Action<GameObject, string> BuffExpiredRaw;

    // ── 击杀事件 ─────────────────────────────────────

    /// <summary>实体被击杀（HealthComp.Died 触发后）?/summary>
    public struct KillArgs
    {
      public GameObject Killer;
      public GameObject Victim;
      public bool IsPlayer;
      public string VictimId;
    }

    public delegate void KillDelegate(KillArgs args);
    public static event KillDelegate OnKill;

    // ── 攻击命中事件 ─────────────────────────────────

    /// <summary>一次攻击命中（每次伤害结算后触发）?/summary>
    public struct AttackHitArgs
    {
      public GameObject Attacker;
      public GameObject Target;
      public float Damage;
      public string AttackProfileId;
      public string[] OnHitBuffIds;
    }

    public delegate void AttackHitDelegate(AttackHitArgs args);
    public static event AttackHitDelegate OnAttackHit;

    // ── 内部 Fire 方法 ────────────────────────────────

    public static void FirePreDamage(GameObject attacker, GameObject target, in DamageRequest request)
    {
      if (PreDamage == null) return;
      var args = new PreDamageArgs { Attacker = attacker, Target = target, Request = request };
      PreDamage(args);
    }

    public static void FirePostDamage(GameObject attacker, GameObject target, in DamageRequest request, DamageResult result)
    {
      PostDamage?.Invoke(new PostDamageArgs
      {
        Attacker = attacker,
        Target = target,
        Request = request,
        Result = result
      });
    }

    public static void FireBuffApplied(GameObject target, string buffId, BuffContainer.BuffApplyContext context = default)
    {
      BuffAppliedRaw?.Invoke(target, buffId);
    }

    public static void FireBuffRemoved(GameObject target, string buffId)
    {
      BuffRemovedRaw?.Invoke(target, buffId);
    }

    public static void FireBuffExpired(GameObject target, string buffId)
    {
      BuffExpiredRaw?.Invoke(target, buffId);
    }

    public static void FireKill(GameObject killer, GameObject victim, bool isPlayer, string victimId = null)
    {
      OnKill?.Invoke(new KillArgs
      {
        Killer = killer,
        Victim = victim,
        IsPlayer = isPlayer,
        VictimId = victimId ?? "unknown"
      });
    }

    public static void FireAttackHit(AttackHitArgs args)
    {
      OnAttackHit?.Invoke(args);
    }
  }
}