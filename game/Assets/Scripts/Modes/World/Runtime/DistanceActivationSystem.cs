using System.Collections.Generic;
using System;

using Game.Shared.Core;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 距离激活系??基于玩家距离的实?LOD 管理?
  ///
  /// 实现 IWorldSystem，由 WorldManager 驱动?
  /// 不影?Arena 模式（通过 IsWorldModeActive 守卫）?
  ///
  /// 规则?
  ///   Level0 (距离 > _level0Radius)        : 超远 ?关闭碰撞/Renderer/AI，仅成长
  ///   Level1 (_level1Radius < 距离 ?_level0Radius) : 中距 ?可视?AI休眠
  ///   Level2 (距离 ?_level1Radius)        : 近距 ?完全激洀"
  ///
  /// 普通怪：Level0 ?SetActive(false)（回对象池等效）
  /// Boss? Level0 ?休眠（保留GameObject，关闭组件）
  ///
  /// ?CampController 集成?
  ///   - CampController 实现 IDistanceActivatable
  ///   - 系统自动管理 CampCore ?Collider/Renderer
  ///   - CampSpawnLogic 的怪物通过此系统管琀"
  ///
  /// 性能策略?
  ///   - ?0.5 秒批量更新一次激活等级（不每帧检查）
  ///   - 仅在等级变化时调?OnActivationLevelChanged
  /// </summary>
  public class DistanceActivationSystem : IWorldSystem
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>Level0 判定距离（世界单位）。超此距??Level0</summary>
    public float Level0Radius { get; set; } = 80f;

    /// <summary>Level1 判定距离（世界单位）。超此距离但 ?Level0Radius ?Level1</summary>
    public float Level1Radius { get; set; } = 40f;

    /// <summary>更新间隔（秒）。降低帧消耗?/summary>
    public float UpdateInterval { get; set; } = 0.5f;

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态"
    // ══════════════════════════════════════════════════════

    /// <summary>已注册的实体数量</summary>
    public int RegisteredCount => _entities.Count;

    /// <summary>各等级的实体数量</summary>
    public int Level0Count { get; private set; }
    public int Level1Count { get; private set; }
    public int Level2Count { get; private set; }

    // ══════════════════════════════════════════════════════
    //  内部状态"
    // ══════════════════════════════════════════════════════

    readonly List<IDistanceActivatable> _entities = new();
    readonly List<IDistanceActivatable> _pendingRemove = new();
    Transform _playerTransform;
    float _updateTimer;
    bool _initialized;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;
      FindPlayer();
      _initialized = true;

      if (DebugLog)
        Debug.Log($"[DistanceActivation] Initialized. L0>{Level0Radius} L1>{Level1Radius}");
    }

    public void Tick(float deltaTime)
    {
      if (!_initialized || !WorldRuntimeContext.IsWorldModeActive) return;

      _updateTimer += deltaTime;
      if (_updateTimer < UpdateInterval) return;
      _updateTimer = 0f;

      if (_playerTransform == null)
      {
        FindPlayer();
        if (_playerTransform == null) return;
      }

      var playerPos = (Vector2)_playerTransform.position;

      // 清理已销毁的实体
      for (int i = _entities.Count - 1; i >= 0; i--)
      {
        if (_entities[i] == null) _entities.RemoveAt(i);
      }

      // 批量更新激活等纀"
      int l0 = 0, l1 = 0, l2 = 0;
      for (int i = 0; i < _entities.Count; i++)
      {
        var entity = _entities[i];
        if (entity == null) continue;

        var dist = Vector2.Distance(playerPos, entity.WorldPosition);
        var newLevel = CalculateLevel(dist, entity);
        var oldLevel = entity.CurrentActivationLevel;

        if (newLevel != oldLevel)
        {
          entity.CurrentActivationLevel = newLevel;
          if (DebugLog)
            Debug.Log($"[DistanceActivation] '{entity.ActivatableId}' {oldLevel}→{newLevel} dist={dist:F1}");

          try { entity.OnActivationLevelChanged(newLevel, oldLevel); }
          catch (Exception e) { Debug.LogError($"[DistanceActivation] Error on {entity.ActivatableId}: {e}"); }
        }

        switch (newLevel)
        {
          case DistanceActivationLevel.Level0_Far: l0++; break;
          case DistanceActivationLevel.Level1_Mid: l1++; break;
          case DistanceActivationLevel.Level2_Near: l2++; break;
        }
      }

      Level0Count = l0; Level1Count = l1; Level2Count = l2;
    }

    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      _entities.Clear();
      _initialized = false;

      if (DebugLog)
        Debug.Log("[DistanceActivation] Shut down.");
    }

    // ══════════════════════════════════════════════════════
    //  注册/注销
    // ══════════════════════════════════════════════════════

    /// <summary>注册一个可激活实?/summary>
    public void Register(IDistanceActivatable entity)
    {
      if (entity == null) return;
      _entities.Add(entity);

      if (DebugLog)
        Debug.Log($"[DistanceActivation] Registered '{entity.ActivatableId}' total={_entities.Count}");
    }

    /// <summary>注销一个实?/summary>
    public void Unregister(IDistanceActivatable entity)
    {
      if (entity == null) return;
      _entities.Remove(entity);

      if (DebugLog)
        Debug.Log($"[DistanceActivation] Unregistered '{entity.ActivatableId}' total={_entities.Count}");
    }

    // ══════════════════════════════════════════════════════
    //  等级计算
    // ══════════════════════════════════════════════════════

    DistanceActivationLevel CalculateLevel(float distance, IDistanceActivatable entity)
    {
      if (entity.CurrentActivationLevel == DistanceActivationLevel.Inactive)
        return DistanceActivationLevel.Inactive;

      if (distance > Level0Radius)
        return DistanceActivationLevel.Level0_Far;

      if (distance > Level1Radius)
        return DistanceActivationLevel.Level1_Mid;

      return DistanceActivationLevel.Level2_Near;
    }

    // ══════════════════════════════════════════════════════
    //  工具方法（供实体?OnActivationLevelChanged 中调用）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// ?GameObject 应用激活等??启用/禁用 Renderer ?Collider?
    /// Level0: 全关闭；Level1+: 全开启?
    /// </summary>
    public static void ApplyToGameObject(GameObject go, DistanceActivationLevel level, bool isBoss = false)
    {
      if (go == null) return;

      var enableComponents = level >= DistanceActivationLevel.Level1_Mid;

      // Renderer
      var renderers = go.GetComponentsInChildren<Renderer>(true);
      foreach (var r in renderers)
        r.enabled = enableComponents;

      // Collider
      var colliders = go.GetComponentsInChildren<Collider2D>(true);
      foreach (var c in colliders)
        c.enabled = enableComponents;

      // 对于普通怪（?Boss），Level0 时直?SetActive(false)
      if (!isBoss && level == DistanceActivationLevel.Level0_Far)
        go.SetActive(false);
      else if (!go.activeSelf)
        go.SetActive(true);
    }

    /// <summary>
    /// 对普通敌人应用距离优化：
    /// Level0 ?SetActive(false)（回池等效）
    /// Level1+ ?SetActive(true)
    /// </summary>
    public static void ApplyToEnemy(GameObject enemy, DistanceActivationLevel level, bool isBoss)
    {
      if (enemy == null) return;

      if (level == DistanceActivationLevel.Level0_Far && !isBoss)
      {
        // 普通??回收
        enemy.SetActive(false);
      }
      else
      {
        if (!enemy.activeSelf) enemy.SetActive(true);
        ApplyToGameObject(enemy, level, isBoss);
      }
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    void FindPlayer()
    {
      var go = GameObject.FindWithTag("Player");
      if (go == null) go = GameObject.Find("Player");
      if (go != null) _playerTransform = go.transform;
    }
  }
}
