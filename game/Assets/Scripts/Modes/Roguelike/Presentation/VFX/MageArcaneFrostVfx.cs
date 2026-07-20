using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Laser;
using Game.Shared.Projectile;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class MageArcaneMissileProjectileVfx : MonoBehaviour
  {
    const float BodyLength = 0.62f;
    const float BodyWidth = 0.22f;

    LineRenderer _body;
    TrailRenderer _trail;
    ParticleSystem _flecks;
    Transform _bodyRoot;
    Vector3 _previousPosition;

    public static void Attach(GameObject projectile)
    {
      if (projectile == null || projectile.GetComponent<MageArcaneMissileProjectileVfx>() != null)
        return;

      projectile.AddComponent<MageArcaneMissileProjectileVfx>();
      MageArcaneMissileCastVfx.Spawn(projectile.transform.position, Mathf.Max(0.75f, projectile.transform.localScale.x * 2.4f));
    }

    public static void Detach(GameObject projectile)
    {
      if (projectile == null)
        return;

      var vfx = projectile.GetComponent<MageArcaneMissileProjectileVfx>();
      if (vfx == null)
        return;

      vfx.RestoreDefaultBody();
      Destroy(vfx);
    }

    void RestoreDefaultBody()
    {
      var renderer = GetComponent<Renderer>();
      if (renderer != null)
        renderer.enabled = true;

      if (_trail != null)
      {
        _trail.emitting = false;
        _trail.Clear();
      }

      if (_flecks != null)
      {
        _flecks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      }

      if (_bodyRoot != null)
        Destroy(_bodyRoot.gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void WirePoolLifecycle()
    {
      ActiveProjectileRegistry.Despawned -= HandleProjectileDespawned;
      ActiveProjectileRegistry.Despawned += HandleProjectileDespawned;
    }

    static void HandleProjectileDespawned(StraightProjectile projectile) =>
      Detach(projectile != null ? projectile.gameObject : null);

    void Awake()
    {
      HideDefaultBody();
      BuildBody();
      BuildTrail();
      BuildFlecks();
      _previousPosition = transform.position;
    }

    void HideDefaultBody()
    {
      var renderer = GetComponent<Renderer>();
      if (renderer != null)
        renderer.enabled = false;
    }

    void BuildBody()
    {
      var root = new GameObject("ArcaneMissileCrystal");
      root.transform.SetParent(transform, false);
      root.transform.localPosition = Vector3.zero;
      _bodyRoot = root.transform;

      _body = root.AddComponent<LineRenderer>();
      _body.useWorldSpace = false;
      _body.loop = true;
      _body.positionCount = 6;
      _body.startWidth = 0.035f;
      _body.endWidth = 0.035f;
      _body.material = MageVfxUtil.LineMaterial;
      _body.sortingLayerName = LaserVfxShared.SortingLayerName;
      _body.sortingOrder = 82;
      _body.SetPosition(0, new Vector3(BodyLength * 0.5f, 0f, 0f));
      _body.SetPosition(1, new Vector3(BodyLength * 0.18f, BodyWidth * 0.5f, 0f));
      _body.SetPosition(2, new Vector3(-BodyLength * 0.34f, BodyWidth * 0.35f, 0f));
      _body.SetPosition(3, new Vector3(-BodyLength * 0.5f, 0f, 0f));
      _body.SetPosition(4, new Vector3(-BodyLength * 0.34f, -BodyWidth * 0.35f, 0f));
      _body.SetPosition(5, new Vector3(BodyLength * 0.18f, -BodyWidth * 0.5f, 0f));
      MageVfxUtil.SetLineColor(_body, new Color(0.74f, 0.46f, 1f, 1f), new Color(0.78f, 0.96f, 1f, 0.9f));

      var glow = MageVfxUtil.CreateGlow(root.transform, "ArcaneCoreGlow", new Color(0.5f, 0.18f, 1f, 0.52f), 0.5f, 81);
      glow.transform.localScale = new Vector3(0.95f, 0.34f, 1f);
    }

    void BuildTrail()
    {
      _trail = GetComponent<TrailRenderer>();
      if (_trail == null)
        _trail = gameObject.AddComponent<TrailRenderer>();
      if (_trail == null)
        return;

      _trail.time = 0.16f;
      _trail.minVertexDistance = 0.04f;
      _trail.widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.2f),
        new Keyframe(0.35f, 0.13f),
        new Keyframe(1f, 0f));
      _trail.colorGradient = ArcaneTrailGradient();
      _trail.material = MageVfxUtil.LineMaterial;
      _trail.sortingLayerName = LaserVfxShared.SortingLayerName;
      _trail.sortingOrder = 80;
      _trail.emitting = true;
    }

    void BuildFlecks()
    {
      _flecks = MageVfxUtil.CreateRadialParticles(
        transform,
        "ArcaneFlightFlecks",
        new Color(0.72f, 0.45f, 1f, 0.88f),
        new Color(0.72f, 0.96f, 1f, 0.88f),
        0.1f,
        0.2f,
        0.2f,
        0.7f,
        79);
      var main = _flecks.main;
      main.loop = true;
      main.maxParticles = 14;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      var emission = _flecks.emission;
      emission.enabled = true;
      emission.rateOverTime = 7f;
      var shape = _flecks.shape;
      shape.radius = 0.06f;
      _flecks.Play(true);
    }

    void LateUpdate()
    {
      var delta = transform.position - _previousPosition;
      delta.z = 0f;
      if (delta.sqrMagnitude > 0.00001f && _bodyRoot != null)
      {
        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        _bodyRoot.rotation = Quaternion.Euler(0f, 0f, angle);
      }
      _previousPosition = transform.position;
    }

    static Gradient ArcaneTrailGradient()
    {
      var gradient = new Gradient();
      gradient.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.9f, 0.98f, 1f), 0f),
          new GradientColorKey(new Color(0.56f, 0.22f, 1f), 0.45f),
          new GradientColorKey(new Color(0.18f, 0.28f, 1f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0.95f, 0f),
          new GradientAlphaKey(0.55f, 0.45f),
          new GradientAlphaKey(0f, 1f)
        });
      return gradient;
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageArcaneMissileCastVfx : MonoBehaviour
  {
    static readonly Queue<MageArcaneMissileCastVfx> Pool = new();

    LineRenderer _ring;
    SpriteRenderer _flash;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.04f);
      fx.Play(scale);
    }

    static MageArcaneMissileCastVfx Create()
    {
      var go = new GameObject("MageArcaneMissileCastVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageArcaneMissileCastVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "ArcaneCastRing", 0.035f, 83);
      fx._flash = MageVfxUtil.CreateGlow(go.transform, "ArcaneCastFlash", new Color(0.7f, 0.42f, 1f, 0.65f), 0.42f, 84);
      go.SetActive(false);
      return fx;
    }

    void Play(float scale)
    {
      _age = 0f;
      _scale = Mathf.Max(0.35f, scale);
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.12f);
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.06f, 0.38f * _scale, t));
      var a = 0.82f * (1f - t);
      MageVfxUtil.SetLineColor(_ring, new Color(0.8f, 0.5f, 1f, a), new Color(0.75f, 0.96f, 1f, a * 0.8f));
      LaserVfxShared.SetSpriteColor(_flash, new Color(0.65f, 0.32f, 1f, 0.5f * (1f - t)));
      _flash.transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 0.62f, t) * _scale;
      if (_age >= 0.12f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageArcaneMissileHitVfx : MonoBehaviour
  {
    static readonly Queue<MageArcaneMissileHitVfx> Pool = new();

    LineRenderer _ring;
    LineRenderer _critH;
    LineRenderer _critV;
    ParticleSystem _shards;
    float _age;
    bool _critical;

    public static void Spawn(Vector3 position, float scale, bool critical)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.05f);
      fx.Play(critical);
    }

    static MageArcaneMissileHitVfx Create()
    {
      var go = new GameObject("MageArcaneMissileHitVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageArcaneMissileHitVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "ArcaneShatterRing", 0.035f, 84);
      fx._critH = CreateCritLine(go.transform, "CritHorizontal");
      fx._critV = CreateCritLine(go.transform, "CritVertical");
      fx._critV.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      fx._shards = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "ArcaneCrystalShards",
        new Color(0.76f, 0.42f, 1f, 1f),
        new Color(0.72f, 0.95f, 1f, 1f),
        0.1f,
        0.2f,
        2.2f,
        5.2f,
        85);
      go.SetActive(false);
      return fx;
    }

    static LineRenderer CreateCritLine(Transform parent, string name)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.positionCount = 2;
      line.startWidth = 0.04f;
      line.endWidth = 0.04f;
      line.material = MageVfxUtil.LineMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = 86;
      line.SetPosition(0, new Vector3(-0.45f, 0f, 0f));
      line.SetPosition(1, new Vector3(0.45f, 0f, 0f));
      return line;
    }

    void Play(bool critical)
    {
      _age = 0f;
      _critical = critical;
      _critH.enabled = critical;
      _critV.enabled = critical;
      _shards.Clear(true);
      _shards.Emit(critical ? 18 : 10);
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.16f);
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.05f, _critical ? 0.85f : 0.55f, t));
      var a = 0.85f * (1f - t);
      MageVfxUtil.SetLineColor(_ring, new Color(0.72f, 0.34f, 1f, a), new Color(0.75f, 0.96f, 1f, a));
      if (_critical)
      {
        var ca = 0.95f * (1f - t);
        MageVfxUtil.SetLineColor(_critH, new Color(0.95f, 0.98f, 1f, ca), new Color(0.65f, 0.3f, 1f, ca * 0.5f));
        MageVfxUtil.SetLineColor(_critV, new Color(0.95f, 0.98f, 1f, ca), new Color(0.65f, 0.3f, 1f, ca * 0.5f));
      }
      if (_age >= 0.2f)
      {
        _shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageTidalWaveVfx : MonoBehaviour
  {
    static readonly Queue<MageTidalWaveVfx> Pool = new();

    LineRenderer _main;
    LineRenderer _edge;
    LineRenderer _ripple;
    ParticleSystem _drops;
    float _age;
    float _radius;
    float _waveIndex;
    const float Duration = 0.98f;

    public static void Spawn(Vector3 position, float radius, float waveIndex)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.07f);
      fx.Play(Mathf.Max(0.75f, radius), Mathf.Max(1f, waveIndex));
    }

    static MageTidalWaveVfx Create()
    {
      var go = new GameObject("MageTidalWaveVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageTidalWaveVfx>();
      fx._main = MageVfxUtil.CreateRing(go.transform, "TidalMainRing", 0.105f, 66);
      fx._edge = MageVfxUtil.CreateRing(go.transform, "TidalEdgeRing", 0.035f, 67);
      fx._ripple = MageVfxUtil.CreateRing(go.transform, "TidalInnerRipple", 0.028f, 65);
      fx._drops = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "TidalDrops",
        new Color(0.88f, 0.98f, 1f, 0.92f),
        new Color(0.22f, 0.68f, 1f, 0.75f),
        0.16f,
        0.34f,
        1.1f,
        3.2f,
        68);
      go.SetActive(false);
      return fx;
    }

    void Play(float radius, float waveIndex)
    {
      _age = 0f;
      _radius = radius;
      _waveIndex = waveIndex;
      var shape = _drops.shape;
      shape.radius = 0.18f;
      shape.radiusThickness = 0.04f;
      _drops.Clear(true);
      _drops.Emit(Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 12, 32));
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / Duration);
      var ease = 1f - Mathf.Pow(1f - t, 2.1f);
      var radius = Mathf.Lerp(0.08f, _radius, ease);
      var alpha = (1f - t) * Mathf.Lerp(0.86f, 0.62f, Mathf.Clamp01((_waveIndex - 1f) / 4f));

      MageVfxUtil.DrawRing(_main, radius);
      MageVfxUtil.DrawRing(_edge, radius * (1.03f + Mathf.Sin(Time.time * 18f) * 0.01f));
      MageVfxUtil.DrawRing(_ripple, Mathf.Lerp(0.03f, _radius * 0.72f, Mathf.Clamp01((t - 0.12f) / 0.78f)));
      _main.startWidth = Mathf.Lerp(0.16f, 0.035f, t);
      _main.endWidth = _main.startWidth;

      MageVfxUtil.SetLineColor(_main, new Color(0.72f, 0.96f, 1f, alpha), new Color(0.12f, 0.58f, 1f, alpha * 0.55f));
      MageVfxUtil.SetLineColor(_edge, new Color(0.95f, 1f, 1f, alpha * 0.85f), new Color(0.24f, 0.78f, 1f, alpha * 0.45f));
      MageVfxUtil.SetLineColor(_ripple, new Color(0.78f, 0.96f, 1f, alpha * 0.32f), new Color(0.18f, 0.62f, 1f, alpha * 0.18f));
      if (_age >= Duration + 0.04f)
      {
        _drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFrostSlowVfx : MonoBehaviour
  {
    static readonly Queue<MageFrostSlowVfx> Pool = new();

    LineRenderer _ring;
    float _age;

    public static void Spawn(Vector3 position, float scale)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.07f);
      fx._age = 0f;
    }

    static MageFrostSlowVfx Create()
    {
      var go = new GameObject("MageFrostSlowVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageFrostSlowVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "FrostSlowRing", 0.025f, 63);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.22f);
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.22f, 0.5f, t));
      var a = 0.48f * (1f - t);
      MageVfxUtil.SetLineColor(_ring, new Color(0.55f, 0.9f, 1f, a), new Color(0.96f, 1f, 1f, a * 0.7f));
      if (_age >= 0.22f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class MageFrostShatterVfx : MonoBehaviour
  {
    static readonly Queue<MageFrostShatterVfx> Pool = new();

    LineRenderer _ring;
    ParticleSystem _shards;
    float _age;
    float _radius;

    public static void Spawn(Vector3 position, float radius)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.08f);
      fx.Play(Mathf.Max(1.5f, radius));
    }

    static MageFrostShatterVfx Create()
    {
      var go = new GameObject("MageFrostShatterVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<MageFrostShatterVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "FrostShatterRing", 0.055f, 67);
      fx._shards = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "FrostShatterCrystals",
        new Color(0.86f, 0.98f, 1f, 1f),
        new Color(0.42f, 0.78f, 1f, 0.92f),
        0.22f,
        0.36f,
        2.4f,
        6.2f,
        68);
      go.SetActive(false);
      return fx;
    }

    void Play(float radius)
    {
      _age = 0f;
      _radius = radius;
      _shards.Clear(true);
      _shards.Emit(Mathf.Clamp(Mathf.RoundToInt(radius * 8f), 18, 54));
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.34f);
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.1f, _radius, t));
      var a = 0.76f * (1f - t);
      MageVfxUtil.SetLineColor(_ring, new Color(0.82f, 0.98f, 1f, a), new Color(0.36f, 0.76f, 1f, a * 0.5f));
      if (_age >= 0.36f)
      {
        _shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }
}
