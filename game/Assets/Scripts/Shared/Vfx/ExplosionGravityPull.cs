using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Runtime.Physics;
namespace Game.Shared.Vfx
{
  /// <summary>爆炸黑洞：短暂持续吸引范围内敌人向中心?/summary>
  [DisallowMultipleComponent]
  public class ExplosionGravityPull : MonoBehaviour
  {
    const float DefaultDuration = 0.5f;

    Vector2 _center;
    float _radius;
    float _strength;
    float _remaining;
    LineRenderer _ring;

    public static void Spawn(Vector2 center, float radius, float pullStrength = 1.2f, float duration = DefaultDuration)
    {
      var go = new GameObject("ExplosionGravityPull");
      var pull = go.AddComponent<ExplosionGravityPull>();
      pull._center = center;
      pull._radius = Mathf.Max(0.5f, radius);
      pull._strength = pullStrength;
      pull._remaining = duration;
      pull.BuildVisual();
    }

    void BuildVisual()
    {
      _ring = gameObject.AddComponent<LineRenderer>();
      _ring.useWorldSpace = false;
      _ring.loop = true;
      _ring.positionCount = 24;
      _ring.startWidth = 0.05f;
      _ring.endWidth = 0.05f;
      _ring.material = new Material(Shader.Find("Sprites/Default"));
      _ring.startColor = new Color(0.35f, 0.1f, 0.55f, 0.65f);
      _ring.endColor = new Color(0.15f, 0.05f, 0.25f, 0.25f);
      _ring.sortingOrder = 36;

      for (var i = 0; i < _ring.positionCount; i++)
      {
        var angle = i / (float)_ring.positionCount * Mathf.PI * 2f;
        _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius, 0f));
      }

      transform.position = new Vector3(_center.x, _center.y, -0.04f);
    }

    void Update()
    {
      _remaining -= Time.deltaTime;
      if (_remaining <= 0f)
      {
        Destroy(gameObject);
        return;
      }

      PullEnemies(Time.deltaTime);
      if (_ring != null)
      {
        var alpha = Mathf.Clamp01(_remaining / DefaultDuration);
        _ring.startColor = new Color(0.35f, 0.1f, 0.55f, 0.65f * alpha);
      }
    }

    void PullEnemies(float dt)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(_center, _radius))
      {
        if (enemy == null)
          continue;

        var pos = GameplayPlane.Position2D(enemy.transform);
        var toCenter = _center - pos;
        var dist = toCenter.magnitude;
        if (dist < 0.08f || dist > _radius)
          continue;

        var falloff = 1f - dist / _radius;
        var pull = toCenter.normalized * (_strength * falloff * dt * 7f);
        var body = enemy.GetComponent<EntityPhysicsBody>();
        if (body != null)
          body.QueuePlanarMove(pull);
      }
    }
  }
}