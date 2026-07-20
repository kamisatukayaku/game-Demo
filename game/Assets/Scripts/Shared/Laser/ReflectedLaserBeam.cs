using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 反弹激光：与敌人激光相?VFX 栈（光束/发射?粒子?命中），配色改为金色?
  /// 持续存在直到源激光结束或护盾收盾；每?Refresh 更新起点与方向?
  /// </summary>
  [DisallowMultipleComponent]
  public class ReflectedLaserBeam : MonoBehaviour
  {
    Vector3 _origin;
    Vector3 _direction;
    LaserBeamSettings _settings;
    DamageRequest _request;

    LaserBeamView _view;
    LaserEmitterEffect _emitter;
    LaserRainEffect _rain;
    LaserHitEffect _hitEffect;

    HashSet<int> _damaged = new();
    float _tickTimer;

    public static ReflectedLaserBeam Spawn(
      Vector3 origin,
      Vector3 direction,
      in LaserBeamSettings settings,
      in DamageRequest request)
    {
      var go = new GameObject("ReflectedLaserBeam");
      var beam = go.AddComponent<ReflectedLaserBeam>();
      beam._origin = origin;
      beam._direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
      beam._settings = settings;
      beam._request = request;
      beam._tickTimer = 0f;
      beam.EnsureBuilt();
      beam.BeginEffects();
      return beam;
    }

    public void Refresh(Vector3 newOrigin, Vector3 newDirection, in LaserBeamSettings settings)
    {
      _origin = newOrigin;
      _direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : _direction;
      _settings = settings;
      RefreshVisuals();
    }

    public void Stop()
    {
      _view?.SetVisible(false);
      _emitter?.End();
      _rain?.End();
      _hitEffect?.End();
      Destroy(gameObject);
    }

    void EnsureBuilt()
    {
      if (_view == null)
      {
        var viewGo = new GameObject("BeamView");
        viewGo.transform.SetParent(transform, false);
        _view = viewGo.AddComponent<LaserBeamView>();
      }

      if (_emitter == null)
      {
        var emitterGo = new GameObject("EmitterEffect");
        emitterGo.transform.SetParent(transform, false);
        _emitter = emitterGo.AddComponent<LaserEmitterEffect>();
      }

      if (_rain == null)
      {
        var rainGo = new GameObject("RainEffect");
        rainGo.transform.SetParent(transform, false);
        _rain = rainGo.AddComponent<LaserRainEffect>();
      }

      if (_hitEffect == null)
      {
        var hitGo = new GameObject("HitEffect");
        hitGo.transform.SetParent(transform, false);
        _hitEffect = hitGo.AddComponent<LaserHitEffect>();
      }

      _view.EnsureBuilt();
      _emitter.EnsureBuilt();
      _rain.EnsureBuilt();
      _hitEffect.EnsureBuilt();
      ApplyGoldenParticleTint(_rain.gameObject);
      ApplyGoldenParticleTint(_hitEffect.gameObject);
    }

    void BeginEffects()
    {
      _view.ApplySettings(_settings);
      _view.SetVisible(true);
      _emitter.Begin(_origin);
      TintEmitterGolden();
      _rain.Begin(_settings.CoreWidth);
      _hitEffect.BeginAt(_origin + _direction * _settings.MaxRange);
      RefreshVisuals();
      _view.UpdatePulse(Time.time);
      ApplyDamageTick();
    }

    void RefreshVisuals()
    {
      var end = _origin + _direction * _settings.MaxRange;
      _view.SetEndpoints(_origin, end);
      _view.UpdatePulse(Time.time);
      _emitter.SyncTo(_origin);
      _rain.SyncBeam(_origin, end);
      _hitEffect?.SyncTo(end);
    }

    void Update()
    {
      _tickTimer += Time.deltaTime;
      if (_tickTimer >= Mathf.Max(0.05f, _settings.DamageTickInterval))
      {
        _tickTimer -= _settings.DamageTickInterval;
        ApplyDamageTick();
      }
    }

    void ApplyDamageTick()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var halfWidth = _settings.CoreWidth * 0.8f;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null) continue;
        var id = enemy.GetInstanceID();
        if (_damaged.Contains(id)) continue;

        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead) continue;

        if (!PlayerLaserBeam.IsOnBeam(_origin, _direction, _settings.MaxRange, halfWidth, enemy.transform.position))
          continue;

        DamagePipeline.Apply(_request, health);
        _damaged.Add(id);
      }
    }

    static void ApplyGoldenParticleTint(GameObject root)
    {
      if (root == null)
        return;

      foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
      {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(
          new Color(1f, 0.9f, 0.35f, 1f),
          new Color(1f, 0.72f, 0.08f, 0.92f));
      }
    }

    void TintEmitterGolden()
    {
      foreach (var sr in _emitter.GetComponentsInChildren<SpriteRenderer>(true))
        LaserVfxShared.SetSpriteColor(sr, new Color(1f, 0.86f, 0.28f, 1f));
    }
  }
}