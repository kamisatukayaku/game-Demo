using System.Collections.Generic;

using UnityEngine;

using Game.Modes.Roguelike.UI;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Gameplay.Events;



namespace Game.Modes.Roguelike.Combat

{

  /// <summary>A17: 随机 15s 双金色 XP 磁吸圈，迫使玩家移动至象限拾取。</summary>

  [DisallowMultipleComponent]

  public sealed class ArenaXpZoneController : MonoBehaviour

  {

    const float ZoneDuration = 15f;

    const float ZoneRadius = 4.2f;

    const float ZonePlacementMinDistance = 3f;
    const float ZonePlacementMaxDistance = 8f;

    const float MinWaveActiveDelay = 18f;

    const float MaxWaveActiveDelay = 42f;

    const int MaxEventsPerRun = 4;



    static ArenaXpZoneController s_instance;

    static readonly List<ActiveZone> s_active = new();



    float _waveActiveTimer;

    float _nextEventTimer = -1f;

    int _eventsThisRun;

    bool _waveActive;



    struct ActiveZone

    {

      public Vector2 Center;

      public float Radius;

      public float ExpiresAt;

    }



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_ArenaXpZoneController");

      go.AddComponent<ArenaXpZoneController>();

    }



    public static void BeginRun()

    {

      EnsureExists();

      if (s_instance == null)

        return;

      s_instance._eventsThisRun = 0;

      s_instance._nextEventTimer = -1f;

      s_active.Clear();

    }



    public static float GetMagnetRangeMultiplier(Vector2 worldPos)

    {

      PruneExpired();

      foreach (var zone in s_active)

      {

        if ((worldPos - zone.Center).sqrMagnitude <= zone.Radius * zone.Radius)

          return 2f;

      }



      return 1f;

    }



    public static float GetXpMultiplier(Vector2 worldPos)

    {

      PruneExpired();

      foreach (var zone in s_active)

      {

        if ((worldPos - zone.Center).sqrMagnitude <= zone.Radius * zone.Radius)

          return 2f;

      }



      return 1f;

    }



    public static bool HasActiveZone => s_active.Count > 0;



    void Awake()

    {

      if (s_instance != null)

      {

        Destroy(gameObject);

        return;

      }



      s_instance = this;

      DontDestroyOnLoad(gameObject);

      WaveDirector.PhaseChanged += OnPhaseChanged;

    }



    void OnDestroy()

    {

      WaveDirector.PhaseChanged -= OnPhaseChanged;

      if (s_instance == this)

        s_instance = null;

    }



    void OnPhaseChanged(WaveDirector.Phase phase, int wave)

    {

      _waveActive = phase == WaveDirector.Phase.WaveActive;

      if (_waveActive)

      {

        _waveActiveTimer = 0f;

        ScheduleNextEvent();

      }

      else

      {

        _nextEventTimer = -1f;

      }

    }



    void Update()

    {

      PruneExpired();

      if (!_waveActive || _eventsThisRun >= MaxEventsPerRun)

        return;



      _waveActiveTimer += Time.deltaTime;

      if (_nextEventTimer < 0f)

        return;



      if (_waveActiveTimer < _nextEventTimer)

        return;



      ActivateDualZones();

      _eventsThisRun++;

      ScheduleNextEvent();

    }



    void ScheduleNextEvent()

    {

      if (_eventsThisRun >= MaxEventsPerRun)

      {

        _nextEventTimer = -1f;

        return;

      }



      _nextEventTimer = _waveActiveTimer + Random.Range(MinWaveActiveDelay, MaxWaveActiveDelay);

    }



    void ActivateDualZones()

    {

      if (!CircleArenaController.IsActive)

        return;



      ArenaTerrainPlacement.PickDualNearPlayer(
        out var firstCenter,
        out var secondCenter,
        ZonePlacementMinDistance,
        ZonePlacementMaxDistance,
        minSeparation: 5.5f);
      var centers = new[] { firstCenter, secondCenter };
      var expire = Time.time + ZoneDuration;

      for (var i = 0; i < centers.Length; i++)
      {
        var center = centers[i];



        s_active.Add(new ActiveZone

        {

          Center = center,

          Radius = ZoneRadius,

          ExpiresAt = expire

        });



        XpZoneVisual.Spawn(center, ZoneRadius, ZoneDuration);
        GameEventBus.Publish(new GroundZoneSpawnedEvent("xp_boost_zone", center, ZoneRadius, ZoneDuration));

      }



      RunTimelineRecorder.Record("XP 磁吸区", "双圈激活");

    }



    static void PruneExpired()

    {

      var now = Time.time;

      for (var i = s_active.Count - 1; i >= 0; i--)

      {

        if (s_active[i].ExpiresAt <= now)

          s_active.RemoveAt(i);

      }

    }

  }



  sealed class XpZoneVisual : MonoBehaviour

  {

    LineRenderer _ring;

    LineRenderer _inner;

    float _expiresAt;



    public static void Spawn(Vector2 center, float radius, float duration)

    {

      var go = new GameObject("XpMagnetZone");

      go.transform.position = new Vector3(center.x, center.y, 0f);

      var visual = go.AddComponent<XpZoneVisual>();

      visual.Build(radius, duration);

    }



    void Build(float radius, float duration)

    {

      _expiresAt = Time.time + duration;

      _ring = CreateRing("Outer", radius, 0.08f, new Color(1f, 0.82f, 0.18f, 0.72f), 8);

      _inner = CreateRing("Inner", radius * 0.72f, 0.045f, new Color(1f, 0.95f, 0.55f, 0.38f), 7);

      DrawCircle(_ring, radius);

      DrawCircle(_inner, radius * 0.72f);

    }



    void Update()

    {

      if (Time.time >= _expiresAt)

      {

        Destroy(gameObject);

        return;

      }



      var remaining = _expiresAt - Time.time;

      var pulse = 0.65f + Mathf.Sin(Time.time * 4.2f) * 0.2f;

      var fade = Mathf.Clamp01(remaining / 2.5f);

      SetAlpha(_ring, new Color(1f, 0.82f, 0.18f, 0.72f * pulse * fade));

      SetAlpha(_inner, new Color(1f, 0.95f, 0.55f, 0.38f * pulse * fade));

    }



    LineRenderer CreateRing(string name, float radius, float width, Color color, int sortOrder)

    {

      var go = new GameObject(name);

      go.transform.SetParent(transform, false);

      var line = go.AddComponent<LineRenderer>();

      line.useWorldSpace = false;

      line.loop = true;

      line.material = new Material(Shader.Find("Sprites/Default")) { name = "XpZoneLine_Runtime" };

      line.startWidth = width;

      line.endWidth = width;

      line.sortingOrder = sortOrder;

      line.startColor = line.endColor = color;

      return line;

    }



    static void DrawCircle(LineRenderer line, float radius)

    {

      const int segments = 48;

      line.positionCount = segments;

      for (var i = 0; i < segments; i++)

      {

        var angle = i * Mathf.PI * 2f / segments;

        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));

      }

    }



    static void SetAlpha(LineRenderer line, Color color)

    {

      if (line == null)

        return;

      line.startColor = line.endColor = color;

    }

  }

}


