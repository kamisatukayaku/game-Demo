using UnityEngine;
using Game.Shared.Laser;

namespace Game.Shared.Vfx
{
  /// <summary>
  /// 冲撞拖尾粒子：厚血冲撞怪冲刺时在身后留下橙红火花轨迹?
  /// 纯代?ParticleSystem，无 Prefab；每怪物一份，随怪物销毁?
  /// </summary>
  [DisallowMultipleComponent]
  public class ChargeDashTrailEffect : MonoBehaviour
  {
    const int SortOrder = 16;
    const float DepthZ = -0.05f;

    static readonly Color TrailBright = new Color(1f, 0.55f, 0.12f, 1f);
    static readonly Color TrailWarm  = new Color(1f, 0.35f, 0.08f, 0.9f);
    static readonly Color TrailDim   = new Color(0.75f, 0.22f, 0.05f, 0.65f);

    Transform _anchor;
    ParticleSystem _trail;
    ParticleSystem _sparks;
    bool _active;
    Vector2 _dashDir = Vector2.right;
    float _visualScale = 1f;

    public bool IsActive => _active;

    public static ChargeDashTrailEffect Ensure(GameObject owner)
    {
      if (owner == null) return null;
      var existing = owner.GetComponent<ChargeDashTrailEffect>();
      if (existing != null) return existing;
      return owner.AddComponent<ChargeDashTrailEffect>();
    }

    void Awake() => EnsureBuilt();

    public void EnsureBuilt()
    {
      if (_trail != null) return;

      _anchor = new GameObject("ChargeDashAnchor").transform;
      _anchor.SetParent(transform, false);

      _trail = BuildTrailParticles();
      _sparks = BuildSparkParticles();
    }

    /// <summary>
    /// 开始冲撞拖尾?
    /// </summary>
    /// <param name="dashDirection">冲刺方向（世界坐标）?/param>
    /// <param name="ownerVisualScale">怪物的视觉缩放，用于调整粒子规模?/param>
    public void Begin(Vector2 dashDirection, float ownerVisualScale = 1f)
    {
      EnsureBuilt();
      _active = true;
      _dashDir = dashDirection.sqrMagnitude > 0.0001f ? dashDirection.normalized : Vector2.right;
      _visualScale = Mathf.Max(0.5f, ownerVisualScale);

      SyncAnchor();
      ApplyScalePhase();

      _trail.Clear(true);
      _trail.Play(true);

      _sparks.Clear(true);
      _sparks.Play(true);
    }

    public void End()
    {
      _active = false;
      _trail?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      _sparks?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void Cancel()
    {
      _active = false;
      _trail?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      _sparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
      if (!_active) return;
      SyncAnchor();
    }

    void SyncAnchor()
    {
      if (_anchor == null) return;

      var pos = LaserVfxShared.GetOwnerEmissionPoint(transform);
      pos.z = DepthZ;
      _anchor.position = pos;

      var angle = Mathf.Atan2(_dashDir.y, _dashDir.x) * Mathf.Rad2Deg;
      _anchor.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void ApplyScalePhase()
    {
      if (_trail == null) return;

      var main = _trail.main;
      main.startSize = new ParticleSystem.MinMaxCurve(
        0.06f * _visualScale,
        0.14f * _visualScale);

      var shape = _trail.shape;
      shape.radius = 0.18f * _visualScale;

      var vel = _trail.velocityOverLifetime;
      vel.speedModifier = new ParticleSystem.MinMaxCurve(
        1.5f * _visualScale,
        3.5f * _visualScale);

      if (_sparks != null)
      {
        var sparkMain = _sparks.main;
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(
          0.03f * _visualScale,
          0.07f * _visualScale);

        var sparkShape = _sparks.shape;
        sparkShape.radius = 0.12f * _visualScale;
      }
    }

    ParticleSystem BuildTrailParticles()
    {
      var go = new GameObject("ChargeDashTrail");
      go.transform.SetParent(_anchor, false);
      go.transform.localPosition = new Vector3(-0.15f, 0f, 0f);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 180f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 80;
      main.startColor = new ParticleSystem.MinMaxGradient(TrailBright, TrailWarm);

      var emission = ps.emission;
      emission.rateOverTime = 55f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Hemisphere;
      shape.radius = 0.18f;
      shape.radiusThickness = 0.35f;
      shape.arc = 180f;
      shape.rotation = new Vector3(0f, 90f, 0f);
      shape.position = Vector3.zero;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.5f, -2.5f);
      vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildTrailGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.2f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SortOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSparkParticles()
    {
      var go = new GameObject("ChargeDashSparks");
      go.transform.SetParent(_anchor, false);
      go.transform.localPosition = new Vector3(-0.08f, 0f, 0f);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.5f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 180f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 48;
      main.startColor = new ParticleSystem.MinMaxGradient(Color.white, TrailBright);

      var emission = ps.emission;
      emission.rateOverTime = 28f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.12f;
      shape.radiusThickness = 0.5f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-1f, -4f);
      vel.y = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildSparkGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.05f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SortOrder + 1);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    static ParticleSystem.MinMaxGradient BuildTrailGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(1f, 0.8f, 0.25f), 0.0f),
          new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.35f),
          new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 0.75f),
          new GradientColorKey(new Color(0.35f, 0.08f, 0.02f), 1.0f)
        },
        new[]
        {
          new GradientAlphaKey(0.9f, 0.0f),
          new GradientAlphaKey(0.75f, 0.35f),
          new GradientAlphaKey(0.35f, 0.75f),
          new GradientAlphaKey(0f, 1.0f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    static ParticleSystem.MinMaxGradient BuildSparkGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(Color.white, 0.0f),
          new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0.25f),
          new GradientColorKey(new Color(0.9f, 0.3f, 0.05f), 0.6f),
          new GradientColorKey(new Color(0.4f, 0.1f, 0.02f), 1.0f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0.0f),
          new GradientAlphaKey(0.8f, 0.3f),
          new GradientAlphaKey(0.3f, 0.7f),
          new GradientAlphaKey(0f, 1.0f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }
  }
}
