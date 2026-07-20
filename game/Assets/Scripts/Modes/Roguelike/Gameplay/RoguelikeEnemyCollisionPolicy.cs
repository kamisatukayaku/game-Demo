using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Runtime.Physics;

namespace Game.Modes.Roguelike.Gameplay
{
  [DisallowMultipleComponent]
  public sealed class RoguelikeEnemyCollisionPolicy : MonoBehaviour
  {
    static RoguelikeEnemyCollisionPolicy s_instance;
    int _enemyLayer = -1;

    public static void EnsureExists()
    {
      if (s_instance == null)
        new GameObject("_RoguelikeEnemyCollisionPolicy").AddComponent<RoguelikeEnemyCollisionPolicy>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      _enemyLayer = GameplayPhysicsLayers.NameToLayer(GameplayPhysicsLayers.Enemy);
      if (_enemyLayer >= 0)
        Physics2D.IgnoreLayerCollision(_enemyLayer, _enemyLayer, true);
      EntityPhysicsBody.IgnoreEnemyObstacleCollisions = true;
    }

    void OnDestroy()
    {
      if (s_instance != this)
        return;

      if (_enemyLayer >= 0)
        Physics2D.IgnoreLayerCollision(_enemyLayer, _enemyLayer, false);
      EntityPhysicsBody.IgnoreEnemyObstacleCollisions = false;
      s_instance = null;
    }
  }
}
