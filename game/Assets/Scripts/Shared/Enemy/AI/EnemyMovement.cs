using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Runtime.Physics;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 敌人移动组件：速度积分、击退、硬直、移动插值。
  /// 附加到敌人 GameObject 上，由 EnemyCore 驱动。
  /// 不挂载此组件 → 敌人不可移动（如营地核心）。
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyMovement : MonoBehaviour
  {
    [Header("Move")]
    [SerializeField] float moveSpeed = 2.5f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float deceleration = 8f;

    [Header("Hit Response")]
    [SerializeField] float hitStunDuration = 0.08f;
    [SerializeField] float knockbackDistance = 0.15f;

    float _dashSpeedScaling = 1f;
    float _dashSpeedMult = 2.2f;

    Vector2 _currentVelocity;
    Vector2 _knockbackVelocity;
    float _hitStunTimer;
    float _knockbackMultiplier = 1f;

    EntityPhysicsBody _physics;

    // Sprint
    bool _isSprinting;
    float _sprintSpeedMultiplier = 1.5f;

    public float MoveSpeed => moveSpeed;
    public Vector2 Velocity => _currentVelocity;
    public bool IsSprinting => _isSprinting;

    void Awake()
    {
      _physics = GetComponent<EntityPhysicsBody>();
    }

    // ── 配置注入 ──────────────────────────────────

    public void ConfigureFromCore(float speed, float accel, float decel, float stunDur, float knockDist,
      float dashMult = 2.2f, float dashScaling = 1f)
    {
      moveSpeed = speed;
      acceleration = accel;
      deceleration = decel;
      hitStunDuration = stunDur;
      knockbackDistance = knockDist;
      _dashSpeedMult = dashMult;
      _dashSpeedScaling = dashScaling;
    }

    public void ApplyScaledStats(float speed, float dashMult = 1f)
    {
      moveSpeed = speed;
      _dashSpeedScaling = dashMult;
    }

    public void SetKnockbackMultiplier(float multiplier) =>
      _knockbackMultiplier = Mathf.Clamp01(multiplier);

    public void SetSprintState(bool sprinting, float speedMult = 1.5f)
    {
      _isSprinting = sprinting;
      if (speedMult > 1f) _sprintSpeedMultiplier = speedMult;
    }

    // ── 移动执行 ──────────────────────────────────

    public void MoveTowards(Vector2 targetPosition, Vector2 selfPos, float stopRange, float dt,
      float accelMultiplier = 1f, float buffSpeedMult = 1f)
    {
      var toTarget = targetPosition - selfPos;
      var dist = toTarget.magnitude;
      if (dist <= stopRange || dist <= 0.01f)
      {
        _currentVelocity = Vector2.MoveTowards(_currentVelocity, Vector2.zero, deceleration * dt);
        MovePlanar(_currentVelocity * dt);
        return;
      }

      var dir = toTarget / dist;
      var effectiveSpeed = GetEffectiveSpeed(buffSpeedMult);
      var targetVel = dir * effectiveSpeed;
      _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVel, acceleration * accelMultiplier * dt);
      MovePlanar(_currentVelocity * dt);
    }

    public void MoveDirection(Vector2 direction, float speedFraction, float dt, float buffSpeedMult = 1f)
    {
      var speed = moveSpeed * speedFraction * buffSpeedMult;
      var targetVel = direction * speed;
      _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVel, acceleration * 0.5f * dt);
      MovePlanar(_currentVelocity * dt);
    }

    public void MoveWander(Vector2 target, Vector2 selfPos, float dt)
    {
      var toTarget = target - selfPos;
      var dist = toTarget.magnitude;
      const float reachDist = 0.5f;
      const float wanderSpeedFraction = 0.35f;

      if (dist > reachDist)
      {
        var dir = toTarget / dist;
        var speed = moveSpeed * wanderSpeedFraction;
        var targetVel = dir * speed;
        _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVel, acceleration * 0.5f * dt);
        MovePlanar(_currentVelocity * dt);
      }
      else
      {
        _currentVelocity = Vector2.MoveTowards(_currentVelocity, Vector2.zero, deceleration * dt);
        MovePlanar(_currentVelocity * dt);
      }
    }

    public void Stop(float dt)
    {
      _currentVelocity = Vector2.MoveTowards(_currentVelocity, Vector2.zero, deceleration * dt);
      MovePlanar(_currentVelocity * dt);
    }

    public float GetEffectiveSpeed(float buffSpeedMult = 1f)
    {
      float mult = buffSpeedMult;
      if (_isSprinting) mult *= _sprintSpeedMultiplier;
      return moveSpeed * mult;
    }

    public Vector2 GetKnockbackDirection(Vector2 selfPos, Vector2 targetPos)
    {
      var away = selfPos - targetPos;
      if (away.sqrMagnitude < 0.001f)
        away = Random.insideUnitCircle.normalized;
      return away.normalized * (knockbackDistance / Mathf.Max(hitStunDuration, 0.01f));
    }

    // ── 硬直 + 击退 ──────────────────────────────

    public void ApplyHitStun() => _hitStunTimer = hitStunDuration;
    public void ApplyKnockback(Vector2 velocity)
    {
      _knockbackVelocity = velocity * _knockbackMultiplier;
      _currentVelocity = Vector2.zero;
    }

    public bool TryUpdateHitStun(float dt)
    {
      if (_hitStunTimer <= 0f) return false;
      _hitStunTimer -= dt;

      if (_knockbackVelocity.sqrMagnitude > 0.001f)
      {
        _knockbackVelocity = Vector2.MoveTowards(_knockbackVelocity, Vector2.zero, 8f * dt);
        MovePlanar(_knockbackVelocity * dt);
      }

      return _hitStunTimer > 0f;
    }

    public void ResetVelocity() => _currentVelocity = Vector2.zero;

    // ── 物理 ──────────────────────────────────────

    void MovePlanar(Vector2 delta)
    {
      if (_physics != null)
        _physics.QueuePlanarMove(delta);
      else
        GameplayPlane.SetPosition2D(transform, GameplayPlane.Position2D(transform) + delta);
    }

    public bool IsMoving => _currentVelocity.sqrMagnitude > 0.0004f;
    public float DashSpeedMult => _dashSpeedMult;
    public float DashSpeedScaling => _dashSpeedScaling;
  }
}
