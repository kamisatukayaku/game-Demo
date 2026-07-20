using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Visual;
using Game.Shared.Runtime.Physics;
using UnityEngine;

namespace Game.Shared.Enemy.Collision
{
  /// <summary>Damage hitbox matching the enemy's rendered geometric silhouette.</summary>
  [DisallowMultipleComponent]
  public sealed class EnemyHitbox : MonoBehaviour
  {
    Vector2[] _localVertices;
    float _fallbackRadius;

    public static EnemyHitbox Ensure(GameObject enemy, string enemyId, float visualScale)
    {
      var hitbox = enemy.GetComponent<EnemyHitbox>() ?? enemy.AddComponent<EnemyHitbox>();
      hitbox.Configure(enemyId, visualScale);
      return hitbox;
    }

    public static bool IntersectsSegment(GameObject enemy, Vector2 start, Vector2 end, float sweepRadius)
    {
      var hitbox = enemy != null ? enemy.GetComponent<EnemyHitbox>() : null;
      if (hitbox != null)
        return hitbox.IntersectsSegment(start, end, sweepRadius);

      var body = enemy != null ? enemy.GetComponent<EntityPhysicsBody>() : null;
      var radius = body != null ? body.CollisionRadius : 0.4f;
      return DistanceToSegment((Vector2)enemy.transform.position, start, end) <= radius + sweepRadius;
    }

    void Configure(string enemyId, float visualScale)
    {
      _fallbackRadius = CombatPlaceholderVisual.VisualRadiusFromScale(visualScale)
        * CombatPlaceholderVisual.CollisionRadiusInset;
      var shapeId = EnemyVisualDatabase.GetMinion(enemyId)?.shape_id ?? ResolveShapeFallback(enemyId);
      _localVertices = BuildVertices(shapeId, _fallbackRadius);

      var old = GetComponent<PolygonCollider2D>();
      if (_localVertices == null)
      {
        // Keep an existing collider component disabled instead of destroying it here.
        // Destroy is deferred until the end of the frame, while C# null coalescing does
        // not use Unity's destroyed-object null semantics; reconfiguration in the same
        // frame could therefore select the pending-destroy component and throw.
        if (old != null) old.enabled = false;
        return;
      }

      // Use Unity's overloaded null check. The ?? operator only checks the CLR
      // reference and can return a native component that has already been destroyed.
      var polygon = old == null ? gameObject.AddComponent<PolygonCollider2D>() : old;
      polygon.enabled = true;
      polygon.isTrigger = true;
      polygon.pathCount = 1;
      polygon.SetPath(0, _localVertices);
    }

    static string ResolveShapeFallback(string enemyId) => enemyId switch
    {
      "wild_boss_hex_king" or "mini_boss_hex_sentinel" => "hex",
      "wild_boss_star_hive" or "mini_boss_star_chorus" => "star5",
      "wild_boss_pent_colossus" => "pent",
      "mini_boss_square_jailer" => "square",
      "final_boss_prism_nexus" => "hex",
      "hex_king_part" => "oct",
      "pent_colossus_part" => "square",
      _ when !string.IsNullOrEmpty(enemyId) && enemyId.EndsWith("_part") => "hex",
      _ when !string.IsNullOrEmpty(enemyId) && enemyId.EndsWith("_phantom") => "hex",
      _ => null
    };

    public bool IntersectsSegment(Vector2 start, Vector2 end, float sweepRadius)
    {
      if (_localVertices == null || _localVertices.Length < 3)
        return DistanceToSegment((Vector2)transform.position, start, end) <= _fallbackRadius + sweepRadius;

      if (ContainsWorldPoint(start) || ContainsWorldPoint(end))
        return true;

      for (var i = 0; i < _localVertices.Length; i++)
      {
        var a = (Vector2)transform.TransformPoint(_localVertices[i]);
        var b = (Vector2)transform.TransformPoint(_localVertices[(i + 1) % _localVertices.Length]);
        if (SegmentDistance(start, end, a, b) <= sweepRadius)
          return true;
      }
      return false;
    }

    bool ContainsWorldPoint(Vector2 point)
    {
      var local = (Vector2)transform.InverseTransformPoint(point);
      var inside = false;
      for (int i = 0, j = _localVertices.Length - 1; i < _localVertices.Length; j = i++)
      {
        var a = _localVertices[i];
        var b = _localVertices[j];
        if ((a.y > local.y) != (b.y > local.y)
            && local.x < (b.x - a.x) * (local.y - a.y) / (b.y - a.y) + a.x)
          inside = !inside;
      }
      return inside;
    }

    static Vector2[] BuildVertices(string shapeId, float radius)
    {
      if (string.IsNullOrEmpty(shapeId) || shapeId == "circle") return null;
      var sides = shapeId switch
      {
        "tri" => 3, "square" or "diamond" => 4, "pent" => 5,
        "hex" => 6, "oct" => 8, _ => 0
      };
      var starPoints = shapeId switch
      {
        "star4" => 4, "star5" => 5, "star6" or "hexagram" => 6,
        "star8" => 8, _ => 0
      };
      var count = starPoints > 0 ? starPoints * 2 : sides;
      if (count == 0) return null;

      var vertices = new Vector2[count];
      var rotation = shapeId == "diamond" ? 0f : Mathf.PI * 0.5f;
      for (var i = 0; i < count; i++)
      {
        var r = starPoints > 0 && (i & 1) == 1 ? radius * 0.5f : radius;
        var angle = rotation + i * Mathf.PI * 2f / count;
        vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
      }
      return vertices;
    }

    static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
      if (SegmentsIntersect(a, b, c, d)) return 0f;
      return Mathf.Min(Mathf.Min(DistanceToSegment(a, c, d), DistanceToSegment(b, c, d)),
        Mathf.Min(DistanceToSegment(c, a, b), DistanceToSegment(d, a, b)));
    }

    static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
      var abC = Cross(b - a, c - a); var abD = Cross(b - a, d - a);
      var cdA = Cross(d - c, a - c); var cdB = Cross(d - c, b - c);
      return abC * abD <= 0f && cdA * cdB <= 0f;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
      var segment = end - start;
      var lengthSq = segment.sqrMagnitude;
      if (lengthSq < 0.0001f) return Vector2.Distance(point, start);
      var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSq);
      return Vector2.Distance(point, start + segment * t);
    }
  }
}
