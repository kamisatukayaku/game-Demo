using System.Collections.Generic;
using System;
using UnityEngine;

using Game.Shared.Combat.Damage;
using Game.Shared.Enemy.AI;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Gameplay.Reflect;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 怪物持续型激光：从怪物 Transform.position 连接到目标中心，持续追踪并周期性造成伤害?
  /// 视觉：白?LineRenderer + 发射点粒?+ 沿束粒子?+ 命中溅射?
  /// </summary>
  [DisallowMultipleComponent]
  public class LaserEnemyAttack : MonoBehaviour
  {
    Transform _owner;
    Transform _target;
    LaserBeamSettings _settings;
    DamageRequest _request;
    Action<Health> _onFirstHit;

    LaserBeamView _view;
    LaserEmitterEffect _emitter;
    LaserRainEffect _rain;
    LaserHitEffect _hitEffect;

    float _remaining;
    float _tickTimer;
    bool _running;
    bool _firstHitDone;
    int _layer = -1;
    const float OffScreenMargin = 1.5f;
    static Camera s_cachedCamera;

    /// <summary>发射瞬间锁定的方向（发射后不再追踪目标）?/summary>
    Vector3 _firingDirection;
    float _firingMaxRange;

    public bool IsRunning => _running;

    public static event Action<Vector3> Fired;

    public LaserBeamSettings CurrentSettings => _settings;

    /// <summary>获取当前激光束的起点和方向（用于护盾反弹检测）?/summary>
    public void GetBeamData(out Vector3 origin, out Vector3 direction, out float maxRange)
    {
      origin = _owner != null ? LaserVfxShared.GetOwnerEmissionPoint(_owner) : Vector3.zero;
      direction = _firingDirection;
      maxRange = _firingMaxRange;
    }

    public void EnsureBuilt()
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
    }

    public void Begin(
      Transform owner,
      Transform target,
      LaserBeamSettings settings,
      in DamageRequest request,
      Action<Health> onFirstHit = null,
      int layer = -1,
      Vector2? lockedFireDirection = null)
    {
      if (owner == null || target == null)
        return;

      EnsureBuilt();

      _owner = owner;
      _target = target;
      _settings = settings;
      _request = request;
      _onFirstHit = onFirstHit;
      _layer = layer;
      _remaining = Mathf.Max(0.05f, settings.Duration);
      _tickTimer = 0f;
      _running = true;
      _firstHitDone = false;

      var startPt = LaserVfxShared.GetOwnerEmissionPoint(owner);
      if (lockedFireDirection.HasValue && lockedFireDirection.Value.sqrMagnitude > 0.0001f)
        _firingDirection = new Vector3(lockedFireDirection.Value.x, lockedFireDirection.Value.y, 0f).normalized;
      else
      {
        var toTarget = target.position - startPt;
        toTarget.z = 0f;
        _firingDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;
      }

      _firingMaxRange = settings.MaxRange;

      gameObject.SetActive(true);
      ApplyLayerRecursive(gameObject);

      _view.ApplySettings(settings);
      _view.SetVisible(true);
      _emitter.Begin(LaserVfxShared.GetOwnerEmissionPoint(owner));
      _rain.Begin(settings.CoreWidth);
      _hitEffect.Begin(target);

      RefreshVisuals();
      _view.UpdatePulse(Time.time);
      ApplyDamageTick();
      Fired?.Invoke(startPt);
    }

    public void Stop()
    {
      if (!_running)
        return;

      _running = false;
      _owner = null;
      _target = null;
      _onFirstHit = null;

      _view?.SetVisible(false);
      _emitter?.End();
      _rain?.End();
      _hitEffect?.End();

      LaserBeamPool.Release(this);
    }

    void Update()
    {
      if (!_running)
        return;

      if (_owner == null || _target == null || !_owner.gameObject.activeInHierarchy)
      {
        Stop();
        return;
      }

      // 激光射程不再限制：仅当发射者和远端均离屏时停止（节省性能?
      var beamEnd = LaserVfxShared.GetOwnerEmissionPoint(_owner) + _firingDirection * _firingMaxRange;
      if (IsOffScreen(_owner.position) && IsOffScreen(beamEnd))
      {
        Stop();
        return;
      }

      RefreshVisuals();
      _view.UpdatePulse(Time.time);

      _remaining -= Time.deltaTime;
      _tickTimer -= Time.deltaTime;
      if (_tickTimer <= 0f)
      {
        _tickTimer = Mathf.Max(0.05f, _settings.DamageTickInterval);
        ApplyDamageTick();
      }

      if (_remaining <= 0f)
        Stop();
    }

    void RefreshVisuals()
    {
      var start = LaserVfxShared.GetOwnerEmissionPoint(_owner);
      var end = start + _firingDirection * _firingMaxRange;

      if (_target != null && _target.CompareTag("Player")
          && PlayerReflectGateHelper.TryClipEnemyLaserAtShield(
            _target.gameObject, start, _firingDirection, _firingMaxRange, out var clipPoint))
      {
        end = clipPoint;
      }

      _view.SetEndpoints(start, end);
      _emitter.SyncTo(start);
      _rain.SyncBeam(start, end);
      _hitEffect?.SyncTo(end);
    }

    void ApplyDamageTick()
    {
      if (_target == null)
        return;

      var health = _target.GetComponent<Health>();
      if (health == null || health.IsDead)
        return;

      var start = LaserVfxShared.GetOwnerEmissionPoint(_owner);

      if (_target.CompareTag("Player")
          && PlayerReflectGateHelper.TryClipEnemyLaserAtShield(
            _target.gameObject, start, _firingDirection, _firingMaxRange, out _))
        return;

      if (_target.CompareTag("Player"))
      {
        var tickInterval = BossBalanceDatabase.Defaults.player_laser_tick_interval_sec;
        if (!BossAttackHitTracker.TryTickHit("boss_laser_player", _target.gameObject.GetInstanceID(), tickInterval))
          return;
      }

      // 检查目标是否仍在锁定方向的激光束上
      if (!PlayerLaserBeam.IsOnBeam(start, _firingDirection, _firingMaxRange, _settings.CoreWidth * 0.8f, _target.position))
        return;

      var result = DamagePipeline.Apply(_request, health);
      if (!_firstHitDone && result.FinalDamage > 0f)
      {
        _firstHitDone = true;
        _onFirstHit?.Invoke(health);
      }

      if (result.FinalDamage > 0f && _target.CompareTag("Player"))
        BossCombatDebugLog.ReportPlayerHit(result.FinalDamage, _request.DamageSourceId);
    }

    void ApplyLayerRecursive(GameObject go)
    {
      if (_layer < 0 || go == null)
        return;

      go.layer = _layer;
      foreach (Transform child in go.transform)
        ApplyLayerRecursive(child.gameObject);
    }

    static bool IsOffScreen(Vector3 worldPos)
    {
      if (s_cachedCamera == null)
        s_cachedCamera = Camera.main;
      if (s_cachedCamera == null)
        return false;

      var vp = s_cachedCamera.WorldToViewportPoint(worldPos);
      return vp.x < -OffScreenMargin || vp.x > 1f + OffScreenMargin
          || vp.y < -OffScreenMargin || vp.y > 1f + OffScreenMargin;
    }

    void OnDisable()
    {
      if (_running)
        Stop();
    }
  }

  /// <summary>激光实例对象池，避免运行时 Instantiate?/summary>
  public static class LaserBeamPool
  {
    const int InitialCapacity = 64;

    static readonly Stack<LaserEnemyAttack> s_pool = new();
    static Transform s_root;
    static bool s_ready;

    public static LaserEnemyAttack Acquire()
    {
      EnsurePool();

      if (s_pool.Count > 0)
        return s_pool.Pop();

      var go = new GameObject("LaserEnemyAttack");
      go.transform.SetParent(s_root, false);
      var attack = go.AddComponent<LaserEnemyAttack>();
      attack.EnsureBuilt();
      return attack;
    }

    public static void Release(LaserEnemyAttack attack)
    {
      if (attack == null)
        return;

      EnsurePool();
      attack.transform.SetParent(s_root, false);
      attack.gameObject.SetActive(false);
      s_pool.Push(attack);
    }

    static void EnsurePool()
    {
      if (s_ready)
        return;

      s_ready = true;
      var rootGo = new GameObject("LaserBeamPool");
      s_root = rootGo.transform;

      for (var i = 0; i < InitialCapacity; i++)
      {
        var go = new GameObject($"LaserEnemyAttack_{i}");
        go.transform.SetParent(s_root, false);
        go.SetActive(false);
        var attack = go.AddComponent<LaserEnemyAttack>();
        attack.EnsureBuilt();
        s_pool.Push(attack);
      }
    }
  }
}