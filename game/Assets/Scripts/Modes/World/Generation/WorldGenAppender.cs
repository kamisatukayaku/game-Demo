using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 世界追加生成管理器 — IWorldSystem。
  ///
  /// 职责：
  ///   1. 营地摧毁 → 概率生成 camp_clear 商店
  ///   2. 世界等级/玩家等级/定时器 → 按 world_events.json 配置生成事件点
  ///   3. 生成成功后通过 WorldEventBus 通知 UI 层显示字幕提示
  /// </summary>
  public class WorldGenAppender : IWorldSystem, IWorldSystem_OnWorldGenerated
  {
    public bool DebugLog { get; set; }

    WorldGenerator _generator;
    List<PlacementEntry> _existingPlacements;

    // 事件生成配置
    WorldEventSpawnRule[] _eventRules;
    readonly Dictionary<string, int> _triggerCounts = new();
    float _timerAccum;
    bool _initialized;

    // 事件点生成上限追踪
    int _totalEventPointsGenerated;

    const string EventTypeId = "event_point"; // MapMarker 创建时用的 TypeId

    public void Initialize()
    {
      if (_initialized) return;
      _initialized = true;
      _generator = new WorldGenerator();
      LoadEventRules();
    }

    public void Tick(float deltaTime)
    {
      if (!_initialized) return;

      // 定时器触发
      if (_eventRules != null)
      {
        _timerAccum += deltaTime;
        foreach (var rule in _eventRules)
        {
          if (rule.trigger != "timer_interval") continue;
          if (rule.interval_seconds <= 0f) continue;
          if (_timerAccum < rule.interval_seconds) continue;
          _timerAccum -= rule.interval_seconds;
          TrySpawnEvents(rule, "timer_interval");
        }
      }
    }

    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      _initialized = false;
      _existingPlacements = null;
      _triggerCounts.Clear();
    }

    // ══════════════════════════════════════════════════════
    //  世界生成完成回调
    // ══════════════════════════════════════════════════════

    public void OnWorldGenerated(WorldGenResult result)
    {
      _existingPlacements = result?.Placements ?? new List<PlacementEntry>();
      _triggerCounts.Clear();
      _totalEventPointsGenerated = 0;
      _timerAccum = 0f;

      // 初始生成事件点
      TrySpawnInitialEvents();
    }

    // ══════════════════════════════════════════════════════
    //  商店追加生成
    // ══════════════════════════════════════════════════════

    /// <summary>营地摧毁后必定追加对应类型商店（代替宝箱，价格随营地级别降低）。</summary>
    /// <param name="playerPos">玩家位置</param>
    /// <param name="campTypeId">营地类型ID，用于确定生成的商店类型</param>
    public void TryAppendCampClearShop(Vector2 playerPos, string campTypeId = null)
    {
      if (_generator == null || _existingPlacements == null) return;

      // 根据营地类型确定商店ID
      var shopTypeId = "camp_clear_normal"; // 默认
      string shopTag = "清剿商店";
      if (!string.IsNullOrEmpty(campTypeId))
      {
        var campType = WorldDatabase.GetCampType(campTypeId);
        if (campType != null && !string.IsNullOrEmpty(campType.shop_on_destroy))
        {
          shopTypeId = campType.shop_on_destroy;
          shopTag = campType.shop_tag ?? "清剿商店";
        }
      }

      float nearMin = 25f;
      float nearMax = 50f;
      var config = new WorldGenConfig
      {
        MerchantConfig = new WorldGenCategoryConfig
        {
          Entries = new List<WorldGenTypeEntry>
          {
            new WorldGenTypeEntry { TypeId = shopTypeId, Count = 1,
              MinSameTypeDistance = 40f, MinSameCategoryDistance = 30f, MinAnyDistance = 20f }
          }
        }
      };

      var previousCount = _existingPlacements.Count;
      var newPlacements = _generator.AppendGenerateNear(config, playerPos, nearMin, nearMax, _existingPlacements);

      if (newPlacements != null && newPlacements.Count > previousCount)
      {
        _existingPlacements = newPlacements;

        // 同步到 WorldMapManager
        NotifyMapManager(newPlacements, previousCount);

        // 字幕提示
        WorldEventBus.FireSubtitleShown($"一家来自{shopTag}的商店出现了！价格大幅降低！");

        if (DebugLog)
          Debug.Log($"[WorldGenAppender] Appended '{shopTypeId}' shop near player (camp={campTypeId}).");
      }
    }

    // ══════════════════════════════════════════════════════
    //  事件点追加生成
    // ══════════════════════════════════════════════════════

    /// <summary>世界等级提升时调用。</summary>
    public void OnWorldLevelUp() => TrySpawnEventsByTrigger("world_level_up");

    /// <summary>玩家等级提升时调用。</summary>
    public void OnPlayerLevelUp() => TrySpawnEventsByTrigger("player_level_up");

    /// <summary>营地摧毁时调用（除商店外也尝试生成事件点）。</summary>
    public void OnCampDestroyed(Vector2 playerPos)
    {
      _existingPlacements = WorldRuntimeContext.MapData?.Placements;
      TrySpawnEventsByTrigger("camp_destroyed", playerPos);
    }

    void TrySpawnEventsByTrigger(string trigger, Vector2? playerPos = null)
    {
      if (_eventRules == null) return;
      foreach (var rule in _eventRules)
      {
        if (rule.trigger != trigger) continue;
        TrySpawnEvents(rule, trigger, playerPos);
      }
    }

    void TrySpawnInitialEvents()
    {
      if (_eventRules == null) return;
      foreach (var rule in _eventRules)
      {
        if (rule.trigger != "world_generated") continue;
        TrySpawnEvents(rule, "world_generated");
      }
    }

    void TrySpawnEvents(WorldEventSpawnRule rule, string trigger, Vector2? playerPos = null)
    {
      // 检查最大触发次数
      int count = _triggerCounts.TryGetValue(trigger, out var c) ? c : 0;
      if (rule.max_total > 0 && count >= rule.max_total) return;
      if (_totalEventPointsGenerated >= rule.max_total && rule.max_total > 0) return;

      // 概率判定
      if (Random.value > rule.probability) return;

      int spawnCount = rule.count_per_trigger > 0
        ? rule.count_per_trigger
        : Random.Range(rule.count_min, rule.count_max + 1);

      int actualSpawned = 0;

      for (int i = 0; i < spawnCount; i++)
      {
        bool success = SpawnOneEventPoint(rule, playerPos);
        if (success) actualSpawned++;
      }

      if (actualSpawned > 0)
      {
        _triggerCounts[trigger] = count + actualSpawned;
        _totalEventPointsGenerated += actualSpawned;

        // 字幕提示
        string msg = actualSpawned > 1
          ? $"{actualSpawned}个新的事件出现了！"
          : "一个新的事件出现了！";
        WorldEventBus.FireSubtitleShown(msg);

        if (DebugLog)
          Debug.Log($"[WorldGenAppender] Spawned {actualSpawned} event points (trigger={trigger}).");
      }
    }

    bool SpawnOneEventPoint(WorldEventSpawnRule rule, Vector2? playerPos)
    {
      if (_generator == null || _existingPlacements == null) return false;

      Vector2? center = null;
      float nearMin = 0f, nearMax = 0f;

      if (rule.position == "near_player_ring")
      {
        if (playerPos == null)
        {
          var player = GameObject.FindGameObjectWithTag("Player");
          if (player == null) return false;
          playerPos = player.transform.position;
        }
        center = playerPos.Value;
        nearMin = rule.near_min;
        nearMax = rule.near_max;
      }

      var config = new WorldGenConfig
      {
        EventConfig = new WorldGenCategoryConfig
        {
          Entries = new List<WorldGenTypeEntry>
          {
            new WorldGenTypeEntry { TypeId = EventTypeId, Count = 1,
              MinSameTypeDistance = 25f, MinSameCategoryDistance = 20f, MinAnyDistance = 10f }
          }
        }
      };

      var previousCount = _existingPlacements.Count;
      List<PlacementEntry> newPlacements;

      if (center.HasValue && nearMax > 0f)
        newPlacements = _generator.AppendGenerateNear(config, center.Value, nearMin, nearMax, _existingPlacements);
      else
        newPlacements = _generator.AppendGenerate(config, _existingPlacements);

      if (newPlacements != null && newPlacements.Count > previousCount)
      {
        _existingPlacements = newPlacements;
        NotifyMapManager(newPlacements, previousCount);
        return true;
      }

      return false;
    }

    void LoadEventRules()
    {
      var path = System.IO.Path.Combine(Application.dataPath, "../../data/world/world_events.json");
      if (!System.IO.File.Exists(path)) return;

      try
      {
        var json = System.IO.File.ReadAllText(path);
        var root = JsonUtility.FromJson<WorldEventSpawnRoot>(json);
        _eventRules = root?.event_points;
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[WorldGenAppender] Failed to load world_events.json: {e.Message}");
      }
    }

    // ══════════════════════════════════════════════════════
    //  MapManager 同步
    // ══════════════════════════════════════════════════════

    void NotifyMapManager(List<PlacementEntry> placements, int fromIndex)
    {
      var mapMgr = WorldManager.Instance?.GetSystem<WorldMapManager>();
      if (mapMgr == null) return;

      for (int i = fromIndex; i < placements.Count; i++)
      {
        var entry = placements[i];
        if (entry == null || entry.Category != "Event") continue;

        var marker = MapMarker.CreateEventPoint(entry.Position, entry.TypeId);
        marker.State = MapMarker.DiscoveryState.Hidden;
        mapMgr.AddMarker(marker);
      }
    }

    // ══════════════════════════════════════════════════════
    //  数据类
    // ══════════════════════════════════════════════════════

    [System.Serializable]
    class WorldEventSpawnRoot { public WorldEventSpawnRule[] event_points; }

    [System.Serializable]
    class WorldEventSpawnRule
    {
      public string trigger;
      public float probability;
      public string position;
      public float near_min, near_max;
      public int count_min, count_max;
      public int count_per_trigger;
      public int max_total;
      public float interval_seconds;
    }
  }
}
