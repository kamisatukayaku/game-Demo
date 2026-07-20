using System.Collections.Generic;
using System;

using UnityEngine;
using Game.Shared.Core;

namespace Game.World
{
  /// <summary>
  /// 世界地图管理器 — 管理地图上所有 POI 标记的数据层。
  ///
  /// 实现 IWorldSystem，由 WorldManager 驱动生命周期。
  /// 实现 IWorldSystem_OnWorldGenerated，在世界生成完毕后初始化标记。
  ///
  /// 职责：
  ///   1. 从 WorldGenerator.WorldGenResult 加载初始标记
  ///   2. 跟踪标记的发现/访问/摧毁状态
  ///   3. 营地被摧毁时自动更新标记状态
  ///   4. 提供空间查询接口（按类型/距离/状态）
  ///   5. 广播发现事件供 UI 层响应
  ///
  /// 显示规则：
  ///   ✓ 显示：营地、Boss、商人、事件点
  ///   ✗ 不显示：怪物、地格、掉落物
  ///
  /// 使用方式：
  ///   var map = WorldManager.Instance.GetSystem<WorldMapManager>();
  ///   var camps = map.GetMarkersByType(MapMarker.MarkerType.Camp);
  ///   var nearby = map.GetMarkersInRadius(playerPos, 50f);
  /// </summary>
  public class WorldMapManager : IWorldSystem, IWorldSystem_OnWorldGenerated
  {
    public float DiscoveryRadius { get; set; } = 40f;
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态
    // ══════════════════════════════════════════════════════

    /// <summary>所有标记（ID → marker）</summary>
    public IReadOnlyDictionary<string, MapMarker> AllMarkers => _markers;

    /// <summary>地图尺寸（世界格?/summary>
    public int MapSize { get; private set; }

    /// <summary>标记数量变更事件：(count by type)</summary>
    public event Action MarkersChanged;

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    readonly Dictionary<string, MapMarker> _markers = new();
    readonly Dictionary<MapMarker.MarkerType, List<MapMarker>> _byType = new();
    Transform _playerTransform;
    bool _initialized;
    bool _paused;
    bool _mapGenerated;

    public void Initialize()
    {
      if (_initialized) return;
      WorldDatabase.EnsureLoaded();
      _byType.Clear();
      foreach (MapMarker.MarkerType t in Enum.GetValues(typeof(MapMarker.MarkerType))) _byType[t] = new List<MapMarker>();
      FindPlayer();
      WorldEventBus.CampDestroyed += OnCampDestroyed;
      _initialized = true;
      if (DebugLog) Debug.Log("[WorldMapManager] Initialized.");
    }

    public void Tick(float deltaTime)
    {
      if (!_initialized || _paused || !_mapGenerated) return;

      // 周期性更新玩家视野 → 发现新标记
      UpdatePlayerDiscovery();

      // 周期性标记已访问（对近距离标记自动转为 Visited）
      TryMarkVisited();
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      WorldEventBus.CampDestroyed -= OnCampDestroyed;
      _markers.Clear();
      foreach (var list in _byType.Values) list.Clear();
      _initialized = false;
      if (DebugLog) Debug.Log("[WorldMapManager] Shut down.");
    }

    public void OnWorldGenerated(WorldGenResult result)
    {
      if (!_initialized) return;
      if (result == null || result.Placements == null)
      {
        Debug.LogWarning("[WorldMapManager] WorldGenResult is null or has no placements, skipping marker init.");
        return;
      }
      foreach (var entry in result.Placements)
      {
        MapMarker marker;
        switch (entry.Category)
        {
          case "Camp":
            marker = MapMarker.CreateCamp(entry.Position, entry.TypeId, $"gen_{entry.Position:F0}", 1);
            marker.State = MapMarker.DiscoveryState.Hidden; // 初始隐藏，玩家靠近时发现
            _byType[MapMarker.MarkerType.Camp].Add(marker); break;
          case "Boss":
            // 最终Boss使用独立图标
            if (entry.TypeId != null && entry.TypeId.StartsWith("final_boss_"))
            {
              marker = MapMarker.CreateFinalBoss(entry.Position, entry.TypeId);
              marker.State = MapMarker.DiscoveryState.Hidden;
              _byType[MapMarker.MarkerType.FinalBoss].Add(marker);
            }
            else
            {
              marker = MapMarker.CreateBoss(entry.Position, entry.TypeId);
              marker.State = MapMarker.DiscoveryState.Hidden;
              _byType[MapMarker.MarkerType.WildBoss].Add(marker);
            }
            break;
          case "Merchant":
            marker = MapMarker.CreateMerchant(entry.Position, entry.TypeId);
            marker.State = MapMarker.DiscoveryState.Hidden;
            _byType[MapMarker.MarkerType.Merchant].Add(marker); break;
          case "Event":
            marker = MapMarker.CreateEventPoint(entry.Position, entry.TypeId);
            marker.State = MapMarker.DiscoveryState.Hidden;
            _byType[MapMarker.MarkerType.EventPoint].Add(marker); break;
          default:
            if (DebugLog) Debug.LogWarning($"[WorldMapManager] Unknown category '{entry.Category}', skipping.");
            continue;
        }
        _markers[marker.MarkerId] = marker;
      }
      _mapGenerated = true;
      MarkersChanged?.Invoke();
      if (DebugLog)
        Debug.Log($"[WorldMapManager] Map markers initialized. Total={_markers.Count} camps={GetMarkerCount(MapMarker.MarkerType.Camp)} " +
                  $"bosses={GetMarkerCount(MapMarker.MarkerType.WildBoss)} merchants={GetMarkerCount(MapMarker.MarkerType.Merchant)} " +
                  $"events={GetMarkerCount(MapMarker.MarkerType.EventPoint)}");
    }

    // ══════════════════════════════════════════════════════
    //  营地摧毁 → 更新标记
    // ══════════════════════════════════════════════════════

    void OnCampDestroyed(string campId, WorldCampData campData)
    {
      // 通过 campId 匹配标记
      if (_markers.TryGetValue(campId, out var marker))
      {
        marker.State = MapMarker.DiscoveryState.Destroyed;
        marker.IsDestroyed = true;

        if (DebugLog)
          Debug.Log($"[WorldMapManager] Marker '{campId}' marked as Destroyed.");

        MarkersChanged?.Invoke();
      }
      else
      {
        // 营地可能不是从 WorldGenResult 创建的（如手动创建的 CampController）
        // 尝试通过 campData.WorldPosition 匹配
        foreach (var kv in _markers)
        {
          var m = kv.Value;
          if (m.Type == MapMarker.MarkerType.Camp &&
              Vector2.Distance(m.WorldPosition, campData.WorldPosition) < 1f)
          {
            m.State = MapMarker.DiscoveryState.Destroyed;
            m.IsDestroyed = true;
            MarkersChanged?.Invoke();
            break;
          }
        }
      }
    }

    void UpdatePlayerDiscovery()
    {
      if (_playerTransform == null) return;
      var playerPos = (Vector2)_playerTransform.position;
      foreach (var kv in _markers)
      {
        var marker = kv.Value;
        if (marker.State != MapMarker.DiscoveryState.Hidden) continue;
        if (Vector2.Distance(playerPos, marker.WorldPosition) <= DiscoveryRadius)
        {
          marker.State = MapMarker.DiscoveryState.Discovered;
          WorldEventBus.FireRegionDiscovered(marker.MarkerId);
        }
      }
    }

    // ══════════════════════════════════════════════════════
    //  查询 API
    // ══════════════════════════════════════════════════════

    /// <summary>获取指定类型的所有标记</summary>
    public IReadOnlyList<MapMarker> GetMarkersByType(MapMarker.MarkerType type)
    {
      return _byType.TryGetValue(type, out var list) ? list : Array.Empty<MapMarker>();
    }

    public List<MapMarker> GetMarkersByTypeAndState(MapMarker.MarkerType type, MapMarker.DiscoveryState state)
    {
      var result = new List<MapMarker>();
      if (!_byType.TryGetValue(type, out var list)) return result;
      foreach (var m in list) if (m.State == state) result.Add(m);
      return result;
    }

    /// <summary>获取指定半径内的所有标记</summary>
    public List<MapMarker> GetMarkersInRadius(Vector2 center, float radius,
      MapMarker.DiscoveryState? stateFilter = null)
    {
      var result = new List<MapMarker>();
      foreach (var kv in _markers)
      {
        var m = kv.Value;
        if (stateFilter.HasValue && m.State != stateFilter.Value) continue;

        var dist = Vector2.Distance(center, m.WorldPosition);
        if (dist <= radius) result.Add(m);
      }
      return result;
    }

    /// <summary>获取玩家已发现的所有标记（供 UI 层渲染地图用）</summary>
    public List<MapMarker> GetVisibleMarkers()
    {
      var result = new List<MapMarker>();
      foreach (var kv in _markers)
      {
        var m = kv.Value;
        if (m.State != MapMarker.DiscoveryState.Hidden)
          result.Add(m);
      }
      return result;
    }

    /// <summary>获取所有标记（含 Hidden 状态）。供小地图渲染全部图标用。</summary>
    public List<MapMarker> GetAllMarkers()
    {
      var result = new List<MapMarker>();
      foreach (var kv in _markers)
        result.Add(kv.Value);
      return result;
    }

    /// <summary>
    /// 获取指定标记对应的实时营地运行时数据。
    /// 通过匹配 Marker.SubTypeId 和 WorldRuntimeContext.Camps 中的 CampTypeId 实现关联。
    /// </summary>
    public WorldCampData? GetCampInfoForMarker(MapMarker marker)
    {
      if (marker == null || marker.Type != MapMarker.MarkerType.Camp) return null;
      // 尝试直接通过 MarkerId 匹配（营地实例化时使用 campInstanceId 作为 MarkerId）
      if (WorldRuntimeContext.Camps.TryGetValue(marker.MarkerId, out var data))
        return data;
      // 回退：通过 world position 匹配
      foreach (var kv in WorldRuntimeContext.Camps)
      {
        if (Vector2.Distance(kv.Value.WorldPosition, marker.WorldPosition) < 2f)
          return kv.Value;
      }
      return null;
    }

    /// <summary>按 ID 获取标记</summary>
    public MapMarker GetMarker(string markerId)
    {
      if (string.IsNullOrEmpty(markerId)) return null;
      _markers.TryGetValue(markerId, out var marker);
      return marker;
    }

    public int GetMarkerCount(MapMarker.MarkerType type)
    {
      return _byType.TryGetValue(type, out var list) ? list.Count : 0;
    }

    public MapMarker GetNearestMarkerOfType(MapMarker.MarkerType type, Vector2 fromPos)
    {
      if (!_byType.TryGetValue(type, out var list) || list.Count == 0) return null;
      MapMarker nearest = null; float minDist = float.MaxValue;
      foreach (var m in list)
      {
        if (m.State == MapMarker.DiscoveryState.Destroyed) continue;
        var dist = Vector2.Distance(fromPos, m.WorldPosition);
        if (dist < minDist) { minDist = dist; nearest = m; }
      }
      return nearest;
    }

    // ══════════════════════════════════════════════════════
    //  手动标记管理（供动态添加/移除）
    // ══════════════════════════════════════════════════════

    /// <summary>动态添加一个标记</summary>
    public void AddMarker(MapMarker marker)
    {
      if (marker == null || string.IsNullOrEmpty(marker.MarkerId)) return;
      _markers[marker.MarkerId] = marker;
      _byType[marker.Type].Add(marker);
      MarkersChanged?.Invoke();
    }

    /// <summary>
    /// 更新标记 ID（字典重键）。由 CampPlacementSystem 在营地实例化后调用，
    /// 将 MapMarker 的 MarkerId 从生成的临时 ID 同步为营地实例 ID。
    /// </summary>
    public void UpdateMarkerId(string oldId, string newId)
    {
      if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId)) return;
      if (oldId == newId) return;
      if (!_markers.TryGetValue(oldId, out var marker)) return;

      _markers.Remove(oldId);
      marker.MarkerId = newId;
      _markers[newId] = marker;

      if (DebugLog)
        Debug.Log($"[WorldMapManager] Updated marker ID: '{oldId}' → '{newId}'");
    }

    /// <summary>移除一个标记</summary>
    public bool RemoveMarker(string markerId)
    {
      if (!_markers.TryGetValue(markerId, out var marker)) return false;
      _markers.Remove(markerId);
      _byType[marker.Type].Remove(marker);
      MarkersChanged?.Invoke();
      return true;
    }

    public void MarkAsVisited(string markerId)
    {
      var marker = GetMarker(markerId);
      if (marker != null && marker.State == MapMarker.DiscoveryState.Discovered)
      {
        marker.State = MapMarker.DiscoveryState.Visited;
        MarkersChanged?.Invoke();
      }
    }

    void FindPlayer()
    {
      var go = GameObject.FindWithTag("Player");
      if (go == null) go = GameObject.Find("Player");
      if (go != null) _playerTransform = go.transform;
    }

    /// <summary>
    /// 对玩家近距离（DiscoveryRadius / 2 内）的 Discovered 标记自动转为 Visited。
    /// 这样玩家"经过"标记附近后，标记变为可查看详情的状态。
    /// </summary>
    void TryMarkVisited()
    {
      if (_playerTransform == null) return;
      var playerPos = (Vector2)_playerTransform.position;

      foreach (var kv in _markers)
      {
        var marker = kv.Value;
        if (marker.State != MapMarker.DiscoveryState.Discovered) continue;

        if (Vector2.Distance(playerPos, marker.WorldPosition) <= DiscoveryRadius * 0.5f)
        {
          marker.State = MapMarker.DiscoveryState.Visited;
          if (DebugLog)
            Debug.Log($"[WorldMapManager] Marker '{marker.MarkerId}' marked as Visited (dist={Vector2.Distance(playerPos, marker.WorldPosition):F1}).");
        }
      }
    }
  }
}
