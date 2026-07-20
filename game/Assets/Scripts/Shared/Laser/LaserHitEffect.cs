using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Laser
{
  /// <summary>激光命中点：白色碎?+ 高亮火花 + 短暂溅射，跟随目标?/summary>
  [DisallowMultipleComponent]
  public class LaserHitEffect : MonoBehaviour
  {
    const float HitDepthZ = -0.06f;
    const int DebrisSortingOrder = 35;
    const int SplashSortingOrder = 36;
    const int SparkSortingOrder = 37;

    ParticleSystem _debris;
    ParticleSystem _splash;
    ParticleSystem _sparks;
    Transform _target;
    Vector3 _staticPosition;
    bool _useStaticPosition;
    bool _active;

    public void EnsureBuilt()
    {
      if (_debris != null)
        return;

      _debris = BuildDebris();
      _splash = BuildSplash();
      _sparks = BuildSparks();
    }

    ParticleSystem BuildDebris()
    {
      var go = new GameObject("HitDebris");
      go.transform.SetParent(transform, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.35f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.048f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 28;
      main.startColor = new ParticleSystem.MinMaxGradient(
        Color.white,
        new Color(1f, 1f, 0.96f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 22f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.045f;
      shape.radiusThickness = 0.6f;

      ConfigureRadialVelocity(ps, 0.8f, 2.2f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.RainFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.08f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, DebrisSortingOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSplash()
    {
      var go = new GameObject("HitSplash");
      go.transform.SetParent(transform, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.3f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.8f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.032f, 0.078f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 24;
      main.startColor = new ParticleSystem.MinMaxGradient(
        Color.white,
        new Color(1f, 0.98f, 0.82f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 18f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.05f;
      shape.radiusThickness = 0.5f;

      ConfigureRadialVelocity(ps, 1.2f, 3f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.HitSplash;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.06f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SplashSortingOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSparks()
    {
      var go = new GameObject("HitSparks");
      go.transform.SetParent(transform, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.25f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 6.5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.038f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 32;
      main.startColor = new ParticleSystem.MinMaxGradient(
        Color.white,
        new Color(1f, 0.96f, 0.55f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 16f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.03f;
      shape.radiusThickness = 0.35f;

      ConfigureRadialVelocity(ps, 1.5f, 3.8f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.SparkFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.04f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SparkSortingOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    public void Begin(Transform target)
    {
      EnsureBuilt();
      _target = target;
      _useStaticPosition = false;
      _active = true;
      SyncPosition();
      _debris.Play(true);
      _splash.Play(true);
      _sparks.Play(true);
    }

    public void BeginAt(Vector3 worldPosition)
    {
      EnsureBuilt();
      _target = null;
      _useStaticPosition = true;
      _staticPosition = worldPosition;
      _staticPosition.z = HitDepthZ;
      _active = true;
      transform.position = _staticPosition;
      _debris.Play(true);
      _splash.Play(true);
      _sparks.Play(true);
    }

    /// <summary>设置命中效果跟随世界坐标（用于方向锁定后的激光）?/summary>
    public void SyncTo(Vector3 worldPosition)
    {
      _useStaticPosition = true;
      _staticPosition = worldPosition;
      _staticPosition.z = HitDepthZ;
    }

    public void End()
    {
      _active = false;
      _target = null;
      _useStaticPosition = false;
      if (_debris != null)
        _debris.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      if (_splash != null)
        _splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      if (_sparks != null)
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void LateUpdate()
    {
      if (!_active)
        return;

      SyncPosition();
    }

    void SyncPosition()
    {
      if (_useStaticPosition)
      {
        transform.position = _staticPosition;
        return;
      }

      if (_target == null)
        return;

      var p = _target.position;
      p.z = HitDepthZ;
      transform.position = p;
    }

    static void ConfigureRadialVelocity(ParticleSystem ps, float radialMin, float radialMax)
    {
      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(radialMin, radialMax);
    }
  }
}
