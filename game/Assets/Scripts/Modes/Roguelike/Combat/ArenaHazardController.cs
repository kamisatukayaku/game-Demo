using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Laser;
using Game.Shared.Runtime;
using Game.Shared.Runtime.Physics;
using UnityEngine;

using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B5/B6: arena hazard rotation, dual-ring outer damage, laser sweep, gravity well.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaHazardController : MonoBehaviour
  {
    public const int DualRingStartWave = 12;
    const float BasicEdgeBand = 3.6f;

    static ArenaHazardController s_instance;

    string _activeHazardId;
    ArenaHazardDatabase.HazardDef _activeDef;
    Vector2 _gravityWellCenter;
    Vector2 _gravityWellTarget;
    float _gravityRetargetTimer;
    float _hazardTick;
    float _laserDamageTick;
    float _laserAngleDeg;
    LineRenderer _laserLine;
    LineRenderer _laserGlow;
    LineRenderer _laserAura;
    LineRenderer _laserCore;
    ParticleSystem _laserSparks;
    SpriteRenderer _laserHub;
    Material _laserAuraMaterial;
    Material _laserGlowMaterial;
    Material _laserLineMaterial;
    Material _laserCoreMaterial;
    LineRenderer _gravityPulseRing;
    Transform _vfxRoot;

    public static string ActiveHazardId => s_instance?._activeHazardId;
    public static bool IsDualRingActive =>
      CircleArenaController.IsActive
      && WaveDirector.Instance != null
      && WaveDirector.Instance.CurrentWave >= DualRingStartWave;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      if (GameSessionConfig.SelectedMode != GameSessionConfig.GameMode.Arena)
        return;

      var go = new GameObject("_ArenaHazardController");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<ArenaHazardController>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      ArenaHazardDatabase.EnsureLoaded();
      BuildVfx();
      WaveDirector.WaveCompleted += OnWaveCompleted;
      WaveDirector.PhaseChanged += OnPhaseChanged;
      RefreshForCurrentWave();
    }

    void OnDestroy()
    {
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      WaveDirector.PhaseChanged -= OnPhaseChanged;
      if (s_instance == this)
        s_instance = null;

      DestroyMaterial(_laserAuraMaterial);
      DestroyMaterial(_laserGlowMaterial);
      DestroyMaterial(_laserLineMaterial);
      DestroyMaterial(_laserCoreMaterial);
      if (_laserHub != null && _laserHub.material != null)
        Destroy(_laserHub.material);
    }

    static void DestroyMaterial(Material material)
    {
      if (material != null)
        Destroy(material);
    }

    public static void ResetForNewRun()
    {
      if (s_instance == null)
        return;

      s_instance._activeHazardId = null;
      s_instance._activeDef = null;
      s_instance._hazardTick = 0f;
      s_instance._laserDamageTick = 0f;
      s_instance._gravityWellCenter = CircleArenaController.Center;
      s_instance._gravityWellTarget = CircleArenaController.Center;
      s_instance._gravityRetargetTimer = 0f;
      s_instance.UpdateVfxVisibility();
      s_instance.RefreshForCurrentWave();
    }

    void OnWaveCompleted(int wave) => ApplyHazardForWave(wave);

    void OnPhaseChanged(WaveDirector.Phase phase, int wave)
    {
      if (phase == WaveDirector.Phase.WaveCountdown || phase == WaveDirector.Phase.WaveActive)
        ApplyHazardForWave(wave);
    }

    void RefreshForCurrentWave()
    {
      var wave = WaveDirector.Instance != null ? WaveDirector.Instance.CurrentWave : 1;
      ApplyHazardForWave(Mathf.Max(1, wave));
    }

    void ApplyHazardForWave(int wave)
    {
      _activeHazardId = ArenaHazardDatabase.PickForWave(wave);
      _activeDef = ArenaHazardDatabase.Get(_activeHazardId);
      if (_activeHazardId == "gravity_well")
      {
        _gravityWellCenter = PickGravityWanderTarget(CircleArenaController.Center);
        _gravityWellTarget = PickGravityWanderTarget(_gravityWellCenter);
        _gravityRetargetTimer = Random.Range(2.8f, 4.6f);
      }
      else
      {
        _gravityWellCenter = CircleArenaController.Center;
        _gravityWellTarget = CircleArenaController.Center;
        _gravityRetargetTimer = 0f;
      }
      _hazardTick = 0f;
      _laserDamageTick = 0f;
      UpdateVfxVisibility();
      PublishHazardGroundZones();
    }

    void PublishHazardGroundZones()
    {
      if (!CircleArenaController.IsActive)
        return;

      var center = CircleArenaController.Center;
      var outer = CircleArenaController.EffectiveRadius;
      const float zoneDuration = 999f;

      if (IsDualRingActive)
      {
        var mid = (CircleArenaController.InnerSafeRadius + outer) * 0.5f;
        GameEventBus.Publish(new GroundZoneSpawnedEvent("dual_ring_outer", center, mid, zoneDuration));
      }

      if (string.IsNullOrEmpty(_activeHazardId) || _activeDef == null)
        return;

      switch (_activeHazardId)
      {
        case "toxic_edge":
          GameEventBus.Publish(new GroundZoneSpawnedEvent(
            "toxic_edge", center, Mathf.Max(4f, outer - BasicEdgeBand * 0.5f), zoneDuration));
          break;
        case "laser_sweep":
          GameEventBus.Publish(new GroundZoneSpawnedEvent("laser_sweep", center, outer, zoneDuration));
          break;
        case "gravity_well":
          GameEventBus.Publish(new GroundZoneSpawnedEvent(
            "gravity_well", _gravityWellCenter, ResolvePullRadius(_activeDef), zoneDuration));
          break;
      }
    }

    void Update()
    {
      if (!CircleArenaController.IsActive)
        return;

      if (_activeHazardId == "laser_sweep" && _activeDef != null)
        _laserAngleDeg += _activeDef.rotation_deg_per_sec * Time.deltaTime;

      if (_activeHazardId == "gravity_well" && _activeDef != null)
        UpdateGravityWellWander();

      UpdateVfx();
      ApplyHazardLogic();
    }

    void ApplyHazardLogic()
    {
      var player = GetPlayerTransform();
      if (player == null)
        return;

      var health = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
      if (health == null || health.IsDead)
        return;

      if (string.IsNullOrEmpty(_activeHazardId) || _activeDef == null)
      {
        ApplyBasicEdgeHazard(player, health);
        return;
      }

      switch (_activeHazardId)
      {
        case "toxic_edge":
          ApplyToxicEdge(player, health);
          break;
        case "laser_sweep":
          ApplyLaserSweep(player, health);
          break;
        case "gravity_well":
          ApplyGravityWell(player, health);
          break;
        default:
          ApplyBasicEdgeHazard(player, health);
          break;
      }
    }

    void ApplyBasicEdgeHazard(Transform player, Health health)
    {
      if (IsDualRingActive)
      {
        ApplyOuterRingHazard(player, health, 1f, BasicEdgeBand);
        return;
      }

      var dist = Vector2.Distance(player.position, CircleArenaController.Center);
      var edgeStart = CircleArenaController.EffectiveRadius - BasicEdgeBand;
      if (dist <= edgeStart)
        return;

      _hazardTick += Time.deltaTime;
      if (_hazardTick < 0.35f)
        return;

      _hazardTick = 0f;
      var pressure = Mathf.Clamp01((dist - edgeStart) / BasicEdgeBand);
      DealDamage(health, (4f + pressure * 10f) * CircleArenaController.EdgeHazardMult, "arena_edge_hazard", player.gameObject);
    }

    void ApplyToxicEdge(Transform player, Health health)
    {
      var mult = Mathf.Max(1f, _activeDef.edge_damage_mult) * CircleArenaController.EdgeHazardMult;
      if (IsDualRingActive)
      {
        ApplyOuterRingHazard(player, health, mult, BasicEdgeBand);
        return;
      }

      var dist = Vector2.Distance(player.position, CircleArenaController.Center);
      var edgeStart = CircleArenaController.EffectiveRadius - BasicEdgeBand;
      if (dist <= edgeStart)
        return;

      _hazardTick += Time.deltaTime;
      if (_hazardTick < _activeDef.tick_interval)
        return;

      _hazardTick = 0f;
      var pressure = Mathf.Clamp01((dist - edgeStart) / BasicEdgeBand);
      DealDamage(health, (5f + pressure * 12f) * mult, "arena_toxic_edge", player.gameObject);
    }

    void ApplyOuterRingHazard(Transform player, Health health, float damageMult, float edgeBand)
    {
      var dist = Vector2.Distance(player.position, CircleArenaController.Center);
      var inner = CircleArenaController.InnerSafeRadius;
      var outer = CircleArenaController.EffectiveRadius;

      if (dist <= inner || dist > outer)
        return;

      _hazardTick += Time.deltaTime;
      if (_hazardTick < (_activeDef?.tick_interval ?? 0.35f))
        return;

      _hazardTick = 0f;
      var outerPressure = Mathf.Clamp01((dist - inner) / Mathf.Max(0.5f, outer - inner));
      var edgePressure = dist > outer - edgeBand
        ? Mathf.Clamp01((dist - (outer - edgeBand)) / edgeBand)
        : 0f;
      var pressure = Mathf.Max(outerPressure, edgePressure);
      DealDamage(health, (6f + pressure * 14f) * damageMult, "arena_outer_hazard", player.gameObject);
    }

    void ApplyLaserSweep(Transform player, Health health)
    {
      if (IsDualRingActive)
        ApplyOuterRingHazard(player, health, 0.55f, BasicEdgeBand * 0.75f);

      if (!IsPlayerOnLaser(player, out _))
        return;

      _laserDamageTick += Time.deltaTime;
      if (_laserDamageTick < _activeDef.tick_interval)
        return;

      _laserDamageTick = 0f;
      DealDamage(health, _activeDef.damage_per_tick, "arena_laser_sweep", player.gameObject);
    }

    bool IsPlayerOnLaser(Transform player, out float crossDistance)
    {
      crossDistance = float.MaxValue;
      if (_activeDef == null)
        return false;

      var playerPos = ResolvePlayerPlanarPosition(player);
      var center = CircleArenaController.Center;
      var dir = new Vector2(
        Mathf.Cos(_laserAngleDeg * Mathf.Deg2Rad),
        Mathf.Sin(_laserAngleDeg * Mathf.Deg2Rad));
      var halfLen = CircleArenaController.EffectiveRadius + 1.5f;
      var start = center + dir * halfLen;
      var end = center - dir * halfLen;
      crossDistance = DistanceToSegment(playerPos, start, end);

      var halfWidth = ResolveEffectiveLaserHalfWidth(_activeDef);
      return crossDistance <= halfWidth;
    }

    void ApplyGravityWell(Transform player, Health health)
    {
      var playerPos = ResolvePlayerPlanarPosition(player);
      var offset = playerPos - _gravityWellCenter;
      var dist = offset.magnitude;
      if (dist < 0.05f)
        return;

      var pullRadius = ResolvePullRadius(_activeDef);
      if (dist <= pullRadius)
      {
        var falloff = 1f - Mathf.Clamp01(dist / Mathf.Max(0.5f, pullRadius));
        var pullDelta = -offset.normalized * (_activeDef.pull_strength * falloff * Time.deltaTime);
        ApplyPlanarMove(player, pullDelta);
      }

      if (dist > _activeDef.core_radius)
        return;

      _hazardTick += Time.deltaTime;
      if (_hazardTick < _activeDef.tick_interval)
        return;

      _hazardTick = 0f;
      var pressure = 1f - dist / Mathf.Max(0.5f, _activeDef.core_radius);
      DealDamage(health, _activeDef.core_damage_per_tick * pressure, "arena_gravity_well", player.gameObject);
    }

    static float ResolvePullRadius(ArenaHazardDatabase.HazardDef def)
    {
      if (def == null)
        return 4.5f;

      if (def.pull_radius > 0.05f)
        return def.pull_radius;

      return Mathf.Max(def.core_radius, 4.5f);
    }

    void UpdateGravityWellWander()
    {
      _gravityRetargetTimer -= Time.deltaTime;
      if (_gravityRetargetTimer <= 0f || Vector2.Distance(_gravityWellCenter, _gravityWellTarget) <= 0.25f)
      {
        _gravityWellTarget = PickGravityWanderTarget(_gravityWellCenter);
        _gravityRetargetTimer = Random.Range(2.8f, 4.6f);
      }

      var wanderSpeed = Mathf.Clamp(1.65f + ResolvePullRadius(_activeDef) * 0.08f, 1.8f, 2.7f);
      _gravityWellCenter = Vector2.MoveTowards(_gravityWellCenter, _gravityWellTarget, wanderSpeed * Time.deltaTime);
      _gravityWellCenter = CircleArenaController.ClampPosition(_gravityWellCenter, Mathf.Max(0.8f, _activeDef.core_radius));
    }

    static Vector2 PickGravityWanderTarget(Vector2 fallback)
    {
      var center = CircleArenaController.Center;
      var radius = Mathf.Max(6f, CircleArenaController.EffectiveRadius - 4f);
      var player = GetPlayerTransform();
      var playerPos = player != null ? ResolvePlayerPlanarPosition(player) : center;
      var minPlayerDistance = Mathf.Clamp(CircleArenaController.EffectiveRadius * 0.32f, 6f, 12f);

      for (var i = 0; i < 18; i++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var distance = Random.Range(radius * 0.2f, radius * 0.85f);
        var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        candidate = CircleArenaController.ClampPosition(candidate, 1f);
        if (Vector2.Distance(candidate, playerPos) >= minPlayerDistance)
          return candidate;
      }

      var away = fallback - playerPos;
      if (away.sqrMagnitude < 0.01f)
        away = fallback - center;
      if (away.sqrMagnitude < 0.01f)
        away = Vector2.right;
      return CircleArenaController.ClampPosition(fallback + away.normalized * 5f, 1f);
    }

    static Vector2 ResolvePlayerPlanarPosition(Transform player)
    {
      if (player == null)
        return Vector2.zero;

      var body = player.GetComponent<Rigidbody2D>();
      if (body != null)
        return body.position;

      return GameplayPlane.Position2D(player);
    }

    static void ApplyPlanarMove(Transform player, Vector2 pullDelta)
    {
      if (player == null || pullDelta.sqrMagnitude <= 0f)
        return;

      var current = ResolvePlayerPlanarPosition(player);
      var next = CircleArenaController.ClampPosition(
        current + pullDelta,
        WorldGridConstants.PlayerCollisionRadius);
      pullDelta = next - current;

      var physics = player.GetComponent<EntityPhysicsBody>();
      if (physics != null)
        physics.QueuePlanarMove(pullDelta);
      else
        player.position = GameplayPlane.ToWorld(next, player.position.z);
    }

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
      var segment = end - start;
      var lengthSq = segment.sqrMagnitude;
      if (lengthSq < 0.0001f)
        return Vector2.Distance(point, start);

      var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSq);
      return Vector2.Distance(point, start + segment * t);
    }

    static void DealDamage(Health health, float amount, string sourceId, GameObject playerRoot = null)
    {
      var request = DamageRequest.Direct(amount, "energy", sourceId, playerRoot);
      DamagePipeline.Apply(request, health);
    }

    void BuildVfx()
    {
      _vfxRoot = new GameObject("ArenaHazardVfx").transform;
      _vfxRoot.SetParent(transform, false);

      _laserAuraMaterial = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _laserGlowMaterial = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _laserLineMaterial = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _laserCoreMaterial = LaserVfxShared.CreateFlatBeamMaterialInstance();

      _laserAura = CreateLaserLine("LaserSweepAura", 0.72f, _laserAuraMaterial, 93);
      _laserGlow = CreateLaserLine("LaserSweepGlow", 0.46f, _laserGlowMaterial, 94);
      _laserLine = CreateLaserLine("LaserSweepShell", 0.24f, _laserLineMaterial, 95);
      _laserCore = CreateLaserLine("LaserSweepCore", 0.04f, _laserCoreMaterial, 96);
      BuildLaserHub();
      BuildLaserSparks();

      _gravityPulseRing = CreateLine("GravityWellPulse", 0.08f, new Color(0.45f, 0.55f, 1f, 0.55f), 11);
      SetLaserVisible(false);
      _gravityPulseRing.enabled = false;
    }

    void BuildLaserHub()
    {
      var go = new GameObject("LaserSweepHub");
      go.transform.SetParent(_vfxRoot, false);
      _laserHub = go.AddComponent<SpriteRenderer>();
      _laserHub.sprite = LaserVfxShared.SoftGlowSprite;
      _laserHub.material = LaserVfxShared.CreateBeamMaterialInstance();
      _laserHub.sortingLayerName = LaserVfxShared.SortingLayerName;
      _laserHub.sortingOrder = 97;
      _laserHub.enabled = false;
    }

    void BuildLaserSparks()
    {
      var go = new GameObject("LaserSweepSparks");
      go.transform.SetParent(_vfxRoot, false);
      _laserSparks = go.AddComponent<ParticleSystem>();
      _laserSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      var main = _laserSparks.main;
      main.loop = true;
      main.playOnAwake = false;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.65f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.maxParticles = 96;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 0.92f, 0.86f, 0.95f),
        new Color(1f, 0.16f, 0.08f, 0.78f));

      var emission = _laserSparks.emission;
      emission.rateOverTime = 38f;

      var shape = _laserSparks.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
      shape.radius = 0.01f;

      var color = _laserSparks.colorOverLifetime;
      color.enabled = true;
      var gradient = new Gradient();
      gradient.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0f),
          new GradientColorKey(new Color(1f, 0.22f, 0.12f), 0.45f),
          new GradientColorKey(new Color(0.55f, 0.04f, 0.02f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0f, 0f),
          new GradientAlphaKey(0.85f, 0.12f),
          new GradientAlphaKey(0f, 1f)
        });
      color.color = gradient;

      var velocity = _laserSparks.velocityOverLifetime;
      velocity.enabled = false;

      LaserVfxShared.ApplySharedParticleRenderer(_laserSparks.GetComponent<ParticleSystemRenderer>(), 92);
    }

    LineRenderer CreateLaserLine(string name, float width, Material material, int sortOrder)
    {
      var line = CreateLine(name, width, Color.white, sortOrder);
      line.material = material;
      line.numCapVertices = 6;
      line.numCornerVertices = 4;
      line.alignment = LineAlignment.TransformZ;
      return line;
    }

    void SetLaserVisible(bool visible)
    {
      if (_laserAura != null) _laserAura.enabled = visible;
      if (_laserGlow != null) _laserGlow.enabled = visible;
      if (_laserLine != null) _laserLine.enabled = visible;
      if (_laserCore != null) _laserCore.enabled = visible;
      if (_laserHub != null) _laserHub.enabled = visible;
      if (_laserSparks == null)
        return;
      if (visible && !_laserSparks.isPlaying)
        _laserSparks.Play(true);
      else if (!visible && _laserSparks.isPlaying)
        _laserSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    LineRenderer CreateLine(string name, float width, Color color, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(_vfxRoot, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.material = LaserVfxShared.CreateBeamMaterialInstance();
      line.textureMode = LineTextureMode.Stretch;
      line.startWidth = line.endWidth = width;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = sortOrder;
      line.startColor = line.endColor = color;
      return line;
    }

    void UpdateVfxVisibility()
    {
      SetLaserVisible(_activeHazardId == "laser_sweep");
      if (_gravityPulseRing != null)
        _gravityPulseRing.enabled = _activeHazardId == "gravity_well";
    }

    void UpdateVfx()
    {
      if (_activeHazardId == "laser_sweep" && _laserLine != null)
      {
        var center = (Vector3)CircleArenaController.Center;
        center.z = LaserVfxShared.VfxDepthZ;
        var dir = new Vector2(
          Mathf.Cos(_laserAngleDeg * Mathf.Deg2Rad),
          Mathf.Sin(_laserAngleDeg * Mathf.Deg2Rad));
        var halfLen = CircleArenaController.EffectiveRadius + 1.5f;
        var a = center + (Vector3)(dir * halfLen);
        var b = center - (Vector3)(dir * halfLen);
        a.z = b.z = LaserVfxShared.VfxDepthZ;

        SetLaserEndpoints(a, b);

        var player = GetPlayerTransform();
        var playerHit = player != null && IsPlayerOnLaser(player, out _);
        var pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * (playerHit ? 5.8f : 3.2f));
        var shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f + _laserAngleDeg * 0.02f);
        var hitBoost = playerHit ? 1.35f : 1f;

        var aura = new Color(0.45f, 0.005f, 0.005f, (0.045f + shimmer * 0.025f) * pulse * hitBoost);
        var glow = new Color(0.95f, 0.015f, 0.005f, (0.26f + shimmer * 0.08f) * pulse * hitBoost);
        var shell = new Color(1f, 0.035f, 0.012f, (0.68f + shimmer * 0.10f) * pulse * hitBoost);
        var core = new Color(1f, 0.86f, 0.78f, (0.16f + shimmer * 0.035f) * pulse * hitBoost);

        ApplyLaserLayer(_laserAura, aura, new Color(0.22f, 0.002f, 0.002f, aura.a * 0.3f), 0.58f * hitBoost, 0.58f * hitBoost);
        ApplyLaserLayer(_laserGlow, glow, new Color(0.72f, 0.004f, 0.002f, glow.a * 0.7f), 0.34f * hitBoost, 0.34f * hitBoost);
        ApplyLaserLayer(_laserLine, shell, new Color(0.95f, 0.012f, 0.006f, shell.a), 0.17f * hitBoost, 0.17f * hitBoost);
        ApplyLaserLayer(_laserCore, core, new Color(1f, 0.45f, 0.38f, core.a * 0.45f), 0.028f * hitBoost, 0.028f * hitBoost);

        if (_laserHub != null)
        {
          _laserHub.transform.position = center;
          var hubPulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 4.6f);
          _laserHub.transform.localScale = Vector3.one * (1.15f + hubPulse * 0.35f) * hitBoost;
          _laserHub.transform.rotation = Quaternion.Euler(0f, 0f, _laserAngleDeg);
          LaserVfxShared.SetSpriteColor(_laserHub, new Color(1f, 0.12f, 0.05f, 0.5f * pulse * hitBoost));
        }

        if (_laserSparks != null)
        {
          var sparkTransform = _laserSparks.transform;
          sparkTransform.position = center;
          sparkTransform.rotation = Quaternion.FromToRotation(Vector3.right, (Vector3)dir);
          var shape = _laserSparks.shape;
          shape.scale = new Vector3(halfLen * 2f, Mathf.Max(0.18f, ResolveEffectiveLaserHalfWidth(_activeDef) * 0.35f), 1f);
          var emission = _laserSparks.emission;
          emission.rateOverTime = (playerHit ? 62f : 38f) * pulse;
        }
      }

      if (_activeHazardId == "gravity_well" && _gravityPulseRing != null)
      {
        var radius = ResolvePullRadius(_activeDef);
        var pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f);
        DrawCircleWorld(_gravityPulseRing, _gravityWellCenter, radius * pulse, 96);
        var alpha = 0.25f + 0.2f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
        var c = new Color(0.45f, 0.55f, 1f, alpha);
        _gravityPulseRing.startColor = _gravityPulseRing.endColor = c;
      }
    }

    static void ApplyLaserLayer(LineRenderer line, Color start, Color end, float startWidth, float endWidth)
    {
      if (line == null)
        return;

      LaserVfxShared.SetLineColor(line, start, end);
      line.startWidth = startWidth;
      line.endWidth = endWidth;
    }

    static float ResolveEffectiveLaserHalfWidth(ArenaHazardDatabase.HazardDef def)
    {
      var configured = def != null ? def.line_half_width : 1.8f;
      return Mathf.Clamp(Mathf.Max(configured, 1.05f), 0.8f, 1.8f) + WorldGridConstants.PlayerCollisionRadius;
    }

    void SetLaserEndpoints(Vector3 a, Vector3 b)
    {
      if (_laserAura != null)
      {
        _laserAura.positionCount = 2;
        _laserAura.SetPosition(0, a);
        _laserAura.SetPosition(1, b);
      }
      if (_laserGlow != null)
      {
        _laserGlow.positionCount = 2;
        _laserGlow.SetPosition(0, a);
        _laserGlow.SetPosition(1, b);
      }
      if (_laserLine != null)
      {
        _laserLine.positionCount = 2;
        _laserLine.SetPosition(0, a);
        _laserLine.SetPosition(1, b);
      }
      if (_laserCore != null)
      {
        _laserCore.positionCount = 2;
        _laserCore.SetPosition(0, a);
        _laserCore.SetPosition(1, b);
      }
    }

    static void DrawCircleWorld(LineRenderer line, Vector2 center, float radius, int segments)
    {
      line.loop = true;
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
      }
    }

    static Transform GetPlayerTransform()
    {
      var player = GameObject.FindGameObjectWithTag("Player");
      if (player == null)
        player = GameObject.Find("Player");
      return player != null ? player.transform : null;
    }
  }
}
