using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Laser;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class WarriorOrbitWeaponVfx : MonoBehaviour
  {
    static readonly List<WarriorOrbitWeaponVfx> Active = new();

    TrailRenderer _trail;
    SpriteRenderer _edgeGlow;
    SpriteRenderer _hotCore;
    SpriteRenderer _shadow;
    LineRenderer _orbitArc;
    LineRenderer _titanRingA;
    LineRenderer _titanRingB;
    LineRenderer _titanPulseRing;
    LineRenderer[] _surfaceLines;
    ParticleSystem _gravityWake;
    Renderer _bodyRenderer;
    Transform _owner;
    Vector3 _previousPosition;
    Vector3 _lastDelta;
    bool _wasOrbiting;
    bool _titan;
    float _boost;
    float _intro;
    float _lastSpeed;
    float _pulseTimer;
    float _rippleTimer;
    float _titanIntro;

    public static void Attach(GameObject weapon, Transform owner, int index, int count, float radius, float size, float rotationSpeed, bool titan)
    {
      if (weapon == null)
        return;

      var fx = weapon.GetComponent<WarriorOrbitWeaponVfx>() ?? weapon.AddComponent<WarriorOrbitWeaponVfx>();
      fx.Configure(owner, titan);
    }

    public static void BoostAll()
    {
      foreach (var fx in Active)
        if (fx != null && fx.isActiveAndEnabled)
          fx._boost = 0.55f;
    }

    void Awake()
    {
      _bodyRenderer = GetComponent<Renderer>();
      if (_bodyRenderer != null)
        _bodyRenderer.sharedMaterial = CreateBodyMaterial();

      _edgeGlow = MageVfxUtil.CreateGlow(transform, "WarriorOrbitEdgeGlow", new Color(1f, 0.42f, 0.05f, 0.62f), 1.45f, 78);
      _hotCore = MageVfxUtil.CreateGlow(transform, "WarriorOrbitHotCore", new Color(1f, 0.82f, 0.18f, 0.45f), 0.62f, 79);
      _shadow = MageVfxUtil.CreateGlow(transform, "TitanCoreShadow", new Color(0.02f, 0.008f, 0f, 0.32f), 1.55f, 70);

      _trail = gameObject.AddComponent<TrailRenderer>();
      _trail.time = 0.11f;
      _trail.minVertexDistance = 0.035f;
      _trail.widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.36f),
        new Keyframe(0.4f, 0.2f),
        new Keyframe(1f, 0f));
      _trail.colorGradient = TrailGradient();
      _trail.material = MageVfxUtil.LineMaterial;
      _trail.sortingLayerName = LaserVfxShared.SortingLayerName;
      _trail.sortingOrder = 77;
      _trail.emitting = true;

      _orbitArc = CreateArcLine(transform, "WarriorOrbitArc");
      _titanRingA = MageVfxUtil.CreateRing(transform, "TitanCoreRingA", 0.045f, 81);
      _titanRingB = MageVfxUtil.CreateRing(transform, "TitanCoreRingB", 0.028f, 82);
      _titanPulseRing = MageVfxUtil.CreateRing(transform, "TitanCorePulseRing", 0.035f, 73);
      _surfaceLines = new[]
      {
        CreateSurfaceLine(transform, "TitanCoreSurfaceLineA", 0f),
        CreateSurfaceLine(transform, "TitanCoreSurfaceLineB", 60f),
        CreateSurfaceLine(transform, "TitanCoreSurfaceLineC", 120f)
      };
      _gravityWake = MageVfxUtil.CreateRadialParticles(
        transform,
        "TitanGravityWake",
        new Color(1f, 0.72f, 0.18f, 0.55f),
        new Color(0.42f, 0.1f, 0.02f, 0.35f),
        0.22f,
        0.34f,
        0.1f,
        0.45f,
        72);
      var main = _gravityWake.main;
      main.loop = true;
      main.maxParticles = 18;
      var emission = _gravityWake.emission;
      emission.enabled = true;
      emission.rateOverTime = 8f;

      _previousPosition = transform.position;
      ApplyTitanVisibility(false);
    }

    void OnEnable()
    {
      if (!Active.Contains(this))
        Active.Add(this);
      _intro = 0.3f;
      _boost = Mathf.Max(_boost, 0.18f);
      _pulseTimer = 0f;
      _rippleTimer = 0f;
      _previousPosition = transform.position;
      if (_trail != null)
        _trail.Clear();
      _wasOrbiting = IsOrbiting();
    }

    void OnDisable()
    {
      Active.Remove(this);
      if (_orbitArc != null)
        _orbitArc.enabled = false;
      if (_gravityWake != null)
        _gravityWake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Configure(Transform owner, bool titan)
    {
      _owner = owner != null ? owner : ResolveOwner();
      if (titan && !_titan)
        _titanIntro = 0.5f;
      _titan = titan;
      ApplyTitanVisibility(_titan);
      _boost = Mathf.Max(_boost, 0.25f);
    }

    void LateUpdate()
    {
      _owner ??= ResolveOwner();
      var isOrbiting = IsOrbiting();
      if (!isOrbiting && _wasOrbiting && _trail != null)
        _trail.Clear();
      _wasOrbiting = isOrbiting;

      var delta = transform.position - _previousPosition;
      delta.z = 0f;
      if (_lastDelta.sqrMagnitude > 0.0001f && delta.sqrMagnitude > 0.0001f)
      {
        var reversal = Vector3.Dot(_lastDelta.normalized, delta.normalized);
        if (reversal < -0.45f && _trail != null)
          _trail.Clear();
      }
      _lastDelta = delta;
      _lastSpeed = Mathf.Lerp(_lastSpeed, delta.magnitude / Mathf.Max(0.0001f, Time.deltaTime), 0.25f);
      _previousPosition = transform.position;

      _boost = Mathf.Max(0f, _boost - Time.deltaTime);
      _intro = Mathf.Max(0f, _intro - Time.deltaTime);

      var speedGlow = Mathf.Clamp01(_lastSpeed / 16f);
      var boostGlow = _boost > 0f ? Mathf.Clamp01(_boost / 0.55f) : 0f;
      var glow = Mathf.Clamp01(0.45f + speedGlow * 0.4f + boostGlow * 0.45f);
      if (_titan)
        UpdateTitanVisual(glow, boostGlow);
      else
        UpdateOrbitVisual(glow, boostGlow);

      _titanIntro = Mathf.Max(0f, _titanIntro - Time.deltaTime);

      if (_intro > 0f && _owner != null)
      {
        var t = 1f - _intro / 0.3f;
        var pulse = Mathf.Sin(t * Mathf.PI);
        _edgeGlow.transform.localScale *= 1f + pulse * 0.35f;
      }

      UpdateOrbitArc();
    }

    void UpdateOrbitVisual(float glow, float boostGlow)
    {
      LaserVfxShared.SetSpriteColor(_edgeGlow, new Color(1f, 0.42f, 0.04f, 0.42f + glow * 0.38f));
      LaserVfxShared.SetSpriteColor(_hotCore, new Color(1f, 0.86f, 0.22f, 0.2f + glow * 0.34f));
      var scaleKick = 1f + boostGlow * 0.25f;
      _edgeGlow.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.65f, glow) * scaleKick;
      _hotCore.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 0.72f, glow);
      _trail.time = Mathf.Lerp(0.095f, 0.18f, boostGlow);
      _trail.widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.36f),
        new Keyframe(0.4f, 0.2f),
        new Keyframe(1f, 0f));
    }

    void UpdateTitanVisual(float glow, float boostGlow)
    {
      _pulseTimer += Time.deltaTime;
      _rippleTimer -= Time.deltaTime;
      var pulse = Mathf.Sin((_pulseTimer % 1f) * Mathf.PI * 2f);
      var heartbeat = Mathf.Max(0f, pulse);
      var introT = _titanIntro > 0f ? 1f - _titanIntro / 0.5f : 1f;
      var introPulse = _titanIntro > 0f ? Mathf.Sin(introT * Mathf.PI) : 0f;
      var weight = 1f + heartbeat * 0.08f + introPulse * 0.32f + boostGlow * 0.12f;

      LaserVfxShared.SetSpriteColor(_edgeGlow, new Color(1f, 0.38f, 0.03f, 0.72f + heartbeat * 0.15f));
      LaserVfxShared.SetSpriteColor(_hotCore, new Color(1f, 0.84f, 0.16f, 0.42f + heartbeat * 0.2f));
      LaserVfxShared.SetSpriteColor(_shadow, new Color(0.02f, 0.008f, 0f, 0.28f + heartbeat * 0.08f));
      _edgeGlow.transform.localScale = Vector3.one * 2.15f * weight;
      _hotCore.transform.localScale = Vector3.one * 0.95f * weight;
      _shadow.transform.localScale = new Vector3(2.45f, 2.05f, 1f) * (1f + heartbeat * 0.05f);

      _trail.time = Mathf.Lerp(0.13f, 0.19f, boostGlow);
      _trail.widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.86f),
        new Keyframe(0.55f, 0.48f),
        new Keyframe(1f, 0f));

      var ringScale = 1.28f + heartbeat * 0.08f;
      MageVfxUtil.DrawRing(_titanRingA, ringScale);
      MageVfxUtil.DrawRing(_titanRingB, ringScale * 0.72f);
      _titanRingA.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 18f);
      _titanRingB.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 13f);
      MageVfxUtil.SetLineColor(_titanRingA, new Color(1f, 0.62f, 0.08f, 0.46f), new Color(1f, 0.92f, 0.22f, 0.18f));
      MageVfxUtil.SetLineColor(_titanRingB, new Color(1f, 0.32f, 0.04f, 0.34f), new Color(1f, 0.85f, 0.2f, 0.12f));

      for (var i = 0; i < _surfaceLines.Length; i++)
      {
        var line = _surfaceLines[i];
        line.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * (10f + i * 4f) + i * 47f);
        var a = 0.26f + heartbeat * 0.16f;
        MageVfxUtil.SetLineColor(line, new Color(1f, 0.74f, 0.18f, a), new Color(1f, 0.22f, 0.02f, a * 0.4f));
      }

      if (_rippleTimer <= 0f && IsOrbiting())
      {
        _rippleTimer = 0.3f;
        WarriorTitanRippleVfx.Spawn(transform.position, transform.localScale.x);
      }

      var pt = Mathf.Repeat(_pulseTimer, 1f);
      MageVfxUtil.DrawRing(_titanPulseRing, Mathf.Lerp(0.9f, 2.2f, pt));
      var pa = Mathf.Sin(pt * Mathf.PI) * 0.22f;
      MageVfxUtil.SetLineColor(_titanPulseRing, new Color(1f, 0.58f, 0.06f, pa), new Color(1f, 0.92f, 0.2f, pa * 0.25f));
    }

    void UpdateOrbitArc()
    {
      if (_orbitArc == null || _owner == null || !IsOrbiting())
      {
        if (_orbitArc != null)
          _orbitArc.enabled = false;
        return;
      }

      var center = _owner.position;
      var toWeapon = transform.position - center;
      toWeapon.z = 0f;
      var radius = toWeapon.magnitude;
      if (radius < 0.2f)
      {
        _orbitArc.enabled = false;
        return;
      }

      _orbitArc.enabled = true;
      var current = Mathf.Atan2(toWeapon.y, toWeapon.x);
      var trailDir = Vector3.Cross(toWeapon, _lastDelta).z >= 0f ? -1f : 1f;
      var arc = Mathf.Lerp(0.45f, 0.9f, Mathf.Clamp01(_lastSpeed / 18f));
      var points = _orbitArc.positionCount;
      for (var i = 0; i < points; i++)
      {
        var t = i / (float)(points - 1);
        var a = current + trailDir * arc * t;
        _orbitArc.SetPosition(i, new Vector3(
          center.x + Mathf.Cos(a) * radius,
          center.y + Mathf.Sin(a) * radius,
          LaserVfxShared.VfxDepthZ - 0.12f));
      }
      var alpha = Mathf.Lerp(0.06f, 0.16f, Mathf.Clamp01(_lastSpeed / 20f));
      MageVfxUtil.SetLineColor(_orbitArc, new Color(1f, 0.58f, 0.08f, alpha), new Color(1f, 0.9f, 0.22f, alpha * 0.2f));
    }

    Transform ResolveOwner()
    {
      var root = transform.parent;
      return root != null ? root.parent : null;
    }

    bool IsOrbiting()
    {
      return _owner != null
        && transform.parent != null
        && transform.parent.parent == _owner;
    }

    void ApplyTitanVisibility(bool titan)
    {
      if (_shadow != null) _shadow.enabled = titan;
      if (_titanRingA != null) _titanRingA.enabled = titan;
      if (_titanRingB != null) _titanRingB.enabled = titan;
      if (_titanPulseRing != null) _titanPulseRing.enabled = titan;
      if (_surfaceLines != null)
        foreach (var line in _surfaceLines)
          if (line != null) line.enabled = titan;
      if (_gravityWake != null)
      {
        if (titan) _gravityWake.Play(true);
        else _gravityWake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      }
      if (_trail != null)
        _trail.Clear();
    }

    static LineRenderer CreateArcLine(Transform parent, string name)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.positionCount = 14;
      line.startWidth = 0.035f;
      line.endWidth = 0.012f;
      line.material = MageVfxUtil.LineMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = 75;
      return line;
    }

    static LineRenderer CreateSurfaceLine(Transform parent, string name, float angle)
    {
      var line = CreateArcLine(parent, name);
      line.useWorldSpace = false;
      line.positionCount = 2;
      line.startWidth = 0.028f;
      line.endWidth = 0.012f;
      line.sortingOrder = 83;
      line.SetPosition(0, new Vector3(-0.46f, 0f, 0f));
      line.SetPosition(1, new Vector3(0.46f, 0f, 0f));
      line.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
      return line;
    }

    static Material CreateBodyMaterial()
    {
      var shader = Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                   ?? Shader.Find("Sprites/Default");
      var mat = new Material(shader) { color = new Color(0.015f, 0.012f, 0.008f, 1f) };
      if (mat.HasProperty("_EmissionColor"))
      {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.32f, 0.03f, 0.55f));
      }
      return mat;
    }

    static Gradient TrailGradient()
    {
      var gradient = new Gradient();
      gradient.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(1f, 0.92f, 0.18f), 0f),
          new GradientColorKey(new Color(1f, 0.46f, 0.04f), 0.45f),
          new GradientColorKey(new Color(0.9f, 0.08f, 0.01f), 1f)
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
  public sealed class WarriorOrbitHitVfx : MonoBehaviour
  {
    static readonly Queue<WarriorOrbitHitVfx> Pool = new();

    SpriteRenderer _flash;
    LineRenderer _ring;
    ParticleSystem _sparks;
    float _age;
    float _scale;
    bool _heavy;

    public static void Spawn(Vector3 position, float scale, bool heavy)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.13f);
      fx.Play(scale, heavy);
    }

    static WarriorOrbitHitVfx Create()
    {
      var go = new GameObject("WarriorOrbitHitVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<WarriorOrbitHitVfx>();
      fx._flash = MageVfxUtil.CreateGlow(go.transform, "ImpactFlash", new Color(1f, 0.7f, 0.16f, 0.78f), 0.55f, 88);
      fx._ring = MageVfxUtil.CreateRing(go.transform, "ImpactRing", 0.045f, 87);
      fx._sparks = MageVfxUtil.CreateRadialParticles(
        go.transform,
        "ImpactSparks",
        new Color(1f, 0.88f, 0.24f, 1f),
        new Color(1f, 0.28f, 0.04f, 0.95f),
        0.08f,
        0.18f,
        2.6f,
        6.8f,
        89);
      go.SetActive(false);
      return fx;
    }

    void Play(float scale, bool heavy)
    {
      _age = 0f;
      _scale = Mathf.Max(0.35f, scale);
      _heavy = heavy;
      if (_flash != null)
        _flash.gameObject.SetActive(!heavy);
      _sparks.Clear(true);
      _sparks.Emit(heavy ? 34 : 10);
    }

    void Update()
    {
      _age += Time.deltaTime;
      var duration = _heavy ? 0.22f : 0.12f;
      var t = Mathf.Clamp01(_age / duration);
      var alpha = 0.86f * (1f - t);
      if (!_heavy && _flash != null)
      {
        LaserVfxShared.SetSpriteColor(_flash, new Color(1f, 0.64f, 0.1f, alpha));
        _flash.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.68f, t) * _scale;
      }
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.08f, _heavy ? 1.45f : 0.48f, t) * _scale);
      MageVfxUtil.SetLineColor(_ring, new Color(1f, 0.78f, 0.18f, alpha), new Color(1f, 0.25f, 0.03f, alpha * 0.35f));
      if (_age >= duration + 0.04f)
      {
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class WarriorTitanRippleVfx : MonoBehaviour
  {
    static readonly Queue<WarriorTitanRippleVfx> Pool = new();

    LineRenderer _ring;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.16f);
      fx._age = 0f;
      fx._scale = Mathf.Max(0.6f, scale);
    }

    static WarriorTitanRippleVfx Create()
    {
      var go = new GameObject("WarriorTitanRippleVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<WarriorTitanRippleVfx>();
      fx._ring = MageVfxUtil.CreateRing(go.transform, "TitanWakeRipple", 0.032f, 72);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.3f);
      MageVfxUtil.DrawRing(_ring, Mathf.Lerp(0.45f, 1.35f, t) * _scale);
      var a = Mathf.Sin(t * Mathf.PI) * 0.18f;
      MageVfxUtil.SetLineColor(_ring, new Color(1f, 0.55f, 0.07f, a), new Color(1f, 0.9f, 0.2f, a * 0.25f));
      if (_age >= 0.3f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  [DisallowMultipleComponent]
  public sealed class WarriorOrbitResonanceVfx : MonoBehaviour
  {
    static readonly Queue<WarriorOrbitResonanceVfx> Pool = new();

    LineRenderer[] _lines;
    float _age;
    float _radius;

    public static void Spawn(Vector3 position, float radius)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.14f);
      fx.Play(Mathf.Max(1f, radius));
    }

    static WarriorOrbitResonanceVfx Create()
    {
      var go = new GameObject("WarriorOrbitResonanceVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<WarriorOrbitResonanceVfx>();
      fx._lines = new LineRenderer[3];
      for (var i = 0; i < fx._lines.Length; i++)
        fx._lines[i] = CreateLine(go.transform, $"ResonanceLine{i}");
      go.SetActive(false);
      return fx;
    }

    static LineRenderer CreateLine(Transform parent, string name)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.positionCount = 2;
      line.startWidth = 0.022f;
      line.endWidth = 0.012f;
      line.material = MageVfxUtil.LineMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = 74;
      return line;
    }

    void Play(float radius)
    {
      _age = 0f;
      _radius = radius;
      var baseAngle = Random.value * Mathf.PI * 2f;
      for (var i = 0; i < _lines.Length; i++)
      {
        var a = baseAngle + i * 2.1f;
        var b = a + Random.Range(0.55f, 1.1f);
        _lines[i].SetPosition(0, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        _lines[i].SetPosition(1, new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, 0f));
      }
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.06f);
      var a = 0.34f * (1f - t);
      foreach (var line in _lines)
        MageVfxUtil.SetLineColor(line, new Color(1f, 0.78f, 0.18f, a), new Color(1f, 0.95f, 0.38f, a * 0.7f));
      if (_age >= 0.065f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }
}
