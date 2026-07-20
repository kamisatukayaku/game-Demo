using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  sealed class DetachedWeaponVisual : MonoBehaviour
  {
    readonly LineRenderer[] _beams = new LineRenderer[DetachedWeaponVisualState.MaxBeams];
    readonly LineRenderer[] _beamCores = new LineRenderer[DetachedWeaponVisualState.MaxBeams];
    readonly LineRenderer[] _beamGlows = new LineRenderer[DetachedWeaponVisualState.MaxBeams];
    DetachedWeaponVisualState _state;
    SpriteRenderer _innerCore;
    SpriteRenderer _halo;
    readonly SpriteRenderer[] _orbitParticles = new SpriteRenderer[4];
    readonly PathNode[] _pathNodes = new PathNode[8];
    LineRenderer _outerRing;
    LineRenderer _secondRing;
    LineRenderer _turnArc;
    LineRenderer _pathNetwork;
    LineRenderer _fireRing;
    TrailRenderer _microTrail;
    ParticleSystem _laserChargeParticles;
    int _nextPathNode;
    float _pathNodeTimer;
    float _turnArcAge;
    Vector3 _lastPosition;
    Vector3 _lastVelocity;
    bool _hasLastPosition;
    bool _wasWarning;
    float _fireFlashAge;
    float _explosionFlashAge;

    void Awake()
    {
      _state = GetComponent<DetachedWeaponVisualState>();

      _halo = CreateSprite("EnergyHalo", 33, 0.92f);
      _innerCore = CreateSprite("InnerCore", 36, 0.38f);

      _outerRing = CreateLine("OuterRing", 35, true);
      _outerRing.loop = true;
      DrawLocalCircle(_outerRing, 0.43f, 48);

      _secondRing = CreateLine("SecondaryRing", 34, true);
      _secondRing.loop = true;
      DrawDashedCircle(_secondRing, 0.55f, 36);
      _secondRing.enabled = false;

      for (var i = 0; i < _orbitParticles.Length; i++)
      {
        var particle = CreateSprite($"CoreParticle_{i + 1}", 37, 0.08f);
        particle.enabled = false;
        _orbitParticles[i] = particle;
      }

      _microTrail = gameObject.AddComponent<TrailRenderer>();
      _microTrail.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      _microTrail.time = 0.11f;
      _microTrail.startWidth = 0.13f;
      _microTrail.endWidth = 0f;
      _microTrail.minVertexDistance = 0.06f;
      _microTrail.numCapVertices = 3;
      _microTrail.sortingLayerName = LaserVfxShared.SortingLayerName;
      _microTrail.sortingOrder = 31;

      for (var i = 0; i < _pathNodes.Length; i++)
        _pathNodes[i] = CreatePathNode(i);

      _turnArc = CreateWorldLine("TurnArc", 32);
      _turnArc.positionCount = 4;
      _turnArc.enabled = false;

      _pathNetwork = CreateWorldLine("PathNetwork", 30);
      _pathNetwork.positionCount = 0;
      _pathNetwork.enabled = false;

      _fireRing = CreateLine("LaserFireShockRing", 38, true);
      _fireRing.loop = true;
      DrawLocalCircle(_fireRing, 0.64f, 48);
      _fireRing.enabled = false;

      _laserChargeParticles = CreateLaserChargeParticles();

      for (var i = 0; i < _beams.Length; i++)
      {
        _beamGlows[i] = CreateBeamWorldLine($"LaserBeamGlow_{i + 1}", 49);
        _beamGlows[i].positionCount = 2;
        _beamGlows[i].enabled = false;

        _beams[i] = CreateBeamWorldLine($"LaserBeamShell_{i + 1}", 51);
        _beams[i].positionCount = 2;
        _beams[i].enabled = false;

        _beamCores[i] = CreateBeamWorldLine($"LaserBeamCore_{i + 1}", 53);
        _beamCores[i].positionCount = 2;
        _beamCores[i].enabled = false;
      }
    }

    void OnDisable()
    {
      _state?.ClearBeams();
      if (_microTrail != null)
        _microTrail.Clear();
      _fireFlashAge = 0f;
      _explosionFlashAge = 0f;
      _turnArcAge = 0f;
      _hasLastPosition = false;
      for (var i = 0; i < _beams.Length; i++)
      {
        if (_beams[i] != null) _beams[i].enabled = false;
        if (_beamCores[i] != null) _beamCores[i].enabled = false;
        if (_beamGlows[i] != null) _beamGlows[i].enabled = false;
      }
      if (_fireRing != null) _fireRing.enabled = false;
      if (_laserChargeParticles != null)
        _laserChargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      if (_turnArc != null) _turnArc.enabled = false;
      if (_pathNetwork != null)
      {
        _pathNetwork.enabled = false;
        _pathNetwork.positionCount = 0;
      }
      foreach (var node in _pathNodes)
        node?.Disable();
    }

    SpriteRenderer CreateSprite(string spriteName, int order, float scale)
    {
      var go = new GameObject(spriteName);
      go.transform.SetParent(transform, false);
      go.transform.localScale = Vector3.one * scale;
      var sprite = go.AddComponent<SpriteRenderer>();
      sprite.sprite = LaserVfxShared.SoftGlowSprite;
      sprite.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
      sprite.sortingOrder = order;
      return sprite;
    }

    PathNode CreatePathNode(int index)
    {
      var go = new GameObject($"DetachedWeaponPathNode_{index + 1}");
      var sprite = go.AddComponent<SpriteRenderer>();
      sprite.sprite = LaserVfxShared.SoftGlowSprite;
      sprite.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
      sprite.sortingOrder = 30;
      go.SetActive(false);
      return new PathNode(go, sprite);
    }

    LineRenderer CreateBeamWorldLine(string lineName, int order)
    {
      var go = new GameObject($"{name}_{lineName}");
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      line.textureMode = LineTextureMode.Stretch;
      line.numCapVertices = 5;
      line.numCornerVertices = 4;
      line.alignment = LineAlignment.TransformZ;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.startWidth = line.endWidth = 0.04f;
      LaserVfxShared.SetLineColor(line, Color.white, Color.white);
      return line;
    }

    ParticleSystem CreateLaserChargeParticles()
    {
      var go = new GameObject("LaserChargeParticles");
      go.transform.SetParent(transform, false);
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      var main = ps.main;
      main.loop = true;
      main.duration = 0.7f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(-1.55f, -0.65f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 1f, 1f, 0.95f),
        new Color(1f, 1f, 1f, 0.72f));
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 42;

      var emission = ps.emission;
      emission.enabled = true;
      emission.rateOverTime = 0f;

      var shape = ps.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.72f;
      shape.radiusThickness = 0.18f;
      shape.arc = 360f;

      var color = ps.colorOverLifetime;
      color.enabled = true;
      color.color = new ParticleSystem.MinMaxGradient(new Gradient
      {
        colorKeys = new[]
        {
          new GradientColorKey(Color.white, 0f),
          new GradientColorKey(Color.white, 0.65f),
          new GradientColorKey(Color.white, 1f)
        },
        alphaKeys = new[]
        {
          new GradientAlphaKey(0f, 0f),
          new GradientAlphaKey(1f, 0.22f),
          new GradientAlphaKey(0f, 1f)
        }
      });

      var size = ps.sizeOverLifetime;
      size.enabled = true;
      size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
        new Keyframe(0f, 0.55f),
        new Keyframe(0.35f, 1.1f),
        new Keyframe(1f, 0.2f)));

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySquareParticleRenderer(renderer, 47);
      renderer.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      return ps;
    }

    LineRenderer CreateWorldLine(string lineName, int order)
    {
      var go = new GameObject($"{name}_{lineName}");
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      line.textureMode = LineTextureMode.Stretch;
      line.numCapVertices = 3;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.startWidth = line.endWidth = 0.04f;
      return line;
    }

    LineRenderer CreateLine(string lineName, int order, bool local)
    {
      var go = new GameObject(lineName);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = !local;
      line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      line.textureMode = LineTextureMode.Stretch;
      line.numCapVertices = 4;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      return line;
    }

    void LateUpdate()
    {
      if (_state == null)
        return;

      var visualId = ResolveVisualId();
      var laser = visualId == "laser_core";
      var missile = visualId == "missile_core";
      var explosion = visualId == "explosion_core";
      var pulseCore = visualId == "pulse_core";
      var boomerang = visualId == "boomerang_core";
      var trail = visualId == "trail_core";
      var palette = ResolvePalette(laser, missile, explosion, pulseCore, boomerang, trail);
      var visualLevel = ResolveVisualLevel(laser, missile, explosion, pulseCore, boomerang, trail);
      var motion = ResolveMotion(Time.unscaledDeltaTime);
      var speedFactor = Mathf.Clamp01(motion.Speed / 12f);
      var breathingCycle = Mathf.Lerp(2.35f, 1.55f, Mathf.Clamp01((visualLevel - 1f) / 9f));
      var breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / breathingCycle);
      var smoothBreath = Mathf.SmoothStep(0f, 1f, breath);
      var intensity = 1f + visualLevel * 0.035f;
      var coreScale = (0.36f + visualLevel * 0.008f) * (0.95f + smoothBreath * 0.1f);
      var haloScale = (0.9f + visualLevel * 0.035f) * (0.97f + smoothBreath * 0.06f);
      var ringWidth = visualLevel >= 3 ? 0.085f : 0.058f;
      if (visualLevel >= 10)
        ringWidth = 0.105f;
      var ringAlpha = Mathf.Clamp01((0.58f + smoothBreath * 0.1f) * intensity);
      var visible = true;

      _innerCore.enabled = visible;
      _halo.enabled = visible;
      _outerRing.enabled = visible;
      _secondRing.enabled = visible && visualLevel >= 5;
      _microTrail.enabled = visible && (_state == null || !_state.IntroActive);

      var laserFiring = laser && _state.BeamCount > 0 && !_state.Warning;
      if (laserFiring && _wasWarning)
        _fireFlashAge = 0.18f;
      _wasWarning = laser && _state.BeamCount > 0 && _state.Warning;

      if (visible)
      {
        var flash = Mathf.Clamp01(_fireFlashAge / 0.18f);
        var explosionFlash = Mathf.Clamp01(_explosionFlashAge / 0.15f);
        _innerCore.transform.localScale = Vector3.one * (coreScale + explosionFlash * 0.22f);
        _halo.transform.localScale = Vector3.one * (haloScale + explosionFlash * 0.28f);
        var coreColor = explosionFlash > 0f
          ? Color.Lerp(palette.Core, new Color(1f, 0.98f, 0.78f), explosionFlash)
          : palette.Core;
        var ringColor = explosionFlash > 0f
          ? Color.Lerp(palette.Ring, new Color(1f, 0.82f, 0.12f), explosionFlash)
          : palette.Ring;
        LaserVfxShared.SetSpriteColor(_innerCore,
          new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(((0.56f + smoothBreath * 0.1f) * intensity) + flash * 0.75f + explosionFlash * 0.7f)));
        LaserVfxShared.SetSpriteColor(_halo,
          new Color(palette.Halo.r, palette.Halo.g, palette.Halo.b, Mathf.Clamp01(0.12f + smoothBreath * 0.045f + flash * 0.18f + explosionFlash * 0.15f)));

        var outerRingColor = new Color(ringColor.r, ringColor.g, ringColor.b,
          Mathf.Clamp01(ringAlpha + flash * 0.35f + explosionFlash * 0.38f));
        LaserVfxShared.SetLineColor(_outerRing, outerRingColor, outerRingColor);
        _outerRing.startWidth = _outerRing.endWidth = ringWidth + flash * 0.04f + explosionFlash * 0.05f;
        _outerRing.transform.localScale = Vector3.one * (1f + smoothBreath * 0.035f + flash * 0.24f + explosionFlash * 0.32f);
        _outerRing.transform.Rotate(0f, 0f, ResolveRingSpeed(laser, missile, explosion, pulseCore, boomerang, trail) * (laser && _state.Warning ? 2.2f : 1f) * Time.unscaledDeltaTime);

        LaserVfxShared.SetLineColor(_secondRing,
          new Color(palette.Ring.r, palette.Ring.g, palette.Ring.b, Mathf.Clamp01(ringAlpha * 0.55f)),
          new Color(palette.Ring.r, palette.Ring.g, palette.Ring.b, Mathf.Clamp01(ringAlpha * 0.55f)));
        _secondRing.startWidth = _secondRing.endWidth = Mathf.Max(0.032f, ringWidth * 0.45f);
        _secondRing.transform.localScale = Vector3.one * (1f + (1f - smoothBreath) * 0.04f);
        _secondRing.transform.Rotate(0f, 0f, -ResolveRingSpeed(laser, missile, explosion, pulseCore, boomerang, trail) * 0.58f * Time.unscaledDeltaTime);

        var trailAlpha = Mathf.Clamp01(0.08f + visualLevel * 0.008f);
        _microTrail.startColor = new Color(palette.Core.r, palette.Core.g, palette.Core.b, trailAlpha);
        _microTrail.endColor = new Color(palette.Core.r, palette.Core.g, palette.Core.b, 0f);
        _microTrail.time = Mathf.Lerp(0.15f, 0.35f, Mathf.Max(speedFactor, Mathf.Clamp01((visualLevel - 1f) / 9f) * 0.55f));
        _microTrail.startWidth = Mathf.Lerp(0.1f, 0.2f, speedFactor) * (1f + visualLevel * 0.025f);
      }

      UpdateOrbitParticles(visible && visualLevel >= 7, palette, visualLevel, smoothBreath);
      UpdatePathNodes(visible && visualLevel >= 3, palette, visualLevel, speedFactor, motion);
      UpdateTurnArc(visible, palette, visualLevel, speedFactor, motion);
      UpdateFireRing(laser && visible, palette);
      UpdateLaserChargeParticles(laser && visible);
      _explosionFlashAge = Mathf.Max(0f, _explosionFlashAge - Time.unscaledDeltaTime);
      UpdateLaserBeams(laser, palette);
    }

    void UpdateLaserChargeParticles(bool laserVisible)
    {
      if (_laserChargeParticles == null || _state == null)
        return;

      var charging = laserVisible && _state.Warning && _state.BeamCount > 0;
      var emission = _laserChargeParticles.emission;
      if (charging)
      {
        var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);
        emission.rateOverTime = 34f + pulse * 26f;
        if (!_laserChargeParticles.isPlaying)
          _laserChargeParticles.Play(true);
      }
      else
      {
        emission.rateOverTime = 0f;
        if (_laserChargeParticles.isPlaying)
          _laserChargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      }
    }

    void UpdateLaserBeams(bool laser, WeaponCorePalette palette)
    {
      for (var i = 0; i < _beams.Length; i++)
      {
        var active = laser && i < _state.BeamCount;
        var shellLine = _beams[i];
        var coreLine = _beamCores[i];
        var glowLine = _beamGlows[i];
        shellLine.enabled = active;
        coreLine.enabled = active && !_state.Warning;
        glowLine.enabled = active;

        if (!active)
          continue;

        shellLine.SetPosition(0, _state.BeamStarts[i]);
        shellLine.SetPosition(1, _state.BeamEnds[i]);
        coreLine.SetPosition(0, _state.BeamStarts[i]);
        coreLine.SetPosition(1, _state.BeamEnds[i]);
        glowLine.SetPosition(0, _state.BeamStarts[i]);
        glowLine.SetPosition(1, _state.BeamEnds[i]);

        var flicker = 0.78f + Mathf.Sin(Time.unscaledTime * 54f + i * 1.7f) * 0.22f;
        var width = _state.BeamWidth;
        if (i >= 2)
          width *= 0.28f;

        if (_state.Warning)
        {
          var warnPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f + i * 1.3f);
          var warnAlpha = 0.28f + warnPulse * 0.34f;
          var warnCore = new Color(1f, 1f, 1f, warnAlpha);
          var warnEdge = new Color(1f, 1f, 1f, warnAlpha * 0.62f);
          LaserVfxShared.SetLineColor(shellLine, warnCore, warnEdge);
          LaserVfxShared.SetLineColor(glowLine,
            new Color(1f, 1f, 1f, 0.2f * warnPulse),
            new Color(1f, 1f, 1f, 0.08f * warnPulse));
          shellLine.startWidth = Mathf.Max(0.035f, width * (0.12f + warnPulse * 0.08f));
          shellLine.endWidth = Mathf.Max(0.026f, width * (0.08f + warnPulse * 0.05f));
          glowLine.startWidth = Mathf.Min(width * 1.15f, 0.22f);
          glowLine.endWidth = glowLine.startWidth * 0.82f;
          continue;
        }

        var shellStart = new Color(1f, 1f, 1f, 0.45f * flicker);
        var shellEnd = new Color(1f, 1f, 1f, 0.26f * flicker);
        var glowStart = new Color(1f, 1f, 1f, 0.24f * flicker);
        var glowEnd = new Color(1f, 1f, 1f, 0.1f * flicker);
        var coreStart = new Color(1f, 1f, 1f, 1f);
        var coreEnd = new Color(1f, 1f, 1f, 0.95f);

        LaserVfxShared.SetLineColor(shellLine, shellStart, shellEnd);
        LaserVfxShared.SetLineColor(glowLine, glowStart, glowEnd);
        LaserVfxShared.SetLineColor(coreLine, coreStart, coreEnd);

        shellLine.startWidth = Mathf.Max(0.08f, width * 0.42f);
        shellLine.endWidth = Mathf.Max(0.06f, width * 0.32f);
        glowLine.startWidth = Mathf.Min(width * 1.75f, 0.32f);
        glowLine.endWidth = glowLine.startWidth * 0.72f;
        coreLine.startWidth = Mathf.Max(0.055f, width * 0.18f);
        coreLine.endWidth = Mathf.Max(0.04f, width * 0.14f);
      }
    }

    public void PlayExplosionFlash()
    {
      _explosionFlashAge = 0.15f;
    }

    public void ResetForSpawn()
    {
      _state?.ResetPresentationState();
      _hasLastPosition = false;
      _lastVelocity = Vector3.zero;
      _fireFlashAge = 0f;
      _explosionFlashAge = 0f;
      _turnArcAge = 0f;
      _pathNodeTimer = 0f;
      _nextPathNode = 0;
      _wasWarning = false;

      if (_microTrail != null)
        _microTrail.Clear();

      for (var i = 0; i < _beams.Length; i++)
      {
        if (_beams[i] != null) _beams[i].enabled = false;
        if (_beamCores[i] != null) _beamCores[i].enabled = false;
        if (_beamGlows[i] != null) _beamGlows[i].enabled = false;
      }

      if (_fireRing != null) _fireRing.enabled = false;
      if (_turnArc != null) _turnArc.enabled = false;
      if (_pathNetwork != null)
      {
        _pathNetwork.enabled = false;
        _pathNetwork.positionCount = 0;
      }

      foreach (var node in _pathNodes)
        node?.Disable();

      foreach (var particle in _orbitParticles)
      {
        if (particle != null)
          particle.enabled = false;
      }
    }

    string ResolveVisualId()
    {
      if (_state != null && !string.IsNullOrEmpty(_state.VisualId))
        return _state.VisualId;

      var controller = GetComponent<DetachedWeaponController>();
      var weaponId = controller != null ? controller.WeaponId : null;
      if (string.IsNullOrEmpty(weaponId))
        return "contact_core";

      return DetachedWeaponDatabase.Get(weaponId)?.visual_id ?? "contact_core";
    }

    void OnDestroy()
    {
      DestroySpriteMaterial(_innerCore);
      DestroySpriteMaterial(_halo);
      foreach (var particle in _orbitParticles)
        DestroySpriteMaterial(particle);
      foreach (var node in _pathNodes)
        node?.Destroy();
      DestroyLineMaterial(_outerRing);
      DestroyLineMaterial(_secondRing);
      DestroyLineMaterial(_microTrail);
      DestroyLineMaterial(_fireRing);
      DestroyLineMaterial(_turnArc);
      DestroyLineMaterial(_pathNetwork);
      for (var i = 0; i < _beams.Length; i++)
      {
        DestroyLineMaterial(_beams[i]);
        DestroyLineMaterial(_beamCores[i]);
        DestroyLineMaterial(_beamGlows[i]);
      }
      if (_turnArc != null)
        Destroy(_turnArc.gameObject);
      if (_pathNetwork != null)
        Destroy(_pathNetwork.gameObject);
    }

    static void DestroyLineMaterial(Component component)
    {
      if (component == null)
        return;
      var line = component as LineRenderer ?? component.GetComponent<LineRenderer>();
      if (line != null && line.material != null)
        Destroy(line.material);
      var trail = component as TrailRenderer ?? component.GetComponent<TrailRenderer>();
      if (trail != null && trail.material != null)
        Destroy(trail.material);
    }

    static void DrawLocalCircle(LineRenderer line, float radius, int segments)
    {
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    static void DrawDashedCircle(LineRenderer line, float radius, int segments)
    {
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        var dashRadius = i % 3 == 0 ? radius * 0.96f : radius;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * dashRadius, Mathf.Sin(angle) * dashRadius, 0f));
      }
    }

    void UpdateOrbitParticles(bool active, WeaponCorePalette palette, int visualLevel, float breath)
    {
      for (var i = 0; i < _orbitParticles.Length; i++)
      {
        var particle = _orbitParticles[i];
        if (particle == null)
          continue;
        particle.enabled = active;
        if (!active)
          continue;
        var angle = Time.unscaledTime * (58f + visualLevel * 4f) * Mathf.Deg2Rad + i * Mathf.PI * 0.5f;
        var radius = 0.24f + breath * 0.03f;
        particle.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.02f);
        particle.transform.localScale = Vector3.one * (0.055f + breath * 0.018f);
        LaserVfxShared.SetSpriteColor(particle,
          new Color(palette.Core.r, palette.Core.g, palette.Core.b, Mathf.Clamp01(0.44f + breath * 0.24f)));
      }
    }

    MotionSample ResolveMotion(float deltaTime)
    {
      var position = transform.position;
      if (!_hasLastPosition || deltaTime <= 0.0001f)
      {
        _hasLastPosition = true;
        _lastPosition = position;
        _lastVelocity = Vector3.zero;
        return new MotionSample(Vector3.zero, 0f, 0f);
      }

      var velocity = (position - _lastPosition) / deltaTime;
      var speed = velocity.magnitude;
      var turn = 0f;
      if (_lastVelocity.sqrMagnitude > 0.02f && velocity.sqrMagnitude > 0.02f)
        turn = Vector3.Angle(_lastVelocity, velocity);
      _lastPosition = position;
      _lastVelocity = velocity;
      return new MotionSample(velocity, speed, turn);
    }

    void UpdatePathNodes(bool active, WeaponCorePalette palette, int visualLevel, float speedFactor, MotionSample motion)
    {
      var maxNodes = visualLevel >= 10 ? 8 : visualLevel >= 7 ? 6 : 5;
      var lifetime = Mathf.Lerp(0.32f, 0.78f, Mathf.Clamp01((visualLevel - 3f) / 7f));
      var deltaTime = Time.unscaledDeltaTime;
      var nodeAlpha = Mathf.Lerp(0.16f, 0.34f, speedFactor) * (visualLevel >= 10 ? 1.2f : 1f);

      if (active && motion.Speed > 0.45f)
      {
        _pathNodeTimer -= deltaTime;
        var interval = Mathf.Lerp(0.12f, 0.06f, Mathf.Max(speedFactor, visualLevel >= 7 ? 0.55f : 0f));
        if (_pathNodeTimer <= 0f)
        {
          _pathNodeTimer = interval;
          var node = _pathNodes[_nextPathNode];
          _nextPathNode = (_nextPathNode + 1) % maxNodes;
          node.Play(transform.position, lifetime, 0.13f + speedFactor * 0.05f, nodeAlpha, palette.Core);
        }
      }

      for (var i = 0; i < _pathNodes.Length; i++)
      {
        if (i >= maxNodes)
        {
          _pathNodes[i].Disable();
          continue;
        }
        _pathNodes[i].Tick(deltaTime);
      }

      UpdatePathNetwork(active && visualLevel >= 10, palette);
    }

    void UpdateTurnArc(bool active, WeaponCorePalette palette, int visualLevel, float speedFactor, MotionSample motion)
    {
      if (active && motion.TurnAngle >= 42f && motion.Speed > 1.8f)
      {
        _turnArcAge = 0.18f;
        var position = transform.position;
        var back = motion.Velocity.sqrMagnitude > 0.001f ? -motion.Velocity.normalized : Vector3.left;
        var side = new Vector3(-back.y, back.x, 0f) * (motion.TurnAngle > 90f ? 1f : 0.55f);
        _turnArc.SetPosition(0, position + back * 0.52f);
        _turnArc.SetPosition(1, position + (back + side).normalized * 0.34f);
        _turnArc.SetPosition(2, position + side.normalized * 0.24f);
        _turnArc.SetPosition(3, position);
      }

      if (_turnArcAge <= 0f)
      {
        _turnArc.enabled = false;
        return;
      }

      _turnArcAge -= Time.unscaledDeltaTime;
      var t = Mathf.Clamp01(_turnArcAge / 0.18f);
      _turnArc.enabled = true;
      _turnArc.startWidth = _turnArc.endWidth = Mathf.Lerp(0.02f, 0.07f, t) * (visualLevel >= 5 ? 1.15f : 1f);
      var alpha = Mathf.Clamp01(t * (0.22f + speedFactor * 0.18f));
      var color = new Color(palette.Core.r, palette.Core.g, palette.Core.b, alpha);
      LaserVfxShared.SetLineColor(_turnArc, color, color);
    }

    void UpdateFireRing(bool active, WeaponCorePalette palette)
    {
      if (!active || _fireFlashAge <= 0f)
      {
        _fireRing.enabled = false;
        _fireFlashAge = Mathf.Max(0f, _fireFlashAge - Time.unscaledDeltaTime);
        return;
      }

      _fireFlashAge -= Time.unscaledDeltaTime;
      var t = 1f - Mathf.Clamp01(_fireFlashAge / 0.18f);
      var alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
      _fireRing.enabled = true;
      var fireColor = new Color(palette.Core.r, palette.Core.g, palette.Core.b, alpha * 0.72f);
      LaserVfxShared.SetLineColor(_fireRing, fireColor, fireColor);
      _fireRing.startWidth = _fireRing.endWidth = 0.075f * alpha;
      _fireRing.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.55f, t);
    }

    void UpdatePathNetwork(bool active, WeaponCorePalette palette)
    {
      if (!active)
      {
        _pathNetwork.enabled = false;
        _pathNetwork.positionCount = 0;
        return;
      }

      var count = 0;
      for (var i = 0; i < _pathNodes.Length; i++)
      {
        if (_pathNodes[i].Active)
          count++;
      }
      if (count < 2)
      {
        _pathNetwork.enabled = false;
        _pathNetwork.positionCount = 0;
        return;
      }

      _pathNetwork.enabled = true;
      _pathNetwork.positionCount = count;
      var write = 0;
      for (var i = 0; i < _pathNodes.Length; i++)
      {
        if (!_pathNodes[i].Active)
          continue;
        _pathNetwork.SetPosition(write++, _pathNodes[i].Position);
      }
      _pathNetwork.startWidth = 0.022f;
      _pathNetwork.endWidth = 0.008f;
      LaserVfxShared.SetLineColor(_pathNetwork,
        new Color(palette.Core.r, palette.Core.g, palette.Core.b, 0.18f),
        new Color(palette.Core.r, palette.Core.g, palette.Core.b, 0.02f));
    }

    static int ResolveVisualLevel(bool laser, bool missile, bool explosion, bool pulseCore, bool boomerang, bool trail)
    {
      if (boomerang)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_boomerang_tier")) * 2, 1, 10);
      if (laser)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_laser_tier")) * 2, 1, 10);
      if (missile)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_missile_tier")) * 2, 1, 10);
      if (explosion)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_explosion_tier")) * 2, 1, 10);
      if (pulseCore)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_pulse_tier")) * 2, 1, 10);
      if (trail)
        return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_trail_tier")) * 2, 1, 10);
      return Mathf.Clamp(Mathf.RoundToInt(RunBuildState.GetStat("detached_contact_level")), 1, 10);
    }

    static float ResolveRingSpeed(bool laser, bool missile, bool explosion, bool pulseCore, bool boomerang, bool trail)
    {
      if (laser)
        return 34f;
      if (missile)
        return 40f;
      if (explosion)
        return -28f;
      if (pulseCore)
        return 24f;
      if (boomerang)
        return -46f;
      if (trail)
        return 38f;
      return 26f;
    }

    static WeaponCorePalette ResolvePalette(bool laser, bool missile, bool explosion, bool pulseCore, bool boomerang, bool trail)
    {
      if (laser)
        return new WeaponCorePalette(
          new Color(1f, 1f, 1f),
          new Color(1f, 1f, 1f),
          new Color(1f, 1f, 1f));
      if (missile)
        return new WeaponCorePalette(new Color(0.62f, 0.92f, 1f), new Color(0.18f, 0.72f, 0.98f), new Color(0.06f, 0.48f, 0.82f));
      if (explosion)
        return new WeaponCorePalette(new Color(1f, 0.84f, 0.2f), new Color(1f, 0.52f, 0.06f), new Color(0.95f, 0.18f, 0.04f));
      if (pulseCore)
        return new WeaponCorePalette(new Color(0.62f, 0.88f, 1f), new Color(0.52f, 0.22f, 1f), new Color(0.28f, 0.04f, 0.78f));
      if (boomerang)
        return new WeaponCorePalette(new Color(1f, 0.88f, 0.35f), new Color(1f, 0.58f, 0.12f), new Color(0.92f, 0.38f, 0.06f));
      if (trail)
        return new WeaponCorePalette(new Color(0.56f, 0.9f, 0.68f), new Color(0.22f, 0.62f, 0.38f), new Color(0.1f, 0.38f, 0.24f));
      return new WeaponCorePalette(new Color(0.78f, 0.96f, 1f), new Color(0.42f, 0.78f, 1f), new Color(0.08f, 0.42f, 0.92f));
    }

    static void DestroySpriteMaterial(SpriteRenderer sprite)
    {
      if (sprite != null && sprite.material != null)
        Destroy(sprite.material);
    }

    readonly struct WeaponCorePalette
    {
      public readonly Color Core;
      public readonly Color Ring;
      public readonly Color Halo;

      public WeaponCorePalette(Color core, Color ring, Color halo)
      {
        Core = core;
        Ring = ring;
        Halo = halo;
      }
    }

    readonly struct MotionSample
    {
      public readonly Vector3 Velocity;
      public readonly float Speed;
      public readonly float TurnAngle;

      public MotionSample(Vector3 velocity, float speed, float turnAngle)
      {
        Velocity = velocity;
        Speed = speed;
        TurnAngle = turnAngle;
      }
    }

    sealed class PathNode
    {
      readonly GameObject _root;
      readonly SpriteRenderer _sprite;
      float _age;
      float _duration;
      float _baseScale;
      float _baseAlpha;
      Color _color;

      public bool Active => _root != null && _root.activeSelf;
      public Vector3 Position => _root != null ? _root.transform.position : Vector3.zero;

      public PathNode(GameObject root, SpriteRenderer sprite)
      {
        _root = root;
        _sprite = sprite;
      }

      public void Play(Vector3 position, float duration, float scale, float alpha, Color color)
      {
        _age = 0f;
        _duration = Mathf.Max(0.05f, duration);
        _baseScale = scale;
        _baseAlpha = alpha;
        _color = color;
        _root.transform.position = position;
        _root.transform.localScale = Vector3.one * scale;
        _root.SetActive(true);
        Tick(0f);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var alpha = _baseAlpha * (1f - Mathf.SmoothStep(0f, 1f, t));
        var scale = _baseScale * Mathf.Lerp(1f, 1.55f, t);
        _root.transform.localScale = Vector3.one * scale;
        LaserVfxShared.SetSpriteColor(_sprite, new Color(_color.r, _color.g, _color.b, alpha));
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void Disable()
      {
        if (_root != null)
          _root.SetActive(false);
      }

      public void Destroy()
      {
        if (_sprite != null && _sprite.material != null)
          Object.Destroy(_sprite.material);
        if (_root != null)
          Object.Destroy(_root);
      }
    }
  }
}
