using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using Game.Shared.Runtime.Physics;
using Game.Shared.Enemy.Death;
using Game.Shared.Enemy.Visual;

namespace Game.DevTools.Sandbox
{
  public class SandboxSpawner
  {
    readonly Transform _root;
    readonly EnemySpawner _spawner;
    readonly List<GameObject> _spawned = new();

    public SandboxSpawner(Transform root, EnemySpawner spawner)
    {
      _root = root;
      _spawner = spawner;
    }

    public IReadOnlyList<GameObject> Spawned => _spawned;

    public GameObject SpawnDummy(Vector2 offset) => Spawn("mob_hex_01", offset);

    public GameObject SpawnElite(Vector2 offset) => Spawn("mob_pent_01", offset);

    public GameObject SpawnBoss(Vector2 offset) => Spawn("mob_tri_05", offset);

    public void SpawnSwarm(int count = 6)
    {
      for (var i = 0; i < count; i++)
      {
        var angle = i / (float)count * Mathf.PI * 2f;
        var pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5.5f;
        Spawn("mob_hex_01", pos);
      }
    }

    public GameObject Spawn(string enemyId, Vector2 localOffset)
    {
      var world = (Vector2)_root.TransformPoint(localOffset);
      GameObject go;
      if (_spawner != null)
        go = _spawner.SpawnEnemy(enemyId, world);
      else
        go = CreateFallbackEnemy(enemyId, world);

      if (go != null)
      {
        go.transform.SetParent(_root, true);

        // Boss 类敌人：不冻结 AI，保留技能系统运作
        if (go.GetComponent<BossCore>() != null)
          SandboxEntityRules.ConfigureBossDummy(go);
        else
          SandboxEntityRules.ConfigureTrainingDummy(go);

        _spawned.Add(go);
      }

      return go;
    }

    public void ClearAll()
    {
      for (var i = _spawned.Count - 1; i >= 0; i--)
      {
        if (_spawned[i] != null)
          Object.Destroy(_spawned[i]);
      }

      _spawned.Clear();
    }

    static GameObject CreateFallbackEnemy(string enemyId, Vector2 world)
    {
      var go = new GameObject($"SandboxEnemy_{enemyId}");
      go.transform.position = new Vector3(world.x, world.y, 0f);
      go.AddComponent<Rigidbody2D>().gravityScale = 0f;
      go.AddComponent<CircleCollider2D>().radius = 0.45f;
      go.AddComponent<EntityPlaceholderVisual>();
      var health = go.AddComponent<Health>();
      health.Configure(80f);
      go.AddComponent<BuffContainer>();
      go.AddComponent<EnemyCore>();
      go.AddComponent<EnemyMovement>();
      go.AddComponent<EnemyAttack>();
      go.AddComponent<EnemyDeathHandler>();
      CombatRoot.EnemyRegistry?.Register(go.GetComponent<EnemyCore>());
      SandboxEntityRules.ConfigureTrainingDummy(go);
      return go;
    }
  }
}
