using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>
  /// 2.5D 碰撞层名（与 TagManager ?PlayerPhysics / EnemyPhysics / TowerPhysics 对应）?
  /// </summary>
  public static class GameplayPhysicsLayers
  {
    public const string Player = "PlayerPhysics";
    public const string Enemy = "EnemyPhysics";
    public const string Obstacle = "TowerPhysics";

    static int s_obstacleMask = -1;

    public static int ObstacleMask
    {
      get
      {
        if (s_obstacleMask < 0)
          s_obstacleMask = LayerMask.GetMask(Obstacle);

        return s_obstacleMask;
      }
    }

    public static int NameToLayer(string layerName) => LayerMask.NameToLayer(layerName);
  }
}