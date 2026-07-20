

using UnityEngine;
using Game.Shared.Combat.Events;

namespace Game.World
{
  /// <summary>
  /// 事件触发??监听游戏事件并触发对应的随机事件?
  ///
  /// 触发来源（通过事件总线监听）：
  ///   1. 营地被摧毀"    ?WorldEventBus.CampDestroyed
  ///   2. Boss 被击杀    ?CombatEventBus.OnKill（VictimId 否""boss"?
  ///   3. 地图事件点位   ?WorldEventBus.RegionDiscovered（预留）
  ///
  /// 触发后调?EventManager 来选择并执行事件?
  /// 不直接执行事件效果，仅做触发检??委托?EventManager?
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期?
  /// </summary>
  public class EventTrigger : IWorldSystem, System.IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>营地摧毁触发事件的概率（0~1?/summary>
    public float CampDestroyTriggerChance { get; set; } = 0.3f;

    /// <summary>Boss 击杀触发事件的概率（0~1?/summary>
    public float BossKillTriggerChance { get; set; } = 0.6f;

    /// <summary>地图点位触发事件的概率（0~1?/summary>
    public float MapPointTriggerChance { get; set; } = 1.0f;

    /// <summary>同一事件的最小冷却时间（秒），防止连续触?/summary>
    public float CooldownSeconds { get; set; } = 15f;

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部引用
    // ══════════════════════════════════════════════════════

    readonly EventManager _eventManager;
    bool _initialized;
    bool _paused;
    float _lastTriggerTime = -999f;

    // ══════════════════════════════════════════════════════
    //  构速"
    // ══════════════════════════════════════════════════════

    public EventTrigger(EventManager eventManager)
    {
      _eventManager = eventManager;
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      // 订阅营地摧毁
      WorldEventBus.CampDestroyed += OnCampDestroyed;

      // 订阅击杀事件（检?Boss 击杀?
      CombatEventBus.OnKill += OnBossKilled;

      // 订阅地图探索（预留）
      WorldEventBus.RegionDiscovered += OnMapPointDiscovered;

      _initialized = true;

      if (DebugLog)
        Debug.Log("[EventTrigger] Initialized. " +
                  $"campChance={CampDestroyTriggerChance} bossChance={BossKillTriggerChance}");
    }

    public void Tick(float deltaTime)
    {
      // 完全事件驱动
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      WorldEventBus.CampDestroyed -= OnCampDestroyed;
      CombatEventBus.OnKill -= OnBossKilled;
      WorldEventBus.RegionDiscovered -= OnMapPointDiscovered;

      _initialized = false;

      if (DebugLog)
        Debug.Log("[EventTrigger] Shut down.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  触发检浀"
    // ══════════════════════════════════════════════════════

    /// <summary>营地被摧??概率触发事件</summary>
    void OnCampDestroyed(string campId, WorldCampData campData)
    {
      if (!CanTrigger()) return;

      TryTrigger("camp_destroyed", CampDestroyTriggerChance, campId);
    }

    /// <summary>Boss 被击杀 ?概率触发事件</summary>
    void OnBossKilled(CombatEventBus.KillArgs args)
    {
      if (!CanTrigger()) return;
      if (args.IsPlayer) return;

      var victimId = args.VictimId ?? "";
      var isBoss = victimId.Contains("boss") || victimId.Contains("Boss") ||
                   victimId.Contains("wild_boss");
      if (!isBoss) return;

      // 只处理玩家击杀?Boss
      var killer = args.Killer;
      if (killer == null) return;
      var isPlayerKill = killer.CompareTag("Player") || killer.name == "Player";
      if (!isPlayerKill) return;

      TryTrigger("boss_killed", BossKillTriggerChance, victimId);
    }

    /// <summary>地图点位被发??触发事件（预留）</summary>
    void OnMapPointDiscovered(string regionId)
    {
      if (!CanTrigger()) return;

      TryTrigger("map_point", MapPointTriggerChance, regionId);
    }

    // ══════════════════════════════════════════════════════
    //  触发逻辑
    // ══════════════════════════════════════════════════════

    bool CanTrigger()
    {
      if (!_initialized || _paused) return false;
      if (_eventManager == null) return false;

      // 冷却检柀"
      if (Time.time - _lastTriggerTime < CooldownSeconds) return false;

      return true;
    }

    void TryTrigger(string triggerType, float chance, string contextId)
    {
      if (chance <= 0f) return;
      if (chance < 1f && Random.value > chance) return;

      _lastTriggerTime = Time.time;

      var eventId = _eventManager.SelectAndTrigger(triggerType);

      if (DebugLog)
      {
        if (eventId != null)
          Debug.Log($"[EventTrigger] Triggered '{eventId}' from {triggerType}({contextId})");
        else
          Debug.Log($"[EventTrigger] No eligible event for {triggerType}({contextId})");
      }
    }
  }
}
