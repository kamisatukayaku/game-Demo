using Game.Shared.Core;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>Places arena terrain events near the player so hazards stay in the active fight space.</summary>
  public static class ArenaTerrainPlacement
  {
    public const float DefaultMinDistance = 1.2f;
    public const float DefaultMaxDistance = 9f;
    public const float MaxViewportFactor = 0.88f;
    public const float HardMaxDistance = 14f;

    public static Vector2 GetPlayerPlanarPosition()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return CircleArenaController.Center;

      var body = player.GetComponent<Rigidbody2D>();
      if (body != null)
        return body.position;

      return GameplayPlane.Position2D(player.transform);
    }

    public static float ResolveMaxDistance(float requestedMax)
    {
      var cam = Camera.main;
      var viewport = cam != null
        ? ArenaSpawnPlanner.GetViewportEdgeRadius(cam) * MaxViewportFactor
        : HardMaxDistance;
      return Mathf.Min(requestedMax, viewport, HardMaxDistance);
    }

    public static Vector2 PickNearPlayer(
      float minDistance,
      float maxDistance,
      float edgePadding = 0.5f,
      Vector2? anchorOverride = null)
    {
      var anchor = anchorOverride ?? GetPlayerPlanarPosition();
      var maxDist = ResolveMaxDistance(maxDistance);
      var minDist = Mathf.Min(minDistance, maxDist * 0.35f);

      for (var attempt = 0; attempt < 16; attempt++)
      {
        var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        var radius = Random.Range(minDist, maxDist);
        var point = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        point = CircleArenaController.ClampPosition(point, edgePadding);
        if ((point - anchor).sqrMagnitude >= minDist * minDist * 0.64f)
          return point;
      }

      return CircleArenaController.ClampPosition(
        anchor + Random.insideUnitCircle.normalized * ((minDist + maxDist) * 0.5f),
        edgePadding);
    }

    public static void PickDualNearPlayer(
      out Vector2 first,
      out Vector2 second,
      float minDistance,
      float maxDistance,
      float minSeparation = 5f)
    {
      var anchor = GetPlayerPlanarPosition();
      var maxDist = ResolveMaxDistance(maxDistance);
      var minDist = Mathf.Min(minDistance, maxDist * 0.35f);

      first = PickNearPlayer(minDist, maxDist, 0.5f, anchor);
      var baseDir = first - anchor;
      if (baseDir.sqrMagnitude < 0.01f)
        baseDir = Random.insideUnitCircle.normalized;
      else
        baseDir.Normalize();

      for (var attempt = 0; attempt < 16; attempt++)
      {
        var angleOffset = Random.Range(70f, 140f) * Mathf.Deg2Rad * (Random.value < 0.5f ? -1f : 1f);
        var cos = Mathf.Cos(angleOffset);
        var sin = Mathf.Sin(angleOffset);
        var dir = new Vector2(
          baseDir.x * cos - baseDir.y * sin,
          baseDir.x * sin + baseDir.y * cos);
        var radius = Random.Range(minDist, maxDist);
        second = CircleArenaController.ClampPosition(anchor + dir * radius, 0.5f);
        if ((second - first).sqrMagnitude >= minSeparation * minSeparation)
          return;
      }

      second = CircleArenaController.ClampPosition(first + new Vector2(minSeparation, 0f), 0.5f);
    }

    public static void PickPortalPairNearPlayer(
      out Vector2 portalA,
      out Vector2 portalB,
      float separation,
      float maxDistanceFromPlayer = 10f)
    {
      var anchor = GetPlayerPlanarPosition();
      var maxDist = ResolveMaxDistance(maxDistanceFromPlayer);
      var mid = PickNearPlayer(2f, maxDist, 0.5f, anchor);
      var dir = mid - anchor;
      if (dir.sqrMagnitude < 0.01f)
        dir = Random.insideUnitCircle.normalized;
      else
        dir.Normalize();

      portalA = CircleArenaController.ClampPosition(mid - dir * (separation * 0.5f), 0.5f);
      portalB = CircleArenaController.ClampPosition(mid + dir * (separation * 0.5f), 0.5f);
    }
  }
}
