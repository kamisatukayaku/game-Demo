using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Game.Shared.Laser;

namespace Game.Shared.Vfx
{
  /// <summary>
  /// 弹体/近战爆炸：瞬时闪?+ 径向火花 + 快速消散烟雾，短促 arcade 风格?
  /// </summary>
  [DisallowMultipleComponent]
  public class BulletExplosionEffect : MonoBehaviour
  {
    const int FlashSort = 40;
    const int FireSort = 41;
    const int SparkSort = 42;
    const int SmokeSort = 39;
    const float AutoReleaseDelay = 0.42f;

    SpriteRenderer _flash;
    ParticleSystem _fireBurst;
    ParticleSystem _sparks;
    ParticleSystem _smoke;
    Coroutine _releaseRoutine;

    public static void Spawn(Vector3 worldPosition, float radius = 0.55f, bool vacuum = false)
    {
      var fx = BulletExplosionPool.Acquire();
      if (fx == null)
        return;

      fx.PlayAt(worldPosition, Mathf.Clamp(radius, 0.25f, 1.4f), vacuum);
    }

    public void EnsureBuilt()
    {
      if (_flash != null)
        return;

      _flash = BuildFlash();
      _fireBurst = BuildFireBurst();
      _sparks = BuildSparks();
      _smoke = BuildSmoke();
    }

    public void PlayAt(Vector3 worldPosition, float radius, bool vacuum = false)
    {
      EnsureBuilt();
      gameObject.SetActive(true);

      var pos = worldPosition;
      pos.z = LaserVfxShared.VfxDepthZ;
      transform.position = pos;

      var scale = radius * 2f;
      _flash.transform.localScale = Vector3.one * scale;
      _flash.color = vacuum
        ? new Color(0.55f, 0.2f, 0.95f, 1f)
        : new Color(1f, 0.98f, 0.88f, 1f);
      _flash.enabled = true;

      PlayBurst(_fireBurst, radius);
      PlayBurst(_sparks, radius);
      PlayBurst(_smoke, radius);

      if (_releaseRoutine != null)
        StopCoroutine(_releaseRoutine);
      _releaseRoutine = StartCoroutine(FadeFlashAndRelease());
    }

    void PlayBurst(ParticleSystem ps, float radius)
    {
      if (ps == null)
        return;

      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      var shape = ps.shape;
      shape.radius = radius * 0.35f;

      ps.Clear(true);
      ps.Play(true);
    }

    IEnumerator FadeFlashAndRelease()
    {
      var elapsed = 0f;
      const float flashDuration = 0.1f;
      while (elapsed < flashDuration)
      {
        elapsed += Time.deltaTime;
        if (_flash != null)
        {
          var c = _flash.color;
          c.a = 1f - elapsed / flashDuration;
          _flash.color = c;
        }
        yield return null;
      }

      if (_flash != null)
        _flash.enabled = false;

      yield return new WaitForSeconds(AutoReleaseDelay - flashDuration);
      _releaseRoutine = null;
      BulletExplosionPool.Release(this);
    }

    SpriteRenderer BuildFlash()
    {
      var go = new GameObject("ExplosionFlash");
      go.transform.SetParent(transform, false);

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.material = LaserVfxShared.CreateBeamMaterialInstance();
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = FlashSort;
      sr.enabled = false;
      return sr;
    }

    ParticleSystem BuildFireBurst()
    {
      var ps = CreateBurstRoot("FireBurst", FireSort);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.12f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 24;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 0.95f, 0.55f, 1f),
        new Color(1f, 0.45f, 0.08f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.15f;
      shape.radiusThickness = 0.35f;

      ConfigureRadialVelocity(ps, 1f, 1.6f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildWarmFadeGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.08f);

      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSparks()
    {
      var ps = CreateBurstRoot("SparkShower", SparkSort);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 32;
      main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.92f, 0.35f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.08f;
      shape.radiusThickness = 0.2f;

      ConfigureRadialVelocity(ps, 1.2f, 2.4f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildSparkFadeGradient();

      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildSmoke()
    {
      var ps = CreateBurstRoot("SmokePuff", SmokeSort);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.14f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 12;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(0.85f, 0.85f, 0.85f, 0.55f),
        new Color(0.45f, 0.45f, 0.45f, 0.35f));

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.12f;
      shape.radiusThickness = 0.5f;

      ConfigureRadialVelocity(ps, 0.5f, 1f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildSmokeFadeGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);

      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem CreateBurstRoot(string name, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), sortOrder);
      return ps;
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

    static ParticleSystem.MinMaxGradient BuildWarmFadeGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(Color.white, 0f),
          new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f),
          new GradientColorKey(new Color(0.4f, 0.08f, 0.02f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0.55f, 0.45f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    static ParticleSystem.MinMaxGradient BuildSparkFadeGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(Color.white, 0f),
          new GradientColorKey(new Color(1f, 0.85f, 0.25f), 0.4f),
          new GradientColorKey(new Color(0.5f, 0.2f, 0.05f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0.35f, 0.65f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    static ParticleSystem.MinMaxGradient BuildSmokeFadeGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f),
          new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0.45f, 0f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    void OnDisable()
    {
      if (_releaseRoutine != null)
      {
        StopCoroutine(_releaseRoutine);
        _releaseRoutine = null;
      }
    }
  }

  public static class BulletExplosionPool
  {
    const int InitialCapacity = 24;

    static readonly Stack<BulletExplosionEffect> s_pool = new();
    static Transform s_root;
    static bool s_ready;

    public static BulletExplosionEffect Acquire()
    {
      EnsurePool();
      if (s_pool.Count > 0)
        return s_pool.Pop();

      var go = new GameObject("BulletExplosion");
      go.transform.SetParent(s_root, false);
      var fx = go.AddComponent<BulletExplosionEffect>();
      fx.EnsureBuilt();
      return fx;
    }

    public static void Release(BulletExplosionEffect fx)
    {
      if (fx == null)
        return;

      EnsurePool();
      fx.gameObject.SetActive(false);
      fx.transform.SetParent(s_root, false);
      s_pool.Push(fx);
    }

    static void EnsurePool()
    {
      if (s_ready)
        return;

      s_ready = true;
      var rootGo = new GameObject("BulletExplosionPool");
      s_root = rootGo.transform;

      for (var i = 0; i < InitialCapacity; i++)
      {
        var go = new GameObject($"BulletExplosion_{i}");
        go.transform.SetParent(s_root, false);
        go.SetActive(false);
        var fx = go.AddComponent<BulletExplosionEffect>();
        fx.EnsureBuilt();
        s_pool.Push(fx);
      }
    }
  }
}
