using System;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  /// <summary>订阅句柄，用?O(1) 取消订阅?/summary>
  public struct EventListenerHandle
  {
    internal Type EventType;
    internal int Id;
    public bool Valid { get; internal set; }

    public static readonly EventListenerHandle Invalid = default;
  }
}
