using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.World
{
  /// <summary>
  /// 事件管理器（节点图版本）。
  /// 管理事件的选择、节点推进、效果执行和生命周期。
  /// 事件生成逻辑不变，仅执行逻辑改为节点图。
  /// </summary>
  public class EventManager : IWorldSystem
  {
    public bool DebugLog { get; set; }

    EventNodeExecutor _executor;
    bool _initialized;
    bool _paused;

    // 当前正在进行的事件
    WorldDatabase.EventDef _activeEvent;
    string _currentNodeId;
    string _suspendParentNodeId; // 暂离时保存的父节点ID（下次从此恢复）
    EventContext _currentContext;

    /// <summary>当前是否有活跃事件</summary>
    public bool HasActiveEvent => _activeEvent != null;
    /// <summary>活跃事件定义</summary>
    public WorldDatabase.EventDef ActiveEvent => _activeEvent;
    /// <summary>当前节点上下文（供 UI 读取）</summary>
    public EventContext CurrentContext => _currentContext;

    public void SetExecutor(EventNodeExecutor executor)
    {
      _executor = executor;
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;
      WorldDatabase.EnsureLoaded();
      _initialized = true;
    }

    public void Tick(float deltaTime) { }
    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      _activeEvent = null;
      _currentNodeId = null;
      _suspendParentNodeId = null;
      _currentContext = null;
      _initialized = false;
    }

    // ══════════════════════════════════════════════════════
    //  选择并启动事件
    // ══════════════════════════════════════════════════════

    public string SelectAndTrigger(string triggerType)
    {
      if (!_initialized || _paused) return null;

      // 有挂起的事件 → 恢复而不是新事件
      if (HasActiveEvent && !string.IsNullOrEmpty(_suspendParentNodeId))
      {
        return ResumeSuspendedEvent();
      }

      if (HasActiveEvent) return null;

      var candidates = GetEligibleEvents(WorldRuntimeContext.WorldLevel);
      if (candidates == null || candidates.Count == 0) return null;

      var selected = WeightedRandomPick(candidates);
      if (selected == null) return null;

      WorldRuntimeContext.ActivateEvent(selected.id);
      _activeEvent = selected;
      _currentNodeId = "start";

      // 加载第一个节点
      _currentContext = _executor.Start(selected);

      if (_currentContext == null || _currentContext.IsEnd)
      {
        FinalizeEvent(0);
        return selected.id;
      }

      WorldEventBus.FireEventPresented(selected.id);

      if (DebugLog)
        Debug.Log($"[EventManager] Event '{selected.id}' started, node={_currentNodeId} " +
                  $"options={_currentContext?.Options?.Length ?? 0} trigger={triggerType}");

      return selected.id;
    }

    /// <summary>恢复挂起的事件，从暂离时的上级节点重新加载。</summary>
    string ResumeSuspendedEvent()
    {
      if (_activeEvent == null || string.IsNullOrEmpty(_suspendParentNodeId)) return null;

      var parentNodeId = _suspendParentNodeId;
      _currentNodeId = parentNodeId;
      _suspendParentNodeId = null;

      // 重新加载暂离时的上级节点（选项会重新显示）
      _currentContext = _executor.RestartFromNode(_activeEvent, parentNodeId);

      if (_currentContext == null || _currentContext.IsEnd)
      {
        FinalizeEvent(0);
        return _activeEvent.id;
      }

      WorldEventBus.FireEventPresented(_activeEvent.id);

      if (DebugLog)
        Debug.Log($"[EventManager] Event '{_activeEvent.id}' resumed from node '{parentNodeId}' " +
                  $"options={_currentContext?.Options?.Length ?? 0}");

      return _activeEvent.id;
    }

    /// <summary>玩家选择了某个选项后的处理。</summary>
    public bool ResolveCurrent(int optionIndex)
    {
      if (_activeEvent == null || _currentContext == null) return false;
      if (!_currentContext.HasOptions) return false;

      if (optionIndex < 0 || optionIndex >= _currentContext.Options.Length) return false;

      var chosen = _currentContext.Options[optionIndex];
      if (chosen == null || string.IsNullOrEmpty(chosen.next)) return false;

      // 推进到下一个节点
      _currentContext = _executor.Advance(_activeEvent, _currentNodeId, chosen.next);
      if (_currentContext == null)
      {
        FinalizeEvent(optionIndex);
        return true;
      }

      if (_currentContext.IsEnd)
      {
        FinalizeEvent(optionIndex);
        return true;
      }

      if (!_currentContext.HasOptions)
      {
        // 自动推进节点已在 LoadNode 中处理完成
        FinalizeEvent(optionIndex);
        return true;
      }

      _currentNodeId = _currentContext.Node?.node_id;
      return true;
    }

    public void CancelEvent()
    {
      if (_activeEvent == null) return;
      WorldRuntimeContext.DeactivateEvent(_activeEvent.id);
      _activeEvent = null;
      _currentNodeId = null;
      _suspendParentNodeId = null;
      _currentContext = null;
    }

    /// <summary>暂离事件：保存当前节点为挂起点，事件保留不销毁。下次触发时从上次暂停的上级节点继续。</summary>
    public void SuspendEvent(int choiceIndex)
    {
      if (_activeEvent == null || _currentContext == null) return;
      if (!_currentContext.HasOptions) return;

      // 保存当前节点ID为挂起点
      _suspendParentNodeId = _currentNodeId;

      // 不执行 Finalize — 事件保持活跃
      WorldEventBus.FireEventSuspended(_activeEvent.id);

      // 清除上下文但不销毁事件
      _currentNodeId = null;
      _currentContext = null;

      if (DebugLog)
        Debug.Log($"[EventManager] Event '{_activeEvent.id}' suspended at node '{_suspendParentNodeId}'. " +
                  $"Will resume from this node next time.");
    }

    void FinalizeEvent(int choiceIndex)
    {
      var evt = _activeEvent;
      if (evt == null) return;

      WorldRuntimeContext.MarkEventTriggered(evt.id);
      if (!evt.is_repeatable)
        WorldRuntimeContext.DeactivateEvent(evt.id);

      WorldEventBus.FireRandomEventTriggered(evt.id, choiceIndex);
      WorldEventBus.FireEventResolved(evt.id, choiceIndex, "node_graph", 1f);

      _activeEvent = null;
      _currentNodeId = null;
      _suspendParentNodeId = null;
      _currentContext = null;

      if (DebugLog)
        Debug.Log($"[EventManager] Event '{evt.id}' finalized.");
    }

    // ══════════════════════════════════════════════════════
    //  筛选与选择
    // ══════════════════════════════════════════════════════

    List<WorldDatabase.EventDef> GetEligibleEvents(int worldLevel)
    {
      var allEvents = WorldDatabase.Events;
      var candidates = new List<WorldDatabase.EventDef>();
      foreach (var kv in allEvents)
      {
        var ev = kv.Value;
        if (ev == null) continue;
        if (ev.min_world_level > worldLevel) continue;
        if (!ev.is_repeatable && WorldRuntimeContext.TriggeredEvents.Contains(ev.id)) continue;
        if (ev.weight <= 0f) continue;
        candidates.Add(ev);
      }
      return candidates;
    }

    static WorldDatabase.EventDef WeightedRandomPick(List<WorldDatabase.EventDef> candidates)
    {
      if (candidates.Count == 0) return null;
      float totalWeight = 0f;
      foreach (var ev in candidates) totalWeight += ev.weight;
      if (totalWeight <= 0f) return candidates[Random.Range(0, candidates.Count)];
      var roll = Random.Range(0f, totalWeight);
      float cumulative = 0f;
      foreach (var ev in candidates)
      {
        cumulative += ev.weight;
        if (roll <= cumulative) return ev;
      }
      return candidates[candidates.Count - 1];
    }

    // ══════════════════════════════════════════════════════
    //  调试
    // ══════════════════════════════════════════════════════

    public void Debug_TriggerEvent(string eventId)
    {
      var def = WorldDatabase.GetEvent(eventId);
      if (def == null) return;
      _activeEvent = def;
      _currentNodeId = "start";
      _currentContext = _executor.Start(def);
      WorldEventBus.FireEventPresented(eventId);
    }

    public void Debug_ResolveEvent(int choiceIndex)
    {
      ResolveCurrent(choiceIndex);
    }
  }
}
