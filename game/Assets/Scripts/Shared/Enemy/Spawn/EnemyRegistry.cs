using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Enemy.AI;
namespace Game.Shared.Enemy.Spawn
{
  /// <summary>
  /// 敌人注册表。追踪场景中所有活跃敌人，由 CombatRoot 持有。
  /// 替代各处 FindObjectsOfType/FindObjectOfType 查找敌人的模式。
  /// </summary>
  public class EnemyRegistry : MonoBehaviour
  {
    readonly List<EnemyCore> _enemies = new();
    readonly Dictionary<string, List<EnemyCore>> _byId = new();

    public int Count => _enemies.Count;
    public IReadOnlyList<EnemyCore> AllEnemies => _enemies;

    /// <summary>是否有活跃的 lane 类型敌人</summary>
    public bool HasLaneEnemies
    {
      get
      {
        foreach (var e in _enemies)
          if (e.IsLaneEnemy) return true;
        return false;
      }
    }

    /// <summary>是否有活跃的非 lane 类型敌人</summary>
    public bool HasNonLaneEnemies
    {
      get
      {
        foreach (var e in _enemies)
          if (!e.IsLaneEnemy) return true;
        return false;
      }
    }

    // ── Registration ──────────────────────────────

    public void Register(EnemyCore enemy)
    {
      if (enemy == null || _enemies.Contains(enemy)) return;
      _enemies.Add(enemy);
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      Game.Shared.Diagnostics.CombatValidationHooks.OnEnemyRegistered?.Invoke();
#endif

      if (!string.IsNullOrEmpty(enemy.EnemyId))
      {
        if (!_byId.TryGetValue(enemy.EnemyId, out var list))
        {
          list = new List<EnemyCore>();
          _byId[enemy.EnemyId] = list;
        }
        list.Add(enemy);
      }
    }

    public void Unregister(EnemyCore enemy)
    {
      if (enemy == null) return;
      _enemies.Remove(enemy);

      if (!string.IsNullOrEmpty(enemy.EnemyId) && _byId.TryGetValue(enemy.EnemyId, out var list))
      {
        list.Remove(enemy);
        if (list.Count == 0)
          _byId.Remove(enemy.EnemyId);
      }
    }

    // ── Query ──────────────────────────────────────

    /// <summary>敌人是否具有生命组件。无 Health 的实体不可被伤害/不参与命中检测。</summary>
    public static bool HasHealth(EnemyCore enemy)
    {
      return enemy != null && enemy.Health != null && !enemy.Health.IsDead;
    }

    public List<EnemyCore> GetById(string enemyId)
    {
      _byId.TryGetValue(enemyId, out var list);
      return list ?? new List<EnemyCore>();
    }

    public List<EnemyCore> GetInRange(Vector2 center, float range)
    {
      var result = new List<EnemyCore>();
      float rangeSq = range * range;
      foreach (var enemy in _enemies)
      {
        if (!HasHealth(enemy)) continue;
        float distSq = ((Vector2)enemy.transform.position - center).sqrMagnitude;
        if (distSq <= rangeSq)
          result.Add(enemy);
      }
      return result;
    }

    public EnemyCore GetNearest(Vector2 center, float maxRange = float.MaxValue)
    {
      EnemyCore nearest = null;
      float minDistSq = maxRange * maxRange;
      foreach (var enemy in _enemies)
      {
        if (!HasHealth(enemy)) continue;
        float distSq = ((Vector2)enemy.transform.position - center).sqrMagnitude;
        if (distSq < minDistSq)
        {
          minDistSq = distSq;
          nearest = enemy;
        }
      }
      return nearest;
    }

    public EnemyCore GetNearestLane(Vector2 center, float maxRange = float.MaxValue)
    {
      EnemyCore nearest = null;
      float minDistSq = maxRange * maxRange;
      foreach (var enemy in _enemies)
      {
        if (!HasHealth(enemy) || !enemy.IsLaneEnemy) continue;
        float distSq = ((Vector2)enemy.transform.position - center).sqrMagnitude;
        if (distSq < minDistSq)
        {
          minDistSq = distSq;
          nearest = enemy;
        }
      }
      return nearest;
    }

    // ── Cleanup ────────────────────────────────────

    void LateUpdate()
    {
      for (int i = _enemies.Count - 1; i >= 0; i--)
        if (_enemies[i] == null)
          _enemies.RemoveAt(i);

      var keysToRemove = new List<string>();
      foreach (var kvp in _byId)
      {
        kvp.Value.RemoveAll(e => e == null);
        if (kvp.Value.Count == 0)
          keysToRemove.Add(kvp.Key);
      }
      foreach (var key in keysToRemove)
        _byId.Remove(key);
    }
  }
}
