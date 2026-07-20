using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Runtime.Physics
{
  /// <summary>
  /// 2.5D 碰撞体：Physics2D ?XY 平面模拟；防御塔/商人为静?BoxCollider2D，玩?怪物?Kinematic + CircleCollider2D?
  /// </summary>
  [DisallowMultipleComponent]
  public class EntityPhysicsBody : MonoBehaviour
  {
    const float CollisionSkin = 0.05f;
    const int MaxSlidePasses = 8; // 从3提升到8，修复怪物在边界墙卡住的问题
    const int MaxDepenetrationPasses = 10;

    [SerializeField] bool debugLog;

    Rigidbody2D _rb;
    CircleCollider2D _circle;
    float _collisionRadius;
    float _worldZ;
    Vector2 _queuedPlanarDelta;
    BodyKind _kind;

    public static bool IgnoreEnemyObstacleCollisions { get; set; }

    static bool s_enemyEnemyIgnoreApplied;

    public Rigidbody2D Body2D => _rb;
    public float CollisionRadius => GetCollisionRadius();

    public static EntityPhysicsBody EnsurePlayer(GameObject root, float radius = -1f)
    {
      if (radius <= 0f)
        radius = WorldGridConstants.PlayerCollisionRadius;

      root.transform.localScale = Vector3.one;
      return Ensure(root, BodyKind.Player, radius, radius);
    }

    public static EntityPhysicsBody EnsureEnemy(GameObject root, float radius)
    {
      radius = Mathf.Max(0.2f, radius);
      root.transform.localScale = Vector3.one;
      return Ensure(root, BodyKind.Enemy, radius, radius);
    }

    /// <param name="collisionWidthTiles">物理碰撞格宽（非建筑占地），如占?5×5 ?2?/param>
    /// <param name="collisionHeightTiles">物理碰撞格高，如占地 3×3 ?1?/param>
    public static EntityPhysicsBody EnsureTower(GameObject root, float collisionWidthTiles, float collisionHeightTiles)
    {
      var w = Mathf.Max(0.5f, collisionWidthTiles * WorldGridConstants.TileSize);
      var h = Mathf.Max(0.5f, collisionHeightTiles * WorldGridConstants.TileSize);
      return Ensure(root, BodyKind.StaticBox, w, h);
    }

    public static EntityPhysicsBody EnsureStaticObstacle(GameObject root, float worldWidth, float worldHeight)
    {
      var w = Mathf.Max(0.5f, worldWidth);
      var h = Mathf.Max(0.5f, worldHeight);
      return Ensure(root, BodyKind.StaticBox, w, h);
    }

    public static float RadiusFromVisual(Transform root)
    {
      var visual = root.Find("Visual");
      if (visual == null)
        return 0.4f;

      var scale = visual.lossyScale;
      return Mathf.Max(scale.x, scale.y) * 0.5f;
    }

    void Awake()
    {
      _worldZ = transform.position.z;
    }

    public void QueuePlanarMove(Vector2 delta)
    {
      if (delta.sqrMagnitude < 0.0000001f)
        return;

      _queuedPlanarDelta += delta;
    }

    public void MoveBy(Vector3 delta) => QueuePlanarMove(new Vector2(delta.x, delta.y));

    public void ResolveOverlapNow()
    {
      if (_circle == null)
        return;

      var mask = GetObstacleMask();
      if (mask == 0)
        return;

      ResolveStaticObstacleOverlap(mask, GetCollisionRadius());
      SyncTransformFromRigidbody();
    }

    public struct PlanarPathResult
    {
      public Vector2 Start;
      public Vector2 RequestedEnd;
      public Vector2 ActualEnd;
      public Vector2 Direction;
      public float Distance;
      public bool BlockedByObstacle;
    }

    /// <summary>Resolves a planar move against static obstacles without mutating any body.</summary>
    public static PlanarPathResult ResolvePlanarPath(Vector2 from, Vector2 requestedTo, float radius)
    {
      var mask = GameplayPhysicsLayers.ObstacleMask;
      var actual = requestedTo;
      if (mask != 0)
      {
        actual = SweepAgainstObstacles(from, requestedTo, radius, mask);
        actual = ClampDestinationFreeOfObstacles(from, actual, radius, mask);
      }

      var delta = actual - from;
      var dist = delta.magnitude;
      return new PlanarPathResult
      {
        Start = from,
        RequestedEnd = requestedTo,
        ActualEnd = actual,
        Direction = dist > 1e-5f ? delta / dist : Vector2.zero,
        Distance = dist,
        BlockedByObstacle = (actual - requestedTo).sqrMagnitude > 0.0004f
      };
    }

    void FixedUpdate()
    {
      var mask = GetObstacleMask();
      var radius = GetCollisionRadius();

      if (_rb == null)
      {
        if (_queuedPlanarDelta.sqrMagnitude > 0.0000001f)
        {
          var planar = GameplayPlane.Position2D(transform) + _queuedPlanarDelta;
          if (mask != 0)
            planar = SweepAgainstObstacles(GameplayPlane.Position2D(transform), planar, radius, mask);

          GameplayPlane.SetPosition2D(transform, planar, _worldZ);
          _queuedPlanarDelta = Vector2.zero;
        }

        return;
      }

      // 先推出重叠，否则 CircleCast 在障碍内部会失效并放行整段位秒"
      if (mask != 0)
        ResolveStaticObstacleOverlap(mask, radius);

      if (_queuedPlanarDelta.sqrMagnitude > 0.0000001f)
      {
        var from = _rb.position;
        var to = from + _queuedPlanarDelta;

        if (mask != 0)
        {
          to = SweepAgainstObstacles(from, to, radius, mask);
          to = ClampDestinationFreeOfObstacles(from, to, radius, mask);
        }

        MovePlanarPosition(to);
        _queuedPlanarDelta = Vector2.zero;
      }

      if (mask != 0)
        ResolveStaticObstacleOverlap(mask, radius);

    }

    void SyncTransformFromRigidbody()
    {
      if (_rb == null)
        return;

      var p = _rb.position;
      transform.position = GameplayPlane.ToWorld(p, _worldZ);
    }

    static Vector2 SweepAgainstObstacles(Vector2 from, Vector2 to, float radius, int mask)
    {
      var delta = to - from;
      var dist = delta.magnitude;
      if (dist < 1e-6f)
        return to;

      var pos = from;
      var remaining = delta;

      for (var pass = 0; pass <= MaxSlidePasses && remaining.sqrMagnitude > 1e-10f; pass++)
      {
        var stepDist = remaining.magnitude;
        var dir = remaining / stepDist;

        var hit = Physics2D.CircleCast(pos, radius, dir, stepDist + CollisionSkin, mask);
        if (hit.collider == null)
        {
          pos += remaining;
          break;
        }

        if (hit.distance <= CollisionSkin)
        {
          remaining = Vector2.zero;
          break;
        }

        var moveDist = Mathf.Max(0f, hit.distance - CollisionSkin);
        pos += dir * moveDist;
        remaining -= dir * moveDist;

        if (pass < MaxSlidePasses && remaining.sqrMagnitude > 1e-10f)
        {
          var slide = remaining - Vector2.Dot(remaining, hit.normal) * hit.normal;
          remaining = slide;
        }
        else
        {
          remaining = Vector2.zero;
        }
      }

      return pos;
    }

    static Vector2 ClampDestinationFreeOfObstacles(Vector2 from, Vector2 to, float radius, int mask)
    {
      if (!OverlapsObstacles(to, radius, mask))
        return to;

      var delta = to - from;
      var dist = delta.magnitude;
      if (dist < 1e-6f)
        return from;

      var dir = delta / dist;
      var lo = 0f;
      var hi = dist;

      for (var i = 0; i < 10; i++)
      {
        var mid = (lo + hi) * 0.5f;
        var sample = from + dir * mid;
        if (OverlapsObstacles(sample, radius, mask))
          hi = mid;
        else
          lo = mid;
      }

      return from + dir * lo;
    }

    static bool OverlapsObstacles(Vector2 center, float radius, int mask)
    {
      return Physics2D.OverlapCircle(center, radius - CollisionSkin * 0.5f, mask) != null;
    }

    float GetCollisionRadius()
    {
      if (_circle != null)
        return _circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

      return _collisionRadius > 0f ? _collisionRadius : 0.4f;
    }

    int GetObstacleMask() =>
      _kind == BodyKind.Enemy && IgnoreEnemyObstacleCollisions
        ? 0
        : GameplayPhysicsLayers.ObstacleMask;

    void ResolveStaticObstacleOverlap(int mask, float radius)
    {
      if (_circle == null)
        return;

      for (var i = 0; i < MaxDepenetrationPasses; i++)
      {
        var center = _rb != null ? _rb.position : GameplayPlane.Position2D(transform);
        if (!OverlapsObstacles(center, radius, mask))
          return;

        var pushed = false;
        var overlaps = Physics2D.OverlapCircleAll(center, radius, mask);

        foreach (var col in overlaps)
        {
          if (col == null || col == _circle)
            continue;

          if (col is BoxCollider2D box && TryPushCircleOutOfBox(center, radius, box, out var push))
          {
            ApplyPlanarPosition(center + push);
            pushed = true;
            break;
          }

          var distInfo = Physics2D.Distance(_circle, col);
          if (distInfo.isOverlapped)
          {
            ApplyPlanarPosition(center + distInfo.normal * (-distInfo.distance + CollisionSkin));
            pushed = true;
            break;
          }
        }

        if (!pushed)
          return;
      }
    }

    void ApplyPlanarPosition(Vector2 planar)
    {
      if (_rb != null)
        _rb.position = planar;

      GameplayPlane.SetPosition2D(transform, planar, _worldZ);
    }

    void MovePlanarPosition(Vector2 planar)
    {
      if (_rb == null)
      {
        GameplayPlane.SetPosition2D(transform, planar, _worldZ);
        return;
      }

      _rb.MovePosition(planar);
    }

    static bool TryPushCircleOutOfBox(Vector2 center, float radius, BoxCollider2D box, out Vector2 push)
    {
      push = Vector2.zero;
      if (box == null || !box.enabled)
        return false;

      var bounds = box.bounds;
      var cx = center.x;
      var cy = center.y;
      var minX = bounds.min.x;
      var maxX = bounds.max.x;
      var minY = bounds.min.y;
      var maxY = bounds.max.y;
      var r = radius + CollisionSkin;

      var closestX = Mathf.Clamp(cx, minX, maxX);
      var closestY = Mathf.Clamp(cy, minY, maxY);
      var dx = cx - closestX;
      var dy = cy - closestY;
      var distSq = dx * dx + dy * dy;

      if (distSq > r * r)
        return false;

      if (distSq > 1e-8f)
      {
        var dist = Mathf.Sqrt(distSq);
        push = new Vector2(dx / dist * (r - dist), dy / dist * (r - dist));
        return true;
      }

      var exitRight = minX - cx + r;
      var exitLeft = cx + r - maxX;
      var exitUp = minY - cy + r;
      var exitDown = cy + r - maxY;

      var best = float.MaxValue;
      if (exitRight > 0f && exitRight < best)
      {
        best = exitRight;
        push = new Vector2(exitRight, 0f);
      }

      if (exitLeft > 0f && exitLeft < best)
      {
        best = exitLeft;
        push = new Vector2(-exitLeft, 0f);
      }

      if (exitUp > 0f && exitUp < best)
      {
        best = exitUp;
        push = new Vector2(0f, exitUp);
      }

      if (exitDown > 0f && exitDown < best)
        push = new Vector2(0f, -exitDown);

      return push.sqrMagnitude > 1e-8f;
    }

    enum BodyKind { Player, Enemy, StaticBox }

    static EntityPhysicsBody Ensure(GameObject root, BodyKind kind, float sizeA, float sizeB)
    {
      var body = root.GetComponent<EntityPhysicsBody>();
      if (body == null)
        body = root.AddComponent<EntityPhysicsBody>();

      body.Configure(kind, sizeA, sizeB);
      return body;
    }

    void Configure(BodyKind kind, float sizeA, float sizeB)
    {
      _kind = kind;
      _worldZ = transform.position.z;
      ClearPhysicsComponents();
      StripChildColliders2D();

      var layerName = kind switch
      {
        BodyKind.Player => GameplayPhysicsLayers.Player,
        BodyKind.Enemy => GameplayPhysicsLayers.Enemy,
        _ => GameplayPhysicsLayers.Obstacle
      };

      var layer = GameplayPhysicsLayers.NameToLayer(layerName);
      if (layer >= 0)
        gameObject.layer = layer;

      if (kind == BodyKind.StaticBox)
      {
        _rb = null;
        _circle = null;
        _queuedPlanarDelta = Vector2.zero;

        var box = gameObject.AddComponent<BoxCollider2D>();
        if (box == null)
        {
          Debug.LogError($"[EntityPhysicsBody] 无法添加 BoxCollider2D：{name}", this);
          return;
        }

        box.size = new Vector2(sizeA, sizeB);
        box.offset = Vector2.zero;
        box.isTrigger = false;
        return;
      }

      _rb = gameObject.AddComponent<Rigidbody2D>();
      _rb.bodyType = RigidbodyType2D.Kinematic;
      _rb.gravityScale = 0f;
      _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
      _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
      _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
      _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

      _circle = gameObject.AddComponent<CircleCollider2D>();
      _circle.radius = sizeA;
      _circle.offset = Vector2.zero;
      _circle.isTrigger = false;
      _collisionRadius = sizeA;

      if (kind == BodyKind.Enemy)
        EnsureEnemyEnemyCollisionIgnored();

      if (debugLog) Debug.Log($"[EntityPhysicsBody] Configured CircleCollider2D: radius={sizeA}, diameter={sizeA*2f}, GameObject={gameObject.name}");

      SyncTransformFromRigidbody();
    }

    static void EnsureEnemyEnemyCollisionIgnored()
    {
      if (s_enemyEnemyIgnoreApplied)
        return;

      var layer = GameplayPhysicsLayers.NameToLayer(GameplayPhysicsLayers.Enemy);
      if (layer < 0)
        return;

      Physics2D.IgnoreLayerCollision(layer, layer, true);
      s_enemyEnemyIgnoreApplied = true;
    }

    void StripChildColliders2D()
    {
      var cols = GetComponentsInChildren<Collider2D>(true);
      foreach (var col in cols)
      {
        if (col == null || col.gameObject == gameObject)
          continue;

        DestroyImmediate(col);
      }
    }

    void ClearPhysicsComponents()
    {
      var cols2d = GetComponents<Collider2D>();
      for (var i = cols2d.Length - 1; i >= 0; i--)
      {
        if (cols2d[i] != null)
          DestroyImmediate(cols2d[i]);
      }

      var cols3d = GetComponents<Collider>();
      for (var i = cols3d.Length - 1; i >= 0; i--)
      {
        if (cols3d[i] != null)
          DestroyImmediate(cols3d[i]);
      }

      var rb3d = GetComponent<Rigidbody>();
      if (rb3d != null)
        DestroyImmediate(rb3d);

      var rb2d = GetComponent<Rigidbody2D>();
      if (rb2d != null)
        DestroyImmediate(rb2d);

      _rb = null;
      _circle = null;
    }
  }
}
