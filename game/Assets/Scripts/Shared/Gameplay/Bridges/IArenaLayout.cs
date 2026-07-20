using UnityEngine;

namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>环形竞技场布局（Roguelike Arena 模式实现）</summary>
  public interface IArenaLayout
  {
    bool IsActive { get; }
    Vector2 Center { get; }
    float PathRadius { get; }
    float FullCombatRange { get; }
    float AngleAtPosition(Vector2 position);
    Vector2 PositionAtAngle(float angleRadians);
    bool ComputeChord(Vector2 enemyPlanar, Vector2 direction, out Vector2 start, out Vector2 end, out float dashDistance);
    Vector2 GetSpawnPointOnCircle(Vector2 hintPos);
  }

  public static class ArenaLayoutLocator
  {
    static IArenaLayout s_layout = NullArenaLayout.Instance;

    public static IArenaLayout Layout => s_layout;

    public static void Register(IArenaLayout layout) =>
      s_layout = layout ?? NullArenaLayout.Instance;

    public static void Clear() => s_layout = NullArenaLayout.Instance;
  }

  sealed class NullArenaLayout : IArenaLayout
  {
    public static readonly NullArenaLayout Instance = new();
    public bool IsActive => false;
    public Vector2 Center => Vector2.zero;
    public float PathRadius => 12f;
    public float FullCombatRange => 999f;
    public float AngleAtPosition(Vector2 position) => 0f;
    public Vector2 PositionAtAngle(float angleRadians) => Vector2.zero;
    public bool ComputeChord(Vector2 enemyPlanar, Vector2 direction, out Vector2 start, out Vector2 end, out float dashDistance)
    {
      start = enemyPlanar;
      end = enemyPlanar;
      dashDistance = 0f;
      return false;
    }

    public Vector2 GetSpawnPointOnCircle(Vector2 hintPos) => hintPos;
  }
}
