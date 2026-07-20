using System;

namespace Game.World
{
  /// <summary>
  /// World 模式专属事件总线。
  ///
  /// 与 <see cref="Game.Shared.Combat.CombatEventBus"/> 分离，避免污染 Arena 模式的事件通道。
  /// World 子系统之间通过此总线解耦通信。
  ///
  /// 约定：
  ///   - 所有订阅在 OnEnable/OnDisable 或 Initialize/Shutdown 中管理。
  ///   - World 模式结束后 ClearAllSubscribers() 清空所有订阅。
  /// </summary>
  public static class WorldEventBus
  {
    // ── 世界等级事件 ─────────────────────────────────

    /// <summary>世界等级变化?oldLevel, newLevel)</summary>
    public static event Action<int, int> WorldLevelChanged;

    /// <summary>世界等级达到特定阈值（用于解锁新怪物种类等）</summary>
    public static event Action<int> WorldLevelMilestone;

    // ── 营地事件 ─────────────────────────────────────

    /// <summary>营地被摧毁：(campId, campData)</summary>
    public static event Action<string, WorldCampData> CampDestroyed;

    /// <summary>营地等级提升?campId, oldLevel, newLevel)</summary>
    public static event Action<string, int, int> CampLevelUp;

    /// <summary>玩家进入营地范围?campId)</summary>
    public static event Action<string> CampEntered;

    /// <summary>玩家离开营地范围?campId)</summary>
    public static event Action<string> CampExited;

    // ── 经济事件 ─────────────────────────────────────

    /// <summary>金币变化?oldValue, newValue, delta)</summary>
    public static event Action<int, int, int> GoldChanged;

    // ── 玩家事件 ─────────────────────────────────────

    /// <summary>World 模式玩家升级?oldLevel, newLevel)</summary>
    public static event Action<int, int> WorldPlayerLevelUp;

    // ── 地图事件 ─────────────────────────────────────

    /// <summary>世界地图生成完成</summary>
    public static event Action WorldMapGenerated;

    /// <summary>探索新区域：(regionId)</summary>
    public static event Action<string> RegionDiscovered;

    // ── 事件系统 ─────────────────────────────────────

    /// <summary>随机事件触发?eventId, choiceIndex 玩家选择的分支索?</summary>
    public static event Action<string, int> RandomEventTriggered;

    /// <summary>事件已展示给玩家（供 UI 弹出选项）：(eventId)</summary>
    public static event Action<string> EventPresented;

    /// <summary>事件已结算（玩家做了选择）：(eventId, choiceIndex, effectType, effectValue)</summary>
    public static event Action<string, int, string, float> EventResolved;

    /// <summary>事件暂离（玩家选择暂离，事件不结束，下次从上级节点继续）：(eventId)</summary>
    public static event Action<string> EventSuspended;

    // ── 结局事件 ─────────────────────────────────────

    /// <summary>玩家死亡（World 模式）</summary>
    public static event Action PlayerDied;

    /// <summary>单局结束：(endType)</summary>
    public static event Action<WorldEndType> RunEnded;

    /// <summary>字幕提示：(message)</summary>
    public static event Action<string> SubtitleShown;

    // ── Fire 方法 ────────────────────────────────────
    // （所有方法通过 ?.Invoke() 安全调用，无订阅者时不抛异常?

    public static void FireWorldLevelChanged(int oldLevel, int newLevel)
    {
      WorldLevelChanged?.Invoke(oldLevel, newLevel);
    }

    public static void FireWorldLevelMilestone(int milestone)
    {
      WorldLevelMilestone?.Invoke(milestone);
    }

    public static void FireCampDestroyed(string campId, WorldCampData data)
    {
      CampDestroyed?.Invoke(campId, data);
    }

    public static void FireCampLevelUp(string campId, int oldLevel, int newLevel)
    {
      CampLevelUp?.Invoke(campId, oldLevel, newLevel);
    }

    public static void FireCampEntered(string campId)
    {
      CampEntered?.Invoke(campId);
    }

    public static void FireCampExited(string campId)
    {
      CampExited?.Invoke(campId);
    }

    public static void FireGoldChanged(int oldValue, int newValue, int delta)
    {
      GoldChanged?.Invoke(oldValue, newValue, delta);
    }

    public static void FireWorldPlayerLevelUp(int oldLevel, int newLevel)
    {
      WorldPlayerLevelUp?.Invoke(oldLevel, newLevel);
    }

    public static void FireWorldMapGenerated()
    {
      WorldMapGenerated?.Invoke();
    }

    public static void FireRegionDiscovered(string regionId)
    {
      RegionDiscovered?.Invoke(regionId);
    }

    public static void FireRandomEventTriggered(string eventId, int choiceIndex)
    {
      RandomEventTriggered?.Invoke(eventId, choiceIndex);
    }

    public static void FireEventPresented(string eventId)
    {
      EventPresented?.Invoke(eventId);
    }

    public static void FireEventResolved(string eventId, int choiceIndex, string effectType, float effectValue)
    {
      EventResolved?.Invoke(eventId, choiceIndex, effectType, effectValue);
    }

    public static void FireEventSuspended(string eventId)
    {
      EventSuspended?.Invoke(eventId);
    }

    public static void FirePlayerDied()
    {
      PlayerDied?.Invoke();
    }

    public static void FireRunEnded(WorldEndType endType)
    {
      RunEnded?.Invoke(endType);
    }

    public static void FireSubtitleShown(string message)
    {
      SubtitleShown?.Invoke(message);
    }

    // ── 生命周期 ────────────────────────────────────

    /// <summary>
    /// 清空所有事件订阅?
    /// ?WorldManager.Shutdown() 中调用，防止 Arena 模式受残留订阅影响?
    /// </summary>
    public static void ClearAllSubscribers()
    {
      WorldLevelChanged = null;
      WorldLevelMilestone = null;
      CampDestroyed = null;
      CampLevelUp = null;
      CampEntered = null;
      CampExited = null;
      GoldChanged = null;
      WorldPlayerLevelUp = null;
      WorldMapGenerated = null;
      RegionDiscovered = null;
      RandomEventTriggered = null;
      EventPresented = null;
      EventResolved = null;
      EventSuspended = null;
      PlayerDied = null;
      RunEnded = null;
      SubtitleShown = null;
    }
  }
}
