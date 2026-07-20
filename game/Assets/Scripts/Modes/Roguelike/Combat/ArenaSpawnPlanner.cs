using System.Collections.Generic;
using Game.Shared.Core;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Picks spawn positions around the player's visible combat space.
  /// </summary>
  public static class ArenaSpawnPlanner
  {
    static readonly List<Vector2> s_recentSpawns = new();

    public static float GetViewportEdgeRadius(Camera cam)
    {
      if (cam == null || !cam.orthographic)
        return ArenaCameraSettings.CombatOrthographicSize;

      var halfH = cam.orthographicSize;
      var halfW = halfH * cam.aspect;
      return Mathf.Max(halfW, halfH);
    }

    public static float[] PickAttackSectors(int wave, float waveProgress01)
    {
      var count = ArenaSpawnSettings.GetAttackSectorCount(wave, waveProgress01);
      var baseAngle = Random.Range(0f, Mathf.PI * 2f);
      var angles = new float[count];

      if (count == 1)
      {
        angles[0] = baseAngle;
        return angles;
      }

      if (count == 2)
      {
        var gap = Mathf.PI + Random.Range(-0.35f, 0.35f);
        angles[0] = baseAngle;
        angles[1] = baseAngle + gap;
        return angles;
      }

      var step = Random.Range(2.05f, 2.35f);
      angles[0] = baseAngle;
      angles[1] = baseAngle + step;
      angles[2] = baseAngle + step * 2f;
      return angles;
    }

    public static Vector2 PickPosition(
      Vector2 playerCenter,
      string role,
      int wave,
      float[] sectorAngles,
      int sectorSlot)
    {
      if (!CircleArenaController.IsActive)
        return playerCenter + Random.insideUnitCircle.normalized * 12f;

      var cam = Camera.main;
      var viewportEdge = GetViewportEdgeRadius(cam);
      var arcWidth = ArenaSpawnSettings.GetSpawnArcWidth(wave);
      var sectorAngle = sectorAngles != null && sectorAngles.Length > 0
        ? sectorAngles[sectorSlot % sectorAngles.Length]
        : Random.Range(0f, Mathf.PI * 2f);

      var inner = Mathf.Max(
        ArenaSpawnSettings.MinPlayerDistance,
        viewportEdge * ArenaSpawnSettings.SpawnBandInnerFactor);
      var outer = Mathf.Min(
        viewportEdge * ArenaSpawnSettings.SpawnBandOuterFactor,
        ArenaSpawnSettings.MaxEngagementDistance);

      Vector2 fallback = default;
      var hasFallback = false;

      for (var attempt = 0; attempt < ArenaSpawnSettings.MaxPickAttempts; attempt++)
      {
        var angle = sectorAngle + Random.Range(-arcWidth * 0.5f, arcWidth * 0.5f);
        var radius = Random.Range(inner, outer);
        ApplyRoleOffset(role, ref angle, ref radius, arcWidth);

        var candidate = playerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        candidate = CircleArenaController.GetSpawnPointOnCircle(candidate);

        if (!IsValidCandidate(candidate, playerCenter, viewportEdge))
          continue;

        RememberSpawn(candidate);
        return candidate;
      }

      for (var attempt = 0; attempt < ArenaSpawnSettings.MaxPickAttempts; attempt++)
      {
        var angle = sectorAngle + Random.Range(-arcWidth, arcWidth);
        var radius = outer + attempt * 0.35f;
        ApplyRoleOffset(role, ref angle, ref radius, arcWidth);

        var candidate = playerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        candidate = CircleArenaController.ClampPosition(candidate, 0.5f);

        if (!hasFallback && IsFarEnoughFromPlayer(candidate, playerCenter))
        {
          fallback = candidate;
          hasFallback = true;
        }

        if (!IsValidCandidate(candidate, playerCenter, viewportEdge))
          continue;

        RememberSpawn(candidate);
        return candidate;
      }

      if (hasFallback)
      {
        RememberSpawn(fallback);
        return fallback;
      }

      var safeAngle = sectorAngle + Mathf.PI;
      var safePos = playerCenter + new Vector2(Mathf.Cos(safeAngle), Mathf.Sin(safeAngle)) * inner;
      safePos = CircleArenaController.ClampPosition(safePos, 0.5f);
      RememberSpawn(safePos);
      return safePos;
    }

    public static void ClearRecentSpawns() => s_recentSpawns.Clear();

    static void ApplyRoleOffset(string role, ref float angle, ref float radius, float arcWidth)
    {
      switch (role)
      {
        case "runner":
          angle += Random.Range(-arcWidth * 0.75f, arcWidth * 0.75f);
          radius *= 0.94f;
          break;
        case "tank":
          radius *= 1.16f;
          break;
        case "shooter":
          radius *= 1.2f;
          break;
        case "supporter":
          angle += Mathf.PI * Random.Range(0.65f, 1.05f);
          radius *= 1.08f;
          break;
        case "bomber":
          radius = Mathf.Max(radius, ArenaSpawnSettings.MinPlayerDistance + 8f);
          break;
        case "splitter":
          radius *= 1.04f;
          break;
        case "disruptor":
          radius *= 1.1f;
          break;
      }
    }

    static bool IsValidCandidate(Vector2 candidate, Vector2 playerCenter, float viewportEdge)
    {
      if (!IsFarEnoughFromPlayer(candidate, playerCenter))
        return false;

      var toPlayer = candidate - playerCenter;
      if (toPlayer.sqrMagnitude < viewportEdge * viewportEdge
          * ArenaSpawnSettings.VisibleCenterRejectFactor
          * ArenaSpawnSettings.VisibleCenterRejectFactor)
        return false;

      if (!IsInsideArena(candidate))
        return false;

      if (!IsAwayFromBoundary(candidate))
        return false;

      if (!IsSeparatedFromRecent(candidate))
        return false;

      return true;
    }

    static bool IsFarEnoughFromPlayer(Vector2 candidate, Vector2 playerCenter) =>
      (candidate - playerCenter).sqrMagnitude
      >= ArenaSpawnSettings.MinPlayerDistance * ArenaSpawnSettings.MinPlayerDistance;

    static bool IsInsideArena(Vector2 candidate)
    {
      if (!CircleArenaController.IsActive)
        return true;

      var center = CircleArenaController.Center;
      var radius = CircleArenaController.PathRadius;
      return (candidate - center).sqrMagnitude <= (radius - 0.75f) * (radius - 0.75f);
    }

    static bool IsAwayFromBoundary(Vector2 candidate)
    {
      if (!CircleArenaController.IsActive)
        return true;

      var center = CircleArenaController.Center;
      var radius = CircleArenaController.PathRadius;
      var dist = (candidate - center).magnitude;
      return dist <= radius - ArenaSpawnSettings.BoundaryWallMargin;
    }

    static bool IsSeparatedFromRecent(Vector2 candidate)
    {
      var minSepSq = ArenaSpawnSettings.MinSpawnSeparation * ArenaSpawnSettings.MinSpawnSeparation;
      for (var i = 0; i < s_recentSpawns.Count; i++)
      {
        if ((candidate - s_recentSpawns[i]).sqrMagnitude < minSepSq)
          return false;
      }

      return true;
    }

    static void RememberSpawn(Vector2 pos)
    {
      s_recentSpawns.Add(pos);
      if (s_recentSpawns.Count > 24)
        s_recentSpawns.RemoveAt(0);
    }
  }
}
