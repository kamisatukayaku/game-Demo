using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
namespace Game.Shared.Projectile
{
  /// <summary>
  /// 弱追踪弹：每帧有限角速度转向目标，可被走?partially 躲开?
  /// </summary>
  [DisallowMultipleComponent]
  public class WeakHomingProjectile : MonoBehaviour
  {
    const float DefaultHitRadius = 0.5f;

    Vector3 _direction;
    Transform _target;
    DamageRequest _request;
    float _speed;
    float _lifetime;
    float _turnRateDeg;
    float _hitRadius;
    bool _reflectMode;
    float _retargetTimer;

    const float ReflectRetargetInterval = 0.12f;
    const float ReflectSearchRadius = 16f;
    const float ReflectConeHalfAngle = 110f;

    public float FlightSpeed => _speed;

    public Vector3 FlightDirection => _direction;

    public void Launch(
      Vector3 origin,
      Transform target,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float lifetime = 4f,
      float hitRadius = 0f)
    {
      _reflectMode = false;
      _target = target;
      _request = request;
      _speed = speed;
      _turnRateDeg = turnRateDeg;
      _lifetime = lifetime;
      _hitRadius = hitRadius > 0f ? hitRadius
        : (request.HitRadius > 0f ? request.HitRadius : DefaultHitRadius);

      transform.position = origin;

      if (target != null)
      {
        var toTarget = target.position - origin;
        toTarget.z = 0f;
        _direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;
      }
      else
      {
        _direction = Vector3.right;
      }
    }

    /// <summary>反弹弹：保留原速与初始方向，弱追踪最近敌人?/summary>
    public void LaunchReflect(
      Vector3 origin,
      Vector3 initialDirection,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float lifetime = 5f,
      float hitRadius = 0f)
    {
      _reflectMode = true;
      _request = request;
      _speed = speed;
      _turnRateDeg = turnRateDeg;
      _lifetime = lifetime;
      _hitRadius = hitRadius > 0f ? hitRadius
        : (request.HitRadius > 0f ? request.HitRadius : DefaultHitRadius);
      _retargetTimer = 0f;

      transform.position = origin;
      initialDirection.z = 0f;
      _direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector3.right;
      _target = FindReflectTarget(origin, _direction);
    }

    void Update()
    {
      _lifetime -= Time.deltaTime;
      if (_lifetime <= 0f)
      {
        Destroy(gameObject);
        return;
      }

      if (_reflectMode)
      {
        UpdateReflectHoming();
        return;
      }

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

      if (dist > 0.0001f)
      {
        var desired = toTarget / dist;
        var maxRadians = _turnRateDeg * Mathf.Deg2Rad * Time.deltaTime;
        _direction = Vector3.RotateTowards(_direction, desired, maxRadians, 0f).normalized;
      }

      transform.position += _direction * (_speed * Time.deltaTime);
    }

    void UpdateReflectHoming()
    {
      _retargetTimer -= Time.deltaTime;
      if (_target == null || !IsValidTarget(_target))
      {
        if (_retargetTimer <= 0f)
        {
          _target = FindReflectTarget(transform.position, _direction);
          _retargetTimer = ReflectRetargetInterval;
        }
      }
      else
      {
        var health = _target.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
          var toTarget = _target.position - transform.position;
          toTarget.z = 0f;
          var dist = toTarget.magnitude;

          if (dist <= _hitRadius)
          {
            DamagePipeline.Apply(_request, health);
            Destroy(gameObject);
            return;
          }

          if (dist > 0.0001f)
          {
            var desired = toTarget / dist;
            var maxRadians = _turnRateDeg * Mathf.Deg2Rad * Time.deltaTime;
            _direction = Vector3.RotateTowards(_direction, desired, maxRadians, 0f).normalized;
          }
        }
      }

      transform.position += _direction * (_speed * Time.deltaTime);
    }

    static bool IsValidTarget(Transform target)
    {
      if (target == null)
        return false;

      var health = target.GetComponent<Health>();
      return health != null && !health.IsDead;
    }

    static Transform FindReflectTarget(Vector3 origin, Vector3 forward)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;

      Transform bestCone = null;
      var bestConeDist = float.MaxValue;
      Transform bestAny = null;
      var bestAnyDist = float.MaxValue;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var toEnemy = enemy.transform.position - origin;
        toEnemy.z = 0f;
        var dist = toEnemy.magnitude;
        if (dist > ReflectSearchRadius || dist < 0.01f)
          continue;

        if (dist < bestAnyDist)
        {
          bestAnyDist = dist;
          bestAny = enemy.transform;
        }

        var dir = toEnemy / dist;
        var angle = Vector3.Angle(forward, dir);
        if (angle > ReflectConeHalfAngle)
          continue;

        if (dist < bestConeDist)
        {
          bestConeDist = dist;
          bestCone = enemy.transform;
        }
      }

      return bestCone != null ? bestCone : bestAny;
    }
  }
}