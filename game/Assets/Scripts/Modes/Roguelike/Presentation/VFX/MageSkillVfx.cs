using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Laser;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  static class MageVfxUtil
  {
    static Material s_lineMaterial;

    public static Material LineMaterial =>
      s_lineMaterial != null
        ? s_lineMaterial
        : s_lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "MageVfxLine_Runtime" };

    public static LineRenderer CreateRing(Transform parent, string name, float width, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.positionCount = 72;
      line.startWidth = width;
      line.endWidth = width;
      line.material = LineMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = sortOrder;
      DrawRing(line, 1f);
      return line;
    }

    public static void DrawRing(LineRenderer line, float radius)
    {
      if (line == null)
        return;
      var count = line.positionCount;
      for (var i = 0; i < count; i++)
      {
        var a = i / (float)count * Mathf.PI * 2f;
        line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
      }
    }

    public static SpriteRenderer CreateGlow(Transform parent, string name, Color color, float size, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.material = LaserVfxShared.CreateParticleMaterialInstance();
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = sortOrder;
      LaserVfxShared.SetSpriteColor(sr, color);
      go.transform.localScale = Vector3.one * size;
      return sr;
    }

    public static ParticleSystem CreateRadialParticles(
      Transform parent,
      string name,
      Color a,
      Color b,
      float lifetimeMin,
      float lifetimeMax,
      float speedMin,
      float speedMax,
      int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      var main = ps.main;
      main.loop = false;
      main.playOnAwake = false;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
      main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.startColor = new ParticleSystem.MinMaxGradient(a, b);

      var emission = ps.emission;
      emission.enabled = false;

      var shape = ps.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.2f;
      shape.radiusThickness = 0f;
      shape.arc = 360f;

      var size = ps.sizeOverLifetime;
      size.enabled = true;
      size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
        new Keyframe(0f, 0.7f),
        new Keyframe(0.35f, 1f),
        new Keyframe(1f, 0.05f)));

      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), sortOrder);
      return ps;
    }

    public static void SetLineColor(LineRenderer line, Color start, Color end)
    {
      if (line == null)
        return;
      line.startColor = start;
      line.endColor = end;
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFlameNovaWarningVfx : MonoBehaviour
  {
    static readonly Queue<MageFlameNovaWarningVfx> Pool = new();

    LineRenderer _outer;
    LineRenderer _inner;
    LineRenderer _runeA;
    LineRenderer _runeB;
    ParticleSystem _embers;
    float _age;
    float _duration;
    float _radius;

    public static void Spawn(Vector3 position, float radius, float duration)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.02f);
      fx.Play(Mathf.Max(0.5f, radius), Mathf.Max(0.25f, duration));
    }

    static MageFlameNovaWarningVfx Create()
    {
      var go = new GameObject("MageFlameNovaWarningVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageFlameNovaWarningVfx>();
      fx._outer = MageVfxUtil.CreateRing(go.transform, "NovaWarningOuter", 0.055f, 61);
      fx._inner = MageVfxUtil.CreateRing(go.transform, "NovaWarningInner", 0.035f, 62);
      fx._runeA = MageVfxUtil.CreateRing(go.transform, "NovaWarningRuneA", 0.025f, 63);
      fx._runeB = MageVfxUtil.CreateRing(go.transform, "NovaWarningRuneB", 0.025f, 63);
      fx._embers = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "NovaWarningEmbers",
        new Color(1f, 0.1f, 0.08f, 0.95f),
        new Color(0.8f, 0.02f, 0.02f, 0.78f),
        0.18f,
        0.34f,
        -1.15f,
        -0.25f,
        64);
      var main = fx._embers.main;
      main.loop = true;
      main.maxParticles = 48;
      var emission = fx._embers.emission;
      emission.enabled = true;
      emission.rateOverTime = 28f;
      go.SetActive(false);
      return fx;
    }

    void Play(float radius, float duration)
    {
      _age = 0f;
      _radius = radius;
      _duration = duration;
      var shape = _embers.shape;
      shape.radius = radius;
      shape.radiusThickness = 0.08f;
      _embers.Clear(true);
      _embers.Play(true);
      // 蓄力开始时立即显示可见的环（修复蓄力期间不显示效果的问题）
      MageVfxUtil.DrawRing(_outer, radius);
      MageVfxUtil.SetLineColor(_outer, new Color(1f, 0.06f, 0.04f, 0.18f), new Color(0.62f, 0.01f, 0.02f, 0.1f));
      MageVfxUtil.DrawRing(_inner, radius * 0.92f);
      MageVfxUtil.SetLineColor(_inner, new Color(1f, 0.08f, 0.04f, 0.46f), new Color(0.55f, 0.01f, 0.02f, 0.22f));
      MageVfxUtil.DrawRing(_runeA, radius * 0.72f);
      MageVfxUtil.SetLineColor(_runeA, new Color(1f, 0.1f, 0.06f, 0.22f), new Color(0.62f, 0.02f, 0.02f, 0.16f));
      MageVfxUtil.DrawRing(_runeB, radius * 0.52f);
      MageVfxUtil.SetLineColor(_runeB, new Color(1f, 0.05f, 0.04f, 0.18f), new Color(0.55f, 0.01f, 0.02f, 0.12f));
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / _duration);
      var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 11f);
      var charge = Mathf.SmoothStep(0.25f, 1f, t);
      var r = _radius * (0.96f + pulse * 0.035f);
      var convergeA = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1f));
      var convergeB = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.18f) / 0.82f));
      var convergeC = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.34f) / 0.66f));

      MageVfxUtil.DrawRing(_outer, r);
      MageVfxUtil.DrawRing(_inner, Mathf.Lerp(_radius * 0.92f, _radius * 0.14f, convergeA));
      MageVfxUtil.DrawRing(_runeA, Mathf.Lerp(_radius * 0.72f, _radius * 0.08f, convergeB));
      MageVfxUtil.DrawRing(_runeB, Mathf.Lerp(_radius * 0.52f, _radius * 0.04f, convergeC));
      _inner.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 10f);
      _runeA.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 18f);
      _runeB.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 26f);

      var outerAlpha = Mathf.Lerp(0.28f, 0.92f, charge);
      MageVfxUtil.SetLineColor(_outer, new Color(1f, 0.06f, 0.04f, outerAlpha), new Color(0.62f, 0.01f, 0.02f, outerAlpha * 0.6f));
      MageVfxUtil.SetLineColor(_inner, new Color(1f, 0.08f, 0.04f, 0.46f + 0.3f * charge), new Color(0.55f, 0.01f, 0.02f, 0.22f + 0.24f * charge));
      MageVfxUtil.SetLineColor(_runeA, new Color(1f, 0.1f, 0.06f, 0.22f + 0.48f * charge), new Color(0.62f, 0.02f, 0.02f, 0.16f + 0.38f * charge));
      MageVfxUtil.SetLineColor(_runeB, new Color(1f, 0.05f, 0.04f, 0.18f + 0.42f * charge), new Color(0.55f, 0.01f, 0.02f, 0.12f + 0.3f * charge));
      if (_age >= _duration)
        Release();
    }

    void Release()
    {
      _embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      gameObject.SetActive(false);
      Pool.Enqueue(this);
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFlameNovaVfx : MonoBehaviour
  {
    static readonly Queue<MageFlameNovaVfx> Pool = new();

    const float Duration = 0.58f;
    LineRenderer _warning;
    LineRenderer _main;
    LineRenderer _heat;
    ParticleSystem _sparks;
    float _age;
    float _radius;

    public static void Spawn(Vector3 position, float radius)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ);
      fx.Play(Mathf.Max(0.5f, radius));
    }

    static MageFlameNovaVfx Create()
    {
      var go = new GameObject("MageFlameNovaVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageFlameNovaVfx>();
      fx.Build();
      go.SetActive(false);
      return fx;
    }

    void Build()
    {
      _warning = MageVfxUtil.CreateRing(transform, "WarningRing", 0.045f, 62);
      _main = MageVfxUtil.CreateRing(transform, "FlameBurstRing", 0.16f, 65);
      _heat = MageVfxUtil.CreateRing(transform, "HeatWaveRing", 0.055f, 63);
      _sparks = MageVfxUtil.CreateRadialParticles(
        transform,
        "FlameFragments",
        new Color(1f, 0.16f, 0.08f, 1f),
        new Color(1f, 0.18f, 0.04f, 1f),
        0.28f,
        0.58f,
        2.4f,
        7.2f,
        66);
    }

    void Play(float radius)
    {
      _age = 0f;
      _radius = radius;
      _warning.enabled = true;
      _main.enabled = true;
      _heat.enabled = true;

      var shape = _sparks.shape;
      shape.radius = 0.12f;
      _sparks.Clear(true);
      _sparks.Emit(Mathf.Clamp(Mathf.RoundToInt(radius * 10f), 28, 72));
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / Duration);
      UpdateWarning(t);
      UpdateMain(t);
      UpdateHeat(t);

      if (_age >= Duration)
        Release();
    }

    void UpdateWarning(float t)
    {
      var wt = Mathf.Clamp01(t / 0.34f);
      var r = Mathf.Lerp(_radius * 0.15f, _radius, wt);
      MageVfxUtil.DrawRing(_warning, r);
      var a = 0.35f * (1f - wt);
      MageVfxUtil.SetLineColor(_warning, new Color(1f, 0.08f, 0.06f, a), new Color(0.58f, 0.02f, 0.02f, a * 0.45f));
    }

    void UpdateMain(float t)
    {
      var mt = Mathf.Clamp01(t / 0.45f);
      var r = Mathf.Lerp(0.05f, _radius, 1f - Mathf.Pow(1f - mt, 2.2f));
      MageVfxUtil.DrawRing(_main, r);
      _main.startWidth = Mathf.Lerp(0.18f, 0.03f, mt);
      _main.endWidth = _main.startWidth;
      var a = 0.9f * (1f - mt);
      MageVfxUtil.SetLineColor(_main, new Color(1f, 0.1f, 0.07f, a), new Color(0.62f, 0.02f, 0.02f, a * 0.52f));
    }

    void UpdateHeat(float t)
    {
      var ht = Mathf.Clamp01((t - 0.08f) / 0.62f);
      var r = Mathf.Lerp(_radius * 0.08f, _radius * 1.08f, ht);
      MageVfxUtil.DrawRing(_heat, r);
      var a = 0.45f * (1f - ht);
      MageVfxUtil.SetLineColor(_heat, new Color(0.95f, 0.05f, 0.05f, a), new Color(0.48f, 0.01f, 0.02f, a * 0.28f));
    }

    void Release()
    {
      _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      gameObject.SetActive(false);
      Pool.Enqueue(this);
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFlameNovaHitVfx : MonoBehaviour
  {
    static readonly Queue<MageFlameNovaHitVfx> Pool = new();

    ParticleSystem _sparks;
    LineRenderer _ring;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.01f);
      fx.Play(scale);
    }

    static MageFlameNovaHitVfx Create()
    {
      var go = new GameObject("MageFlameNovaHitVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageFlameNovaHitVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "HitRing", 0.04f, 68);
      fx._sparks = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "HitSparks",
        new Color(1f, 0.22f, 0.08f, 1f),
        new Color(1f, 0.25f, 0.02f, 1f),
        0.08f,
        0.2f,
        1.5f,
        4f,
        69);
      go.SetActive(false);
      return fx;
    }

    void Play(float scale)
    {
      _age = 0f;
      _scale = Mathf.Max(0.35f, scale);
      _sparks.Clear(true);
      _sparks.Emit(8);
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.16f);
      var radius = Mathf.Lerp(0.08f, 0.46f * _scale, t);
      MageVfxUtil.DrawRing(_ring, radius);
      _ring.startWidth = Mathf.Lerp(0.055f, 0.012f, t);
      _ring.endWidth = _ring.startWidth;
      var alpha = 0.85f * (1f - t);
      MageVfxUtil.SetLineColor(_ring, new Color(1f, 0.18f, 0.08f, alpha), new Color(0.65f, 0.02f, 0.02f, alpha * 0.45f));
      if (_age >= 0.2f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageGravityWellVfx : MonoBehaviour
  {
    static readonly Queue<MageGravityWellVfx> Pool = new();
    static readonly List<MageGravityWellVfx> Active = new();
    const float ConvergeRingPeriod = 0.92f;
    const float ConvergeRingStagger = 0.32f;

    LineRenderer[] _rings;
    SpriteRenderer _core;
    SpriteRenderer[] _orbiters;
    ParticleSystem _accretion;
    float[] _ringAge;
    float _age;
    float _duration;
    float _radius;
    float _pulse;

    public static void Spawn(Vector3 position, float radius, float duration)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.02f);
      fx.Play(Mathf.Max(0.8f, radius), Mathf.Max(0.5f, duration));
      Active.Add(fx);
    }

    public static void PulseNearest(Vector3 position, float radius)
    {
      MageGravityWellVfx best = null;
      var bestDist = float.MaxValue;
      var p = new Vector2(position.x, position.y);
      foreach (var fx in Active)
      {
        if (fx == null || !fx.gameObject.activeSelf)
          continue;
        var dist = Vector2.Distance(p, fx.transform.position);
        if (dist < bestDist && dist <= Mathf.Max(1.5f, radius * 0.35f))
        {
          bestDist = dist;
          best = fx;
        }
      }
      best?.Pulse();
    }

    public static void ReleaseNearest(Vector3 position, float radius)
    {
      MageGravityWellVfx best = null;
      var bestDist = float.MaxValue;
      var p = new Vector2(position.x, position.y);
      foreach (var fx in Active)
      {
        if (fx == null || !fx.gameObject.activeSelf)
          continue;
        var dist = Vector2.Distance(p, fx.transform.position);
        if (dist < bestDist && dist <= Mathf.Max(1.5f, radius * 0.5f))
        {
          bestDist = dist;
          best = fx;
        }
      }
      best?.Release();
    }

    static MageGravityWellVfx Create()
    {
      var go = new GameObject("MageGravityWellVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageGravityWellVfx>();
      fx.Build();
      go.SetActive(false);
      return fx;
    }

    void Build()
    {
      _core = MageVfxUtil.CreateGlow(transform, "SingularityCore", new Color(0.05f, 0f, 0.12f, 0.98f), 0.95f, 70);
      _rings = new[]
      {
        MageVfxUtil.CreateRing(transform, "ConvergeRingA", 0.055f, 58),
        MageVfxUtil.CreateRing(transform, "ConvergeRingB", 0.045f, 58),
        MageVfxUtil.CreateRing(transform, "ConvergeRingC", 0.035f, 58)
      };
      _ringAge = new float[_rings.Length];
      _orbiters = new SpriteRenderer[8];
      for (var i = 0; i < _orbiters.Length; i++)
        _orbiters[i] = MageVfxUtil.CreateGlow(transform, "OrbitParticle", new Color(0.55f, 0.7f, 1f, 0.8f), 0.12f, 71);

      _accretion = MageVfxUtil.CreateRadialParticles(
        transform,
        "AccretionFragments",
        new Color(0.45f, 0.25f, 1f, 0.95f),
        new Color(0.85f, 0.95f, 1f, 0.95f),
        0.45f,
        0.8f,
        0f,
        0f,
        59);
      var main = _accretion.main;
      main.loop = true;
      main.maxParticles = 70;
      var emission = _accretion.emission;
      emission.enabled = true;
      emission.rateOverTime = 34f;
      var vel = _accretion.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
      vel.radial = new ParticleSystem.MinMaxCurve(-5.8f, -2.6f);
      var shape = _accretion.shape;
      shape.radiusThickness = 0.08f;
    }

    void Play(float radius, float duration)
    {
      _age = 0f;
      _duration = duration;
      _radius = radius;
      _pulse = 0f;
      for (var i = 0; i < _ringAge.Length; i++)
        _ringAge[i] = -i * ConvergeRingStagger;
      var shape = _accretion.shape;
      shape.radius = radius;
      _accretion.Clear(true);
      _accretion.Play(true);
    }

    void Pulse() => _pulse = 0.15f;

    void Update()
    {
      _age += Time.deltaTime;
      _pulse = Mathf.Max(0f, _pulse - Time.deltaTime);
      var life = Mathf.Clamp01(_age / _duration);
      UpdateCore(life);
      UpdateRings();
      UpdateOrbiters(life);
      if (_age >= _duration)
        Release();
    }

    void UpdateCore(float life)
    {
      var p = _pulse > 0f ? Mathf.Sin((1f - _pulse / 0.15f) * Mathf.PI) : 0f;
      var size = Mathf.Lerp(0.95f, 0.68f, p);
      _core.transform.localScale = Vector3.one * size;
      LaserVfxShared.SetSpriteColor(_core, new Color(0.04f, 0f, 0.1f, 0.98f * (1f - life * 0.25f)));
    }

    void UpdateRings()
    {
      for (var i = 0; i < _rings.Length; i++)
      {
        _ringAge[i] += Time.deltaTime;
        if (_ringAge[i] > ConvergeRingPeriod)
          _ringAge[i] -= ConvergeRingPeriod;
        var t = Mathf.Clamp01(_ringAge[i] / ConvergeRingPeriod);
        var r = Mathf.Lerp(_radius, 0.12f, t * t);
        MageVfxUtil.DrawRing(_rings[i], r);
        var a = Mathf.Sin(t * Mathf.PI) * 0.65f;
        MageVfxUtil.SetLineColor(_rings[i], new Color(0.55f, 0.25f, 1f, a), new Color(0.75f, 0.92f, 1f, a * 0.55f));
      }
    }

    void UpdateOrbiters(float life)
    {
      var time = Time.time * 2.2f;
      for (var i = 0; i < _orbiters.Length; i++)
      {
        var phase = i / (float)_orbiters.Length * Mathf.PI * 2f;
        var inward = Mathf.Repeat(life * 1.5f + i * 0.13f, 1f);
        var r = Mathf.Lerp(_radius * 0.42f, 0.22f, inward);
        var a = time + phase + inward * 2f;
        _orbiters[i].transform.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        LaserVfxShared.SetSpriteColor(_orbiters[i], new Color(0.55f, 0.78f, 1f, 0.65f * (1f - inward * 0.5f)));
      }
    }

    void Release()
    {
      _accretion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      Active.Remove(this);
      gameObject.SetActive(false);
      Pool.Enqueue(this);
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageGravityTetherVfx : MonoBehaviour
  {
    static readonly Queue<MageGravityTetherVfx> Pool = new();

    LineRenderer _line;
    float _age;

    public static void Spawn(Vector3 center, Vector3 target)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.Play(center, target);
    }

    static MageGravityTetherVfx Create()
    {
      var go = new GameObject("MageGravityTetherVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageGravityTetherVfx>();
      fx._line = go.AddComponent<LineRenderer>();
      fx._line.useWorldSpace = true;
      fx._line.positionCount = 2;
      fx._line.startWidth = 0.035f;
      fx._line.endWidth = 0.01f;
      fx._line.material = MageVfxUtil.LineMaterial;
      fx._line.sortingLayerName = LaserVfxShared.SortingLayerName;
      fx._line.sortingOrder = 61;
      go.SetActive(false);
      return fx;
    }

    void Play(Vector3 center, Vector3 target)
    {
      _age = 0f;
      _line.SetPosition(0, new Vector3(center.x, center.y, LaserVfxShared.VfxDepthZ - 0.03f));
      _line.SetPosition(1, new Vector3(target.x, target.y, LaserVfxShared.VfxDepthZ - 0.03f));
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.1f);
      var a = 0.45f * (1f - t);
      MageVfxUtil.SetLineColor(_line, new Color(0.65f, 0.88f, 1f, a), new Color(0.42f, 0.2f, 1f, a * 0.5f));
      if (_age >= 0.1f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }
}
