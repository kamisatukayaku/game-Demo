using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Core;
using Game.Shared.Vfx;
using Game.Shared.Laser;
namespace Game.Shared.Enemy.Visual
{
  /// <summary>
  /// 怪物 Debuff 视觉：灼烧（橙火）、中毒（绿泡）、流血（红滴）、减速（黄色移动拖尾）?
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyDebuffVisual : MonoBehaviour
  {
    const int BurnSort = 12;
    const int PoisonSort = 13;
    const int BleedSort = 13;

    Transform _fxRoot;
    ParticleSystem _burnPs;
    ParticleSystem _poisonPs;
    ParticleSystem _bleedPs;
    SlowMovementTrailEffect _slowTrail;
    LineRenderer _slowOverlay;
    BuffContainer _buffs;
    Vector3 _lastPos;
    bool _burnActive;
    bool _poisonActive;
    bool _bleedActive;
    float _slowOverlayAlpha;

    public static EnemyDebuffVisual Ensure(GameObject owner)
    {
      if (owner == null)
        return null;

      var existing = owner.GetComponent<EnemyDebuffVisual>();
      if (existing != null)
        return existing;

      return owner.AddComponent<EnemyDebuffVisual>();
    }

    void Awake()
    {
      _buffs = GetComponent<BuffContainer>();
      _lastPos = transform.position;
      EnsureBuilt();
    }

    void EnsureBuilt()
    {
      if (_fxRoot != null)
        return;

      _fxRoot = new GameObject("DebuffVfx").transform;
      _fxRoot.SetParent(transform, false);

      _burnPs = BuildBurnParticles();
      _poisonPs = BuildPoisonParticles();
      _bleedPs = BuildBleedParticles();
      _slowTrail = SlowMovementTrailEffect.Ensure(gameObject);
      _slowTrail.EnsureBuilt();
      _slowOverlay = BuildSlowOverlay();
    }

    void Update()
    {
      if (_buffs == null)
        _buffs = GetComponent<BuffContainer>();

      SyncBurn(_buffs != null && _buffs.HasBuff("buff_burn"));
      SyncPoison(_buffs != null && _buffs.HasBuff("buff_poison"));
      SyncBleed(_buffs != null && _buffs.HasBuff("buff_bleed"));
      var slowed = _buffs != null && _buffs.HasSlowEffect();
      SyncSlowTrail(slowed);
      SyncSlowOverlay(slowed);

      if (_burnActive || _poisonActive || _bleedActive || _slowOverlayAlpha > 0.01f)
        SyncFxRoot();

      _lastPos = transform.position;
    }

    void SyncBurn(bool active)
    {
      if (active == _burnActive)
        return;

      _burnActive = active;
      if (active)
      {
        SyncFxRoot();
        _burnPs.Clear(true);
        _burnPs.Play(true);
      }
      else
      {
        _burnPs?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      }
    }

    void SyncPoison(bool active)
    {
      if (active == _poisonActive)
        return;

      _poisonActive = active;
      if (active)
      {
        SyncFxRoot();
        _poisonPs.Clear(true);
        _poisonPs.Play(true);
      }
      else
      {
        _poisonPs?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      }
    }

    void SyncBleed(bool active)
    {
      if (active == _bleedActive)
        return;

      _bleedActive = active;
      if (active)
      {
        SyncFxRoot();
        _bleedPs.Clear(true);
        _bleedPs.Play(true);
      }
      else
      {
        _bleedPs?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      }
    }

    void SyncSlowTrail(bool slowed)
    {
      if (_slowTrail == null)
        return;

      var delta = transform.position - _lastPos;
      delta.z = 0f;
      var moving = delta.sqrMagnitude > 0.00008f;

      if (slowed && moving)
      {
        _slowTrail.SyncDirection(new Vector2(delta.x, delta.y));
        _slowTrail.Begin(new Vector2(delta.x, delta.y));
      }
      else
      {
        _slowTrail.End();
      }
    }

    void SyncSlowOverlay(bool slowed)
    {
      if (_slowOverlay == null)
        return;

      _slowOverlayAlpha = Mathf.MoveTowards(
        _slowOverlayAlpha,
        slowed ? 0.22f : 0f,
        Time.deltaTime * (slowed ? 3.5f : 2.2f));

      var active = _slowOverlayAlpha > 0.01f;
      if (_slowOverlay.gameObject.activeSelf != active)
        _slowOverlay.gameObject.SetActive(active);

      if (!active)
        return;

      var radius = LaserVfxShared.GetOwnerVisualRadius(transform);
      var ringRadius = Mathf.Max(0.32f, radius * 1.18f);
      DrawSlowOverlayRing(_slowOverlay, ringRadius);
      _slowOverlay.startWidth = Mathf.Max(0.018f, ringRadius * 0.055f);
      _slowOverlay.endWidth = _slowOverlay.startWidth;
      _slowOverlay.startColor = new Color(0.45f, 0.82f, 1f, _slowOverlayAlpha * 0.95f);
      _slowOverlay.endColor = new Color(0.9f, 1f, 1f, _slowOverlayAlpha * 0.45f);
    }

    void SyncFxRoot()
    {
      if (_fxRoot == null)
        return;

      var pos = LaserVfxShared.GetOwnerEmissionPoint(transform);
      pos.z = LaserVfxShared.VfxDepthZ + 0.05f;
      _fxRoot.position = pos;
    }

    ParticleSystem BuildBurnParticles()
    {
      var go = new GameObject("BurnFx");
      go.transform.SetParent(_fxRoot, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 36;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 0.85f, 0.2f, 1f),
        new Color(1f, 0.35f, 0.05f, 1f));

      var emission = ps.emission;
      emission.rateOverTime = 22f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.28f;
      shape.radiusThickness = 0.65f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
      vel.y = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildBurnGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.2f);

      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), BurnSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    LineRenderer BuildSlowOverlay()
    {
      var go = new GameObject("SlowBlueOverlay");
      go.transform.SetParent(_fxRoot, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.positionCount = 64;
      line.startWidth = 0.025f;
      line.endWidth = 0.025f;
      line.material = SlowOverlayMaterial;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = 14;
      DrawSlowOverlayRing(line, 0.45f);
      go.SetActive(false);
      return line;
    }

    static Material s_slowOverlayMaterial;

    static Material SlowOverlayMaterial =>
      s_slowOverlayMaterial != null
        ? s_slowOverlayMaterial
        : s_slowOverlayMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EnemySlowOverlayLine_Runtime" };

    static void DrawSlowOverlayRing(LineRenderer line, float radius)
    {
      if (line == null)
        return;

      var count = line.positionCount;
      for (var i = 0; i < count; i++)
      {
        var angle = i / (float)count * Mathf.PI * 2f;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    ParticleSystem BuildPoisonParticles()
    {
      var go = new GameObject("PoisonFx");
      go.transform.SetParent(_fxRoot, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 28;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(0.45f, 1f, 0.35f, 0.95f),
        new Color(0.15f, 0.75f, 0.2f, 0.85f));

      var emission = ps.emission;
      emission.rateOverTime = 14f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.32f;
      shape.radiusThickness = 0.55f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
      vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildPoisonGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);

      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), PoisonSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    ParticleSystem BuildBleedParticles()
    {
      var go = new GameObject("BleedFx");
      go.transform.SetParent(_fxRoot, false);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 24;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(0.95f, 0.08f, 0.08f, 1f),
        new Color(0.55f, 0.02f, 0.02f, 0.9f));

      var emission = ps.emission;
      emission.rateOverTime = 16f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.26f;
      shape.radiusThickness = 0.4f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
      vel.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.35f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildBleedGradient();

      LaserVfxShared.ApplySharedParticleRenderer(ps.GetComponent<ParticleSystemRenderer>(), BleedSort);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    static ParticleSystem.MinMaxGradient BuildBurnGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
          new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.5f),
          new GradientColorKey(new Color(0.35f, 0.05f, 0.02f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0.65f, 0.55f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    static ParticleSystem.MinMaxGradient BuildPoisonGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.55f, 1f, 0.45f), 0f),
          new GradientColorKey(new Color(0.2f, 0.75f, 0.18f), 0.55f),
          new GradientColorKey(new Color(0.08f, 0.35f, 0.08f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0.85f, 0f),
          new GradientAlphaKey(0.45f, 0.6f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }

    static ParticleSystem.MinMaxGradient BuildBleedGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0f),
          new GradientColorKey(new Color(0.55f, 0.02f, 0.02f), 0.65f),
          new GradientColorKey(new Color(0.2f, 0.01f, 0.01f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0.5f, 0.55f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }
  }
}
