using UnityEngine;

using Game.Shared.Player;
using Game.Shared.Stats;
using Game.Shared.Gameplay.Bridges;

namespace Game.Shared.Combat
{
  /// <summary>
  /// 玩家受击反馈：受击时弹出伤害数字 + 受击回复 + 击退
  /// </summary>
  [DisallowMultipleComponent]
  public class PlayerHitFeedback : MonoBehaviour, IPlayerHitStunGate
  {
    [Header("Damage Number")]
    [SerializeField] bool enableDamageNumber = true;

    [Header("Knockback")]
    [SerializeField] float baseKnockbackForce = 3f;
    [SerializeField] float baseHitStunDuration = 0.12f;

    Health.Health _health;
    PlayerSphereController _move;
    Rigidbody2D _rb;
    float _hitStunTimer;

    void Awake()
    {
      _health = GetComponent<Health.Health>();
      _move = GetComponent<PlayerSphereController>();
      _rb = GetComponent<Rigidbody2D>();

      if (_health != null)
        _health.Damaged += OnDamaged;
    }

    void Update()
    {
      if (_hitStunTimer > 0f)
        _hitStunTimer -= Time.deltaTime;
    }

    void OnDestroy()
    {
      if (_health != null)
        _health.Damaged -= OnDamaged;
    }

    /// <summary>玩家是否处于受击硬直中（禁止移动输入）</summary>
    public bool IsInHitStun => _hitStunTimer > 0f;

    void OnDamaged(float amount)
    {
      // 伤害数字
      if (enableDamageNumber)
        CombatFeedbackManager.ShowDamageNumber(
          transform.position + Vector3.up * 0.6f,
          amount,
          DamageNumberStyle.Player);

      // 受击回复：按百分比回复所受伤害
      var provider = CombatStatProviderLocator.Provider;
      var healOnHitPct = provider.HealOnHitPct;
      if (healOnHitPct > 0f && _health != null && !_health.IsDead)
        _health.Heal(amount * healOnHitPct);

      // 击退：受击时推开玩家，击退抗性降低击退力度
      var knockbackResist = Mathf.Clamp01(provider.KnockbackResist);
      var effectiveKnockback = baseKnockbackForce * (1f - knockbackResist);
      if (effectiveKnockback > 0.01f && _rb != null)
      {
        // 取任何可用方向：来自最后攻击者，或随机方向
        Vector2 knockDir;
        if (_health.LastAttacker != null)
        {
          knockDir = ((Vector2)(transform.position - _health.LastAttacker.transform.position)).normalized;
        }
        else
        {
          knockDir = Random.insideUnitCircle.normalized;
        }

        _rb.AddForce(knockDir * effectiveKnockback, ForceMode2D.Impulse);
        _hitStunTimer = baseHitStunDuration * (1f - knockbackResist * 0.5f);
      }
    }
  }
}
