using System;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  /// <summary>RAII 包装：Dispose 时自?Unsubscribe?/summary>
  public sealed class GameEventSubscription : IDisposable
  {
    EventListenerHandle _handle;

    internal GameEventSubscription(EventListenerHandle handle)
    {
      _handle = handle;
    }

    public void Dispose()
    {
      if (!_handle.Valid)
        return;

      GameEventBus.Unsubscribe(_handle);
      _handle = EventListenerHandle.Invalid;
    }
  }
}