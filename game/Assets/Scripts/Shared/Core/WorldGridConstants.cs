using UnityEngine;
namespace Game.Shared.Core
{

  /// <summary>世界格网常量（地图格、视野、相机）?/summary>
  public static class WorldGridConstants
  {
    /// <summary>单格边长（默?1 世界单位）?/summary>
    public const float TileSize = 1f;

    /// <summary>玩家 Physics2D 圆形碰撞半径（与 PlayerSpriteVisual 直径 2.565 格匹配）。</summary>
    public const float PlayerCollisionRadius = 1.2825f;

    /// <summary>一屏可见放置格边长（格数）。相?orthographicSize = 本?× cellSize / 2?/summary>
    public const int GameplayViewportTiles = 32;

    /// <summary>原型 gameplay 相机半高?2×32 ?/ 屏）?/summary>
    public const float GameplayOrthographicSize = GameplayViewportTiles * TileSize * 0.5f;

    /// <summary>半屏世界距离（沿任一轴约 16 格）?/summary>
    public static float HalfViewportWorldSize(float cellSize = TileSize) =>
      GameplayViewportTiles * cellSize * 0.5f;
  }
}