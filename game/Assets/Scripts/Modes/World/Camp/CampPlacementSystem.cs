using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Core;

namespace Game.World
{
  /// <summary>
  /// 营地实例化系统 — 将 WorldGenerator 产出的 PlacementEntry 数据转化为场景中的 CampController GameObject。
  ///
  /// 实现 IWorldSystem + IWorldSystem_OnWorldGenerated，由 WorldManager 统一驱动生命周期。
  ///
  /// 工作流程：
  ///   1. WorldManager.InitWorld() → WorldGenerator.Generate() → 产生 Placements 列表
  ///   2. WorldManager 广播 NotifySystems_OnWorldGenerated()
  ///   3. 本系统 OnWorldGenerated() 遍历 "Camp" 类 PlacementEntry
  ///   4. 为每个营地创建 GameObject，挂载 CampController 并调用 Initialize()
  ///   5. 注册到 DistanceActivationSystem 实现远近 LOD
  ///
  /// GameObject 层级结构：
  ///   _WorldCamps (容器)
  ///     └── Camp_{typeId}_{guid8} (CampController)
  ///           └── CampCore_{id} (CampCore + Health + Collider2D + SpriteRenderer)
  ///
  /// 注意：本系统不负责 Boss/Merchant/Event 的实例化，仅处理 Camp 类。
  /// </summary>
  public class CampPlacementSystem : IWorldSystem, IWorldSystem_OnWorldGenerated
  {
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    GameObject _container;
    DistanceActivationSystem _distanceSystem;
    readonly List<CampController> _spawnedCamps = new();
    bool _initialized;
    bool _paused;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;
      WorldDatabase.EnsureLoaded();
      _initialized = true;
      if (DebugLog) Debug.Log("[CampPlacementSystem] Initialized.");
    }

    public void Tick(float deltaTime)
    {
      if (!_initialized || _paused) return;
      // CampController 自身有 Tick，不需要本系统额外驱动
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      foreach (var camp in _spawnedCamps)
      {
        if (camp != null)
        {
          _distanceSystem?.Unregister(camp);
          camp.Shutdown();
          Object.Destroy(camp.gameObject);
        }
      }
      _spawnedCamps.Clear();
      _initialized = false;
      if (DebugLog) Debug.Log("[CampPlacementSystem] Shut down.");
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem_OnWorldGenerated
    // ══════════════════════════════════════════════════════

    public void OnWorldGenerated(WorldGenResult result)
    {
      if (!_initialized) return;
      if (result == null || result.Placements == null || result.Placements.Count == 0)
      {
        if (DebugLog) Debug.LogWarning("[CampPlacementSystem] WorldGenResult has no placements, skipping camp instantiation.");
        return;
      }

      // 获取 DistanceActivationSystem 引用（在 WorldManager 已注册的系统中查找）
      var wm = WorldManager.Instance;
      _distanceSystem = wm?.GetSystem<DistanceActivationSystem>();

      CreateContainer();

      int spawned = 0;
      int skipped = 0;

      foreach (var entry in result.Placements)
      {
        if (entry == null || entry.Category != "Camp" || string.IsNullOrEmpty(entry.TypeId))
          continue;

        var typeDef = WorldDatabase.GetCampType(entry.TypeId);
        if (typeDef == null)
        {
          if (DebugLog) Debug.LogWarning($"[CampPlacementSystem] Unknown camp type '{entry.TypeId}', skipping.");
          skipped++;
          continue;
        }

        // 检查世界等级要求
        if (typeDef.min_world_level > WorldRuntimeContext.WorldLevel)
        {
          if (DebugLog) Debug.Log($"[CampPlacementSystem] Skipping camp '{entry.TypeId}' at ({entry.Position.x:F0},{entry.Position.y:F0}) — requires WLv.{typeDef.min_world_level}, current WLv.{WorldRuntimeContext.WorldLevel}");
          skipped++;
          continue;
        }

        SpawnCamp(entry, typeDef);
        spawned++;
      }

      if (DebugLog)
        Debug.Log($"[CampPlacementSystem] Camps instantiated: {spawned} spawned, {skipped} skipped (total placements: {result.Placements.Count}).");
    }

    /// <summary>
    /// 设置 DistanceActivationSystem 引用（由 WorldManager.CreateWorldSystems 在注册后调用）。
    /// 如果 CampPlacementSystem 在 DistanceActivationSystem 之前注册，
    /// 则 OnWorldGenerated 时会自动通过 WorldManager.GetSystem 查找。
    /// </summary>
    public void SetDistanceSystem(DistanceActivationSystem system)
    {
      _distanceSystem = system;
    }

    // ══════════════════════════════════════════════════════
    //  营地创建
    // ══════════════════════════════════════════════════════

    void CreateContainer()
    {
      if (_container != null) return;

      _container = new GameObject("_WorldCamps");
      _container.transform.SetParent(WorldManager.Instance?.transform, false);
      _container.transform.position = Vector3.zero;
    }

    void SpawnCamp(PlacementEntry entry, WorldDatabase.CampTypeDef typeDef)
    {
      // 创建营地 GameObject
      var campGo = new GameObject($"Camp_{entry.TypeId}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}");
      campGo.transform.SetParent(_container.transform, false);
      campGo.transform.position = new Vector3(entry.Position.x, entry.Position.y, 0);

      // 挂载 CampController
      var controller = campGo.AddComponent<CampController>();

      // 通过反射设置私有字段 _campTypeId（因为它是 [SerializeField] private）
      SetPrivateField(controller, "_campTypeId", entry.TypeId);
      // 同步设置 _alertRadius 和 _activeRadius（来自 Controller 默认值或按类型调整）
      // 默认值已合理（alert=16f, active=8f），保持不动

      // 创建 CampCore 子对象
      var coreGo = new GameObject("CampCore");
      coreGo.transform.SetParent(campGo.transform, false);
      coreGo.transform.localPosition = Vector3.zero;

      // CampCore 需要 [RequireComponent(typeof(Health))]，它会自己添加 Health
      var core = coreGo.AddComponent<CampCore>();

      // 通过反射设置 controller 的 _campCore 引用
      SetPrivateField(controller, "_campCore", core);

      // 初始化营地（加载配表、创建等级逻辑、刷怪逻辑、注册到 WorldRuntimeContext）
      controller.Initialize();

      // 注册到 DistanceActivationSystem
      if (_distanceSystem != null)
      {
        _distanceSystem.Register(controller);
      }

      _spawnedCamps.Add(controller);

      // 同步标记 ID：将 WorldMapManager 中对应位置标记的 MarkerId 更新为营地的 CampInstanceId
      SyncMarkerId(controller);

      if (DebugLog)
        Debug.Log($"[CampPlacementSystem] Spawned camp '{controller.CampInstanceId}' " +
                  $"type={entry.TypeId} at ({entry.Position.x:F0},{entry.Position.y:F0})");
    }

    /// <summary>
    /// 将 WorldMapManager 中与该营地位置匹配的标记的 MarkerId 同步为营地的 CampInstanceId。
    /// 这样 GetCampInfoForMarker 可以通过 MarkerId 直接索引 WorldRuntimeContext.Camps。
    /// </summary>
    void SyncMarkerId(CampController controller)
    {
      var wm = WorldManager.Instance;
      var mapManager = wm?.MapManager;
      if (mapManager == null) return;

      var campPos = (Vector2)controller.transform.position;
      foreach (var kv in mapManager.AllMarkers)
      {
        var marker = kv.Value;
        if (marker.Type != MapMarker.MarkerType.Camp) continue;
        if (Vector2.Distance(marker.WorldPosition, campPos) < 1f)
        {
          mapManager.UpdateMarkerId(kv.Key, controller.CampInstanceId);
          return;
        }
      }
    }

    /// <summary>通过反射设置私有字段（Unity [SerializeField] 标记的字段）</summary>
    static void SetPrivateField(object target, string fieldName, object value)
    {
      var field = target.GetType().GetField(fieldName,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
      if (field != null)
      {
        field.SetValue(target, value);
      }
      else
      {
        Debug.LogWarning($"[CampPlacementSystem] Could not set field '{fieldName}' on {target.GetType().Name}.");
      }
    }
  }
}
