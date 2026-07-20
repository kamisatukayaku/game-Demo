using UnityEngine;

namespace Game.Shared.Laser
{
  /// <summary>
  /// 橙红激光：Shader Graph / URP Additive 双层 Sprite 光束 + 方形粒子?
  /// </summary>
  public static class LaserBeamParticleEffect
  {
    public static GameObject SpawnCharge(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      float beamHalfWidth,
      Color color,
      float duration,
      int layer = -1)
    {
      direction.z = 0f;
      if (direction.sqrMagnitude < 0.0001f)
        direction = Vector3.right;
      else
        direction.Normalize();

      var root = new GameObject("LaserChargeTelegraphVfx");
      root.transform.position = new Vector3(origin.x, origin.y, LaserVfxShared.VfxDepthZ - 0.01f);
      root.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);

      var warn = LaserVfxShared.CreateBeamSprite(
        root.transform,
        "ChargeNeedle",
        new Color(color.r, color.g, color.b, 0.42f),
        maxRange,
        Mathf.Max(0.035f, beamHalfWidth * 0.22f),
        sortingOrder: 19);
      var focus = LaserVfxShared.CreateBeamSprite(
        root.transform,
        "ChargeFocus",
        new Color(1f, 1f, 1f, 0.2f),
        maxRange * 0.34f,
        Mathf.Max(0.08f, beamHalfWidth * 0.55f),
        sortingOrder: 20);
      focus.transform.localPosition = new Vector3(maxRange * 0.17f, 0f, 0f);

      SpawnChargeParticles(root.transform, beamHalfWidth, color, duration);

      if (layer >= 0)
        SetLayerRecursive(root, layer);

      var charge = root.AddComponent<LaserChargeTelegraphVisual>();
      charge.Init(duration, warn, focus, Mathf.Max(0.035f, beamHalfWidth * 0.22f));
      return root;
    }

    public static GameObject Spawn(
      Vector3 origin,
      Vector3 direction,
      float maxRange,
      float beamHalfWidth,
      Color coreColor,
      float duration = 0.35f,
      int layer = -1)
    {
      direction.z = 0f;
      if (direction.sqrMagnitude < 0.0001f)
        direction = Vector3.right;
      else
        direction.Normalize();

      var root = new GameObject("LaserBeamVfx");
      root.transform.position = new Vector3(origin.x, origin.y, LaserVfxShared.VfxDepthZ);
      root.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);

      var isWhiteBeam = coreColor.r > 0.92f && coreColor.g > 0.92f && coreColor.b > 0.92f;
      var glowTint = isWhiteBeam
        ? new Color(0.86f, 0.96f, 1f, 0.5f)
        : Color.Lerp(LaserVfxShared.BeamGlowColor, coreColor, 0.35f);
      var tailTint = isWhiteBeam
        ? new Color(0.55f, 0.75f, 1f, 0.28f)
        : Color.Lerp(LaserVfxShared.BeamTailColor, coreColor, 0.2f);

      SpawnBeamSprites(root.transform, maxRange, beamHalfWidth, glowTint, tailTint);
      SpawnBeamFragmentParticles(root.transform, maxRange, beamHalfWidth, coreColor);
      SpawnOriginFlare(root.transform, beamHalfWidth, coreColor);
      SpawnImpactFlare(root.transform, maxRange, beamHalfWidth, coreColor);

      if (layer >= 0)
        SetLayerRecursive(root, layer);

      var fade = root.AddComponent<LaserBeamVisual>();
      fade.Init(duration);
      return root;
    }

    static void SpawnChargeParticles(Transform parent, float halfWidth, Color coreColor, float duration)
    {
      var go = new GameObject("ChargeConvergeParticles");
      go.transform.SetParent(parent, false);
      go.transform.localPosition = Vector3.zero;

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = Mathf.Max(0.1f, duration);
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.9f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 48;
      main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 1f, 1f, 0.95f),
        new Color(coreColor.r, coreColor.g, coreColor.b, 0.78f));

      var emission = ps.emission;
      emission.rateOverTime = 46f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = Mathf.Clamp(halfWidth * 3.6f, 0.24f, 0.72f);
      shape.radiusThickness = 0.8f;

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.radial = new ParticleSystem.MinMaxCurve(-4.5f, -1.6f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      var grad = new Gradient();
      grad.SetKeys(
        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(coreColor, 1f) },
        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.18f), new GradientAlphaKey(0f, 1f) });
      col.color = grad;

      var renderer = go.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySquareParticleRenderer(renderer, 21);
      ps.Play();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
      go.layer = layer;
      foreach (Transform child in go.transform)
        SetLayerRecursive(child.gameObject, layer);
    }

    static void SpawnBeamSprites(Transform parent, float length, float halfWidth, Color glowColor, Color tailColor)
    {
      var glow = LaserVfxShared.CreateBeamSprite(
        parent, "BeamGlow", glowColor, length, halfWidth * 2.8f, sortingOrder: 18);

      var coreStart = Color.Lerp(LaserVfxShared.BeamCoreColor, glowColor, 0.05f);
      var core = LaserVfxShared.CreateBeamSprite(
        parent, "BeamCore", coreStart, length, Mathf.Max(0.08f, halfWidth * 0.65f), sortingOrder: 20);

      var tail = tailColor;
      tail.a *= 0.85f;
      LaserVfxShared.SetSpriteColor(glow, glowColor);
      LaserVfxShared.SetSpriteColor(core, coreStart);
    }

    static void SpawnBeamFragmentParticles(Transform parent, float length, float halfWidth, Color coreColor)
    {
      var go = new GameObject("BeamSquareFragments");
      go.transform.SetParent(parent, false);
      go.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.32f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.38f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.6f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 120;
      main.startColor = new ParticleSystem.MinMaxGradient(
        Color.white,
        Color.Lerp(coreColor, LaserVfxShared.BeamGlowColor, 0.35f));

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      var burstCount = (short)Mathf.Clamp((int)(length * 22f), 36, 96);
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Box;
      shape.scale = new Vector3(length, halfWidth * 2.8f, 0.1f);

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.4f);
      vel.y = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      var warm = Color.Lerp(coreColor, LaserVfxShared.BeamGlowColor, 0.4f);
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(Color.white, 0f),
          new GradientColorKey(warm, 0.5f),
          new GradientColorKey(warm, 1f)
        },
        new[]
        {
          new GradientAlphaKey(1f, 0f),
          new GradientAlphaKey(0.7f, 0.55f),
          new GradientAlphaKey(0f, 1f)
        });
      col.color = grad;

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.08f);

      var renderer = go.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySquareParticleRenderer(renderer, 22);
      ps.Play();
    }

    static void SpawnOriginFlare(Transform parent, float halfWidth, Color coreColor) =>
      SpawnPointBurst(parent, Vector3.zero, halfWidth * 1.2f, coreColor, 24, 24);

    static void SpawnImpactFlare(Transform parent, float length, float halfWidth, Color coreColor) =>
      SpawnPointBurst(parent, new Vector3(length, 0f, 0f), halfWidth, coreColor, 18, 24);

    static void SpawnPointBurst(
      Transform parent,
      Vector3 localPos,
      float radius,
      Color coreColor,
      int burstCount,
      int sortingOrder)
    {
      var go = new GameObject("BeamPointBurst");
      go.transform.SetParent(parent, false);
      go.transform.localPosition = localPos;

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = false;
      main.duration = 0.14f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 3f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 90f * Mathf.Deg2Rad);
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.startColor = new ParticleSystem.MinMaxGradient(Color.white, coreColor);
      main.maxParticles = burstCount + 12;

      var emission = ps.emission;
      emission.rateOverTime = 0f;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = radius;
      shape.radiusThickness = 0.7f;

      var col = ps.colorOverLifetime;
      col.enabled = true;
      var grad = new Gradient();
      grad.SetKeys(
        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(coreColor, 0.4f) },
        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
      col.color = grad;

      var renderer = go.GetComponent<ParticleSystemRenderer>();
      LaserVfxShared.ApplySquareParticleRenderer(renderer, sortingOrder);
      ps.Play();
    }

    sealed class LaserChargeTelegraphVisual : MonoBehaviour
    {
      SpriteRenderer _needle;
      SpriteRenderer _focus;
      float _duration;
      float _elapsed;
      float _baseWidth;

      public void Init(float duration, SpriteRenderer needle, SpriteRenderer focus, float baseWidth)
      {
        _duration = Mathf.Max(0.05f, duration);
        _needle = needle;
        _focus = focus;
        _baseWidth = baseWidth;
      }

      void Update()
      {
        _elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_elapsed / _duration);
        var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 38f);
        var alpha = Mathf.Lerp(0.22f, 0.75f, t) * (0.72f + pulse * 0.28f);

        if (_needle != null)
        {
          var c = Color.white;
          c.a = alpha;
          LaserVfxShared.SetSpriteColor(_needle, c);
          var scale = _needle.transform.localScale;
          scale.y = Mathf.Max(0.001f, _baseWidth * Mathf.Lerp(0.75f, 1.75f, t));
          _needle.transform.localScale = scale;
        }

        if (_focus != null)
        {
          var c = new Color(0.86f, 0.96f, 1f, Mathf.Lerp(0.12f, 0.38f, t) * (0.8f + pulse * 0.2f));
          LaserVfxShared.SetSpriteColor(_focus, c);
        }

        if (_elapsed >= _duration)
          Destroy(gameObject);
      }
    }
  }
}
