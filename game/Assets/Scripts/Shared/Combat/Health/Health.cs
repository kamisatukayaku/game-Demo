using System;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Stats;
using Game.Shared.Vfx;
namespace Game.Shared.Combat.Health
{
  /// <summary>
  /// 通用生命值组件?
  /// 支持装备修改最?HP、治疗、伤害事件?
  /// </summary>
  [DisallowMultipleComponent]
  public class Health : MonoBehaviour
  {
    [SerializeField] float maxHp = 30f;
    [SerializeField] bool debugLog;

    float _currentHp;
    float _baseMaxHp;
    bool _dead;
    GameObject _lastAttacker;
    string _lastDamageSourceId;
    float _invulnerableUntil;

    /// <summary>Build Sandbox：忽略所有伤害（玩家）。</summary>
    public bool SandboxIgnoreDamage { get; set; }

    /// <summary>Build Sandbox：受击反馈后立刻满血，不死亡（木桩）。</summary>
    public bool SandboxNeverDie { get; set; }

    public float MaxHp => maxHp;
    public float CurrentHp => _currentHp;
    public float HpPercent => maxHp > 0f ? _currentHp / maxHp : 0f;
    public bool IsDead => _dead;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;

    /// <summary>最后造成伤害的攻击?GameObjec。DmageEipeline ?Apply 时写入?/summary>
    public GameObject LastAttacker
    {
      get => _lastAttacker;
      set => _lastAttacker = value;
    }

    /// <summary>最后一次命中的伤害来源 ID（如 reflect、skill）?/summary>
    public string LastDamageSourceId => _lastDamageSourceId;

    internal void SetLastDamageSource(string sourceId) => _lastDamageSourceId = sourceId;

    public void GrantInvulnerability(float duration)
    {
      if (duration <= 0f)
        return;

      _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + duration);
    }

    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action Died;

    void Awake()
    {
      _baseMaxHp = maxHp;
      _currentHp = maxHp;

      // Auto-attach BuffContainer for buff/debuff support
      if (GetComponent<BuffContainer>() == null)
        gameObject.AddComponent<BuffContainer>();

      if (gameObject.CompareTag("Player") || gameObject.name == "Player")
        PlayerShieldEffect.Ensure(gameObject);
    }

    public void Configure(float max)
    {
      maxHp = Mathf.Max(1f, max);
      _baseMaxHp = maxHp;
      _currentHp = maxHp;
      _dead = false;
    }

    /// <summary>装备系统修改最大生命值，保持当前血量百分比?/summary>
    public void ApplyEquipmentMaxHp(float newMax, float currentPercent)
    {
      var oldMax = maxHp;
      maxHp = Mathf.Max(1f, newMax);
      _currentHp = Mathf.Clamp(maxHp * Mathf.Clamp01(currentPercent), 0f, maxHp);

      if (_currentHp <= 0f && !_dead)
        Die();
    }

    /// <summary>恢复生命值。溢出部分可转化为护盾?/summary>
    public void Heal(float amount)
    {
      if (_dead || amount <= 0f) return;

      var before = _currentHp;
      var missing = maxHp - before;
      var actualHeal = Mathf.Min(missing, amount);
      _currentHp = before + actualHeal;

      if (actualHeal > 0f)
      {
        Healed?.Invoke(actualHeal);

        if (IsPlayerEntity())
        {
          GameEventBus.Publish(new PlayerHealedEvent(
            gameObject,
            actualHeal,
            _currentHp,
            maxHp));
        }
      }

      // 溢出护盾：治疗超过最?HP 的部分按比例转为临时护盾
      var overflow = amount - actualHeal;
      if (overflow > 0f && gameObject.CompareTag("Player"))
      {
        var overhealShield = CombatStatProviderLocator.Provider.OverhealShield;
        if (overhealShield > 0f)
        {
          var shieldAmount = overflow * overhealShield;
          if (shieldAmount > 1f)
          {
            var buffContainer = GetComponent<BuffContainer>();
            buffContainer?.ApplyBuff("buff_overheal_shield", new BuffContainer.BuffApplyContext
            {
              sourceEntity = gameObject,
              sourceKind = "heal",
              abilityId = "overheal",
              stacks = 1,
              durationOverride = 5f
            });
          }
        }
      }
    }

    /// <summary>?<see cref="DamagePipeline"/> 调用；外部伤害请?DamagePipeline.Apply?/summary>
    public void TakeDamage(float amount)
    {
      if (_dead || amount <= 0f)
        return;

      if (SandboxIgnoreDamage)
        return;

      if (IsInvulnerable)
        return;

      if (debugLog)
        Debug.Log($"[Health] {name} TakeDamage({amount:F1}) currentHp={_currentHp:F1}/{maxHp:F1}", this);

      _currentHp = Mathf.Max(0f, _currentHp - amount);

      if (debugLog)
        Debug.Log($"[Health] {name} took {amount:F1} dmg | HP: {_currentHp}/{maxHp}", this);

      Damaged?.Invoke(amount);

      if (IsPlayerEntity())
      {
        GameEventBus.Publish(new PlayerDamagedEvent(
          gameObject,
          _lastAttacker,
          amount,
          _currentHp,
          maxHp));
      }

      if (SandboxNeverDie)
      {
        if (_currentHp <= 0f)
        {
          _currentHp = 0f;
          _dead = false;
        }

        return;
      }

      if (_currentHp <= 0f)
        Die();
    }

    void Die()
    {
      if (_dead || SandboxNeverDie)
        return;

      _dead = true;

      if (debugLog)
        Debug.Log($"[Health] {name} died.", this);

      Died?.Invoke();

      bool isPlayer = IsPlayerEntity();
      string victimId = isPlayer ? "player" : (GetComponent<EnemyCore>()?.EnemyId ?? "unknown");

      if (isPlayer)
      {
        GameEventBus.Publish(new PlayerDeathEvent(
          gameObject,
          _lastAttacker,
          transform.position));
      }

      // Legacy combat bus (World / buff systems still listening)
      CombatEventBus.FireKill(_lastAttacker, gameObject, isPlayer, victimId);
    }

    bool IsPlayerEntity()
    {
      return gameObject.CompareTag("Player") || gameObject.name == "Player";
    }

#if UNITY_EDITOR
    /// <summary>测试用：对自身造成 10 点伤害?/summary>
    [ContextMenu("Debug: Take 10 Damage")]
    void DebugTakeDamage()
    {
      TakeDamage(10f);
    }

    /// <summary>测试用：回复 10 点生命?/summary>
    [ContextMenu("Debug: Heal 10")]
    void DebugHeal()
    {
      Heal(10f);
    }
#endif
  }
}
