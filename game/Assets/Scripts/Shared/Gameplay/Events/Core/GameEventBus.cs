using System.Collections.Generic;
using System;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  /// <summary>
  /// 全项目统一 gameplay 事件总线?
  /// 热路径无反射；Publish 仅遍历已注册 delegate 列表?
  /// </summary>
  public static class GameEventBus
  {
    static readonly Dictionary<Type, IHandlerList> s_handlers = new();
    static int s_nextHandlerId = 1;

    interface IHandlerList
    {
      int Count { get; }
      bool RemoveById(int id);
    }

    sealed class HandlerList<T> : IHandlerList where T : struct, IGameEvent
    {
      internal readonly List<HandlerEntry<T>> Handlers = new();

      public int Count => Handlers.Count;

      public bool RemoveById(int id)
      {
        for (var index = Handlers.Count - 1; index >= 0; index--)
        {
          if (Handlers[index].Id != id)
            continue;

          Handlers.RemoveAt(index);
          return true;
        }

        return false;
      }
    }

    readonly struct HandlerEntry<T> where T : struct, IGameEvent
    {
      internal readonly int Id;
      internal readonly Action<T> Callback;

      internal HandlerEntry(int id, Action<T> callback)
      {
        Id = id;
        Callback = callback;
      }
    }

    static HandlerList<T> GetOrCreateList<T>() where T : struct, IGameEvent
    {
      var type = typeof(T);
      if (!s_handlers.TryGetValue(type, out var list))
      {
        list = new HandlerList<T>();
        s_handlers[type] = list;
      }

      return (HandlerList<T>)list;
    }

    static HandlerList<T> TryGetList<T>() where T : struct, IGameEvent
    {
      if (!s_handlers.TryGetValue(typeof(T), out var list))
        return null;

      return (HandlerList<T>)list;
    }

    public static EventListenerHandle Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
      if (handler == null)
        return EventListenerHandle.Invalid;

      var list = GetOrCreateList<T>();
      var id = s_nextHandlerId++;
      if (s_nextHandlerId <= 0)
        s_nextHandlerId = 1;

      list.Handlers.Add(new HandlerEntry<T>(id, handler));
      return new EventListenerHandle
      {
        EventType = typeof(T),
        Id = id,
        Valid = true
      };
    }

    public static GameEventSubscription SubscribeScoped<T>(Action<T> handler) where T : struct, IGameEvent
    {
      return new GameEventSubscription(Subscribe(handler));
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
      if (handler == null)
        return;

      var list = TryGetList<T>();
      if (list == null)
        return;

      var handlers = list.Handlers;
      for (var i = handlers.Count - 1; i >= 0; i--)
      {
        if (handlers[i].Callback != handler)
          continue;

        handlers.RemoveAt(i);
        return;
      }
    }

    public static void Unsubscribe(EventListenerHandle handle)
    {
      if (!handle.Valid || handle.EventType == null)
        return;

      if (!s_handlers.TryGetValue(handle.EventType, out var list))
        return;

      list.RemoveById(handle.Id);
    }

    public static void Publish<T>(in T evt) where T : struct, IGameEvent
    {
      var list = TryGetList<T>();
      if (list == null || list.Count == 0)
        return;

      GameEventDebugger.RecordPublished(typeof(T));

      var handlers = list.Handlers;
      for (var i = handlers.Count - 1; i >= 0; i--)
        handlers[i].Callback(evt);
    }

    public static void ClearAll()
    {
      s_handlers.Clear();
    }
  }
}
