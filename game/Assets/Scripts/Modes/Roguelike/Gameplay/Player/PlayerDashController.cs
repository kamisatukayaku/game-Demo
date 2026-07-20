using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Events;
using Game.Shared.Gameplay.Input;
using Game.Shared.Player;
using Game.Shared.Runtime;
using Game.Shared.Runtime.Physics;

namespace Game.Modes.Roguelike.Gameplay.Player
{
  [DisallowMultipleComponent]
  public sealed class PlayerDashController : MonoBehaviour, IPlayerMovementInputGate
  {
    static PlayerDashController s_instance;

    EntityPhysicsBody _physics;
    Health _health;
    Transform _scaleTarget;
    Vector2 _lastMoveDir = Vector2.right;
    float _lastMoveInputTime;
    float _cooldownUntil;
    float _cooldownDuration = 2f;
    bool _dashing;
    Coroutine _dashRoutine;
    readonly HashSet<int> _pathHitIds = new();
    readonly List<EnemyCore> _hitBuffer = new();
    float _dashRefundUsed;
    float _dashRefundCap;
    bool _pursuitChargeReady;
    float _pursuitExpiresAt;
    bool _currentDashIsPursuit;
    int _currentDashHitCount;

    public static PlayerDashController Instance => s_instance;
    public static float CooldownRemaining =>
      s_instance != null ? Mathf.Max(0f, s_instance._cooldownUntil - Time.time) : 0f;
    public static float CooldownDuration => ComputeEffectiveCooldown();
    public static float Cooldown01 => Mathf.Clamp01(CooldownRemaining / CooldownDuration);
    public static bool IsReady =>
      !s_dashBlocked && (CooldownRemaining <= 0f || IsPursuitReady);
    public static bool IsPursuitReady =>
      s_instance != null
      && s_instance._pursuitChargeReady
      && Time.time <= s_instance._pursuitExpiresAt;
    public static bool HasDashCombat =>
      BuildProgressionState.HasMechanic("dash_melee");

    public static void SetDashBlocked(bool blocked) => s_dashBlocked = blocked;
    public static void ResetRunState()
    {
      PlayerDashCooldownReducer.Reset();
      if (s_instance == null)
        return;
      s_instance.AbortDashImmediate();
      s_instance._cooldownUntil = 0f;
      s_instance._pursuitChargeReady = false;
      s_instance._pursuitExpiresAt = 0f;
    }

    static bool s_dashBlocked;

    public bool BlocksMovementInput => _dashing;

    public static PlayerDashController Ensure(GameObject player)
    {
      if (player == null)
        return null;
      return player.GetComponent<PlayerDashController>() ?? player.AddComponent<PlayerDashController>();
    }

    void Awake()
    {
      s_instance = this;
      DashCombatConfig.EnsureLoaded();
      _physics = GetComponent<EntityPhysicsBody>() ?? EntityPhysicsBody.EnsurePlayer(gameObject);
      _health = GetComponent<Health>();
      _scaleTarget = ResolveScaleTarget();
      _cooldownDuration = ComputeEffectiveCooldown();
    }

    void OnDisable() => AbortDashImmediate();

    void OnDestroy()
    {
      AbortDashImmediate();
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      var input = GameInputBindings.ReadMoveVector();
      if (input.sqrMagnitude > 0.0001f)
      {
        _lastMoveDir = input.normalized;
        _lastMoveInputTime = Time.time;
      }

      if (_pursuitChargeReady && Time.time > _pursuitExpiresAt)
        _pursuitChargeReady = false;

      if (_dashing)
        return;

      if (LevelUpController.IsWaiting || GameplayInputGateLocator.BlocksPlayerInput)
        return;

      if (_health != null && _health.IsDead)
        return;

      if (Input.GetKeyDown(KeyCode.LeftShift)
#if DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS
          || GameInputBindings.ConsumeSyntheticDash()
#endif
         )
        TryDash();
    }

    void TryDash()
    {
      if (s_dashBlocked)
        return;

      var dir = ResolveDashDirection();
      if (dir.sqrMagnitude < 0.0001f)
        return;

      var usingPursuit = _pursuitChargeReady && Time.time <= _pursuitExpiresAt;
      if (!usingPursuit && Time.time < _cooldownUntil)
        return;

      if (usingPursuit)
      {
        _pursuitChargeReady = false;
        _currentDashIsPursuit = true;
      }
      else
      {
        _currentDashIsPursuit = false;
        _cooldownDuration = ComputeEffectiveCooldown();
        _cooldownUntil = Time.time + _cooldownDuration;
      }

      _dashRoutine = StartCoroutine(DashRoutine(dir.normalized));
    }

    Vector2 ResolveDashDirection()
    {
      var input = GameInputBindings.ReadMoveVector();
      if (input.sqrMagnitude > 0.0001f)
        return input.normalized;

      var grace = DashCombatConfig.Base.input_grace_seconds;
      if (Time.time - _lastMoveInputTime <= grace && _lastMoveDir.sqrMagnitude > 0.0001f)
        return _lastMoveDir.normalized;

      return Vector2.zero;
    }

    IEnumerator DashRoutine(Vector2 dir)
    {
      _dashing = true;
      _pathHitIds.Clear();
      _dashRefundUsed = 0f;
      _currentDashHitCount = 0;
      var config = DashCombatConfig.Base;
      _dashRefundCap = ComputeEffectiveCooldown() * config.cooldown_refund_cap_ratio;

      var isContactStrike = IsContactDashStrike();
      var distance = config.distance + RunBuildState.GetStat("dash_distance_add");
      if (_currentDashIsPursuit)
        distance *= config.pursuit_distance_mult;

      var duration = Mathf.Max(0.05f, config.duration + RunBuildState.GetStat("dash_duration_add"));
      var invincible = Mathf.Max(0.01f,
        config.invincible_time + RunBuildState.GetDashInvincibleTimeAdd());

      var start = GameplayPlane.Position2D(transform);
      var requestedEnd = start + dir * distance;
      if (CircleArenaController.IsActive)
        requestedEnd = CircleArenaController.ClampPosition(requestedEnd, WorldGridConstants.PlayerCollisionRadius);

      var path = EntityPhysicsBody.ResolvePlanarPath(
        start,
        requestedEnd,
        _physics != null ? _physics.CollisionRadius : WorldGridConstants.PlayerCollisionRadius);
      var hasDamageTrail = HasDashDamageTrail();
      var trailWidth = GetDashDamageTrailWidth();
      var trailLifetime = GetDashDamageTrailLifetime();

      GameEventBus.Publish(new PlayerDashedEvent(transform.position));
      GameEventBus.Publish(new DashStartedEvent(
        gameObject,
        path.Start,
        path.Direction,
        distance,
        path.Distance,
        duration,
        isContactStrike,
        _currentDashIsPursuit));

      if (hasDamageTrail)
      {
        GameEventBus.Publish(new DashDamageTrailEvent(gameObject, path.Start, path.ActualEnd, trailWidth, trailLifetime));
        StartCoroutine(DashDamageTrailRoutine(path.Start, path.ActualEnd, trailWidth, trailLifetime));
      }

      _health?.GrantInvulnerability(invincible);

      var elapsed = 0f;
      var lastSample = path.Start;
      var lastPhysicsPos = start;
      while (elapsed < duration)
      {
        if (!isActiveAndEnabled || (_health != null && _health.IsDead))
        {
          AbortDashImmediate();
          yield break;
        }

        if (LevelUpController.IsWaiting || GameplayInputGateLocator.BlocksPlayerInput)
        {
          AbortDashImmediate();
          yield break;
        }

        elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var eased = 1f - Mathf.Pow(1f - t, 2.2f);
        var target = Vector2.Lerp(path.Start, path.ActualEnd, eased);
        var delta = target - lastPhysicsPos;
        if (delta.sqrMagnitude > 0.000001f)
        {
          _physics.QueuePlanarMove(delta);
          lastPhysicsPos = target;
        }

        if (isContactStrike)
          ProcessPathSegment(lastSample, target, dir);

        lastSample = target;
        yield return null;
      }

      var actualEnd = GameplayPlane.Position2D(transform);
      var settleDelta = path.ActualEnd - actualEnd;
      if (settleDelta.sqrMagnitude > 0.000001f)
        _physics.QueuePlanarMove(settleDelta);

      _physics.ResolveOverlapNow();

      var triggeredAftershock = false;
      if (isContactStrike)
      {
        ProcessPathSegment(lastSample, path.ActualEnd, dir);
        triggeredAftershock = ApplyDashAftershock(path.ActualEnd, dir);
      }

      var grantedPursuit = TryGrantPursuitCharge(isContactStrike);
      GameEventBus.Publish(new DashEndedEvent(
        gameObject,
        path.ActualEnd,
        dir,
        _currentDashHitCount,
        path.BlockedByObstacle,
        triggeredAftershock,
        isContactStrike,
        grantedPursuit));

      _dashing = false;
      _dashRoutine = null;
      _currentDashIsPursuit = false;
    }

    void ProcessPathSegment(Vector2 from, Vector2 to, Vector2 dir)
    {
      var strikeRadius = GetStrikeRadius();
      var added = DashStrikeResolver.CollectSegmentHits(from, to, strikeRadius, _pathHitIds, _hitBuffer);
      if (added <= 0)
        return;

      foreach (var enemy in _hitBuffer)
        ApplyStrikeHit(enemy, dir, false);
    }

    void ApplyStrikeHit(EnemyCore enemy, Vector2 dir, bool isAftershock)
    {
      if (enemy == null)
        return;

      var health = enemy.GetComponent<Health>();
      if (health == null || health.IsDead)
        return;

      var damage = isAftershock ? GetAftershockDamage() : GetStrikeDamage();
      DamagePipeline.Apply(
        DamageRequest.Direct(damage, "physical", isAftershock ? "contact_dash_aftershock" : "contact_dash_strike", gameObject),
        health);

      _currentDashHitCount++;
      GameEventBus.Publish(new DashEnemyHitEvent(
        gameObject,
        enemy.gameObject,
        GameplayPlane.Position2D(enemy.transform),
        dir,
        damage,
        false,
        _currentDashHitCount,
        isAftershock));

      if (!isAftershock)
      {
        ApplyDirectionalKnockback(enemy, dir, GetStrikeKnockback());
        ApplyCooldownRefund(enemy);
      }
    }

    bool ApplyDashAftershock(Vector2 center, Vector2 dir)
    {
      var config = DashCombatConfig.Base;
      var radius = config.aftershock_radius + RunBuildState.GetStat("dash_strike_aftershock_radius");
      if (radius <= 0.05f || CombatRoot.EnemyRegistry == null)
        return false;

      var hits = DashStrikeResolver.CollectRadialHits(center, radius, _hitBuffer);
      foreach (var enemy in _hitBuffer)
      {
        if (enemy == null)
          continue;
        ApplyStrikeHit(enemy, dir, true);
        ApplyRadialKnockback(enemy, center, config.aftershock_knockback + RunBuildState.GetStat("dash_aftershock_knockback_add"));
      }

      if (hits > 0)
      {
        GameEventBus.Publish(new DashAftershockEvent(
          gameObject,
          center,
          radius,
          config.aftershock_damage_ratio,
          hits));
      }

      return hits > 0;
    }

    void ApplyDirectionalKnockback(EnemyCore enemy, Vector2 dir, float force)
    {
      if (force <= 0f)
        return;

      var scale = ResolveKnockbackScale(enemy);
      enemy.GetComponent<EnemyMovement>()?.ApplyKnockback(dir.normalized * force * scale);
    }

    void ApplyRadialKnockback(EnemyCore enemy, Vector2 center, float force)
    {
      if (force <= 0f || enemy == null)
        return;

      var outward = GameplayPlane.Position2D(enemy.transform) - center;
      if (outward.sqrMagnitude < 0.0001f)
        outward = Random.insideUnitCircle.normalized;
      else
        outward.Normalize();

      var scale = ResolveKnockbackScale(enemy);
      enemy.GetComponent<EnemyMovement>()?.ApplyKnockback(outward * force * scale);
    }

    float ResolveKnockbackScale(EnemyCore enemy)
    {
      var config = DashCombatConfig.Base;
      if (EnemySpawnMetadata.IsBossEnemy(enemy.gameObject))
        return config.boss_knockback_scale;

      var metadata = enemy.GetComponent<EnemySpawnMetadata>();
      if (metadata != null && metadata.enemyId != null
          && (metadata.enemyId.Contains("tank") || metadata.enemyId.Contains("brute")))
        return config.elite_knockback_scale;

      return 1f;
    }

    void ApplyCooldownRefund(EnemyCore enemy)
    {
      var refundPerHit = RunBuildState.GetStat("dash_strike_cooldown_refund");
      if (refundPerHit <= 0f)
        return;

      if (_dashRefundUsed >= _dashRefundCap - 0.0001f)
        return;

      var applied = Mathf.Min(refundPerHit, _dashRefundCap - _dashRefundUsed);
      _dashRefundUsed += applied;
      _cooldownUntil = Mathf.Max(Time.time, _cooldownUntil - applied);

      if (_dashRefundUsed >= _dashRefundCap - 0.0001f)
        GameEventBus.Publish(new DashRefundCapReachedEvent(gameObject));
    }

    bool TryGrantPursuitCharge(bool isContactStrike)
    {
      if (!isContactStrike || _currentDashIsPursuit)
        return false;
      if (RunBuildState.GetStat("dash_pursuit_unlock") <= 0.5f)
        return false;

      var config = DashCombatConfig.Base;
      var threshold = config.pursuit_hit_threshold + RunBuildState.GetStat("dash_pursuit_hit_threshold_add");
      if (_currentDashHitCount < threshold)
        return false;

      _pursuitChargeReady = true;
      _pursuitExpiresAt = Time.time + config.pursuit_window_seconds + RunBuildState.GetStat("dash_pursuit_window_add");
      GameEventBus.Publish(new DashPursuitChargeGrantedEvent(gameObject, _pursuitExpiresAt - Time.time));
      return true;
    }

    IEnumerator DashDamageTrailRoutine(Vector2 from, Vector2 to, float width, float lifetime)
    {
      var elapsed = 0f;
      const float tick = 0.16f;
      var nextTick = 0f;
      var hitIds = new HashSet<int>();
      var hitEnemies = new List<EnemyCore>(24);

      while (elapsed < lifetime)
      {
        elapsed += Time.deltaTime;
        nextTick -= Time.deltaTime;
        if (nextTick <= 0f)
        {
          nextTick = tick;
          hitIds.Clear();
          var hits = DashStrikeResolver.CollectSegmentHits(from, to, width * 0.5f, hitIds, hitEnemies);
          if (hits > 0)
          {
            var damage = GetDashDamageTrailDamage();
            foreach (var enemy in hitEnemies)
            {
              if (enemy == null)
                continue;
              var health = enemy.GetComponent<Health>();
              if (health == null || health.IsDead)
                continue;

              DamagePipeline.Apply(
                DamageRequest.Direct(damage, "energy", "dash_damage_trail", gameObject),
                health);
            }
          }
        }
        yield return null;
      }
    }

    void AbortDashImmediate()
    {
      if (_dashRoutine != null)
      {
        StopCoroutine(_dashRoutine);
        _dashRoutine = null;
      }

      _dashing = false;
      _pathHitIds.Clear();
      _dashRefundUsed = 0f;
      _currentDashIsPursuit = false;
      RestoreVisualScale();
    }

    void RestoreVisualScale()
    {
      _scaleTarget = ResolveScaleTarget();
      if (_scaleTarget != null)
        _scaleTarget.localScale = Vector3.one;
    }

    static bool IsContactDashStrike() => BuildProgressionState.HasMechanic("dash_melee");
    static bool HasDashDamageTrail() => RunBuildState.GetStat("dash_damage_trail") > 0.5f;

    static float GetStrikeDamage()
    {
      var config = DashCombatConfig.Base;
      var dashBonus = 1f + Mathf.Max(0f, RunBuildState.GetStat("dash_strike_damage_mult"));
      var general = RunBuildState.GetWeaponDamageMult()
        + RunBuildCombatHooks.GetEffectiveDamageMult() - 1f;
      return config.strike_damage * Mathf.Max(0.1f, general) * dashBonus;
    }

    static float GetAftershockDamage() =>
      GetStrikeDamage() * DashCombatConfig.Base.aftershock_damage_ratio;

    static float GetDashDamageTrailDamage() =>
      GetStrikeDamage() * Mathf.Max(0.08f, 0.22f + RunBuildState.GetStat("dash_damage_trail_ratio_add"));

    static float GetDashDamageTrailWidth() =>
      Mathf.Max(0.45f, GetStrikeRadius() * 2.2f + RunBuildState.GetStat("dash_damage_trail_width_add"));

    static float GetDashDamageTrailLifetime() =>
      Mathf.Max(0.25f, 0.85f + RunBuildState.GetStat("dash_damage_trail_lifetime_add"));

    static float GetStrikeRadius() =>
      DashCombatConfig.Base.strike_width + Mathf.Max(0f, RunBuildState.GetStat("dash_strike_width_add"));

    static float GetStrikeKnockback() =>
      DashCombatConfig.Base.knockback + Mathf.Max(0f, RunBuildState.GetStat("dash_strike_knockback"));

    static float ComputeEffectiveCooldown()
    {
      var config = DashCombatConfig.Base;
      var reduced = PlayerDashCooldownReducer.Bonus
        + RunBuildState.GetDashCooldownReduction();
      return Mathf.Max(
        config.minimum_cooldown,
        config.cooldown + RunBuildState.GetStat("dash_cooldown_add") - reduced);
    }

    Transform ResolveScaleTarget()
    {
      var visual = transform.Find("Visual");
      return visual != null ? visual : transform;
    }
  }
}
