using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>
  /// 2.5D 玩法平面：逻辑与物理在 XY（Physics2D），Z 仅用于渲染深度?
  /// </summary>
  public static class GameplayPlane
  {
    public const float DefaultEntityZ = 0f;

    public static Vector2 ToPlanar(Vector3 world) => new(world.x, world.y);

    public static Vector3 ToWorld(Vector2 planar, float z = DefaultEntityZ) => new(planar.x, planar.y, z);

    public static Vector2 Position2D(Transform transform) => ToPlanar(transform.position);

    public static void SetPosition2D(Transform transform, Vector2 planar, float? z = null)
    {
      var depth = z ?? transform.position.z;
      transform.position = ToWorld(planar, depth);
    }

    public static Vector3 ApplyDelta(Vector3 world, Vector2 delta) =>
      ToWorld(ToPlanar(world) + delta, world.z);

    public static float PlanarDistance(Vector3 a, Vector3 b)
    {
      var dx = a.x - b.x;
      var dy = a.y - b.y;
      return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public static float PlanarDistance(Transform a, Transform b) =>
      PlanarDistance(a.position, b.position);

    public static Vector2 PlanarDirection(Vector3 from, Vector3 to)
    {
      var d = ToPlanar(to) - ToPlanar(from);
      return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.zero;
    }
  }
}