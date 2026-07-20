using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  [DisallowMultipleComponent]
  public sealed class MageTidalBoundaryZone : MonoBehaviour
  {
    static readonly List<MageTidalBoundaryZone> s_active = new();

    Vector2 _center;
    float _radius;
    float _remaining;
    float _pushStrength;

    public static void Spawn(Vector2 center, float radius, float duration, float pushStrength)
    {
      var go = new GameObject("MageTidalBoundary");
      var zone = go.AddComponent<MageTidalBoundaryZone>();
      zone._center = center;
      zone._radius = radius;
      zone._remaining = duration;
      zone._pushStrength = pushStrength;
      s_active.Add(zone);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageTidalBoundary",
        new Vector3(center.x, center.y, 0f),
        null,
        radius,
        duration));
    }

    public static void ResetAll()
    {
      for (var i = s_active.Count - 1; i >= 0; i--)
      {
        if (s_active[i] != null)
          Destroy(s_active[i].gameObject);
      }
      s_active.Clear();
    }

    void Update()
    {
      _remaining -= Time.deltaTime;
      if (_remaining <= 0f)
      {
        s_active.Remove(this);
        Destroy(gameObject);
        return;
      }

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(_center, _radius))
      {
        if (enemy == null)
          continue;

        var pos = GameplayPlane.Position2D(enemy.transform);
        var dist = Vector2.Distance(pos, _center);
        if (dist > _radius)
          continue;

        var outward = pos - _center;
        if (outward.sqrMagnitude < 0.0001f)
          outward = Vector2.up;
        outward.Normalize();

        var movement = enemy.GetComponent<EnemyMovement>();
        movement?.ApplyKnockback(outward * _pushStrength);
      }
    }
  }
}
