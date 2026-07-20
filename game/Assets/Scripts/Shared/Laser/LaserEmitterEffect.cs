using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Laser
{
  /// <summary>激光持续照射期间：中心高亮核心 + ?黄火花高频向外喷溅?/summary>
  [DisallowMultipleComponent]
  public class LaserEmitterEffect : MonoBehaviour
  {
    const float EmitterDepthZ = -0.08f;
    const int CoreGlowSortingOrder = 33;
    const int SparkSortingOrder = 34;

    static readonly Color CoreGlowColor = new(1f, 1f, 1f, 1f);
    static readonly Color SparkWhite = new(1f, 1f, 1f, 1f);
    static readonly Color SparkYellow = new(1f, 0.97f, 0.58f, 1f);

    SpriteRenderer _coreGlow;
    ParticleSystem _sparks;
    float _pulsePhase;
    bool _active;

    public void EnsureBuilt()
    {
      if (_coreGlow != null)
        return;

      _coreGlow = BuildCoreGlow();
      _sparks = BuildSparks();
    }

    SpriteRenderer BuildCoreGlow()
    {
      var go = new GameObject("EnergyCore");
      go.transform.SetParent(transform, false);
      go.transform.localScale = Vector3.one * 0.24f;

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.material = LaserVfxShared.CreateBeamMaterialInstance();
      LaserVfxShared.SetSpriteColor(sr, CoreGlowColor);
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = CoreGlowSortingOrder;
      return sr;
    }

    ParticleSystem BuildSparks()
    {
      var go = new GameObject("EmitterSparks");
      go.transform.SetParent(transform, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.2f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.04f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 56;
      main.startColor = new ParticleSystem.MinMaxGradient(SparkWhite, SparkYellow);

      var emission = ps.emission;
      emission.rateOverTime = 52f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.04f;
      shape.radiusThickness = 0.7f;

      ConfigureRadialVelocity(ps, 1f, 2.4f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.SparkFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.06f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SparkSortingOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    public void Begin(Vector3 ownerPosition)
    {
      EnsureBuilt();
      _active = true;
      _pulsePhase = Random.value * Mathf.PI * 2f;
      SyncTo(ownerPosition);
      if (_coreGlow != null)
        _coreGlow.enabled = true;
      _sparks.Play(true);
    }

    public void SyncTo(Vector3 ownerPosition)
    {
      if (!_active)
        return;

      var p = ownerPosition;
      p.z = EmitterDepthZ;
      transform.position = p;
    }

    void Update()
    {
      if (!_active || _coreGlow == null)
        return;

      _pulsePhase += Time.deltaTime * 16f;
      var pulse = 0.9f + 0.1f * Mathf.Sin(_pulsePhase);
      _coreGlow.transform.localScale = Vector3.one * (0.22f + 0.05f * pulse);

      var c = CoreGlowColor;
      c.a = 0.92f + 0.08f * pulse;
      LaserVfxShared.SetSpriteColor(_coreGlow, c);
    }

    public void End()
    {
      _active = false;
      if (_sparks != null)
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      if (_coreGlow != null)
        _coreGlow.enabled = false;
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
