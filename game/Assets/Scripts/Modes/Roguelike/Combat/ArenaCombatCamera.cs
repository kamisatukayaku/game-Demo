using Game.Shared.Player;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Applies arena combat camera settings at runtime. Does not depend on wave state.
  /// </summary>
  public static class ArenaCombatCamera
  {
    public static void Apply(Camera camera)
    {
      if (camera == null)
        return;

      var ortho = ArenaCameraSettings.CombatOrthographicSize;
      CameraFollow2D.ApplyGameplayZoom(camera, ortho);

      var follow = camera.GetComponent<CameraFollow2D>();
      if (follow == null)
        follow = camera.gameObject.AddComponent<CameraFollow2D>();

      follow.SetOrthographicSize(ortho);
      follow.ConfigureArenaFollow(
        ArenaCameraSettings.FollowSmoothTime,
        ArenaCameraSettings.LookAheadDistance,
        ArenaCameraSettings.LookAheadSmoothness,
        ArenaCameraSettings.MaximumLookAhead,
        ArenaCameraSettings.ArenaEdgeFramingDistance,
        ArenaCameraSettings.EdgeFramingPullStrength);
    }
  }
}
