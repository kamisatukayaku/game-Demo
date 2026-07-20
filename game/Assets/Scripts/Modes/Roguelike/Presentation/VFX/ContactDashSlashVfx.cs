using UnityEngine;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>Legacy entry point — presentation is handled by ContactDashPresentationSystem.</summary>
  public static class ContactDashSlashVfx
  {
    public static void Play(Vector2 origin, Vector2 aimDir, float radius = 2.4f)
    {
      ContactDashPresentationSystem.EnsureExists();
    }
  }
}
