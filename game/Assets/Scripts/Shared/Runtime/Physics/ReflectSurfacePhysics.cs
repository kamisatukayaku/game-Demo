using UnityEngine;

namespace Game.Shared.Runtime.Physics
{
  /// <summary>
  /// 护盾弧面镜面反射：入射角 = 出射角（相对法线）?
  /// </summary>
  public static class ReflectSurfacePhysics
  {
    public static bool TryReflectOffArc(
      Vector2 incomingVelocityDir,
      Vector2 shieldCenter,
      Vector2 shieldFacing,
      float shieldRadius,
      float arcHalfDeg,
      Vector2 objectPosition,
      float angleSlackDeg,
      float radialSlack,
      out Vector2 reflectDir,
      out Vector2 surfaceNormal,
      out Vector2 contactPoint)
    {
      reflectDir = Vector2.zero;
      surfaceNormal = Vector2.zero;
      contactPoint = objectPosition;

      if (shieldFacing.sqrMagnitude < 0.0001f)
        shieldFacing = Vector2.right;
      shieldFacing.Normalize();

      incomingVelocityDir = ResolveIncoming(incomingVelocityDir, objectPosition, shieldCenter, shieldFacing);
      if (incomingVelocityDir.sqrMagnitude < 0.0001f)
        return false;

      var toObject = objectPosition - shieldCenter;
      var dist = toObject.magnitude;
      if (dist < 0.01f)
        return false;

      var radial = toObject / dist;
      var angle = Vector2.Angle(shieldFacing, radial);
      if (angle > arcHalfDeg + angleSlackDeg)
        return false;

      var shellGap = Mathf.Abs(dist - shieldRadius);
      if (shellGap > radialSlack)
        return false;

      contactPoint = shieldCenter + radial * shieldRadius;
      surfaceNormal = (contactPoint - shieldCenter).normalized;
      if (surfaceNormal.sqrMagnitude < 0.0001f)
        surfaceNormal = shieldFacing;

      // 法线指向被撞击的外表面（与入射方向相反侧?
      if (Vector2.Dot(incomingVelocityDir, surfaceNormal) > 0f)
        surfaceNormal = -surfaceNormal;

      reflectDir = Vector2.Reflect(incomingVelocityDir, surfaceNormal);
      if (reflectDir.sqrMagnitude < 0.0001f)
        return false;

      reflectDir.Normalize();
      return true;
    }

    static Vector2 ResolveIncoming(
      Vector2 incomingVelocityDir,
      Vector2 objectPosition,
      Vector2 shieldCenter,
      Vector2 shieldFacing)
    {
      if (incomingVelocityDir.sqrMagnitude > 0.0001f)
        return incomingVelocityDir.normalized;

      var toShield = shieldCenter - objectPosition;
      if (toShield.sqrMagnitude > 0.0001f)
        return toShield.normalized;

      return -shieldFacing;
    }
  }
}