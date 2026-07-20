using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 沿持续激光路径的白色能量粒子雨：从发射点流向命中点，增强能量输送感?
  /// </summary>
  [DisallowMultipleComponent]
  public class LaserRainEffect : MonoBehaviour
  {
    const float RainDepthZ = -0.1f;
    const int SortingOrder = 32;

    ParticleSystem _rain;
    float _beamThickness = 0.14f;
    float _baseRate = 46f;
    float _ratePhase;
    bool _active;

    public void EnsureBuilt()
    {
      if (_rain != null)
        return;

      _rain = gameObject.GetComponent<ParticleSystem>();
      if (_rain == null)
        _rain = gameObject.AddComponent<ParticleSystem>();

      _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = _rain.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
      main.startSpeed = 0f;
      main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.042f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 64;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 1f, 0.98f, 1f));

      var emission = _rain.emission;
      emission.rateOverTime = _baseRate;

      var shape = _rain.shape;
      shape.shapeType = ParticleSystemShapeType.Box;
      shape.scale = new Vector3(4f, _beamThickness, 0.01f);

      ConfigureBeamVelocity(_rain, 14f);

      var col = _rain.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.RainFade;

      var sizeOverLife = _rain.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.2f);

      var renderer = _rain.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SortingOrder);
      _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Begin(float beamThickness)
    {
      EnsureBuilt();
      _beamThickness = Mathf.Max(0.1f, beamThickness * 0.68f);
      _baseRate = 46f;
      _ratePhase = Random.value * Mathf.PI * 2f;
      _active = true;
      _rain.Play(true);
    }

    public void SyncBeam(Vector3 start, Vector3 end)
    {
      if (!_active || _rain == null)
        return;

      var delta = end - start;
      delta.z = 0f;
      var len = delta.magnitude;
      if (len < 0.05f)
        return;

      var dir = delta / len;
      var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
      var anchor = start;
      anchor.z = RainDepthZ;

      transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, 0f, angle));

      var flowSpeed = Mathf.Clamp(len / 0.13f, 13f, 32f);
      ConfigureBeamVelocity(_rain, flowSpeed);

      var shape = _rain.shape;
      shape.position = new Vector3(len * 0.5f, 0f, 0f);
      shape.scale = new Vector3(len, _beamThickness, 0.01f);
    }

    public void End()
    {
      _active = false;
      if (_rain == null)
        return;

      _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Update()
    {
      if (!_active || _rain == null)
        return;

      _ratePhase += Time.deltaTime;
      var flicker = 0.86f + 0.14f * Mathf.Sin(_ratePhase * 8.7f);
      var emission = _rain.emission;
      emission.rateOverTime = _baseRate * flicker;
    }

    static void ConfigureBeamVelocity(ParticleSystem ps, float forwardSpeed)
    {
      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(forwardSpeed * 0.92f, forwardSpeed);
      vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }
  }
}
