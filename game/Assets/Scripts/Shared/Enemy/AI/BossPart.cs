using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Boss 部件。附加到部件 GameObject 上，由 BossCore 管理。
  ///
  /// 损伤模式：
  ///   None        — 无 Health/EnemyCore，不注册 EnemyRegistry，不可被命中
  ///   Independent — 有 Health+EnemyCore，注册到 EnemyRegistry，独立 HP
  ///   Shared      — 有 Health(哑值)+EnemyCore，注册到 EnemyRegistry，伤害转发给核心
  /// </summary>
  public class BossPart : MonoBehaviour
  {
    public enum DamageMode { None, Independent, Shared }
    public enum MovementMode { None, FixedOffset, CircularOrbit }

    DamageMode _damageMode;
    MovementMode _movementMode;
    Vector2 _offset;
    float _orbitRadius;
    float _orbitSpeed;
    float _orbitAngle;

    BossCore _core;
    Health _health;
    bool _initialised;

    public DamageMode PartDamageMode => _damageMode;
    public bool IsAttackOrigin { get; set; }
    public bool IsDestroyed => _health != null && _health.IsDead;

    // ── 配置（由 BossCore 调用）───────────────

    public void Configure(
      DamageMode damageMode,
      MovementMode movementMode,
      Vector2 offset,
      float orbitRadius,
      float orbitSpeed,
      float orbitStartAngle)
    {
      _damageMode = damageMode;
      _movementMode = movementMode;
      _offset = offset;
      _orbitRadius = orbitRadius;
      _orbitSpeed = orbitSpeed;
      _orbitAngle = orbitStartAngle;
    }

    public void Initialize(BossCore core)
    {
      _core = core;
      _health = GetComponent<Health>();

      if (_damageMode == DamageMode.Shared && _health != null)
      {
        _health.Damaged += OnSharedDamaged;
      }
      else if (_damageMode == DamageMode.Independent && _health != null)
      {
        _health.Damaged += OnPartDamaged;
        _health.Died += OnPartDied;
      }
      // None 模式：无 Health，不做任何订阅

      // None 模式需要确保不可被物理命中（无 Collider 或设为 Trigger）
      if (_damageMode == DamageMode.None)
      {
        var cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
      }

      _initialised = true;
    }

    void OnDestroy()
    {
      if (_health != null)
      {
        _health.Damaged -= OnPartDamaged;
        _health.Damaged -= OnSharedDamaged;
        _health.Died -= OnPartDied;
      }
    }

    // ── 每帧运动（由 BossCore 驱动）───────────

    public void TickPart(float dt)
    {
      if (!_initialised || _core == null || IsDestroyed) return;

      Vector2 targetPos;
      switch (_movementMode)
      {
        case MovementMode.FixedOffset:
          targetPos = _core.Position + _offset;
          break;
        case MovementMode.CircularOrbit:
          _orbitAngle += _orbitSpeed * dt;
          targetPos = _core.Position + new Vector2(
            Mathf.Cos(_orbitAngle * Mathf.Deg2Rad),
            Mathf.Sin(_orbitAngle * Mathf.Deg2Rad)) * _orbitRadius;
          break;
        default:
          return;
      }
      transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
    }

    // ── 损伤回调 ──────────────────────────────

    void OnPartDamaged(float amount) { } // Independent: handled by Health directly

    void OnSharedDamaged(float amount)
    {
      if (_core != null && _health != null)
      {
        _health.Heal(amount); // 撤销部件扣血
        _core.ForwardDamage(amount);
      }
    }

    void OnPartDied()
    {
      _core?.OnPartDestroyed(this);
    }
  }
}
