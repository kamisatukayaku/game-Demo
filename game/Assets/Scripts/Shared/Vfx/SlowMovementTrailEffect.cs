using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Laser;
namespace Game.Shared.Vfx
{
  /// <summary>
  /// 减速移动拖尾：怪物被减速且移动时脚下出现黄色线条（LoL 风格）?
  /// </summary>
  [DisallowMultipleComponent]
  public class SlowMovementTrailEffect : MonoBehaviour
  {
    const int SortOrder = 14;
    const float DepthZ = -0.06f;

    static readonly Color TrailCyan = new(0.72f, 0.95f, 1f, 0.95f);
    static readonly Color TrailBlue = new(0.18f, 0.58f, 1f, 0.75f);

    Transform _anchor;
    ParticleSystem _streaks;
    bool _active;
    Vector2 _moveDir = Vector2.down;

    public static SlowMovementTrailEffect Ensure(GameObject owner)
    {
      if (owner == null)
        return null;

      var existing = owner.GetComponent<SlowMovementTrailEffect>();
      if (existing != null)
        return existing;

      return owner.AddComponent<SlowMovementTrailEffect>();
    }

    void Awake() => EnsureBuilt();

    public void EnsureBuilt()
    {
      if (_streaks != null)
        return;

      _anchor = new GameObject("SlowTrailAnchor").transform;
      _anchor.SetParent(transform, false);
      _streaks = BuildStreakParticles();
    }

    public void Begin(Vector2 moveDirection)
    {
      EnsureBuilt();
      _active = true;
      _moveDir = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.down;
      SyncAnchor();

      if (_streaks.isPlaying)
        return;

      _streaks.Clear(true);
      _streaks.Play(true);
    }

    public void SyncDirection(Vector2 moveDirection)
    {
      if (moveDirection.sqrMagnitude > 0.0001f)
        _moveDir = moveDirection.normalized;
    }

    public void End()
    {
      _active = false;
      _streaks?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Update()
    {
      if (!_active)
        return;

      SyncAnchor();
    }

    void SyncAnchor()
    {
      if (_anchor == null)
        return;

      var pos = LaserVfxShared.GetOwnerEmissionPoint(transform);
      pos.z = DepthZ;
      _anchor.position = pos;

      var angle = Mathf.Atan2(_moveDir.y, _moveDir.x) * Mathf.Rad2Deg;
      _anchor.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
    }

    ParticleSystem BuildStreakParticles()
    {
      var go = new GameObject("SlowTrailStreaks");
      go.transform.SetParent(_anchor, false);
      go.transform.localPosition = new Vector3(0.06f, 0f, 0f);

      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = true;
      main.duration = 1f;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.045f);
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, 0f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = 48;
      main.startColor = new ParticleSystem.MinMaxGradient(TrailCyan, TrailBlue);

      var emission = ps.emission;
      emission.rateOverTime = 38f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Box;
      shape.scale = new Vector3(0.22f, 0.08f, 0.01f);
      shape.rotation = new Vector3(0f, 0f, 0f);

      var vel = ps.velocityOverLifetime;
      vel.enabled = true;
      vel.space = ParticleSystemSimulationSpace.Local;
      vel.x = new ParticleSystem.MinMaxCurve(-0.15f, -0.55f);
      vel.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
      vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

      var col = ps.colorOverLifetime;
      col.enabled = true;
      col.color = BuildTrailGradient();

      var sizeOverLife = ps.sizeOverLifetime;
      sizeOverLife.enabled = true;
      sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, 0.15f);

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      renderer.renderMode = ParticleSystemRenderMode.Stretch;
      renderer.lengthScale = 2.8f;
      renderer.velocityScale = 0.08f;
      LaserVfxShared.ApplySharedParticleRenderer(renderer, SortOrder);
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      return ps;
    }

    static ParticleSystem.MinMaxGradient BuildTrailGradient()
    {
      var grad = new Gradient();
      grad.SetKeys(
        new[]
        {
          new GradientColorKey(new Color(0.82f, 0.98f, 1f), 0f),
          new GradientColorKey(new Color(0.28f, 0.68f, 1f), 0.45f),
          new GradientColorKey(new Color(0.08f, 0.32f, 0.9f), 1f)
        },
        new[]
        {
          new GradientAlphaKey(0.95f, 0f),
          new GradientAlphaKey(0.55f, 0.55f),
          new GradientAlphaKey(0f, 1f)
        });
      return new ParticleSystem.MinMaxGradient(grad);
    }
  }
}
