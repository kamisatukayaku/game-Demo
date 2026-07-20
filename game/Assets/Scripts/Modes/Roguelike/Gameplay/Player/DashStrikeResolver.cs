using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Runtime.Physics;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Gameplay.Player
{
  /// <summary>Continuous dash segment hit detection without per-frame allocations.</summary>
  public static class DashStrikeResolver
  {
    const float DefaultEnemyRadius = 0.42f;

    public static int CollectSegmentHits(
      Vector2 segmentStart,
      Vector2 segmentEnd,
      float strikeRadius,
      HashSet<int> pathHitIds,
      List<EnemyCore> hitEnemies)
    {
      hitEnemies.Clear();
      if (pathHitIds == null || CombatRoot.EnemyRegistry == null)
        return 0;

      var segment = segmentEnd - segmentStart;
      var segmentLength = segment.magnitude;
      if (segmentLength < 1e-5f)
        return 0;

      var midpoint = (segmentStart + segmentEnd) * 0.5f;
      var queryRadius = segmentLength * 0.5f + strikeRadius + DefaultEnemyRadius + 0.35f;
      var candidates = CombatRoot.EnemyRegistry.GetInRange(midpoint, queryRadius);
      if (candidates == null)
        return 0;

      var added = 0;
      foreach (var enemy in candidates)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;

        var enemyPos = GameplayPlane.Position2D(enemy.transform);
        var enemyRadius = ResolveEnemyRadius(enemy);
        var distance = DistancePointToSegment(enemyPos, segmentStart, segmentEnd);
        if (distance > strikeRadius + enemyRadius)
          continue;

        if (!pathHitIds.Add(health.GetInstanceID()))
          continue;

        hitEnemies.Add(enemy);
        added++;
      }

      return added;
    }

    public static int CollectRadialHits(
      Vector2 center,
      float radius,
      List<EnemyCore> hitEnemies)
    {
      hitEnemies.Clear();
      if (CombatRoot.EnemyRegistry == null)
        return 0;

      var candidates = CombatRoot.EnemyRegistry.GetInRange(center, radius + DefaultEnemyRadius);
      if (candidates == null)
        return 0;

      foreach (var enemy in candidates)
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        hitEnemies.Add(enemy);
      }

      return hitEnemies.Count;
    }

    static float ResolveEnemyRadius(EnemyCore enemy)
    {
      var body = enemy.GetComponent<EntityPhysicsBody>();
      if (body != null)
        return body.CollisionRadius;
      return DefaultEnemyRadius;
    }

    static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
      var ab = b - a;
      var lenSq = ab.sqrMagnitude;
      if (lenSq < 1e-8f)
        return Vector2.Distance(point, a);

      var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
      var closest = a + ab * t;
      return Vector2.Distance(point, closest);
    }
  }
}
