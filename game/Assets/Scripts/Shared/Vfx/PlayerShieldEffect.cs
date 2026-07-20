using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Core;
using Game.Shared.Laser;
namespace Game.Shared.Vfx
{
  /// <summary>
  /// 玩家护盾 VFX：半透明青蓝能量穹顶 + 六边形线?+ 轨道火花 + 脉冲光晕?
  /// </summary>
  [DisallowMultipleComponent]
  public class PlayerShieldEffect : MonoBehaviour
  {
    const int HexSort = 27;
    const int SparkSort = 28;
    const float DomeDepthZ = -0.12f;

    static readonly Color HexLineColor = new(0.45f, 0.98f, 1f, 0.72f);

    Transform _domeRoot;
    LineRenderer _hexRing;
    ParticleSystem _orbitSparks;
    ParticleSystem _arcSparks;
    BuffContainer _buffs;
    float _pulsePhase;
    bool _visible;

    public static PlayerShieldEffect Ensure(GameObject owner)
    {
      if (owner == null)
        return null;

      var existing = owner.GetComponent<PlayerShieldEffect>();
      if (existing != null)
        return existing;

      return owner.AddComponent<PlayerShieldEffect>();
    }

    void Awake()
    {
      _buffs = GetComponent<BuffContainer>();
      EnsureBuilt();
    }

    void EnsureBuilt()
    {
      if (_domeRoot != null)
        return;

      _domeRoot = new GameObject("ShieldVfx").transform;
      _domeRoot.SetParent(transform, false);

      _hexRing = BuildHexRing();
      _orbitSparks = BuildOrbitSparks();
      _arcSparks = BuildArcSparks();
      SetVisible(false);
    }

    void Update()
    {
      if (_buffs == null)
        _buffs = GetComponent<BuffContainer>();

      var show = _buffs != null && _buffs.HasShieldEffect();
      if (show != _visible)
        SetVisible(show);

      if (!_visible)
        return;

      SyncTransform();
      UpdatePulse();
      RotateHex();
    }

    void SetVisible(bool visible)
    {
      _visible = visible;
      if (_domeRoot == null)
        return;

      _domeRoot.gameObject.SetActive(visible);
      if (visible)
      {
        _orbitSparks?.Play(true);
        _arcSparks?.Play(true);
      }
      else
      {
        _orbitSparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _arcSparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      }
    }

    void SyncTransform()
    {
      if (_domeRoot == null)
        return;

      var pos = LaserVfxShared.GetOwnerEmissionPoint(transform);
      pos.z = DomeDepthZ;
      _domeRoot.position = pos;

      var radius = LaserVfxShared.GetOwnerVisualRadius(transform) * 1.35f;
      UpdateHexGeometry(radius);

      if (_orbitSparks != null)
      {
        var shape = _orbitSparks.shape;
        shape.radius = radius * 0.95f;
      }

      if (_arcSparks != null)
      {
        var shape = _arcSparks.shape;
        shape.radius = radius * 0.85f;
      }
    }

    void UpdatePulse()
    {
      _pulsePhase += Time.deltaTime * 3.2f;
      var pulse = 0.88f + 0.12f * Mathf.Sin(_pulsePhase);

      if (_hexRing != null)
      {
        var hc = HexLineColor;
        hc.a = HexLineColor.a * pulse;
        _hexRing.startColor = hc;
        _hexRing.endColor = hc;
      }
    }

    void RotateHex()
    {
      if (_domeRoot == null)
        return;

      _domeRoot.Rotate(0f, 0f, 18f * Time.deltaTime, Space.Self);
    }

    LineRenderer BuildHexRing()
    {
      var go = new GameObject("HexRing");
      go.transform.SetParent(_domeRoot, false);

      var lr = go.AddComponent<LineRenderer>();
      ConfigureHexLine(lr, 7);
      lr.sortingOrder = HexSort;
      return lr;
    }

    void ConfigureHexLine(LineRenderer lr, int pointCount)
    {
      lr.useWorldSpace = false;
      lr.loop = pointCount > 2;
      lr.positionCount = pointCount;
      lr.startWidth = 0.035f;
      lr.endWidth = 0.035f;
      lr.numCapVertices = 4;
      lr.material = LaserVfxShared.CreateBeamMaterialInstance();
      lr.sortingLayerName = LaserVfxShared.SortingLayerName;
      lr.startColor = HexLineColor;
      lr.endColor = HexLineColor;
    }

    void UpdateHexGeometry(float radius)
    {
      if (_hexRing == null)
        return;

      const int segments = 6;
      _hexRing.positionCount = segments + 1;
      for (var i = 0; i <= segments; i++)
      {
        var angle = i * 60f * Mathf.Deg2Rad;
        _hexRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    ParticleSystem BuildOrbitSparks()
    {
        var go = new GameObject("ShieldOrbitSparks");
        go.transform.SetParent(_domeRoot, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 20;
        main.startColor = new ParticleSystem.MinMaxGradient(
          new Color(0.7f, 1f, 1f, 1f),
          new Color(0.35f, 0.85f, 1f, 0.85f));

        var emission = ps.emission;
        emission.rateOverTime = 10f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.55f;
        shape.radiusThickness = 0.05f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildSparkGradient();

        LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), SparkSort);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    ParticleSystem BuildArcSparks()
    {
        var go = new GameObject("ShieldArcSparks");
        go.transform.SetParent(_domeRoot, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 16;
        main.startColor = new ParticleSystem.MinMaxGradient(
          new Color(0.85f, 1f, 1f, 1f),
          new Color(0.4f, 0.9f, 1f, 0.9f));

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.48f;
        shape.radiusThickness = 0.15f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildSparkGradient();

        LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), SparkSort + 1);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    static ParticleSystem.MinMaxGradient BuildSparkGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.75f, 1f, 1f), 0f),
          new GradientColorKey(new Color(0.25f, 0.65f, 1f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }
  }
}
