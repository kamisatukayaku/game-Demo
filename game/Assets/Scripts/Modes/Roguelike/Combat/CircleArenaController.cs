using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Laser;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.Progression;
using Health = global::Game.Shared.Combat.Health.Health;
namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// 环形竞技场控制器：圆形生存区域。
  /// 仅在 GameSessionConfig.SelectedMode == Arena 时激活?
  ///
  /// 功能?
  ///  - 绘制正圆边界（白色细?+ 半透明填充?
  ///  - 限制玩家无法离开圆形区域
  ///  - 怪物沿圆环路径移劀"
  /// </summary>
  [DisallowMultipleComponent]
  public class CircleArenaController : MonoBehaviour
  {
    /// <summary>竞技场半径（世界单位）。默认移速 8 下约 10 秒可达边缘。</summary>
    public const float ArenaRadius = ArenaCombatScale.DefaultArenaRadius;

    /// <summary>怪物行走/生成的圆环半径（与视觉白边重合）。</summary>
    public static float PathRadius => s_layoutRadius;

    /// <summary>竞技场内怪物索敌范围（覆盖整圈竞技场 + 余量，全图追击玩家）。</summary>
    public const float FullCombatRange = ArenaRadius * 4f;

    /// <summary>竞技场圆心（世界坐标）。初始为 (0,0)?/summary>
    public static Vector2 Center => s_center;

    /// <summary>当前是否为竞技场模式?/summary>
    public static float EffectiveRadius => s_instance != null ? s_instance._effectiveRadius : s_layoutRadius;
    public static string ActiveShapeHint => s_shapeHint;
    public static string ActiveLayoutId => s_layoutId;

    public const float InnerSafeRadiusRatio = 0.68f;

    public static float InnerSafeRadius => EffectiveRadius * InnerSafeRadiusRatio;

    public static float EdgeHazardMult => s_instance != null ? s_instance._edgeHazardMult : 1f;

    public static bool IsDualRingActive =>
      IsActive
      && WaveDirector.Instance != null
      && WaveDirector.Instance.CurrentWave >= ArenaHazardController.DualRingStartWave;

    public static void ApplyLayout(ArenaLayoutDatabase.LayoutEntry layout)
    {
      if (layout == null)
        return;

      s_layoutId = layout.id;
      s_shapeHint = layout.shape_hint ?? "circle";
      s_layoutRadius = layout.base_radius > 0f ? layout.base_radius : ArenaRadius;
      s_layoutFillColor = ArenaLayoutDatabase.ParseColor(layout.fill_color, ArenaFillColor);
      s_layoutBorderColor = ArenaLayoutDatabase.ParseColor(layout.border_color, BorderColor);
      s_layoutIslands = layout.islands;
      s_corridorWidth = layout.corridor_width > 0f ? layout.corridor_width : 11f;

      if (s_instance != null)
        s_instance.RebuildLayoutVisuals();
    }

    public static float PreviewNextRadius(int upcomingWave)
    {
      var shrink = s_layoutRadius * Mathf.Min(0.12f, Mathf.Max(0, upcomingWave - 1) * 0.004f);
      return Mathf.Max(s_layoutRadius * 0.85f, s_layoutRadius - shrink);
    }

    public static bool ShouldShowShrinkPreview(int currentWave)
    {
      foreach (var milestone in new[] { 5, 10, 15 })
      {
        if (currentWave >= milestone - 3 && currentWave < milestone)
          return true;
      }
      return false;
    }

    public static int GetUpcomingShrinkWave(int currentWave)
    {
      foreach (var milestone in new[] { 5, 10, 15 })
      {
        if (currentWave >= milestone - 3 && currentWave < milestone)
          return milestone;
      }
      return 0;
    }

    public static void SetBossPrepHighlight(bool active)
    {
      if (s_instance != null)
        s_instance._bossPrepHighlight = active;
    }

    public static void SetEdgeHazardMult(float mult)
    {
      if (s_instance != null)
        s_instance._edgeHazardMult = Mathf.Max(0.1f, mult);
    }

    public static bool IsActive => s_instance != null;

    public static void PlayKillStreakPulse(float intensity)
    {
      if (s_instance == null)
        return;

      s_instance.TriggerArenaPulse(Mathf.Clamp01(intensity));
    }

    static readonly Color BorderColor = new(0.78f, 0.96f, 1f, 0.95f);
    static readonly Color ArenaFillColor = new(0.2f, 0.39f, 0.55f, 0.44f);

    static CircleArenaController s_instance;
    static Vector2 s_center = Vector2.zero;
    static float s_layoutRadius = ArenaRadius;
    static string s_shapeHint = "circle";
    static string s_layoutId = "default";
    static Color s_layoutFillColor = new(0.2f, 0.39f, 0.55f, 0.44f);
    static Color s_layoutBorderColor = new(0.78f, 0.96f, 1f, 0.95f);
    static ArenaLayoutDatabase.IslandDef[] s_layoutIslands;
    static float s_corridorWidth = 11f;

    LineRenderer _circleLine;
    SpriteRenderer _fillDisc;
    LineRenderer[] _energyRings;
    LineRenderer[] _barrierRings;
    LineRenderer[] _boundaryFlowSegments;
    LineRenderer[] _boundaryTextureSegments;
    LineRenderer _outerGhostRing;
    LineRenderer[] _pulseRings;
    ParticleSystem _barrierParticles;
    Transform _floatingRoot;
    FloatingShard[] _floatingShards;
    EventListenerHandle _enemyKilledHandle;
    EventListenerHandle _levelUpHandle;
    float _pulseAge;
    float _nextPulseAt;
    int _activePulseCount;
    float _boundaryResponse;
    float _edgeHazardMult = 1f;
    bool _bossPrepHighlight;
    float _effectiveRadius = ArenaRadius;

    /// <summary>Drawable radius — follows wave shrink so border feedback stays aligned with gameplay.</summary>
    float VisualRadius => _effectiveRadius;

    static float LayoutVisualRadius => s_layoutRadius > 0.01f ? s_layoutRadius : ArenaRadius;

    LineRenderer _crackPreviewRing;
    LineRenderer _nextRadiusRing;
    LineRenderer _innerSafeRing;
    LineRenderer _outerHazardRing;
    LineRenderer[] _shapeAccentLines;
    SpriteRenderer[] _islandDiscs;
    float _crackPulseAge;
    const float EdgeBand = 3.6f;
    const float PulseDuration = 0.65f;
    const int CircleSegments = 192;
    const int BoundarySegmentPoints = 16;
    const float BorderWidth = 0.1f;

    Color _activeBorderColor;
    Color _activeFillColor;
    static Material s_lineMaterial;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      if (!GameSessionConfig.UsesArenaLayout)
        return;

      var go = new GameObject("_CircleArena");
      s_instance = go.AddComponent<CircleArenaController>();
    }

    void Awake()
    {
      s_instance = this;
      s_center = Vector2.zero;
      _activeBorderColor = s_layoutBorderColor;
      _activeFillColor = s_layoutFillColor;
      BuildVisuals();
      RebuildLayoutVisuals();
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
      _levelUpHandle = GameEventBus.Subscribe<LevelUpEvent>(OnLevelUp);
      WaveDirector.WaveCompleted += OnWaveCompleted;
      Game.Modes.Roguelike.Combat.WaveDirector.SpawnPositionClamper = ClampPositionWorld;
    }

    void OnDestroy()
    {
      if (s_instance == this)
      {
        s_instance = null;
          Game.Modes.Roguelike.Combat.WaveDirector.SpawnPositionClamper = null;
      }

      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      if (_levelUpHandle.Valid)
        GameEventBus.Unsubscribe(_levelUpHandle);
      WaveDirector.WaveCompleted -= OnWaveCompleted;
    }

    void OnWaveCompleted(int waveNumber) => ApplyWaveChapterPalette(waveNumber);

    void ApplyWaveChapterPalette(int waveNumber)
    {
      var chapter = Mathf.Max(0, (waveNumber - 1) / 5);
      switch (chapter)
      {
        case 0:
          _activeBorderColor = new Color(0.78f, 0.96f, 1f, 0.95f);
          _activeFillColor = new Color(0.2f, 0.39f, 0.55f, 0.44f);
          break;
        case 1:
          _activeBorderColor = new Color(0.82f, 0.62f, 1f, 0.95f);
          _activeFillColor = new Color(0.28f, 0.16f, 0.42f, 0.48f);
          break;
        default:
          _activeBorderColor = new Color(1f, 0.42f, 0.38f, 0.95f);
          _activeFillColor = new Color(0.42f, 0.12f, 0.14f, 0.5f);
          break;
      }

      if (_fillDisc != null)
        _fillDisc.color = _activeFillColor;
      if (_circleLine != null)
        _circleLine.startColor = _circleLine.endColor = _activeBorderColor;
    }

    static Vector2 ClampPositionWorld(Vector2 pos) =>
      ArenaQuadrantBlocker.ClampPosition(ClampPosition(pos, WorldGridConstants.PlayerCollisionRadius), WorldGridConstants.PlayerCollisionRadius);

    void LateUpdate()
    {
      UpdateEffectiveRadius();
      UpdateShrinkPreview();
      UpdateDualRingVisuals();
      UpdateVisuals();
      ClampPlayerPosition();
    }

    void UpdateEffectiveRadius()
    {
      var wave = WaveDirector.Instance != null ? WaveDirector.Instance.CurrentWave : 1;
      var shrink = s_layoutRadius * Mathf.Min(0.12f, Mathf.Max(0, wave - 1) * 0.004f);
      _effectiveRadius = Mathf.Max(s_layoutRadius * 0.85f, s_layoutRadius - shrink);
    }

    void RebuildLayoutVisuals()
    {
      DestroyLayoutAccents();
      _activeBorderColor = s_layoutBorderColor;
      _activeFillColor = s_layoutFillColor;

      if (_fillDisc != null)
      {
        _fillDisc.color = _activeFillColor;
        var scale = s_layoutRadius / LayoutVisualRadius;
        _fillDisc.transform.localScale = Vector3.one * scale;
      }

      if (_circleLine != null)
        DrawCircle(_circleLine, _effectiveRadius, CircleSegments);

      switch (s_shapeHint)
      {
        case "cross":
          BuildCrossAccents();
          break;
        case "island_chain":
          BuildIslandAccents();
          break;
      }
    }

    void DestroyLayoutAccents()
    {
      if (_shapeAccentLines != null)
      {
        foreach (var line in _shapeAccentLines)
          if (line != null)
            Destroy(line.gameObject);
        _shapeAccentLines = null;
      }

      if (_islandDiscs != null)
      {
        foreach (var disc in _islandDiscs)
          if (disc != null)
            Destroy(disc.gameObject);
        _islandDiscs = null;
      }
    }

    void BuildCrossAccents()
    {
      _shapeAccentLines = new LineRenderer[4];
      var half = s_corridorWidth * 0.5f;
      var extent = s_layoutRadius * 0.92f;
      var segments = new[]
      {
        new[] { new Vector3(-half, -extent, 0f), new Vector3(-half, extent, 0f) },
        new[] { new Vector3(half, -extent, 0f), new Vector3(half, extent, 0f) },
        new[] { new Vector3(-extent, -half, 0f), new Vector3(extent, -half, 0f) },
        new[] { new Vector3(-extent, half, 0f), new Vector3(extent, half, 0f) }
      };

      for (var i = 0; i < segments.Length; i++)
      {
        var line = CreateRingLine($"CrossAccent_{i + 1}", 0.06f, new Color(0.82f, 0.55f, 1f, 0.55f), 7);
        line.loop = false;
        line.positionCount = 2;
        line.SetPositions(segments[i]);
        _shapeAccentLines[i] = line;
      }
    }

    void BuildIslandAccents()
    {
      if (s_layoutIslands == null || s_layoutIslands.Length == 0)
        return;

      _islandDiscs = new SpriteRenderer[s_layoutIslands.Length];
      for (var i = 0; i < s_layoutIslands.Length; i++)
      {
        var island = s_layoutIslands[i];
        var angle = island.angle_deg * Mathf.Deg2Rad;
        var pos = new Vector3(
          Mathf.Cos(angle) * island.orbit,
          Mathf.Sin(angle) * island.orbit,
          0f);

        var go = new GameObject($"ArenaIsland_{i + 1}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var disc = go.AddComponent<SpriteRenderer>();
        disc.sprite = CreateDiscSprite(128);
        disc.color = new Color(_activeFillColor.r * 1.2f, _activeFillColor.g * 1.15f, _activeFillColor.b * 1.1f, 0.62f);
        disc.sortingOrder = 5;
        var scale = island.radius / LayoutVisualRadius;
        go.transform.localScale = Vector3.one * scale * 2f;
        SpriteMaterialUtility.ApplyUnlit(disc);
        _islandDiscs[i] = disc;
      }
    }

    void UpdateDualRingVisuals()
    {
      EnsureDualRingBuilt();
      var show = IsDualRingActive;
      if (_innerSafeRing != null)
        _innerSafeRing.enabled = show;
      if (_outerHazardRing != null)
        _outerHazardRing.enabled = show;

      if (!show)
        return;

      var inner = InnerSafeRadius;
      var outer = _effectiveRadius;
      var pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.4f);

      if (_innerSafeRing != null)
      {
        DrawCircle(_innerSafeRing, inner, CircleSegments);
        var c = new Color(0.42f, 0.95f, 0.72f, 0.28f + pulse * 0.18f);
        _innerSafeRing.startColor = _innerSafeRing.endColor = c;
        _innerSafeRing.startWidth = 0.07f;
        _innerSafeRing.endWidth = 0.07f;
      }

      if (_outerHazardRing != null)
      {
        DrawCircle(_outerHazardRing, outer - EdgeBand * 0.35f, CircleSegments);
        var c = new Color(1f, 0.38f, 0.22f, 0.22f + pulse * 0.28f);
        _outerHazardRing.startColor = _outerHazardRing.endColor = c;
        _outerHazardRing.startWidth = 0.09f + pulse * 0.03f;
        _outerHazardRing.endWidth = _outerHazardRing.startWidth;
      }
    }

    void EnsureDualRingBuilt()
    {
      if (_innerSafeRing != null)
        return;

      _innerSafeRing = CreateRingLine("InnerSafeRing", 0.07f, new Color(0.42f, 0.95f, 0.72f, 0.35f), 9);
      _outerHazardRing = CreateRingLine("OuterHazardRing", 0.09f, new Color(1f, 0.38f, 0.22f, 0.4f), 10);
      _innerSafeRing.enabled = false;
      _outerHazardRing.enabled = false;
    }

    void UpdateShrinkPreview()
    {
      var wave = WaveDirector.Instance != null ? WaveDirector.Instance.CurrentWave : 1;
      var show = ShouldShowShrinkPreview(wave);
      EnsureCrackPreviewBuilt();

      if (_crackPreviewRing != null)
        _crackPreviewRing.enabled = show;
      if (_nextRadiusRing != null)
        _nextRadiusRing.enabled = show;

      if (!show)
        return;

      _crackPulseAge += Time.deltaTime * 2.8f;
      var pulse = 0.55f + 0.45f * Mathf.Sin(_crackPulseAge);
      var upcoming = GetUpcomingShrinkWave(wave);
      var nextRadius = PreviewNextRadius(upcoming);

      if (_crackPreviewRing != null)
      {
        DrawCircle(_crackPreviewRing, _effectiveRadius, CircleSegments);
        var c = new Color(1f, 0.45f, 0.35f, 0.35f + pulse * 0.35f);
        _crackPreviewRing.startColor = _crackPreviewRing.endColor = c;
        _crackPreviewRing.startWidth = 0.08f + pulse * 0.04f;
        _crackPreviewRing.endWidth = _crackPreviewRing.startWidth;
      }

      if (_nextRadiusRing != null)
      {
        DrawCircle(_nextRadiusRing, nextRadius, CircleSegments);
        var c = new Color(1f, 0.72f, 0.28f, 0.22f + pulse * 0.18f);
        _nextRadiusRing.startColor = _nextRadiusRing.endColor = c;
      }
    }

    void EnsureCrackPreviewBuilt()
    {
      if (_crackPreviewRing != null)
        return;

      _crackPreviewRing = CreateRingLine("ShrinkCrackPreview", 0.1f, new Color(1f, 0.4f, 0.3f, 0.5f), 14);
      _nextRadiusRing = CreateRingLine("ShrinkNextRadiusPreview", 0.06f, new Color(1f, 0.7f, 0.25f, 0.35f), 10);
      _crackPreviewRing.enabled = false;
      _nextRadiusRing.enabled = false;
    }

    void BuildVisuals()
    {
      var fillGo = new GameObject("ArenaFill");
      fillGo.transform.SetParent(transform, false);
      _fillDisc = fillGo.AddComponent<SpriteRenderer>();
      _fillDisc.sprite = CreateDiscSprite(768);
      _fillDisc.color = _activeFillColor;
      _fillDisc.sortingOrder = 4;
      SpriteMaterialUtility.ApplyUnlit(_fillDisc);

      var borderGo = new GameObject("ArenaBorder");
      borderGo.transform.SetParent(transform, false);
      _circleLine = borderGo.AddComponent<LineRenderer>();
      ConfigureLine(_circleLine, BorderWidth, _activeBorderColor, 8, true);
      _circleLine.loop = true;

      var borderPoints = new Vector3[CircleSegments];
      for (int i = 0; i < CircleSegments; i++)
      {
        var angle = i * Mathf.PI * 2f / CircleSegments;
        borderPoints[i] = new Vector3(Mathf.Cos(angle) * LayoutVisualRadius, Mathf.Sin(angle) * LayoutVisualRadius, 0f);
      }

      _circleLine.positionCount = borderPoints.Length;
      _circleLine.SetPositions(borderPoints);

      BuildEnergyPattern();
      BuildEnergyBarrier();
      BuildBoundaryLoop();
      BuildArenaPulse();
      BuildFloatingGeometry();
      ScheduleNextPulse();
    }

    void BuildEnergyPattern()
    {
      _energyRings = new LineRenderer[5];
      for (var i = 0; i < _energyRings.Length; i++)
      {
        var go = new GameObject($"ArenaEnergyRing_{i + 1}");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        ConfigureLine(line, 0.026f, new Color(0.45f, 0.9f, 1f, 0.07f), 5, false);
        line.loop = true;
        DrawCircle(line, LayoutVisualRadius * (i + 1f) / (_energyRings.Length + 1f), CircleSegments);
        _energyRings[i] = line;
      }
    }

    void BuildEnergyBarrier()
    {
      _barrierRings = new LineRenderer[3];
      for (var i = 0; i < _barrierRings.Length; i++)
      {
        var go = new GameObject($"ArenaBarrierRing_{i + 1}");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        ConfigureLine(line, 0.09f - i * 0.018f, new Color(0.72f, 0.96f, 1f, 0.45f), 9 + i, false);
        line.loop = true;
        DrawCircle(line, LayoutVisualRadius + i * 0.28f, CircleSegments);
        _barrierRings[i] = line;
      }

      var particlesGo = new GameObject("ArenaBarrierParticles");
      particlesGo.transform.SetParent(transform, false);
      _barrierParticles = particlesGo.AddComponent<ParticleSystem>();
      _barrierParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      var main = _barrierParticles.main;
      main.loop = true;
      main.playOnAwake = false;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.maxParticles = 120;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(0.65f, 0.95f, 1f, 0.55f),
        new Color(0.2f, 0.62f, 1f, 0.25f));

      var emission = _barrierParticles.emission;
      emission.rateOverTime = 42f;

      var shape = _barrierParticles.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = LayoutVisualRadius;
      shape.radiusThickness = 0.015f;
      shape.arc = 360f;

      var velocity = _barrierParticles.velocityOverLifetime;
      velocity.enabled = true;
      velocity.space = ParticleSystemSimulationSpace.Local;
      velocity.x = new ParticleSystem.MinMaxCurve(0f);
      velocity.radial = new ParticleSystem.MinMaxCurve(0.3f);
      velocity.y = new ParticleSystem.MinMaxCurve(0.16f);
      velocity.z = new ParticleSystem.MinMaxCurve(0f);

      var color = _barrierParticles.colorOverLifetime;
      color.enabled = true;
      var gradient = new Gradient();
      gradient.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.75f, 0.98f, 1f), 0f),
          new GradientColorKey(new Color(0.32f, 0.72f, 1f), 0.7f),
          new GradientColorKey(new Color(0.15f, 0.32f, 0.9f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0f, 0f),
          new GradientAlphaKey(0.55f, 0.18f),
          new GradientAlphaKey(0f, 1f)
        });
      color.color = gradient;

      var size = _barrierParticles.sizeOverLifetime;
      size.enabled = true;
      size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
        new Keyframe(0f, 0.4f),
        new Keyframe(0.35f, 1f),
        new Keyframe(1f, 0.05f)));

      LaserVfxShared.ApplySharedParticleRenderer(_barrierParticles.GetComponent<ParticleSystemRenderer>(), 11);
      _barrierParticles.Play(true);
    }

    void BuildBoundaryLoop()
    {
      _outerGhostRing = CreateRingLine("ArenaOuterGhostBoundary", 0.035f, new Color(0.56f, 0.9f, 1f, 0.1f), 6);
      DrawCircle(_outerGhostRing, LayoutVisualRadius + 1.25f, CircleSegments);

      _boundaryFlowSegments = new LineRenderer[5];
      for (var i = 0; i < _boundaryFlowSegments.Length; i++)
      {
        var line = CreateRingLine($"ArenaBoundaryFlow_{i + 1}", 0.13f, new Color(0.84f, 1f, 1f, 0.42f), 12 + i);
        line.loop = false;
        _boundaryFlowSegments[i] = line;
      }

      _boundaryTextureSegments = new LineRenderer[10];
      for (var i = 0; i < _boundaryTextureSegments.Length; i++)
      {
        var width = i % 3 == 0 ? 0.032f : 0.022f;
        var line = CreateRingLine($"ArenaBoundaryTexture_{i + 1}", width, new Color(0.42f, 0.86f, 1f, 0.07f), 10);
        line.loop = false;
        _boundaryTextureSegments[i] = line;
      }
    }

    void BuildArenaPulse()
    {
      _pulseRings = new LineRenderer[2];
      for (var i = 0; i < _pulseRings.Length; i++)
      {
        var go = new GameObject($"ArenaPulseRing_{i + 1}");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        ConfigureLine(line, 0.08f - i * 0.025f, Color.clear, 7 + i, false);
        line.loop = true;
        line.enabled = false;
        DrawCircle(line, 0.25f, CircleSegments);
        _pulseRings[i] = line;
      }
    }

    void BuildFloatingGeometry()
    {
      _floatingRoot = new GameObject("ArenaFloatingGeometry").transform;
      _floatingRoot.SetParent(transform, false);

      const int count = 72;
      _floatingShards = new FloatingShard[count];
      for (var i = 0; i < count; i++)
      {
        var go = new GameObject($"FloatingShard_{i + 1}");
        go.transform.SetParent(_floatingRoot, false);

        var line = go.AddComponent<LineRenderer>();
        ConfigureLine(line, Random.Range(0.02f, 0.045f), new Color(0.4f, 0.82f, 1f, 0f), 3, false);
        line.loop = true;
        DrawShard(line, Random.Range(0, 4));

        _floatingShards[i] = new FloatingShard
        {
          Transform = go.transform,
          Line = line,
        };
        ResetFloatingShard(i, true);
      }
    }

    void UpdateVisuals()
    {
      var time = Time.time;
      var rawBreath = 0.5f - Mathf.Cos(time * Mathf.PI * 2f / 5.2f) * 0.5f;
      var breath = 0.94f + Mathf.SmoothStep(0f, 1f, rawBreath) * 0.08f;
      _fillDisc.color = new Color(
        ArenaFillColor.r * breath,
        ArenaFillColor.g * breath,
        ArenaFillColor.b * breath,
        ArenaFillColor.a);

      UpdateEnergyPattern(time, breath);
      UpdateBarrier(time, breath);
      UpdateBoundaryLoop(time, breath);
      UpdateArenaPulse(time);
      UpdateFloatingGeometry(time);

      if (_fillDisc != null)
      {
        var fillScale = _effectiveRadius / LayoutVisualRadius;
        _fillDisc.transform.localScale = Vector3.one * fillScale;
      }

      if (_bossPrepHighlight && _circleLine != null)
      {
        var pulse = 0.65f + 0.35f * Mathf.Sin(time * 3.5f);
        var c = new Color(1f, 0.55f, 0.22f, 0.85f + pulse * 0.15f);
        _circleLine.startColor = _circleLine.endColor = c;
      }
      else if (_circleLine != null)
      {
        _circleLine.startColor = _circleLine.endColor = _activeBorderColor;
      }
    }

    void UpdateEnergyPattern(float time, float breath)
    {
      if (_energyRings == null)
        return;

      for (var i = 0; i < _energyRings.Length; i++)
      {
        var pulse = 0.5f + 0.5f * Mathf.Sin(time * 0.18f + i * 1.37f);
        var radius = VisualRadius * (i + 1f) / (_energyRings.Length + 1f) + pulse * 0.05f;
        var alpha = (0.025f + pulse * 0.018f) * breath;
        var line = _energyRings[i];
        DrawCircle(line, radius, CircleSegments);
        line.startWidth = 0.018f + pulse * 0.008f;
        line.endWidth = line.startWidth;
        line.startColor = new Color(0.38f, 0.85f, 1f, alpha);
        line.endColor = new Color(0.12f, 0.45f, 1f, alpha * 0.4f);
      }
    }

    void UpdateBarrier(float time, float breath)
    {
      if (_barrierRings == null)
        return;

      if (_barrierParticles != null)
      {
        var shape = _barrierParticles.shape;
        shape.radius = VisualRadius;
      }

      for (var i = 0; i < _barrierRings.Length; i++)
      {
        var pulse = 0.5f + 0.5f * Mathf.Sin(time * (1.2f + i * 0.35f) + i * 1.7f);
        var radius = VisualRadius + i * 0.25f + pulse * 0.08f;
        var alpha = (0.25f + pulse * 0.28f) * breath;
        var line = _barrierRings[i];
        DrawCircle(line, radius, CircleSegments);
        line.startColor = new Color(0.72f, 0.96f, 1f, alpha);
        line.endColor = new Color(0.28f, 0.58f, 1f, alpha * 0.55f);
      }
    }

    float ComputeEdgeProximity()
    {
      var player = GetPlayerTransform();
      if (player == null || _effectiveRadius <= 0.01f)
        return 0f;

      var dist = Vector2.Distance(GameplayPlane.Position2D(player), s_center);
      var edgeStart = _effectiveRadius * 0.68f;
      if (dist <= edgeStart)
        return 0f;

      return Mathf.Clamp01((dist - edgeStart) / Mathf.Max(0.01f, _effectiveRadius - edgeStart));
    }

    void UpdateBoundaryLoop(float time, float breath)
    {
      var edgeProximity = ComputeEdgeProximity();
      _boundaryResponse = Mathf.Max(_boundaryResponse, edgeProximity);
      _boundaryResponse = Mathf.MoveTowards(_boundaryResponse, edgeProximity, Time.deltaTime * 0.75f);
      var response = Mathf.Clamp01(_boundaryResponse);
      var borderBreath = breath * (1f + response * 0.22f);

      if (_circleLine != null)
      {
        DrawCircle(_circleLine, VisualRadius, CircleSegments);
        _circleLine.transform.localRotation = Quaternion.Euler(0f, 0f, time * 0.85f);
        _circleLine.startWidth = BorderWidth * (1f + response * 0.32f);
        _circleLine.endWidth = _circleLine.startWidth;
        _circleLine.startColor = WithAlpha(BorderColor, Mathf.Clamp01(0.76f + 0.13f * borderBreath + response * 0.16f));
        _circleLine.endColor = new Color(0.34f, 0.72f, 1f, Mathf.Clamp01(0.48f + response * 0.22f));
      }

      if (_outerGhostRing != null)
      {
        _outerGhostRing.transform.localRotation = Quaternion.Euler(0f, 0f, -time * 0.42f);
        var ghostPulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / 5.6f);
        var ghostAlpha = (0.05f + ghostPulse * 0.06f + response * 0.06f) * breath;
        DrawCircle(_outerGhostRing, VisualRadius + 1.15f + ghostPulse * 0.18f + response * 0.28f, CircleSegments);
        _outerGhostRing.startColor = new Color(0.48f, 0.86f, 1f, ghostAlpha);
        _outerGhostRing.endColor = new Color(0.2f, 0.45f, 1f, ghostAlpha * 0.42f);
      }

      UpdateBoundaryFlowSegments(time, breath, response);
      UpdateBoundaryTextureSegments(time, breath, response);
    }

    void UpdateBoundaryFlowSegments(float time, float breath, float response)
    {
      if (_boundaryFlowSegments == null)
        return;

      for (var i = 0; i < _boundaryFlowSegments.Length; i++)
      {
        var line = _boundaryFlowSegments[i];
        if (line == null)
          continue;

        var phase = Mathf.Repeat(time * (0.035f + i * 0.004f) + i / (float)_boundaryFlowSegments.Length, 1f);
        var arc = Mathf.Lerp(10f, 17f, 0.5f + 0.5f * Mathf.Sin(time * 0.31f + i));
        var startDeg = phase * 360f;
        DrawArc(line, VisualRadius + 0.08f + i * 0.025f, startDeg, arc, BoundarySegmentPoints);

        var shimmer = 0.5f + 0.5f * Mathf.Sin(time * 1.1f + i * 1.9f);
        var alpha = (0.08f + shimmer * 0.055f + response * 0.12f) * breath;
        line.startWidth = 0.045f + response * 0.025f;
        line.endWidth = 0.012f;
        line.startColor = new Color(0.9f, 1f, 1f, alpha);
        line.endColor = new Color(0.18f, 0.62f, 1f, alpha * 0.22f);
      }
    }

    void UpdateBoundaryTextureSegments(float time, float breath, float response)
    {
      if (_boundaryTextureSegments == null)
        return;

      for (var i = 0; i < _boundaryTextureSegments.Length; i++)
      {
        var line = _boundaryTextureSegments[i];
        if (line == null)
          continue;

        var baseDeg = i * (360f / _boundaryTextureSegments.Length);
        var drift = time * (1.15f + (i % 4) * 0.08f);
        var phase = Mathf.Sin(time * 0.47f + i * 0.73f);
        var arc = 4.5f + (i % 4) * 1.35f + phase * 0.8f;
        var radius = VisualRadius - 0.75f - (i % 3) * 0.18f + phase * 0.025f;
        DrawArc(line, radius, baseDeg + drift, arc, 8);

        var alpha = (0.02f + Mathf.Abs(phase) * 0.035f + response * 0.045f) * breath;
        line.startColor = new Color(0.46f, 0.92f, 1f, alpha);
        line.endColor = new Color(0.18f, 0.48f, 1f, alpha * 0.35f);
      }
    }

    void UpdateArenaPulse(float time)
    {
      if (_pulseRings == null)
        return;

      if (_activePulseCount == 0 && time >= _nextPulseAt)
      {
        _activePulseCount = _pulseRings.Length;
        _pulseAge = 0f;
        foreach (var ring in _pulseRings)
          ring.enabled = true;
      }

      if (_activePulseCount == 0)
        return;

      _pulseAge += Time.deltaTime;
      var allDone = true;
      for (var i = 0; i < _pulseRings.Length; i++)
      {
        var delay = i * 0.22f;
        var t = Mathf.Clamp01((_pulseAge - delay) / PulseDuration);
        var line = _pulseRings[i];
        var active = _pulseAge >= delay && t < 1f;
        line.enabled = active;
        if (!active)
          continue;

        allDone = false;
        var ease = 1f - Mathf.Pow(1f - t, 2.4f);
        DrawCircle(line, Mathf.Lerp(0.4f, VisualRadius * 1.03f, ease), CircleSegments);
        var alpha = Mathf.Sin(t * Mathf.PI) * 0.32f;
        line.startWidth = Mathf.Lerp(0.11f, 0.025f, t);
        line.endWidth = line.startWidth;
        line.startColor = new Color(0.82f, 0.98f, 1f, alpha);
        line.endColor = new Color(0.24f, 0.72f, 1f, alpha * 0.5f);
      }

      if (allDone)
      {
        _activePulseCount = 0;
        ScheduleNextPulse();
      }
    }

    void UpdateFloatingGeometry(float time)
    {
      if (_floatingShards == null)
        return;

      for (var i = 0; i < _floatingShards.Length; i++)
      {
        var shard = _floatingShards[i];
        if (shard.Transform == null)
          continue;

        shard.Age += Time.deltaTime;
        if (shard.Age >= shard.Lifetime)
        {
          _floatingShards[i] = shard;
          ResetFloatingShard(i, false);
          continue;
        }

        shard.BasePosition += shard.Velocity * Time.deltaTime;
        var offset = Mathf.Sin(time * shard.BobSpeed + shard.Phase) * shard.BobAmplitude;
        shard.Transform.localPosition = shard.BasePosition + new Vector3(0f, offset, 0f);
        shard.Transform.Rotate(0f, 0f, shard.RotateSpeed * Time.deltaTime, Space.Self);
        var normalizedAge = Mathf.Clamp01(shard.Age / shard.Lifetime);
        var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedAge / 0.24f));
        var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalizedAge - 0.68f) / 0.32f));
        var shimmer = 0.72f + (0.5f + 0.5f * Mathf.Sin(time * 0.55f + shard.Phase)) * 0.28f;
        var a = shard.BaseAlpha * fadeIn * fadeOut * shimmer;
        shard.Line.startColor = new Color(0.46f, 0.88f, 1f, a);
        shard.Line.endColor = new Color(0.18f, 0.48f, 1f, a * 0.46f);
        _floatingShards[i] = shard;
      }
    }

    void ResetFloatingShard(int index, bool initial)
    {
      if (_floatingShards == null || index < 0 || index >= _floatingShards.Length)
        return;

      var shard = _floatingShards[index];
      if (shard.Transform == null || shard.Line == null)
        return;

      var insideArena = Random.value < 0.68f;
      var angle = Random.Range(0f, Mathf.PI * 2f);
      var dist = insideArena
        ? Random.Range(VisualRadius * 0.16f, VisualRadius * 0.86f)
        : Random.Range(VisualRadius + 0.5f, VisualRadius + 4f);
      var pos = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
      var driftAngle = angle + Random.Range(-0.7f, 0.7f);

      shard.BasePosition = pos;
      shard.Velocity = new Vector3(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle), 0f) * Random.Range(0.08f, 0.26f);
      shard.Phase = Random.Range(0f, Mathf.PI * 2f);
      shard.BobAmplitude = Random.Range(0.08f, 0.32f);
      shard.BobSpeed = Random.Range(0.2f, 0.48f);
      shard.RotateSpeed = Random.Range(-8f, 8f);
      shard.Age = initial ? Random.Range(0f, 5f) : 0f;
      shard.Lifetime = Random.Range(7.5f, 13.5f);
      shard.BaseAlpha = insideArena ? Random.Range(0.09f, 0.17f) : Random.Range(0.14f, 0.26f);

      shard.Transform.localPosition = pos;
      shard.Transform.localScale = Vector3.one * Random.Range(0.42f, insideArena ? 1.05f : 1.45f);
      shard.Transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
      shard.Line.startWidth = Random.Range(0.018f, insideArena ? 0.034f : 0.046f);
      shard.Line.endWidth = shard.Line.startWidth;
      DrawShard(shard.Line, Random.Range(0, 4));

      _floatingShards[index] = shard;
    }

    void ScheduleNextPulse()
    {
      _nextPulseAt = Time.time + Random.Range(8f, 12f);
    }

    void TriggerArenaPulse(float intensity)
    {
      _boundaryResponse = Mathf.Max(_boundaryResponse, Mathf.Lerp(0.28f, 1f, Mathf.Clamp01(intensity)));
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (!IsActive)
        return;

      _boundaryResponse = Mathf.Max(_boundaryResponse, evt.IsBoss ? 0.42f : 0.12f);
    }

    void OnLevelUp(LevelUpEvent evt)
    {
      _boundaryResponse = Mathf.Max(_boundaryResponse, 0.55f);
    }

    static Sprite CreateDiscSprite(int resolution)
    {
      var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };

      var center = resolution * 0.5f;
      var radius = center - 1.5f;
      var pixels = new Color[resolution * resolution];
      for (int y = 0; y < resolution; y++)
      {
        for (int x = 0; x < resolution; x++)
        {
          var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
          var edge = Mathf.Clamp01((radius - dist) / 3.5f);
          pixels[y * resolution + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, edge));
        }
      }

      tex.SetPixels(pixels);
      tex.Apply();

      var ppu = resolution / (ArenaRadius * 2f);
      return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), ppu);
    }

    static void ConfigureLine(LineRenderer lr, float width, Color color, int sortOrder, bool useWorldSpace)
    {
      lr.useWorldSpace = useWorldSpace;
      lr.startWidth = width;
      lr.endWidth = width;
      lr.material = ArenaLineMaterial;
      lr.startColor = color;
      lr.endColor = color;
      lr.sortingOrder = sortOrder;
    }

    LineRenderer CreateRingLine(string name, float width, Color color, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      ConfigureLine(line, width, color, sortOrder, false);
      return line;
    }

    static Material ArenaLineMaterial =>
      s_lineMaterial != null
        ? s_lineMaterial
        : s_lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EnergyArenaLine_Runtime" };

    static void DrawCircle(LineRenderer line, float radius, int segments)
    {
      if (line == null)
        return;

      line.loop = true;
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    static void DrawArc(LineRenderer line, float radius, float startDeg, float arcDeg, int points)
    {
      if (line == null)
        return;

      points = Mathf.Max(2, points);
      line.loop = false;
      line.positionCount = points;
      for (var i = 0; i < points; i++)
      {
        var t = points <= 1 ? 0f : i / (float)(points - 1);
        var angle = (startDeg + arcDeg * t) * Mathf.Deg2Rad;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }

    static void DrawShard(LineRenderer line, int shape)
    {
      var points = shape switch
      {
        0 => new[]
        {
          new Vector3(-0.45f, -0.45f, 0f),
          new Vector3(0.45f, -0.45f, 0f),
          new Vector3(0.45f, 0.45f, 0f),
          new Vector3(-0.45f, 0.45f, 0f)
        },
        1 => new[]
        {
          new Vector3(0f, 0.62f, 0f),
          new Vector3(0.46f, 0f, 0f),
          new Vector3(0f, -0.62f, 0f),
          new Vector3(-0.46f, 0f, 0f)
        },
        2 => new[]
        {
          new Vector3(0f, 0.62f, 0f),
          new Vector3(0.56f, -0.32f, 0f),
          new Vector3(-0.56f, -0.32f, 0f)
        },
        _ => new[]
        {
          new Vector3(0f, 0.7f, 0f),
          new Vector3(0.5f, 0.2f, 0f),
          new Vector3(0.32f, -0.52f, 0f),
          new Vector3(-0.32f, -0.52f, 0f),
          new Vector3(-0.5f, 0.2f, 0f)
        }
      };

      line.positionCount = points.Length;
      line.SetPositions(points);
    }

    struct FloatingShard
    {
      public Transform Transform;
      public LineRenderer Line;
      public Vector3 BasePosition;
      public Vector3 Velocity;
      public float Phase;
      public float BobAmplitude;
      public float BobSpeed;
      public float RotateSpeed;
      public float Age;
      public float Lifetime;
      public float BaseAlpha;
    }

    /// <summary>将玩家位置限制在竞技场内（边缘软阻力，避免频繁撞墙）。</summary>
    void ClampPlayerPosition()
    {
      var player = GetPlayerTransform();
      if (player == null) return;

      var pos = (Vector2)player.position;
      var toCenter = pos - s_center;
      var dist = toCenter.magnitude;

      var maxDist = _effectiveRadius - WorldGridConstants.PlayerCollisionRadius;
      var softStart = maxDist * 0.93f;
      Vector2 clamped;
      if (dist > maxDist && dist > 0.01f)
        clamped = s_center + toCenter.normalized * maxDist;
      else if (dist > softStart && dist > 0.01f)
      {
        var t = (dist - softStart) / Mathf.Max(0.001f, maxDist - softStart);
        var eased = Mathf.Lerp(dist, maxDist, t * t);
        clamped = s_center + toCenter.normalized * eased;
      }
      else
        clamped = pos;

      clamped = ArenaQuadrantBlocker.ClampPosition(clamped, WorldGridConstants.PlayerCollisionRadius);
      if ((clamped - pos).sqrMagnitude > 0.0001f)
      {
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
          rb.MovePosition(clamped);
        else
          GameplayPlane.SetPosition2D(player, clamped, player.position.z);
      }
    }

    /// <summary>将世界位置限制在竞技场内（用于刷怪）?/summary>
    public static Vector2 ClampPosition(Vector2 worldPos, float entityRadius = 0.5f)
    {
      if (!IsActive) return worldPos;

      var radius = s_instance != null ? s_instance._effectiveRadius : ArenaRadius;
      var toCenter = worldPos - s_center;
      var dist = toCenter.magnitude;
      var maxDist = radius - Mathf.Max(0.1f, entityRadius);

      if (dist > maxDist && dist > 0.01f)
        worldPos = s_center + toCenter.normalized * maxDist;

      return ArenaQuadrantBlocker.IsActive
        ? ArenaQuadrantBlocker.ClampPosition(worldPos, entityRadius)
        : worldPos;
    }

    /// <summary>返回圆环路径上的一个生成点（竞技场模式下怪物只在圆环上生成）?/summary>
    public static Vector2 GetSpawnPointOnCircle(Vector2 hintPos)
    {
      if (!IsActive) return hintPos;

      var toHint = hintPos - s_center;
      var hintDist = toHint.magnitude;
      var radius = s_instance != null ? s_instance._effectiveRadius : s_layoutRadius;

      // Player-relative spawns stay near the hinted ring instead of snapping to the outer path.
      if (hintDist > 0.01f && hintDist < radius * 0.88f)
        return ClampPosition(hintPos, 0.5f);

      var angle = hintDist > 0.0001f
        ? Mathf.Atan2(toHint.y, toHint.x)
        : Random.Range(0f, Mathf.PI * 2f);

      angle += Random.Range(-0.12f, 0.12f);
      return PositionAtAngle(angle);
    }

    /// <summary>圆环路径上指定角度的位置?/summary>
    public static Vector2 PositionAtAngle(float angleRad)
    {
      return s_center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * PathRadius;
    }

    /// <summary>世界坐标相对圆心的极角（弧度）?/summary>
    public static float AngleAtPosition(Vector2 worldPos)
    {
      var offset = worldPos - s_center;
      return offset.sqrMagnitude > 0.0001f
        ? Mathf.Atan2(offset.y, offset.x)
        : 0f;
    }

    /// <summary>将位置投影到圆环路径上?/summary>
    public static Vector2 SnapToPath(Vector2 worldPos)
    {
      return PositionAtAngle(AngleAtPosition(worldPos));
    }

    /// <summary>?from ?to 的最短有符号角差（弧度，范围 -π..π）?/summary>
    public static float ShortestSignedAngle(float fromRad, float toRad)
    {
      return Mathf.DeltaAngle(fromRad * Mathf.Rad2Deg, toRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;
    }

    /// <summary>计算圆环弦：起点/终点均在圆环上，?dir 方向穿过圆盘?/summary>
    public static void ComputeChord(
      Vector2 ownerPlanar,
      Vector2 dir,
      out Vector2 start,
      out Vector2 end,
      out float chordLength)
    {
      dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
      var toOwner = ownerPlanar - s_center;
      start = toOwner.sqrMagnitude > 0.0001f
        ? s_center + toOwner.normalized * PathRadius
        : s_center + Vector2.right * PathRadius;

      var radial = start - s_center;
      chordLength = -2f * Vector2.Dot(dir, radial);
      if (chordLength < 0.001f)
      {
        dir = (s_center - start).normalized;
        chordLength = -2f * Vector2.Dot(dir, radial);
      }

      chordLength = Mathf.Max(chordLength, 0.5f);
      end = start + dir * chordLength;
    }

    /// <summary>检查位置是否在竞技场内?/summary>
    public static bool IsInside(Vector2 worldPos, float margin = 0f)
    {
      if (!IsActive) return true;
      var radius = s_instance != null ? s_instance._effectiveRadius : s_layoutRadius;
      return (worldPos - s_center).sqrMagnitude <= (radius - margin) * (radius - margin);
    }

    static Transform GetPlayerTransform()
    {
      var player = GameObject.FindGameObjectWithTag("Player");
      if (player == null)
        player = GameObject.Find("Player");
      return player != null ? player.transform : null;
    }

    void OnDrawGizmosSelected()
    {
      Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.5f);
      var center = Application.isPlaying ? (Vector3)s_center : Vector3.zero;
      Gizmos.DrawWireSphere(center, Application.isPlaying ? EffectiveRadius : LayoutVisualRadius);
      Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
      Gizmos.DrawWireSphere(center, PathRadius);
    }
  }
}
