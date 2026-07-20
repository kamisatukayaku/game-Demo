using System.Collections;
using UnityEngine;

using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.UI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Laser;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B1: W8+ 波内 20–30s 随机触发 mid-wave 事件（陨石 / 能量风暴 / 传送门）。</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaMidWaveEventDirector : MonoBehaviour
  {
    static ArenaMidWaveEventDirector s_instance;

    float _waveActiveTimer;
    float _triggerAt = -1f;
    int _activeWave;
    bool _eventFiredThisWave;
    bool _waveActive;
    Coroutine _runningEvent;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaMidWaveEventDirector");
      go.AddComponent<ArenaMidWaveEventDirector>();
    }

    public static void BeginRun()
    {
      EnsureExists();
      if (s_instance == null)
        return;
      s_instance.ResetRunState();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      MidWaveEventDatabase.EnsureLoaded();
      WaveDirector.PhaseChanged += OnPhaseChanged;
    }

    void OnDestroy()
    {
      WaveDirector.PhaseChanged -= OnPhaseChanged;
      if (s_instance == this)
        s_instance = null;
    }

    void ResetRunState()
    {
      _waveActiveTimer = 0f;
      _triggerAt = -1f;
      _activeWave = 0;
      _eventFiredThisWave = false;
      _waveActive = false;
      if (_runningEvent != null)
      {
        StopCoroutine(_runningEvent);
        _runningEvent = null;
      }
    }

    void OnPhaseChanged(WaveDirector.Phase phase, int wave)
    {
      if (_runningEvent != null)
      {
        StopCoroutine(_runningEvent);
        _runningEvent = null;
      }

      _waveActive = phase == WaveDirector.Phase.WaveActive;
      _activeWave = wave;
      _waveActiveTimer = 0f;
      _eventFiredThisWave = false;
      _triggerAt = _waveActive && CanScheduleForWave(wave) ? PickTriggerDelay() : -1f;
    }

    void Update()
    {
      if (!_waveActive || _eventFiredThisWave || _triggerAt < 0f)
        return;

      _waveActiveTimer += Time.deltaTime;
      if (_waveActiveTimer < _triggerAt)
        return;

      _eventFiredThisWave = true;
      var entry = MidWaveEventDatabase.PickRandom();
      if (entry == null)
        return;

      _runningEvent = StartCoroutine(RunEvent(entry));
    }

    static bool CanScheduleForWave(int wave)
    {
      var settings = MidWaveEventDatabase.CurrentSettings;
      if (wave < settings.min_wave)
        return false;
      if (HuntContractRuntime.IsContractWave(wave))
        return false;

      var director = WaveDirector.Instance;
      if (director == null || director.IsBossWave(wave))
        return false;

      return true;
    }

    float PickTriggerDelay()
    {
      var settings = MidWaveEventDatabase.CurrentSettings;
      return Random.Range(settings.trigger_delay_min, settings.trigger_delay_max);
    }

    IEnumerator RunEvent(MidWaveEventDatabase.EventEntry entry)
    {
      var accent = MidWaveEventDatabase.ParseColor(entry.color, new Color(0.55f, 0.85f, 1f, 1f));
      ArenaMomentUI.ShowBanner($"波中事件 — {entry.display_name}", accent);
      RunTimelineRecorder.Record("波中事件", entry.display_name);

      switch (entry.id)
      {
        case "meteor_strike":
          yield return MeteorStrikeRoutine(entry, accent);
          break;
        case "energy_storm":
          yield return EnergyStormRoutine(entry, accent);
          break;
        case "warp_portal":
          yield return WarpPortalRoutine(entry, accent);
          break;
        default:
          Debug.LogWarning($"[MidWaveEvent] Unknown event id '{entry.id}'.");
          break;
      }

      _runningEvent = null;
    }

    IEnumerator MeteorStrikeRoutine(MidWaveEventDatabase.EventEntry entry, Color accent)
    {
      if (!CircleArenaController.IsActive)
        yield break;

      var center = ArenaTerrainPlacement.PickNearPlayer(1.2f, 7.5f);
      var warning = entry.warning_seconds > 0f ? entry.warning_seconds : 2.5f;
      var visual = MidWaveEventVisuals.SpawnWarningCircle(center, entry.radius, warning, accent);
      GameEventBus.Publish(new GroundZoneSpawnedEvent("meteor_strike_zone", center, entry.radius, warning + 0.35f));
      yield return new WaitForSeconds(warning);

      if (visual != null)
        visual.PlayImpactFlash();

      ApplyAreaDamage(center, entry.radius, entry.damage, "mid_meteor");
      yield return new WaitForSeconds(0.35f);
      if (visual != null)
        Destroy(visual.gameObject);
    }

    IEnumerator EnergyStormRoutine(MidWaveEventDatabase.EventEntry entry, Color accent)
    {
      if (!CircleArenaController.IsActive)
        yield break;

      var center = ArenaTerrainPlacement.PickNearPlayer(2f, 8.5f);
      var duration = entry.duration > 0f ? entry.duration : 12f;
      var visual = MidWaveEventVisuals.SpawnStormZone(center, entry.radius, duration, accent);
      GameEventBus.Publish(new GroundZoneSpawnedEvent("energy_storm_zone", center, entry.radius, duration));
      var tick = entry.tick_interval > 0f ? entry.tick_interval : 0.85f;
      var elapsed = 0f;
      var nextTick = tick;

      while (elapsed < duration)
      {
        if (WaveDirector.Instance == null || WaveDirector.Instance.CurrentPhase != WaveDirector.Phase.WaveActive)
          break;

        elapsed += Time.deltaTime;
        if (elapsed >= nextTick)
        {
          nextTick += tick;
          ApplyAreaDamage(center, entry.radius, entry.damage, "mid_storm");
        }

        yield return null;
      }

      if (visual != null)
        Destroy(visual.gameObject);
    }

    IEnumerator WarpPortalRoutine(MidWaveEventDatabase.EventEntry entry, Color accent)
    {
      if (!CircleArenaController.IsActive)
        yield break;

      ArenaTerrainPlacement.PickPortalPairNearPlayer(out var portalA, out var portalB, 8.5f, 10f);
      var duration = entry.duration > 0f ? entry.duration : 8f;
      var radius = entry.radius > 0f ? entry.radius : 2.2f;

      var visualA = MidWaveEventVisuals.SpawnPortal(portalA, radius, accent);
      var visualB = MidWaveEventVisuals.SpawnPortal(portalB, radius, accent * new Color(1f, 1f, 1f, 0.85f));
      GameEventBus.Publish(new GroundZoneSpawnedEvent("warp_portal_zone", portalA, radius, duration));
      GameEventBus.Publish(new GroundZoneSpawnedEvent("warp_portal_zone", portalB, radius, duration));
      var elapsed = 0f;
      var cooldown = 0f;

      while (elapsed < duration)
      {
        if (WaveDirector.Instance == null || WaveDirector.Instance.CurrentPhase != WaveDirector.Phase.WaveActive)
          break;

        elapsed += Time.deltaTime;
        cooldown -= Time.deltaTime;
        if (cooldown <= 0f)
        {
          if (TryWarpPlayer(portalA, portalB, radius))
          {
            cooldown = 0.6f;
            MidWaveEventVisuals.PulsePortal(visualB);
          }
          else if (TryWarpPlayer(portalB, portalA, radius))
          {
            cooldown = 0.6f;
            MidWaveEventVisuals.PulsePortal(visualA);
          }
        }

        yield return null;
      }

      if (visualA != null)
        Destroy(visualA.gameObject);
      if (visualB != null)
        Destroy(visualB.gameObject);
    }

    static void ApplyAreaDamage(Vector2 center, float radius, float damage, string sourceId)
    {
      if (damage <= 0f || radius <= 0f)
        return;

      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player != null)
      {
        var playerPos = GameplayPlane.Position2D(player.transform);
        if ((playerPos - center).sqrMagnitude <= radius * radius)
        {
          var health = player.GetComponent<Health>();
          if (health != null && !health.IsDead)
          {
            DamagePipeline.Apply(
              DamageRequest.Direct(damage, "energy", sourceId, null),
              health);
          }
        }
      }

      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var hits = registry.GetInRange(center, radius);
      var request = DamageRequest.Direct(damage, "energy", sourceId, null);
      foreach (var enemy in hits)
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
          continue;
        var pos = GameplayPlane.Position2D(enemy.transform);
        if ((pos - center).sqrMagnitude > radius * radius)
          continue;
        DamagePipeline.Apply(request, health);
      }
    }

    static bool TryWarpPlayer(Vector2 portal, Vector2 destination, float radius)
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return false;

      var pos = GameplayPlane.Position2D(player.transform);
      if ((pos - portal).sqrMagnitude > radius * radius)
        return false;

      GameplayPlane.SetPosition2D(player.transform, CircleArenaController.ClampPosition(destination, 0.5f));
      return true;
    }
  }

  sealed class MidWaveEventVisuals : MonoBehaviour
  {
    const float GroundZ = -0.08f;
    const int RingSortOrder = 88;
    const int FillSortOrder = 86;
    const int MeteorSortOrder = 94;

    LineRenderer _outer;
    LineRenderer _inner;
    SpriteRenderer _fill;
    SpriteRenderer _meteor;
    float _expiresAt;
    float _pulseSpeed = 4f;
    Color _baseColor;
    float _radius;
    float _warningDuration;
    float _spawnTime;
    bool _meteorWarning;

    public static MidWaveEventVisuals SpawnWarningCircle(Vector2 center, float radius, float duration, Color color)
    {
      var go = new GameObject("MidWaveMeteorWarning");
      go.transform.position = new Vector3(center.x, center.y, GroundZ);
      var visual = go.AddComponent<MidWaveEventVisuals>();
      visual._baseColor = color;
      visual._radius = radius;
      visual._warningDuration = Mathf.Max(0.35f, duration);
      visual._spawnTime = Time.time;
      visual._meteorWarning = true;
      visual._expiresAt = Time.time + duration + 0.75f;
      visual._pulseSpeed = 7f;
      visual._fill = visual.CreateFill("WarningFill", radius, new Color(color.r, color.g, color.b, 0.28f), FillSortOrder);
      visual._outer = visual.CreateRing("WarningOuter", radius, 0.18f, new Color(color.r, color.g, color.b, 0.82f), RingSortOrder);
      visual._inner = visual.CreateRing("WarningInner", radius * 0.55f, 0.1f, new Color(1f, 0.92f, 0.72f, 0.9f), RingSortOrder + 1);
      visual._meteor = visual.CreateMeteor("MeteorBody", new Color(1f, 0.55f, 0.22f, 0.95f));
      visual.DrawCircle(visual._outer, radius);
      visual.DrawCircle(visual._inner, radius * 0.55f);
      return visual;
    }

    public static MidWaveEventVisuals SpawnStormZone(Vector2 center, float radius, float duration, Color color)
    {
      var go = new GameObject("MidWaveEnergyStorm");
      go.transform.position = new Vector3(center.x, center.y, GroundZ);
      var visual = go.AddComponent<MidWaveEventVisuals>();
      visual._baseColor = color;
      visual._radius = radius;
      visual._expiresAt = Time.time + duration;
      visual._pulseSpeed = 3.2f;
      visual._fill = visual.CreateFill("StormFill", radius, new Color(color.r, color.g, color.b, 0.16f), FillSortOrder);
      visual._outer = visual.CreateRing("StormOuter", radius, 0.16f, new Color(color.r, color.g, color.b, 0.62f), RingSortOrder);
      visual._inner = visual.CreateRing("StormInner", radius * 0.68f, 0.09f, new Color(color.r, color.g, color.b, 0.42f), RingSortOrder + 1);
      visual.DrawCircle(visual._outer, radius);
      visual.DrawCircle(visual._inner, radius * 0.68f);
      return visual;
    }

    public static MidWaveEventVisuals SpawnPortal(Vector2 center, float radius, Color color)
    {
      var go = new GameObject("MidWaveWarpPortal");
      go.transform.position = new Vector3(center.x, center.y, GroundZ);
      var visual = go.AddComponent<MidWaveEventVisuals>();
      visual._baseColor = color;
      visual._radius = radius;
      visual._expiresAt = Time.time + 60f;
      visual._pulseSpeed = 5f;
      visual._fill = visual.CreateFill("PortalFill", radius, new Color(color.r, color.g, color.b, 0.2f), FillSortOrder);
      visual._outer = visual.CreateRing("PortalOuter", radius, 0.12f, new Color(color.r, color.g, color.b, 0.78f), RingSortOrder);
      visual._inner = visual.CreateRing("PortalInner", radius * 0.62f, 0.07f, new Color(0.92f, 0.98f, 1f, 0.72f), RingSortOrder + 1);
      visual.DrawCircle(visual._outer, radius);
      visual.DrawCircle(visual._inner, radius * 0.62f);
      return visual;
    }

    public static void PulsePortal(MidWaveEventVisuals visual)
    {
      if (visual == null)
        return;
      visual._pulseSpeed = 14f;
    }

    public void PlayImpactFlash()
    {
      if (_fill != null)
      {
        _fill.color = new Color(1f, 0.82f, 0.35f, 0.72f);
        _fill.transform.localScale = Vector3.one * (_radius * 2.4f);
      }

      if (_outer != null)
        _outer.startColor = _outer.endColor = new Color(1f, 0.92f, 0.55f, 0.98f);
      if (_inner != null)
        _inner.startColor = _inner.endColor = Color.white;
      if (_meteor != null)
        _meteor.enabled = false;
    }

    void Update()
    {
      if (Time.time >= _expiresAt)
      {
        Destroy(gameObject);
        return;
      }

      var pulse = 0.65f + Mathf.Sin(Time.time * _pulseSpeed) * 0.28f;
      _pulseSpeed = Mathf.Lerp(_pulseSpeed, 4f, Time.deltaTime * 2f);
      if (_outer != null)
        _outer.startColor = _outer.endColor = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * pulse);
      if (_inner != null)
        _inner.startColor = _inner.endColor = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * pulse * 0.72f);
      if (_fill != null)
        _fill.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * 0.22f * pulse);

      if (!_meteorWarning || _meteor == null)
        return;

      var progress = Mathf.Clamp01((Time.time - _spawnTime) / _warningDuration);
      var fallHeight = Mathf.Lerp(_radius * 5.5f, 0.35f, progress * progress);
      _meteor.transform.localPosition = new Vector3(0f, fallHeight, LaserVfxShared.VfxDepthZ - GroundZ);
      var meteorScale = Mathf.Lerp(0.55f, 1.15f, progress) * Mathf.Max(0.8f, _radius * 0.55f);
      _meteor.transform.localScale = new Vector3(meteorScale, meteorScale * 1.35f, 1f);

      if (_inner != null)
      {
        var shrink = Mathf.Lerp(0.55f, 0.18f, progress);
        DrawCircle(_inner, _radius * shrink);
      }
    }

    SpriteRenderer CreateFill(string name, float radius, Color color, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.color = color;
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = sortOrder;
      go.transform.localScale = Vector3.one * (radius * 2f);
      return sr;
    }

    SpriteRenderer CreateMeteor(string name, Color color)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.color = color;
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = MeteorSortOrder;
      return sr;
    }

    LineRenderer CreateRing(string name, float radius, float width, Color color, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.material = LaserVfxShared.CreateBeamMaterialInstance();
      line.textureMode = LineTextureMode.Stretch;
      line.startWidth = line.endWidth = width;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = sortOrder;
      line.startColor = line.endColor = color;
      return line;
    }

    void DrawCircle(LineRenderer line, float radius)
    {
      const int segments = 40;
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }
  }
}
