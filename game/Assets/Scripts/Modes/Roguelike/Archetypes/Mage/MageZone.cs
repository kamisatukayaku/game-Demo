using UnityEngine;
using System.Collections.Generic;

using Game.Shared.Core;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Gameplay.Events;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Player;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  [DisallowMultipleComponent]
  public class MageZone : MonoBehaviour
  {
    static readonly List<MageZone> ActiveZones = new();
    public const float TickInterval = 0.25f;
    const float DepthZ = -0.05f;

    Transform _caster;
    Vector2 _center;
    float _radius;
    float _remaining;
    float _tickTimer;
    float _rampTime;
    float _baseDamage;
    SkillContext _ctx;
    ISkillSystem _skills;
    MageZoneVisual _visual;
    readonly MageZoneEffectRunner _effects = new();

    public static MageZone Spawn(Transform caster, Vector2 center, float radius, float duration, float baseDamage)
    {
      var zone = MageZonePool.Acquire();
      zone.Activate(caster, center, radius, duration, baseDamage);
      return zone;
    }

    public static bool Contains(Vector2 position)
    {
      foreach (var zone in ActiveZones)
      {
        if (zone != null && zone.gameObject.activeSelf
            && Vector2.Distance(position, zone._center) <= zone._radius)
          return true;
      }
      return false;
    }

    public static void ResetActiveZones()
    {
      for (var i = ActiveZones.Count - 1; i >= 0; i--)
      {
        var zone = ActiveZones[i];
        if (zone != null)
          zone.Shutdown();
      }
      ActiveZones.Clear();
      ChargeDashInfluenceLocator.Clear();
    }

    public static Vector2 GetChargeDashPullOffset(Vector2 position, float deltaTime)
    {
      if (deltaTime <= 0f)
        return Vector2.zero;

      var total = Vector2.zero;
      foreach (var zone in ActiveZones)
      {
        if (zone == null || !zone.gameObject.activeSelf || !zone._ctx.GravityDashPull)
          continue;

        var toCenter = zone._center - position;
        var dist = toCenter.magnitude;
        if (dist > zone._radius || dist < 0.08f)
          continue;

        var t = 1f - Mathf.Clamp01(dist / zone._radius);
        var pullSpeed = (2.4f + zone._ctx.SkillVacuumStrength * 4f) * Mathf.Lerp(0.45f, 1f, t);
        total += toCenter.normalized * Mathf.Min(dist, pullSpeed * deltaTime);
      }

      return total;
    }

    public void Activate(Transform caster, Vector2 center, float radius, float duration, float baseDamage)
    {
      ChargeDashInfluenceLocator.Register(MageChargeDashInfluenceProvider.Instance);
      _ctx = MageSystemLocator.Context;
      _skills = MageSystemLocator.System;
      _caster = caster;
      _center = center;
      _radius = radius;
      _remaining = Mathf.Max(0.5f, duration);
      _baseDamage = baseDamage;
      _tickTimer = 0f;
      _rampTime = 0f;
      _visual ??= new MageZoneVisual(transform);
      _visual.EnsureBuilt();
      transform.position = new Vector3(center.x, center.y, DepthZ);
      gameObject.SetActive(true);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageGravityWell",
        transform.position,
        caster != null ? caster.gameObject : null,
        radius,
        _remaining));
      if (!ActiveZones.Contains(this))
        ActiveZones.Add(this);
    }

    public void Shutdown()
    {
      _visual?.Shutdown();
      ActiveZones.Remove(this);
      gameObject.SetActive(false);
    }

    void Update()
    {
      _ctx = MageSystemLocator.Context;
      if (_ctx.MovingGravityWell)
      {
        var aim = PlayerAimController.Instance?.AimWorldPoint ?? GameplayPlane.Position2D(_caster);
        var delta = aim - _center;
        if (delta.sqrMagnitude > 0.0001f)
        {
          var step = delta.normalized * Mathf.Min(delta.magnitude, 3.6f * Time.deltaTime);
          _center += step;
          transform.position = new Vector3(_center.x, _center.y, DepthZ);
        }
      }

      var overlapAmp = ComputeOverlapPullAmp(_center);
      _effects.UpdatePullMotion(_center, _radius, _ctx, Time.deltaTime, overlapAmp);

      _remaining -= Time.deltaTime;
      if (_remaining <= 0f)
      {
        GameEventBus.Publish(new TriggerActivatedEvent(
          "MageGravityWellEnd",
          transform.position,
          _caster != null ? _caster.gameObject : null,
          _radius));
        _effects.Collapse(_caster, _center, _radius, _baseDamage, _ctx, _skills);
        MageZonePool.Release(this);
        return;
      }

      _tickTimer -= Time.deltaTime;
      if (_tickTimer > 0f)
        return;

      _tickTimer = TickInterval;
      _effects.TickPull(_caster, _center, _radius, _baseDamage, _ctx, _skills, TickInterval, ref _rampTime, overlapAmp);
      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageGravityPulse",
        transform.position,
        _caster != null ? _caster.gameObject : null,
        _radius));
      _visual?.Update(transform.position, _radius);
    }

    static float ComputeOverlapPullAmp(Vector2 position)
    {
      var count = 0;
      foreach (var zone in ActiveZones)
      {
        if (zone == null || !zone.gameObject.activeSelf)
          continue;
        if (zone._ctx.SkillVacuumOverlapAmp <= 0.5f)
          continue;
        if (Vector2.Distance(position, zone._center) <= zone._radius)
          count++;
      }

      return count >= 2 ? 1.35f : 1f;
    }

    sealed class MageChargeDashInfluenceProvider : IChargeDashInfluenceProvider
    {
      public static readonly MageChargeDashInfluenceProvider Instance = new();

      MageChargeDashInfluenceProvider() { }

      public Vector2 GetDashOffset(GameObject enemy, Vector2 currentPosition, Vector2 dashDirection, float deltaTime) =>
        GetChargeDashPullOffset(currentPosition, deltaTime);
    }
  }
}
