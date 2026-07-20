using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Projectile
{
  /// <summary>
  /// 失锁追踪弹：初始弱追踪玩家，当玩家移动方向与子弹方向夹角超过阈值时?
  /// 切断追踪切换为直线飞行。奖励横向走位躲闪?
  /// </summary>
  [DisallowMultipleComponent]
  public class LockLossHomingProjectile : MonoBehaviour
  {
    const float DefaultHitRadius = 0.5f;
    const float DefaultLockLossAngle = 60f;

    Vector3 _direction;
    Transform _target;
    DamageRequest _request;
    float _speed;
    float _lifetime;
    float _turnRateDeg;
    float _hitRadius;

    // 失锁参数
    float _lockLossAngleDeg;
    bool _lockLost;
    Vector3 _playerPrevPosition;
    bool _hasPrevPlayerPos;

    public float FlightSpeed => _speed;

    public Vector3 FlightDirection => _direction;

    public void Launch(
      Vector3 origin,
      Transform target,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float lockLossAngleDeg = DefaultLockLossAngle,
      float lifetime = 4f,
      float hitRadius = 0f)
    {
      _target = target;
      _request = request;
      _speed = speed;
      _turnRateDeg = turnRateDeg;
      _lifetime = lifetime;
      _hitRadius = hitRadius > 0f ? hitRadius
        : (request.HitRadius > 0f ? request.HitRadius : DefaultHitRadius);
      _lockLossAngleDeg = lockLossAngleDeg > 0f ? lockLossAngleDeg : DefaultLockLossAngle;
      _lockLost = false;
      _hasPrevPlayerPos = target != null;

      transform.position = origin;

      if (target != null)
      {
        _playerPrevPosition = target.position;
        var toTarget = target.position - origin;
        toTarget.z = 0f;
        _direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;
      }
      else
      {
        _direction = Vector3.right;
      }
    }

    void Update()
    {
      _lifetime -= Time.deltaTime;
      if (_lifetime <= 0f)
      {
        Destroy(gameObject);
        return;
      }

      // 失锁模式下：目标消失或死亡时，子弹继续直线飞行"
      if (_lockLost)
      {
        UpdateStraightFlight();
        return;
      }

      // 追踪模式下目标丢??销毁（?WeakHomingProjectile 一致）
      if (_target == null)
      {
        Destroy(gameObject);
        return;
      }

      var health = _target.GetComponent<Health>();
      if (health == null || health.IsDead)
      {
        Destroy(gameObject);
        return;
      }

      var toTarget = _target.position - transform.position;
      toTarget.z = 0f;
      var dist = toTarget.magnitude;

      if (dist <= _hitRadius)
      {
        DamagePipeline.Apply(_request, health);
        Destroy(gameObject);
        return;
      }

      // ── 检测失锁条件：玩家移动方向 vs 子弹方向夹角 ──
      if (!_lockLost && _hasPrevPlayerPos && Time.deltaTime > 0.0001f)
      {
        var playerVelocity = (_target.position - _playerPrevPosition) / Time.deltaTime;
        if (playerVelocity.sqrMagnitude > 0.1f) // 玩家在移劀"
        {
          var playerMoveDir = playerVelocity.normalized;
          var angle = Vector3.Angle(_direction, playerMoveDir);
          if (angle > _lockLossAngleDeg)
          {
            _lockLost = true;
            // 切直线后保持当前方向飞行
          }
        }
      }

      _playerPrevPosition = _target.position;

      // ── 追踪转向（与 WeakHomingProjectile 相同）──
      if (!_lockLost && dist > 0.0001f)
      {
        var desired = toTarget / dist;
        var maxRadians = _turnRateDeg * Mathf.Deg2Rad * Time.deltaTime;
        _direction = Vector3.RotateTowards(_direction, desired, maxRadians, 0f).normalized;
      }

      transform.position += _direction * (_speed * Time.deltaTime);
    }

    /// <summary>失锁后直线飞?+ 线段-球体命中检测?/summary>
    void UpdateStraightFlight()
    {
      if (_target == null)
      {
        transform.position += _direction * (_speed * Time.deltaTime);
        return;
      }

      var targetHealth = _target.GetComponent<Health>();
      if (targetHealth == null || targetHealth.IsDead)
      {
        transform.position += _direction * (_speed * Time.deltaTime);
        return;
      }

      var prevPos = transform.position;
      var moveDelta = _direction * (_speed * Time.deltaTime);
      var currPos = prevPos + moveDelta;

      // 线段-球体碰撞检测（?StraightProjectile 相同?
      var toTarget = _target.position - prevPos;
      toTarget.z = 0f;

      if (toTarget.magnitude <= _hitRadius)
      {
        DamagePipeline.Apply(_request, targetHealth);
        Destroy(gameObject);
        return;
      }

      var moveLen = moveDelta.magnitude;
      if (moveLen < 0.0001f)
      {
        transform.position = currPos;
        return;
      }

      var moveDir = moveDelta / moveLen;
      var projT = Vector3.Dot(toTarget, moveDir);
      projT = Mathf.Clamp(projT, 0f, moveLen + _hitRadius);

      var closestPoint = prevPos + moveDir * Mathf.Min(projT, moveLen);
      closestPoint.z = 0f;

      var targetPos = _target.position;
      targetPos.z = 0f;

      if ((targetPos - closestPoint).magnitude <= _hitRadius)
      {
        DamagePipeline.Apply(_request, targetHealth);
        Destroy(gameObject);
        return;
      }

      transform.position = currPos;
    }
  }
}