using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Laser;

namespace Game.Shared.Vfx
{
  static class RangedVfxUtil
  {
    static Material s_lineMaterial;
    static Material s_blueWhiteLineMaterial;
    static Material s_blueWhiteParticleMaterial;

    public static Material LineMaterial => s_lineMaterial ??= LaserVfxShared.CreateBeamMaterialInstance();

    public static Material BlueWhiteLineMaterial => s_blueWhiteLineMaterial ??= CreateBlueWhiteLineMaterial();

    public static Material BlueWhiteParticleMaterial => s_blueWhiteParticleMaterial ??= CreateBlueWhiteParticleMaterial();

    static Material CreateBlueWhiteLineMaterial()
    {
      var shader = Shader.Find("Sprites/Default")
                   ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Universal Render Pipeline/Unlit");
      var material = new Material(shader) { name = "RangedBlueWhiteLine_Runtime" };
      if (material.HasProperty("_Color"))
        material.SetColor("_Color", Color.white);
      if (material.HasProperty("_BaseColor"))
        material.SetColor("_BaseColor", Color.white);
      return material;
    }

    static Material CreateBlueWhiteParticleMaterial()
    {
      var shader = Shader.Find("Sprites/Default")
                   ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Universal Render Pipeline/Unlit");
      var material = new Material(shader) { name = "RangedBlueWhiteParticle_Runtime" };
      if (material.HasProperty("_MainTex"))
        material.SetTexture("_MainTex", LaserVfxShared.SquareParticleTexture);
      if (material.HasProperty("_BaseMap"))
        material.SetTexture("_BaseMap", LaserVfxShared.SquareParticleTexture);
      if (material.HasProperty("_Color"))
        material.SetColor("_Color", Color.white);
      if (material.HasProperty("_BaseColor"))
        material.SetColor("_BaseColor", Color.white);
      if (material.HasProperty("_EmissionColor"))
        material.SetColor("_EmissionColor", Color.white);
      return material;
    }

    public static void ApplyBlueWhiteParticleRenderer(ParticleSystem ps, int sortingOrder)
    {
      if (ps == null)
        return;

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      if (renderer == null)
        return;

      renderer.renderMode = ParticleSystemRenderMode.Billboard;
      renderer.sortingLayerName = LaserVfxShared.SortingLayerName;
      renderer.sortingOrder = sortingOrder;
      renderer.material = BlueWhiteParticleMaterial;
    }

    public static LineRenderer CreateLine(Transform parent, string name, int sortOrder, float width, bool worldSpace = false)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = worldSpace;
      line.positionCount = 2;
      line.startWidth = width;
      line.endWidth = width;
      line.material = LineMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = sortOrder;
      return line;
    }

    public static void DrawRing(LineRenderer line, float radius, int segments = 48)
    {
      if (line == null)
        return;

      line.useWorldSpace = false;
      line.loop = true;
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var a = i / (float)segments * Mathf.PI * 2f;
        line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
      }
    }

    public static void SetLineColor(LineRenderer line, Color start, Color end)
    {
      if (line == null)
        return;

      line.startColor = start;
      line.endColor = end;
    }

    public static ParticleSystem CreateBurstParticles(
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
      main.duration = 0.2f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
      main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
      main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 64;
      main.startColor = new ParticleSystem.MinMaxGradient(a, b);

      var emission = ps.emission;
      emission.enabled = false;

      var shape = ps.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.06f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.radial = new ParticleSystem.MinMaxCurve(speedMin, speedMax);

      var size = ps.sizeOverLifetime;
      size.enabled = true;
      size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.6f, 0.75f),
        new Keyframe(1f, 0.05f)));

      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), sortOrder);
      return ps;
    }

    public static Gradient BuildGradient(Color head, Color mid, Color tail)
    {
      var gradient = new Gradient();
      gradient.SetKeys(
        new[]
        {
          new GradientColorKey(head, 0f),
          new GradientColorKey(mid, 0.4f),
          new GradientColorKey(tail, 1f)
        },
        new[]
        {
          new GradientAlphaKey(head.a, 0f),
          new GradientAlphaKey(mid.a, 0.45f),
          new GradientAlphaKey(0f, 1f)
        });
      return gradient;
    }
  }

  public enum RangedProjectileVisualKind
  {
    Primary,
    Pierce,
    Explosive,
    Lightning,
    Heavy,
    Split
  }

  [DisallowMultipleComponent]
  public sealed class RangedProjectileVfx : MonoBehaviour
  {
    const float VisibilityBoost = 1.32f;

    public static event System.Action<Vector3> Spawned;

    Transform _bodyRoot;
    LineRenderer _body;
    LineRenderer _accent;
    SpriteRenderer _glow;
    TrailRenderer _trail;
    ParticleSystem _flecks;
    Vector3 _previousPosition;
    float _fleckTimer;
    float _piercePulse;
    RangedProjectileVisualKind _kind = RangedProjectileVisualKind.Primary;

    public static void Attach(GameObject projectile, RangedProjectileVisualKind kind = RangedProjectileVisualKind.Primary)
    {
      if (projectile == null)
        return;

      var vfx = projectile.GetComponent<RangedProjectileVfx>();
      if (vfx == null)
      {
        Spawned?.Invoke(projectile.transform.position);
        vfx = projectile.AddComponent<RangedProjectileVfx>();
        RangedMuzzleFlashVfx.Spawn(projectile.transform.position, Mathf.Max(0.55f, projectile.transform.localScale.x * 1.9f));
      }

      vfx.ApplyVisualKind(kind);
    }

    void Awake()
    {
      HideDefaultBody();
      BuildBody();
      BuildTrail();
      BuildFlecks();
      _previousPosition = transform.position;
      ApplyVisualKind(_kind);
    }

    public void ResetForReuse()
    {
      _previousPosition = transform.position;
      _fleckTimer = 0f;
      _piercePulse = 0f;
      if (_trail != null)
      {
        _trail.Clear();
        _trail.emitting = true;
      }

      if (_flecks != null)
      {
        _flecks.Clear(true);
        _flecks.Play(true);
      }

      if (_bodyRoot != null)
        _bodyRoot.localScale = Vector3.one;
    }

    public void ApplyVisualKind(RangedProjectileVisualKind kind)
    {
      _kind = kind;
      if (_body == null || _glow == null || _trail == null || _flecks == null)
        return;

      var spec = GetVisualSpec(kind);
      ApplyBodyShape(spec);
      ApplyTrail(spec);
      ApplyFlecks(spec);
    }

    public void PulsePierce()
    {
      _piercePulse = 0.14f;
    }

    void HideDefaultBody()
    {
      var renderer = GetComponent<Renderer>();
      if (renderer != null)
        renderer.enabled = false;
    }

    void BuildBody()
    {
      var root = new GameObject("RangedProjectileBody");
      root.transform.SetParent(transform, false);
      _bodyRoot = root.transform;

      _body = root.AddComponent<LineRenderer>();
      _body.useWorldSpace = false;
      _body.loop = false;
      _body.material = RangedVfxUtil.LineMaterial;
      _body.sortingLayerName = LaserVfxShared.SortingLayerName;
      _body.sortingOrder = 82;

      var accentGo = new GameObject("RangedProjectileAccent");
      accentGo.transform.SetParent(root.transform, false);
      _accent = accentGo.AddComponent<LineRenderer>();
      _accent.useWorldSpace = false;
      _accent.loop = false;
      _accent.material = RangedVfxUtil.BlueWhiteLineMaterial;
      _accent.sortingLayerName = LaserVfxShared.SortingLayerName;
      _accent.sortingOrder = 83;

      var glowGo = new GameObject("RangedProjectileGlow");
      glowGo.transform.SetParent(root.transform, false);
      _glow = glowGo.AddComponent<SpriteRenderer>();
      _glow.sprite = LaserVfxShared.SoftGlowSprite;
      _glow.material = LaserVfxShared.CreateBeamMaterialInstance();
      _glow.sortingLayerName = LaserVfxShared.SortingLayerName;
      _glow.sortingOrder = 81;
    }

    void BuildTrail()
    {
      var scale = Mathf.Max(0.12f, transform.localScale.x);
      _trail = gameObject.AddComponent<TrailRenderer>();
      _trail.time = 0.16f;
      _trail.minVertexDistance = 0.03f;
      _trail.widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.28f * scale * VisibilityBoost),
        new Keyframe(0.45f, 0.14f * scale * VisibilityBoost),
        new Keyframe(1f, 0f));
      _trail.material = RangedVfxUtil.LineMaterial;
      _trail.sortingLayerName = LaserVfxShared.SortingLayerName;
      _trail.sortingOrder = 80;
      _trail.emitting = true;
    }

    void BuildFlecks()
    {
      _flecks = RangedVfxUtil.CreateBurstParticles(
        transform,
        "RangedFlightFlecks",
        new Color(1f, 0.95f, 0.28f, 0.85f),
        new Color(1f, 0.48f, 0.06f, 0.75f),
        0.08f,
        0.13f,
        0.15f,
        0.45f,
        79);
      var main = _flecks.main;
      main.loop = true;
      main.maxParticles = 12;
      var emission = _flecks.emission;
      emission.enabled = true;
      emission.rateOverTime = 5f;
      _flecks.Play(true);
    }

    void ApplyBodyShape(in VisualSpec spec)
    {
      _body.positionCount = spec.bodyPoints.Length;
      for (var i = 0; i < spec.bodyPoints.Length; i++)
        _body.SetPosition(i, spec.bodyPoints[i]);
      _body.loop = spec.loopBody;
      _body.startWidth = spec.bodyWidth * VisibilityBoost;
      _body.endWidth = spec.bodyWidth * 0.82f * VisibilityBoost;
      RangedVfxUtil.SetLineColor(_body, spec.bodyHead, spec.bodyTail);

      if (spec.accentPoints != null && spec.accentPoints.Length > 1)
      {
        _accent.enabled = true;
        _accent.positionCount = spec.accentPoints.Length;
        for (var i = 0; i < spec.accentPoints.Length; i++)
          _accent.SetPosition(i, spec.accentPoints[i]);
        _accent.loop = spec.loopAccent;
        _accent.startWidth = spec.accentWidth * VisibilityBoost;
        _accent.endWidth = spec.accentWidth * 0.75f * VisibilityBoost;
        RangedVfxUtil.SetLineColor(_accent, spec.accentHead, spec.accentTail);
      }
      else
      {
        _accent.enabled = false;
      }

      _glow.transform.localScale = spec.glowScale * VisibilityBoost;
      LaserVfxShared.SetSpriteColor(_glow, spec.glowColor);
    }

    void ApplyTrail(in VisualSpec spec)
    {
      _trail.time = spec.trailTime;
      _trail.colorGradient = RangedVfxUtil.BuildGradient(spec.trailHead, spec.trailMid, spec.trailTail);
    }

    void ApplyFlecks(in VisualSpec spec)
    {
      var main = _flecks.main;
      main.startColor = new ParticleSystem.MinMaxGradient(spec.fleckA, spec.fleckB);
      var emission = _flecks.emission;
      emission.rateOverTime = spec.fleckRate;
    }

    void LateUpdate()
    {
      var delta = transform.position - _previousPosition;
      delta.z = 0f;
      var speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
      if (delta.sqrMagnitude > 0.00001f && _bodyRoot != null)
      {
        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        _bodyRoot.rotation = Quaternion.Euler(0f, 0f, angle);
      }

      if (_flecks != null)
      {
        _fleckTimer += Time.deltaTime;
        if (_fleckTimer >= 0.12f)
        {
          _fleckTimer = 0f;
          var spec = GetVisualSpec(_kind);
          var rate = speed > 22f ? spec.fleckRate * 0.45f : speed > 16f ? spec.fleckRate * 0.75f : spec.fleckRate;
          var emission = _flecks.emission;
          emission.rateOverTime = rate;
        }
      }

      if (_piercePulse > 0f)
      {
        _piercePulse -= Time.deltaTime;
        var t = Mathf.Clamp01(_piercePulse / 0.14f);
        var boost = 1f + t * 0.18f;
        if (_bodyRoot != null)
          _bodyRoot.localScale = new Vector3(boost, boost, 1f);
        if (_trail != null)
          _trail.time = GetVisualSpec(_kind).trailTime + t * 0.05f;
      }
      else if (_bodyRoot != null && _bodyRoot.localScale != Vector3.one)
      {
        _bodyRoot.localScale = Vector3.one;
      }

      _previousPosition = transform.position;
    }

    readonly struct VisualSpec
    {
      public readonly Vector3[] bodyPoints;
      public readonly Vector3[] accentPoints;
      public readonly bool loopBody;
      public readonly bool loopAccent;
      public readonly float bodyWidth;
      public readonly float accentWidth;
      public readonly Color bodyHead;
      public readonly Color bodyTail;
      public readonly Color accentHead;
      public readonly Color accentTail;
      public readonly Color glowColor;
      public readonly Vector3 glowScale;
      public readonly Color trailHead;
      public readonly Color trailMid;
      public readonly Color trailTail;
      public readonly float trailTime;
      public readonly Color fleckA;
      public readonly Color fleckB;
      public readonly float fleckRate;

      public VisualSpec(
        Vector3[] bodyPoints,
        Vector3[] accentPoints,
        bool loopBody,
        bool loopAccent,
        float bodyWidth,
        float accentWidth,
        Color bodyHead,
        Color bodyTail,
        Color accentHead,
        Color accentTail,
        Color glowColor,
        Vector3 glowScale,
        Color trailHead,
        Color trailMid,
        Color trailTail,
        float trailTime,
        Color fleckA,
        Color fleckB,
        float fleckRate)
      {
        this.bodyPoints = bodyPoints;
        this.accentPoints = accentPoints;
        this.loopBody = loopBody;
        this.loopAccent = loopAccent;
        this.bodyWidth = bodyWidth;
        this.accentWidth = accentWidth;
        this.bodyHead = bodyHead;
        this.bodyTail = bodyTail;
        this.accentHead = accentHead;
        this.accentTail = accentTail;
        this.glowColor = glowColor;
        this.glowScale = glowScale;
        this.trailHead = trailHead;
        this.trailMid = trailMid;
        this.trailTail = trailTail;
        this.trailTime = trailTime;
        this.fleckA = fleckA;
        this.fleckB = fleckB;
        this.fleckRate = fleckRate;
      }
    }

    static VisualSpec GetVisualSpec(RangedProjectileVisualKind kind) => kind switch
    {
      RangedProjectileVisualKind.Pierce => BuildPierceSpec(),
      RangedProjectileVisualKind.Explosive => BuildExplosiveSpec(),
      RangedProjectileVisualKind.Lightning => BuildLightningSpec(),
      RangedProjectileVisualKind.Heavy => BuildHeavySpec(),
      RangedProjectileVisualKind.Split => BuildSplitSpec(),
      _ => BuildPrimarySpec()
    };

    static VisualSpec BuildPrimarySpec()
    {
      return new VisualSpec(
        new[]
        {
          new Vector3(0.44f, 0f, 0f),
          new Vector3(0.08f, 0.11f, 0f),
          new Vector3(-0.28f, 0f, 0f),
          new Vector3(0.08f, -0.11f, 0f)
        },
        new[]
        {
          new Vector3(0.18f, 0f, 0f),
          new Vector3(-0.12f, 0f, 0f)
        },
        true,
        false,
        0.072f,
        0.028f,
        new Color(1f, 1f, 0.98f, 1f),
        new Color(0.42f, 0.92f, 1f, 0.95f),
        new Color(1f, 1f, 1f, 0.95f),
        new Color(0.55f, 0.92f, 1f, 0.55f),
        new Color(0.72f, 0.96f, 1f, 0.72f),
        new Vector3(0.95f, 0.42f, 1f),
        new Color(1f, 1f, 0.92f, 1f),
        new Color(0.55f, 0.92f, 1f, 0.78f),
        new Color(0.12f, 0.48f, 1f, 0f),
        0.14f,
        new Color(0.92f, 0.98f, 1f, 0.9f),
        new Color(0.35f, 0.78f, 1f, 0.75f),
        4.5f);
    }

    static VisualSpec BuildPierceSpec()
    {
      return new VisualSpec(
        new[]
        {
          new Vector3(0.52f, 0f, 0f),
          new Vector3(-0.34f, 0f, 0f)
        },
        new[]
        {
          new Vector3(0.52f, 0f, 0f),
          new Vector3(0.18f, 0.05f, 0f),
          new Vector3(-0.34f, 0f, 0f),
          new Vector3(0.18f, -0.05f, 0f)
        },
        false,
        true,
        0.048f,
        0.022f,
        new Color(1f, 1f, 1f, 1f),
        new Color(0.62f, 0.88f, 1f, 0.88f),
        new Color(0.92f, 0.98f, 1f, 0.95f),
        new Color(0.38f, 0.72f, 1f, 0.45f),
        new Color(0.78f, 0.94f, 1f, 0.58f),
        new Vector3(1.05f, 0.24f, 1f),
        new Color(1f, 1f, 1f, 0.95f),
        new Color(0.62f, 0.9f, 1f, 0.72f),
        new Color(0.18f, 0.55f, 1f, 0f),
        0.18f,
        new Color(0.95f, 0.98f, 1f, 0.85f),
        new Color(0.42f, 0.78f, 1f, 0.65f),
        3.2f);
    }

    static VisualSpec BuildExplosiveSpec()
    {
      var ring = BuildRingPoints(0.24f, 10);
      return new VisualSpec(
        ring,
        new[]
        {
          new Vector3(0.16f, 0f, 0f),
          new Vector3(-0.16f, 0f, 0f),
          new Vector3(0f, 0.16f, 0f),
          new Vector3(0f, -0.16f, 0f)
        },
        true,
        false,
        0.062f,
        0.034f,
        new Color(1f, 0.92f, 0.42f, 1f),
        new Color(1f, 0.42f, 0.08f, 0.95f),
        new Color(1f, 0.78f, 0.18f, 0.95f),
        new Color(1f, 0.28f, 0.04f, 0.55f),
        new Color(1f, 0.58f, 0.12f, 0.78f),
        new Vector3(0.82f, 0.82f, 1f),
        new Color(1f, 0.88f, 0.35f, 1f),
        new Color(1f, 0.52f, 0.08f, 0.78f),
        new Color(1f, 0.12f, 0.02f, 0f),
        0.12f,
        new Color(1f, 0.82f, 0.22f, 0.9f),
        new Color(1f, 0.35f, 0.05f, 0.75f),
        6.5f);
    }

    static VisualSpec BuildLightningSpec()
    {
      return new VisualSpec(
        new[]
        {
          new Vector3(0.36f, 0.08f, 0f),
          new Vector3(0.12f, -0.08f, 0f),
          new Vector3(-0.04f, 0.1f, 0f),
          new Vector3(-0.2f, -0.1f, 0f),
          new Vector3(-0.36f, 0.04f, 0f)
        },
        new[]
        {
          new Vector3(0.36f, 0.08f, 0f),
          new Vector3(-0.36f, 0.04f, 0f)
        },
        false,
        false,
        0.058f,
        0.024f,
        new Color(0.98f, 1f, 1f, 1f),
        new Color(0.42f, 0.86f, 1f, 0.92f),
        new Color(1f, 1f, 1f, 0.92f),
        new Color(0.48f, 0.86f, 1f, 0.45f),
        new Color(0.62f, 0.92f, 1f, 0.82f),
        new Vector3(0.88f, 0.48f, 1f),
        new Color(0.98f, 1f, 1f, 1f),
        new Color(0.52f, 0.88f, 1f, 0.78f),
        new Color(0.18f, 0.62f, 1f, 0f),
        0.11f,
        new Color(0.95f, 1f, 1f, 0.92f),
        new Color(0.45f, 0.82f, 1f, 0.72f),
        7.5f);
    }

    static VisualSpec BuildHeavySpec()
    {
      return new VisualSpec(
        new[]
        {
          new Vector3(0f, 0.22f, 0f),
          new Vector3(0.28f, 0f, 0f),
          new Vector3(0f, -0.22f, 0f),
          new Vector3(-0.28f, 0f, 0f)
        },
        null,
        true,
        false,
        0.078f,
        0f,
        new Color(1f, 0.88f, 0.38f, 1f),
        new Color(1f, 0.52f, 0.08f, 0.95f),
        Color.clear,
        Color.clear,
        new Color(1f, 0.68f, 0.12f, 0.74f),
        new Vector3(0.92f, 0.92f, 1f),
        new Color(1f, 0.92f, 0.45f, 1f),
        new Color(1f, 0.58f, 0.08f, 0.78f),
        new Color(1f, 0.18f, 0.02f, 0f),
        0.1f,
        new Color(1f, 0.82f, 0.28f, 0.88f),
        new Color(1f, 0.42f, 0.05f, 0.72f),
        5.5f);
    }

    static VisualSpec BuildSplitSpec()
    {
      return new VisualSpec(
        new[]
        {
          new Vector3(0.24f, 0f, 0f),
          new Vector3(-0.12f, 0.08f, 0f),
          new Vector3(-0.12f, -0.08f, 0f)
        },
        null,
        true,
        false,
        0.05f,
        0f,
        new Color(1f, 0.96f, 0.62f, 0.92f),
        new Color(1f, 0.72f, 0.18f, 0.78f),
        Color.clear,
        Color.clear,
        new Color(1f, 0.82f, 0.28f, 0.48f),
        new Vector3(0.62f, 0.34f, 1f),
        new Color(1f, 0.92f, 0.55f, 0.85f),
        new Color(1f, 0.68f, 0.12f, 0.55f),
        new Color(1f, 0.28f, 0.04f, 0f),
        0.08f,
        new Color(1f, 0.88f, 0.35f, 0.75f),
        new Color(1f, 0.48f, 0.08f, 0.55f),
        3.5f);
    }

    static Vector3[] BuildRingPoints(float radius, int segments)
    {
      var points = new Vector3[segments];
      for (var i = 0; i < segments; i++)
      {
        var angle = i / (float)segments * Mathf.PI * 2f;
        points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
      }
      return points;
    }
  }

  public sealed class RangedMuzzleFlashVfx : MonoBehaviour
  {
    static readonly Queue<RangedMuzzleFlashVfx> Pool = new();

    LineRenderer _ring;
    SpriteRenderer _flash;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.03f);
      fx._scale = scale;
      fx._age = 0f;
    }

    static RangedMuzzleFlashVfx Create()
    {
      var go = new GameObject("RangedMuzzleFlashVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedMuzzleFlashVfx>();
      fx._ring = RangedVfxUtil.CreateLine(go.transform, "MuzzleRing", 85, 0.035f);
      fx._flash = CreateGlow(go.transform, "MuzzleFlash", new Color(1f, 0.88f, 0.24f, 0.58f), 84);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.07f);
      RangedVfxUtil.DrawRing(_ring, Mathf.Lerp(0.06f, 0.35f * _scale, t), 36);
      RangedVfxUtil.SetLineColor(_ring, new Color(1f, 0.96f, 0.55f, 0.9f * (1f - t)), new Color(1f, 0.42f, 0.08f, 0.55f * (1f - t)));
      _flash.color = new Color(1f, 0.82f, 0.18f, 0.45f * (1f - t));
      _flash.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.55f, t) * _scale;
      if (_age >= 0.08f)
        Recycle();
    }

    void Recycle()
    {
      gameObject.SetActive(false);
      Pool.Enqueue(this);
    }

    public static SpriteRenderer CreateGlow(Transform parent, string name, Color color, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = LaserVfxShared.SoftGlowSprite;
      sr.material = LaserVfxShared.CreateBeamMaterialInstance();
      LaserVfxShared.SetSpriteColor(sr, color);
      sr.sortingLayerName = LaserVfxShared.SortingLayerName;
      sr.sortingOrder = sortOrder;
      return sr;
    }
  }

  public sealed class RangedProjectileHitVfx : MonoBehaviour
  {
    static readonly Queue<RangedProjectileHitVfx> Pool = new();

    LineRenderer _ring;
    SpriteRenderer _flash;
    ParticleSystem _sparks;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale = 1f)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.04f);
      fx._scale = Mathf.Max(0.55f, scale);
      fx._age = 0f;
      fx._sparks.Clear(true);
      fx._sparks.Emit(8);
    }

    static RangedProjectileHitVfx Create()
    {
      var go = new GameObject("RangedProjectileHitVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedProjectileHitVfx>();
      fx._ring = RangedVfxUtil.CreateLine(go.transform, "HitRing", 88, 0.04f);
      fx._flash = RangedMuzzleFlashVfx.CreateGlow(go.transform, "HitFlash", Color.white, 87);
      fx._sparks = RangedVfxUtil.CreateBurstParticles(
        go.transform,
        "HitSparks",
        Color.white,
        new Color(1f, 0.82f, 0.18f, 1f),
        0.04f,
        0.1f,
        2.2f,
        5.4f,
        89);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.1f);
      RangedVfxUtil.DrawRing(_ring, Mathf.Lerp(0.04f, 0.42f * _scale, t), 32);
      RangedVfxUtil.SetLineColor(_ring, new Color(1f, 1f, 1f, 0.92f * (1f - t)), new Color(1f, 0.62f, 0.1f, 0.45f * (1f - t)));
      _flash.color = new Color(1f, 1f, 1f, 0.55f * (1f - t));
      _flash.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.5f, t) * _scale;
      if (_age >= 0.12f)
      {
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedChainLightningVfx : MonoBehaviour
  {
    static readonly Queue<RangedChainLightningVfx> Pool = new();

    public static event System.Action<Vector3> Spawned;

    LineRenderer _line;
    ParticleSystem _sparks;
    float _age;

    public static void Spawn(Vector3 from, Vector3 to, bool finalJump = false)
    {
      Spawned?.Invoke(from);
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx._age = 0f;
      fx.DrawJagged(from, to, finalJump);
      fx._sparks.transform.position = Vector3.Lerp(from, to, 0.5f);
      fx._sparks.Clear(true);
      fx._sparks.Emit(finalJump ? 14 : 8);
    }

    static RangedChainLightningVfx Create()
    {
      var go = new GameObject("RangedChainLightningVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedChainLightningVfx>();
      fx._line = RangedVfxUtil.CreateLine(go.transform, "ChainArc", 93, 0.055f, true);
      fx._line.material = RangedVfxUtil.BlueWhiteLineMaterial;
      fx._line.positionCount = 8;
      fx._sparks = RangedVfxUtil.CreateBurstParticles(
        go.transform,
        "ChainSparks",
        new Color(1f, 1f, 1f, 1f),
        new Color(0.62f, 0.9f, 1f, 1f),
        0.05f,
        0.13f,
        0.7f,
        2.4f,
        94);
      RangedVfxUtil.ApplyBlueWhiteParticleRenderer(fx._sparks, 94);
      go.SetActive(false);
      return fx;
    }

    void DrawJagged(Vector3 from, Vector3 to, bool finalJump)
    {
      from.z = LaserVfxShared.VfxDepthZ - 0.08f;
      to.z = LaserVfxShared.VfxDepthZ - 0.08f;
      var delta = to - from;
      var flat = new Vector2(delta.x, delta.y);
      var perp = flat.sqrMagnitude > 0.0001f ? new Vector3(-flat.y, flat.x, 0f).normalized : Vector3.up;
      var amp = Mathf.Clamp(flat.magnitude * 0.08f, 0.05f, 0.22f);
      for (var i = 0; i < _line.positionCount; i++)
      {
        var t = i / (float)(_line.positionCount - 1);
        var p = Vector3.Lerp(from, to, t);
        if (i > 0 && i < _line.positionCount - 1)
          p += perp * Random.Range(-amp, amp);
        _line.SetPosition(i, p);
      }
      var a = finalJump ? 1f : 0.85f;
      RangedVfxUtil.SetLineColor(_line, new Color(0.98f, 1f, 1f, a), new Color(0.46f, 0.86f, 1f, a));
      _line.startWidth = finalJump ? 0.07f : 0.055f;
      _line.endWidth = finalJump ? 0.05f : 0.035f;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.14f);
      var a = 1f - t;
      RangedVfxUtil.SetLineColor(_line, new Color(0.98f, 1f, 1f, a), new Color(0.46f, 0.86f, 1f, a * 0.85f));
      if (_age >= 0.16f)
      {
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedElectricBurstVfx : MonoBehaviour
  {
    static readonly Queue<RangedElectricBurstVfx> Pool = new();

    LineRenderer _h;
    LineRenderer _v;
    SpriteRenderer _glow;
    float _age;
    bool _final;

    public static void Spawn(Vector3 position, bool finalTarget = false)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.09f);
      fx._age = 0f;
      fx._final = finalTarget;
    }

    static RangedElectricBurstVfx Create()
    {
      var go = new GameObject("RangedElectricBurstVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedElectricBurstVfx>();
      fx._h = RangedVfxUtil.CreateLine(go.transform, "ElectricFlashH", 95, 0.04f);
      fx._v = RangedVfxUtil.CreateLine(go.transform, "ElectricFlashV", 95, 0.04f);
      fx._h.material = RangedVfxUtil.BlueWhiteLineMaterial;
      fx._v.material = RangedVfxUtil.BlueWhiteLineMaterial;
      fx._h.SetPosition(0, new Vector3(-0.35f, 0f, 0f));
      fx._h.SetPosition(1, new Vector3(0.35f, 0f, 0f));
      fx._v.SetPosition(0, new Vector3(0f, -0.35f, 0f));
      fx._v.SetPosition(1, new Vector3(0f, 0.35f, 0f));
      fx._glow = RangedMuzzleFlashVfx.CreateGlow(go.transform, "ElectricGlow", new Color(0.58f, 0.9f, 1f, 0.45f), 94);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var duration = _final ? 0.2f : 0.14f;
      var t = Mathf.Clamp01(_age / duration);
      var a = 1f - t;
      var size = Mathf.Lerp(0.55f, _final ? 1.25f : 0.85f, t);
      _h.transform.localScale = Vector3.one * size;
      _v.transform.localScale = Vector3.one * size;
      _glow.transform.localScale = Vector3.one * size;
      RangedVfxUtil.SetLineColor(_h, new Color(0.98f, 1f, 1f, a), new Color(0.52f, 0.88f, 1f, a * 0.7f));
      RangedVfxUtil.SetLineColor(_v, new Color(0.98f, 1f, 1f, a), new Color(0.52f, 0.88f, 1f, a * 0.7f));
      _glow.color = new Color(0.58f, 0.9f, 1f, 0.42f * a);
      if (_age >= duration)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedExplosionVfx : MonoBehaviour
  {
    static readonly Queue<RangedExplosionVfx> Pool = new();

    public static event System.Action<Vector3> Spawned;

    LineRenderer _mainRing;
    LineRenderer _shockRing;
    LineRenderer _flashRing;
    SpriteRenderer _coreGlow;
    ParticleSystem _fragments;
    float _age;
    float _radius;

    public static void Spawn(Vector3 position, float radius)
    {
      Spawned?.Invoke(position);
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.07f);
      fx._radius = Mathf.Max(0.35f, radius);
      fx._age = 0f;
      fx._fragments.Clear(true);
      fx._fragments.Emit(Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 18, 42));
      if (fx._coreGlow != null)
      {
        fx._coreGlow.color = new Color(1f, 0.72f, 0.18f, 0.95f);
        fx._coreGlow.transform.localScale = Vector3.one * Mathf.Clamp(radius * 0.22f, 0.55f, 1.35f);
      }
    }

    static RangedExplosionVfx Create()
    {
      var go = new GameObject("RangedExplosionVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedExplosionVfx>();
      fx._mainRing = RangedVfxUtil.CreateLine(go.transform, "ExplosionRing", 91, 0.12f);
      fx._shockRing = RangedVfxUtil.CreateLine(go.transform, "ExplosionShockRing", 90, 0.06f);
      fx._flashRing = RangedVfxUtil.CreateLine(go.transform, "ExplosionFlashRing", 89, 0.045f);
      fx._coreGlow = RangedMuzzleFlashVfx.CreateGlow(
        go.transform,
        "ExplosionCoreGlow",
        new Color(1f, 0.78f, 0.22f, 0.92f),
        88);
      fx._fragments = RangedVfxUtil.CreateBurstParticles(
        go.transform,
        "ExplosionFragments",
        new Color(1f, 0.92f, 0.28f, 1f),
        new Color(1f, 0.22f, 0.04f, 1f),
        0.11f,
        0.24f,
        4.2f,
        9.5f,
        92);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      const float duration = 0.28f;
      var t = Mathf.Clamp01(_age / duration);
      var mainAlpha = 1f * (1f - t);
      var shockAlpha = 0.72f * (1f - Mathf.Clamp01(t * 1.08f));
      var flashAlpha = 0.95f * (1f - Mathf.Clamp01(t * 1.45f));
      var ringScale = Mathf.Lerp(0.06f, _radius, t);
      var shockScale = Mathf.Lerp(0.14f, _radius * 1.22f, Mathf.Clamp01(t * 0.9f));
      var flashScale = Mathf.Lerp(0.04f, _radius * 0.72f, Mathf.Clamp01(t * 1.25f));

      RangedVfxUtil.DrawRing(_mainRing, ringScale, 64);
      RangedVfxUtil.DrawRing(_shockRing, shockScale, 64);
      RangedVfxUtil.DrawRing(_flashRing, flashScale, 48);
      RangedVfxUtil.SetLineColor(
        _mainRing,
        new Color(1f, 0.48f, 0.08f, mainAlpha),
        new Color(1f, 0.12f, 0.02f, mainAlpha * 0.72f));
      RangedVfxUtil.SetLineColor(
        _shockRing,
        new Color(1f, 0.78f, 0.22f, shockAlpha),
        new Color(1f, 0.24f, 0.04f, shockAlpha * 0.55f));
      RangedVfxUtil.SetLineColor(
        _flashRing,
        new Color(1f, 0.95f, 0.72f, flashAlpha),
        new Color(1f, 0.55f, 0.12f, flashAlpha * 0.45f));

      if (_coreGlow != null)
      {
        var glowAlpha = 0.95f * (1f - Mathf.Clamp01(t * 1.35f));
        _coreGlow.color = new Color(1f, 0.72f, 0.18f, glowAlpha);
        _coreGlow.transform.localScale = Vector3.one * Mathf.Lerp(
          Mathf.Clamp(_radius * 0.22f, 0.55f, 1.35f),
          Mathf.Clamp(_radius * 0.08f, 0.18f, 0.55f),
          t);
      }

      if (_age >= duration + 0.04f)
      {
        _fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedSlowPulseVfx : MonoBehaviour
  {
    static readonly Queue<RangedSlowPulseVfx> Pool = new();

    LineRenderer _ring;
    SpriteRenderer _glow;
    float _age;
    float _scale;

    public static void Spawn(Vector3 position, float scale = 1f)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.05f);
      fx._age = 0f;
      fx._scale = Mathf.Max(0.5f, scale);
    }

    static RangedSlowPulseVfx Create()
    {
      var go = new GameObject("RangedSlowPulseVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedSlowPulseVfx>();
      fx._ring = RangedVfxUtil.CreateLine(go.transform, "SlowDragRing", 83, 0.035f);
      fx._glow = RangedMuzzleFlashVfx.CreateGlow(go.transform, "SlowGlow", new Color(0.2f, 0.72f, 1f, 0.26f), 82);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.28f);
      var a = 1f - t;
      RangedVfxUtil.DrawRing(_ring, Mathf.Lerp(0.2f, 0.65f, t) * _scale, 42);
      RangedVfxUtil.SetLineColor(_ring, new Color(0.75f, 0.95f, 1f, 0.5f * a), new Color(0.18f, 0.62f, 1f, 0.28f * a));
      _glow.color = new Color(0.2f, 0.72f, 1f, 0.22f * a);
      _glow.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.95f, t) * _scale;
      if (_age >= 0.3f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedExplosionDetonateLinkVfx : MonoBehaviour
  {
    static readonly Queue<RangedExplosionDetonateLinkVfx> Pool = new();

    LineRenderer _line;
    float _age;

    public static void Spawn(Vector3 from, Vector3 to)
    {
      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx._age = 0f;
      from.z = LaserVfxShared.VfxDepthZ - 0.06f;
      to.z = LaserVfxShared.VfxDepthZ - 0.06f;
      fx._line.SetPosition(0, from);
      fx._line.SetPosition(1, to);
      RangedVfxUtil.SetLineColor(fx._line, new Color(1f, 0.92f, 0.72f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.75f));
    }

    static RangedExplosionDetonateLinkVfx Create()
    {
      var go = new GameObject("RangedExplosionDetonateLinkVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedExplosionDetonateLinkVfx>();
      fx._line = RangedVfxUtil.CreateLine(go.transform, "DetonateLink", 90, 0.05f, true);
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var a = 1f - Mathf.Clamp01(_age / 0.1f);
      RangedVfxUtil.SetLineColor(_line, new Color(1f, 0.92f, 0.72f, a), new Color(1f, 0.35f, 0.08f, a * 0.8f));
      if (_age >= 0.11f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public sealed class RangedPierceTrailVfx : MonoBehaviour
  {
    static readonly Queue<RangedPierceTrailVfx> Pool = new();

    LineRenderer _slash;
    float _age;
    Vector3 _dir;

    public static void Spawn(Vector3 position, Vector3 direction)
    {
      if (direction.sqrMagnitude < 0.0001f)
        return;

      var fx = Pool.Count > 0 ? Pool.Dequeue() : Create();
      fx.gameObject.SetActive(true);
      fx.transform.position = new Vector3(position.x, position.y, LaserVfxShared.VfxDepthZ - 0.04f);
      fx._age = 0f;
      fx._dir = direction.normalized;
      var half = fx._dir * 0.42f;
      fx._slash.SetPosition(0, -half);
      fx._slash.SetPosition(1, half);
    }

    static RangedPierceTrailVfx Create()
    {
      var go = new GameObject("RangedPierceTrailVfx");
      Object.DontDestroyOnLoad(go);
      var fx = go.AddComponent<RangedPierceTrailVfx>();
      fx._slash = RangedVfxUtil.CreateLine(go.transform, "PierceSlash", 86, 0.04f, true);
      fx._slash.material = RangedVfxUtil.LineMaterial;
      go.SetActive(false);
      return fx;
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / 0.08f);
      var a = 1f - t;
      RangedVfxUtil.SetLineColor(_slash,
        new Color(1f, 0.98f, 0.82f, 0.85f * a),
        new Color(1f, 0.72f, 0.18f, 0.45f * a));
      if (_age >= 0.09f)
      {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
      }
    }
  }

  public static class RangedVfxSandboxReset
  {
    public static void ResetAll()
    {
      foreach (var fx in Object.FindObjectsOfType<RangedMuzzleFlashVfx>(true))
        fx.gameObject.SetActive(false);
      foreach (var fx in Object.FindObjectsOfType<RangedProjectileHitVfx>(true))
        fx.gameObject.SetActive(false);
      foreach (var fx in Object.FindObjectsOfType<RangedChainLightningVfx>(true))
        fx.gameObject.SetActive(false);
      foreach (var fx in Object.FindObjectsOfType<RangedExplosionVfx>(true))
        fx.gameObject.SetActive(false);
      foreach (var fx in Object.FindObjectsOfType<RangedExplosionDetonateLinkVfx>(true))
        fx.gameObject.SetActive(false);
      foreach (var fx in Object.FindObjectsOfType<RangedPierceTrailVfx>(true))
        fx.gameObject.SetActive(false);
    }
  }
}
