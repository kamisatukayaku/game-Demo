using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  sealed class DetachedMissileVisual : MonoBehaviour
  {
    LineRenderer _body;
    LineRenderer _engineRing;
    LineRenderer _nose;
    TrailRenderer _trail;
    SpriteRenderer _glow;
    SpriteRenderer _engineGlow;
    SpriteRenderer[] _particles;
    bool _child;
    int _tier = 1;
    Vector3 _lastPosition;
    Vector3 _lastVelocity;
    float _turnFlash;
    bool _hasLastPosition;
    bool _built;

    void Awake()
    {
      EnsureBuilt();
    }

    void EnsureBuilt()
    {
      if (_built && _body != null && _nose != null && _engineRing != null && _trail != null && _glow != null && _engineGlow != null && _particles != null)
        return;

      var material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      var bodyGo = new GameObject("BodyLine");
      bodyGo.transform.SetParent(transform, false);
      _body = bodyGo.AddComponent<LineRenderer>();
      if (_body == null)
        return;
      _body.useWorldSpace = false;
      _body.loop = true;
      _body.positionCount = 7;
      _body.material = material;
      _body.sortingLayerName = LaserVfxShared.SortingLayerName;
      _body.sortingOrder = 43;
      _body.startWidth = _body.endWidth = 0.07f;
      _body.SetPosition(0, new Vector3(0.54f, 0f));
      _body.SetPosition(1, new Vector3(0.18f, 0.2f));
      _body.SetPosition(2, new Vector3(-0.18f, 0.18f));
      _body.SetPosition(3, new Vector3(-0.44f, 0.08f));
      _body.SetPosition(4, new Vector3(-0.44f, -0.08f));
      _body.SetPosition(5, new Vector3(-0.18f, -0.18f));
      _body.SetPosition(6, new Vector3(0.18f, -0.2f));

      var noseGo = new GameObject("NoseLine");
      noseGo.transform.SetParent(transform, false);
      _nose = noseGo.AddComponent<LineRenderer>();
      if (_nose == null)
        return;
      _nose.useWorldSpace = false;
      _nose.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _nose.sortingLayerName = LaserVfxShared.SortingLayerName;
      _nose.sortingOrder = 44;
      _nose.positionCount = 2;
      _nose.SetPosition(0, new Vector3(0.18f, 0f));
      _nose.SetPosition(1, new Vector3(0.62f, 0f));

      var ringGo = new GameObject("EngineRing");
      ringGo.transform.SetParent(transform, false);
      ringGo.transform.localPosition = new Vector3(-0.46f, 0f, 0f);
      _engineRing = ringGo.AddComponent<LineRenderer>();
      if (_engineRing == null)
        return;
      _engineRing.useWorldSpace = false;
      _engineRing.loop = true;
      _engineRing.positionCount = 16;
      _engineRing.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _engineRing.sortingLayerName = LaserVfxShared.SortingLayerName;
      _engineRing.sortingOrder = 42;
      for (var i = 0; i < 16; i++)
      {
        var angle = i * Mathf.PI * 2f / 16f;
        _engineRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.16f, Mathf.Sin(angle) * 0.16f));
      }

      var glowGo = new GameObject("MissileGlow");
      glowGo.transform.SetParent(transform, false);
      _glow = glowGo.AddComponent<SpriteRenderer>();
      if (_glow == null)
        return;
      _glow.sprite = LaserVfxShared.SoftGlowSprite;
      _glow.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _glow.sortingLayerName = LaserVfxShared.SortingLayerName;
      _glow.sortingOrder = 41;
      glowGo.transform.localScale = new Vector3(0.9f, 0.48f, 1f);

      var engineGlowGo = new GameObject("EngineGlow");
      engineGlowGo.transform.SetParent(transform, false);
      engineGlowGo.transform.localPosition = new Vector3(-0.52f, 0f, 0f);
      _engineGlow = engineGlowGo.AddComponent<SpriteRenderer>();
      if (_engineGlow == null)
        return;
      _engineGlow.sprite = LaserVfxShared.SoftGlowSprite;
      _engineGlow.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _engineGlow.sortingLayerName = LaserVfxShared.SortingLayerName;
      _engineGlow.sortingOrder = 40;
      engineGlowGo.transform.localScale = new Vector3(0.42f, 0.28f, 1f);

      if (_particles == null || _particles.Length < 5)
      {
        _particles = new SpriteRenderer[5];
        for (var i = 0; i < _particles.Length; i++)
        {
          var particleGo = new GameObject($"EnergyShard_{i + 1}");
          particleGo.transform.SetParent(transform, false);
          var particle = particleGo.AddComponent<SpriteRenderer>();
          particle.sprite = LaserVfxShared.SoftGlowSprite;
          particle.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
          particle.sortingLayerName = LaserVfxShared.SortingLayerName;
          particle.sortingOrder = 39;
          _particles[i] = particle;
        }
      }

      if (_trail == null)
      {
        var trailGo = new GameObject("MissileTrail");
        trailGo.transform.SetParent(transform, false);
        _trail = trailGo.AddComponent<TrailRenderer>();
      }
      if (_trail == null)
        return;
      _trail.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _trail.time = 0.28f;
      _trail.startWidth = 0.26f;
      _trail.endWidth = 0f;
      _trail.minVertexDistance = 0.04f;
      _trail.sortingLayerName = LaserVfxShared.SortingLayerName;
      _trail.sortingOrder = 40;
      _trail.numCapVertices = 3;
      _built = true;
    }

    public void Configure(bool child)
    {
      EnsureBuilt();
      if (_body == null || _nose == null || _engineRing == null || _trail == null || _glow == null || _engineGlow == null)
        return;

      _child = child;
      _tier = Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_missile_tier")), 1, 5);
      var scale = child ? 0.72f : 1f;
      transform.localScale = Vector3.one * scale;
      var core = child ? new Color(1f, 0.62f, 0.22f, 0.88f) : new Color(1f, 0.72f, 0.28f, 1f);
      var edge = child ? new Color(1f, 0.22f, 0.04f, 0.56f) : new Color(1f, 0.32f, 0.06f, 0.82f);
      var accent = child ? new Color(1f, 0.48f, 0.08f, 0.36f) : new Color(1f, 0.58f, 0.12f, 0.48f);
      var tierT = (_tier - 1) / 4f;
      LaserVfxShared.SetLineColor(_body, core, edge);
      _body.startWidth = _body.endWidth = Mathf.Lerp(child ? 0.05f : 0.07f, child ? 0.08f : 0.115f, tierT);
      LaserVfxShared.SetLineColor(_nose, new Color(1f, 0.96f, 0.82f, 1f), core);
      _nose.startWidth = Mathf.Lerp(child ? 0.08f : 0.1f, child ? 0.11f : 0.15f, tierT);
      _nose.endWidth = Mathf.Lerp(child ? 0.035f : 0.045f, child ? 0.05f : 0.07f, tierT);
      LaserVfxShared.SetLineColor(_engineRing, edge, edge);
      _engineRing.startWidth = _engineRing.endWidth = Mathf.Lerp(child ? 0.026f : 0.04f, child ? 0.045f : 0.075f, tierT);
      _trail.startColor = core;
      _trail.endColor = new Color(1f, 0.18f, 0.04f, 0f);
      _trail.time = Mathf.Lerp(child ? 0.14f : 0.24f, child ? 0.24f : 0.46f, tierT);
      _trail.startWidth = Mathf.Lerp(child ? 0.14f : 0.24f, child ? 0.22f : 0.42f, tierT);
      LaserVfxShared.SetSpriteColor(_glow, new Color(core.r, core.g, core.b, Mathf.Lerp(0.2f, 0.42f, tierT)));
      LaserVfxShared.SetSpriteColor(_engineGlow, new Color(edge.r, edge.g, edge.b, child ? Mathf.Lerp(0.18f, 0.3f, tierT) : Mathf.Lerp(0.3f, 0.5f, tierT)));
      if (_particles != null)
      {
        foreach (var particle in _particles)
          LaserVfxShared.SetSpriteColor(particle, accent);
      }
      _trail.Clear();
      _hasLastPosition = false;
      _turnFlash = 0f;
    }

    void OnEnable()
    {
      if (_trail != null)
        _trail.Clear();
    }

    void Update()
    {
      if (_glow == null || _engineGlow == null || _engineRing == null)
        return;
      var position = transform.position;
      if (_hasLastPosition)
      {
        var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        var velocity = (position - _lastPosition) / dt;
        if (_lastVelocity.sqrMagnitude > 0.02f && velocity.sqrMagnitude > 0.02f)
        {
          var turn = Vector3.Angle(_lastVelocity, velocity);
          if (turn > 38f)
            _turnFlash = 0.14f;
        }
        _lastVelocity = velocity;
      }
      else
      {
        _hasLastPosition = true;
        _lastVelocity = Vector3.zero;
      }
      _lastPosition = position;
      _turnFlash = Mathf.Max(0f, _turnFlash - Time.unscaledDeltaTime);
      var pulse = 0.86f + Mathf.Sin(Time.unscaledTime * (_child ? 16f : 12f)) * 0.08f;
      var turnBoost = Mathf.Clamp01(_turnFlash / 0.14f);
      _glow.transform.localScale = new Vector3(0.9f, 0.48f, 1f) * pulse;
      _engineGlow.transform.localScale = new Vector3(0.42f, 0.28f, 1f) * (1f + turnBoost * 0.45f);
      _engineRing.transform.Rotate(0f, 0f, (_child ? -180f : -240f) * Time.unscaledDeltaTime);
      _engineRing.transform.localScale = Vector3.one * (1f + turnBoost * 0.3f);
      UpdateEnergyShards(turnBoost);
    }

    void UpdateEnergyShards(float turnBoost)
    {
      if (_particles == null)
        return;
      var time = Time.unscaledTime;
      for (var i = 0; i < _particles.Length; i++)
      {
        var particle = _particles[i];
        if (particle == null)
          continue;
        particle.enabled = i < Mathf.Clamp(_tier, 1, _particles.Length);
        var phase = Mathf.Repeat(time * (3.8f + i * 0.4f) + i * 0.31f, 1f);
        var side = Mathf.Sin(time * 11f + i * 1.9f) * (_child ? 0.08f : 0.12f);
        var trailDepth = Mathf.Lerp(1.1f, 1.65f, (_tier - 1) / 4f);
        particle.transform.localPosition = new Vector3(Mathf.Lerp(-0.62f, -trailDepth, phase), side, 0f);
        particle.transform.localScale = Vector3.one * Mathf.Lerp(_child ? 0.045f : 0.06f, _child ? 0.018f : 0.026f, phase);
        var alpha = (1f - phase) * Mathf.Lerp(_child ? 0.18f : 0.28f, _child ? 0.34f : 0.5f, (_tier - 1) / 4f) * (1f + turnBoost * 0.6f);
        LaserVfxShared.SetSpriteColor(particle, new Color(1f, 0.72f, 0.28f, alpha));
      }
    }
  }
}
