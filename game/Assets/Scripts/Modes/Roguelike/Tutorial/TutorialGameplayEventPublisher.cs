using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Publishes movement tutorial events from the player transform.</summary>
  public sealed class TutorialGameplayEventPublisher : MonoBehaviour
  {
    Vector3 _lastPosition;
    bool _hasLast;

    public static void EnsureOn(GameObject player)
    {
      if (player == null)
        return;
      if (player.GetComponent<TutorialGameplayEventPublisher>() == null)
        player.AddComponent<TutorialGameplayEventPublisher>();
    }

    void LateUpdate()
    {
      var pos = transform.position;
      if (!_hasLast)
      {
        _hasLast = true;
        _lastPosition = pos;
        return;
      }

      var delta = pos - _lastPosition;
      _lastPosition = pos;
      if (delta.sqrMagnitude < 0.0004f)
        return;

      var distance = new Vector2(delta.x, delta.y).magnitude;
      GameEventBus.Publish(new PlayerMovedEvent(new Vector2(delta.x, delta.y), distance));
    }
  }
}
