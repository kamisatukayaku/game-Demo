using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Tracks player proximity and enter/exit for active ground zones.</summary>
  public sealed class GroundZoneProximityTracker : MonoBehaviour
  {
    struct TrackedZone
    {
      public int InstanceId;
      public string ZoneId;
      public Vector2 Center;
      public float Radius;
      public float ExpiresAt;
    }

    static int s_nextZoneInstanceId;

    static GroundZoneProximityTracker s_instance;

    readonly List<TrackedZone> _zones = new();
    readonly HashSet<int> _inside = new();
    readonly HashSet<string> _proximityShown = new();
    EventListenerHandle _spawnHandle;
    Transform _player;

    const float ProximityFactor = 1.35f;

    public static void ResetForNewRun()
    {
      if (s_instance == null)
        return;
      s_instance._zones.Clear();
      s_instance._inside.Clear();
      s_instance._proximityShown.Clear();
      s_nextZoneInstanceId = 0;
    }

#if UNITY_EDITOR
    public static void EditorDestroyForTests()
    {
      foreach (var tracker in Object.FindObjectsOfType<GroundZoneProximityTracker>(true))
      {
        if (tracker != null)
          DestroyImmediate(tracker.gameObject);
      }
      s_instance = null;
      s_nextZoneInstanceId = 0;
    }
#endif

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_GroundZoneProximityTracker");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      else
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<GroundZoneProximityTracker>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
      _spawnHandle = GameEventBus.Subscribe<GroundZoneSpawnedEvent>(OnZoneSpawned);
    }

    void OnDestroy()
    {
      GameEventBus.Unsubscribe(_spawnHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      PruneExpired();
      ResolvePlayer();
      if (_player == null || RoguelikeTutorialDirector.Instance == null)
        return;

      var pos = GameplayPlane.Position2D(_player);
      for (var i = 0; i < _zones.Count; i++)
      {
        var zone = _zones[i];
        var dist = Vector2.Distance(pos, zone.Center);
        var inside = dist <= zone.Radius;
        var near = dist <= zone.Radius * ProximityFactor;

        if (near && !_proximityShown.Contains(zone.ZoneId))
        {
          _proximityShown.Add(zone.ZoneId);
          RoguelikeTutorialDirector.Instance.OnGroundZoneProximity(zone.ZoneId);
        }

        if (inside)
        {
          if (_inside.Add(zone.InstanceId))
            GameEventBus.Publish(new GroundZoneEnteredEvent(zone.ZoneId, zone.Center, zone.Radius));
        }
        else if (_inside.Remove(zone.InstanceId))
        {
          GameEventBus.Publish(new GroundZoneExitedEvent(zone.ZoneId));
        }
      }
    }

    void OnZoneSpawned(GroundZoneSpawnedEvent evt)
    {
      _zones.Add(new TrackedZone
      {
        InstanceId = ++s_nextZoneInstanceId,
        ZoneId = evt.ZoneId,
        Center = evt.Center,
        Radius = evt.Radius,
        ExpiresAt = evt.Duration > 0f ? Time.time + evt.Duration : float.MaxValue
      });
    }

    void PruneExpired()
    {
      var now = Time.time;
      for (var i = _zones.Count - 1; i >= 0; i--)
      {
        if (_zones[i].ExpiresAt <= now)
        {
          _inside.Remove(_zones[i].InstanceId);
          _zones.RemoveAt(i);
        }
      }
    }

    void ResolvePlayer()
    {
      if (_player != null)
        return;
      var go = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (go != null)
        _player = go.transform;
    }

    public void DebugInjectZone(string zoneId, Vector2 center, float radius, float duration)
    {
      GameEventBus.Publish(new GroundZoneSpawnedEvent(zoneId, center, radius, duration));
    }
  }
}
