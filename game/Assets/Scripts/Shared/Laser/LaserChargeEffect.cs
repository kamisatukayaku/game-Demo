using System.Collections;
using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 激光蓄?VFX：外围粒子向中心汇聚、末期火花、高亮核心、发射瞬间闪光与前向喷射?
  /// 纯代?ParticleSystem，无 Prefab；每怪物一份，Awake 构建一次?
  /// </summary>
  [DisallowMultipleComponent]
  public class LaserChargeEffect : MonoBehaviour
  {
    const float SparkLeadTime = 0.3f;
    const int ConvergeSort = 18;
    const int SparkSort = 19;
    const int CoreSort = 20;
    const int ReleaseSort = 22;

    static readonly Color CoreWhite = new(1f, 1f, 1f, 0.45f);
    static readonly Color ParticleWhite = new(1f, 1f, 1f, 1f);

    Transform _anchor;
    ParticleSystem _converge;
    ParticleSystem _sparks;
    ParticleSystem _releaseSpray;
    ParticleSystem _releaseFlash;
    SpriteRenderer _core;

    float _duration = 1f;
    float _elapsed;
    float _outerRadius = 0.62f;
    bool _active;
    bool _sparksStarted;
    Vector2 _fireDir = Vector2.right;

    public bool IsActive => _active;

    public static LaserChargeEffect Ensure(GameObject owner)
    {
      if (owner == null)
        return null;

      var existing = owner.GetComponent<LaserChargeEffect>();
      if (existing != null)
        return existing;

      return owner.AddComponent<LaserChargeEffect>();
    }

    void Awake() => EnsureBuilt();

    public void EnsureBuilt()
    {
      if (_converge != null)
        return;

      _anchor = new GameObject("LaserChargeAnchor").transform;
      _anchor.SetParent(transform, false);

      _core = BuildCoreGlow();
      _converge = BuildConvergeParticles();
      _sparks = BuildSparkParticles();
      _releaseSpray = BuildReleaseSpray();
      _releaseFlash = BuildReleaseFlash();
    }

    public void BeginCharge(float duration, Vector2 fireDirection)
    {
      EnsureBuilt();
      _duration = Mathf.Max(0.15f, duration);
      _elapsed = 0f;
      _active = true;
      _sparksStarted = false;
      _fireDir = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.right;
      _outerRadius = Mathf.Clamp(LaserVfxShared.GetOwnerVisualRadius(transform) * 1.05f, 0.45f, 1.1f);

      SyncAnchor();
      ResetCoreVisual(0f);
      _core.enabled = true;

      _converge.Clear(true);
      var main = _converge.main;
      main.startSize = 0.14f;
      _converge.Play(true);
      _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      ApplyConvergePhase(0f);
    }

    public void EndCharge(Vector2 fireDirection)
    {
      if (!_active && _converge == null)
        return;

      _active = false;
      if (fireDirection.sqrMagnitude > 0.0001f)
        _fireDir = fireDirection.normalized;

      _converge?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      _sparks?.Stop(true, ParticleSystemStopBehavior.StopEmitting);

      PlayRelease();
      StartCoroutine(FadeCoreAfterRelease());
    }

    public void Cancel()
    {
      _active = false;
      if (_converge != null)
        _converge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      if (_sparks != null)
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      if (_core != null)
        _core.enabled = false;
    }

    void Update()
    {
      if (!_active)
        return;

      SyncAnchor();
      _elapsed += Time.deltaTime;
      var t = Mathf.Clamp01(_elapsed / _duration);

      ApplyConvergePhase(t);
      UpdateCore(t);

      if (!_sparksStarted && _elapsed >= Mathf.Max(0f, _duration - SparkLeadTime))
      {
        _sparksStarted = true;
        _sparks.Play(true);
        ApplySparkPhase(t);
      }
      else if (_sparksStarted)
      {
        ApplySparkPhase(t);
      }

      if (_elapsed >= _duration)
        _active = false;
    }

    void ApplyConvergePhase(float t)
    {
      if (_converge == null)
        return;

      var ramp = t * t;

      var emission = _converge.emission;
      emission.rateOverTime = Mathf.Lerp(8f, 62f, ramp);

      var speed = Mathf.Lerp(2f, 9f, ramp);
      var vel = _converge.velocityOverLifetime;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(-speed, -speed);

      var shape = _converge.shape;
      shape.radius = _outerRadius;
    }

    void ApplySparkPhase(float t)
    {
      if (_sparks == null)
        return;

      var sparkT = Mathf.InverseLerp(1f - SparkLeadTime / _duration, 1f, t);
      var ramp = sparkT * sparkT;

      var emission = _sparks.emission;
      emission.rateOverTime = Mathf.Lerp(6f, 48f, ramp);

      var speed = Mathf.Lerp(2.5f, 7.5f, ramp);
      var vel = _sparks.velocityOverLifetime;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(speed, speed);
    }

    void UpdateCore(float t)
    {
      if (_core == null)
        return;

      var ramp = t * t * t;
      var scale = Mathf.Lerp(0.18f, 0.44f, ramp);
      var alpha = Mathf.Lerp(0.38f, 1f, ramp);
      var pulse = 1f + 0.06f * Mathf.Sin(_elapsed * (8f + t * 22f));

      _core.transform.localScale = Vector3.one * scale * pulse;
      var c = CoreWhite;
      c.a = alpha;
      c.r = Mathf.Lerp(0.95f, 1f, ramp);
      c.g = Mathf.Lerp(0.95f, 1f, ramp);
      LaserVfxShared.SetSpriteColor(_core, c);
    }

    void ResetCoreVisual(float t)
    {
      if (_core == null)
        return;

      _core.transform.localScale = Vector3.one * 0.18f;
      var c = CoreWhite;
      c.a = 0.38f;
      LaserVfxShared.SetSpriteColor(_core, c);
    }

    void PlayRelease()
    {
      SyncAnchor();

      if (_core != null)
      {
        _core.enabled = true;
        _core.transform.localScale = Vector3.one * 0.52f;
        LaserVfxShared.SetSpriteColor(_core, new Color(1f, 1f, 1f, 1f));
      }

      var angle = Mathf.Atan2(_fireDir.y, _fireDir.x) * Mathf.Rad2Deg;
      if (_releaseSpray != null)
      {
        _releaseSpray.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        _releaseSpray.Play(true);
      }

      if (_releaseFlash != null)
      {
        _releaseFlash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        _releaseFlash.Play(true);
      }
    }

    IEnumerator FadeCoreAfterRelease()
    {
      yield return new WaitForSeconds(0.12f);
      if (_core != null)
        _core.enabled = false;
    }

    void SyncAnchor()
    {
      if (_anchor == null)
        return;

      _anchor.position = LaserVfxShared.GetOwnerEmissionPoint(transform);
    }

    SpriteRenderer BuildCoreGlow()
    {
      var go = new GameObject("ChargeCore");
      go.transform.SetParent(_anchor, false);

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.material = LaserVfxShared.CreateBeamMaterialInstance();
      LaserVfxShared.SetSpriteColor(sr, CoreWhite);
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = CoreSort;
      sr.enabled = false;
      return sr;
    }

    ParticleSystem BuildConvergeParticles()
    {
      var go = new GameObject("ChargeConverge");
      go.transform.SetParent(_anchor, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 2f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
      main.startSpeed = 0f;
      main.startSize = 0.12f;
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 96;
      main.startColor = new ParticleSystem.MinMaxGradient(ParticleWhite, ParticleWhite);

      var emission = ps.emission;
      emission.rateOverTime = 10f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.62f;
      shape.radiusThickness = 0f;

      ConfigureInwardVelocity(ps, 2.5f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.WhiteFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.15f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, ConvergeSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSparkParticles()
    {
      var go = new GameObject("ChargeSparks");
      go.transform.SetParent(_anchor, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 0.35f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
      main.startSpeed = 0f;
      main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.085f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 64;
      main.startColor = new ParticleSystem.MinMaxGradient(ParticleWhite, ParticleWhite);

      var emission = ps.emission;
      emission.rateOverTime = 8f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.08f;
      shape.radiusThickness = 0.55f;

      ConfigureOutwardVelocity(ps, 3f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.WhiteFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.05f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SparkSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildReleaseSpray()
    {
      var go = new GameObject("ChargeReleaseSpray");
      go.transform.SetParent(_anchor, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.22f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 48;
      main.startColor = new ParticleSystem.MinMaxGradient(ParticleWhite, ParticleWhite);

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Cone;
      shape.angle = 18f;
      shape.radius = 0.06f;
      shape.rotation = new Vector3(0f, 0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.WhiteFade;

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, ReleaseSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildReleaseFlash()
    {
      var go = new GameObject("ChargeReleaseFlash");
      go.transform.SetParent(_anchor, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.12f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 24;
      main.startColor = Color.white;

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.04f;
      shape.radiusThickness = 0.65f;

      ConfigureOutwardVelocity(ps, 2f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = LaserVfxGradients.WhiteFade;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySharedParticleRenderer(renderer, ReleaseSort + 1);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    static void ConfigureInwardVelocity(ParticleSystem ps, float radialSpeed)
    {
      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(-radialSpeed, -radialSpeed);
    }

    static void ConfigureOutwardVelocity(ParticleSystem ps, float radialSpeed)
    {
      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(radialSpeed, radialSpeed);
    }
  }
}
