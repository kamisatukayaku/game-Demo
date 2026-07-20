using UnityEngine;



using Game.Modes.Roguelike.Combat;

using Game.Modes.Roguelike.Progression;

using Game.Shared.Combat.Damage;

using Game.Shared.Combat.Health;

using Game.Shared.Core;

using Game.Shared.Enemy.Spawn;

using Game.Shared.Gameplay.Input;

using Game.Shared.Player;

using Game.Shared.Projectile;



namespace Game.Modes.Roguelike.Archetypes.Ranged

{

  /// <summary>A3: Shooter overload bar — rapid fire fills bar, full bar releases fan burst.</summary>

  [DisallowMultipleComponent]

  public sealed class RangedOverloadController : MonoBehaviour

  {

    const float OverloadPerShot = 0.08f;

    static float OverloadBonus => RangedOverloadRuntime.RelicBonus;

    const float OverloadDecayPerSec = 0.04f;

    const int FanProjectileCount = 7;

    const float FanHalfAngle = 42f;

    const float BurstDamageMult = 1.35f;



    static RangedOverloadController s_instance;



    float _overload01;

    bool _subscribed;

    Health _health;



    public static float Overload01 => s_instance != null ? s_instance._overload01 : 0f;

    public static bool IsActive =>

      ArenaBuildBootstrap.SelectedBuildId == ArenaBuildBootstrap.Shooter;



    public static RangedOverloadController Ensure(GameObject player)

    {

      if (player == null)

        return null;

      var c = player.GetComponent<RangedOverloadController>();

      return c != null ? c : player.AddComponent<RangedOverloadController>();

    }



    public static void ResetForNewRun()

    {

      if (s_instance == null)

        return;



      s_instance._overload01 = 0f;

      s_instance.Unsubscribe();

      if (s_instance.enabled && IsActive)

        s_instance.Subscribe();

    }



    void Awake()

    {

      s_instance = this;

      _health = GetComponent<Health>();

    }



    void OnDestroy()

    {

      if (s_instance == this)

        s_instance = null;

      Unsubscribe();

    }



    void OnEnable() => Subscribe();



    void OnDisable() => Unsubscribe();



    void Subscribe()

    {

      if (_subscribed || !IsActive)

        return;

      PlayerAttackDirector.AttackPerformed += OnAttackPerformed;

      _subscribed = true;

    }



    void Unsubscribe()

    {

      if (!_subscribed)

        return;

      PlayerAttackDirector.AttackPerformed -= OnAttackPerformed;

      _subscribed = false;

    }



    void Update()

    {

      if (!IsActive || !enabled || _overload01 <= 0f)

        return;



      if (_health != null && _health.IsDead)

        return;



      _overload01 = Mathf.Max(0f, _overload01 - OverloadDecayPerSec * Time.deltaTime);

    }



    void OnAttackPerformed(string theme, string delivery)

    {

      if (!IsActive || !enabled || theme != "ranged" || !CanAccumulateOverload())

        return;



      if (_overload01 >= 1f)

      {

        FireFanBurst();

        _overload01 = 0f;

        return;

      }



      _overload01 = Mathf.Clamp01(_overload01 + OverloadPerShot + OverloadBonus);

      if (_overload01 >= 1f)

        FireFanBurst();

    }



    bool CanAccumulateOverload()

    {

      if (_health != null && _health.IsDead)

        return false;



      if (LevelUpController.IsWaiting || CombatTimePause.IsPaused)

        return false;



      return !GameplayInputGateLocator.BlocksPlayerInput;

    }



    void FireFanBurst()

    {

      var aim = ResolveAimDirection();

      var origin = GameplayPlane.ToWorld(GameplayPlane.Position2D(transform), 0f);

      var baseDamage = 8f * BurstDamageMult;

      var request = DamageRequest.Direct(baseDamage, "projectile", "ranged_overload_burst", gameObject);



      for (var i = 0; i < FanProjectileCount; i++)

      {

        var t = FanProjectileCount <= 1 ? 0.5f : i / (float)(FanProjectileCount - 1);

        var angle = Mathf.Lerp(-FanHalfAngle, FanHalfAngle, t);

        var dir = Rotate2D(aim, angle);

        ProjectileFactory.SpawnDirectional(

          origin, GameplayPlane.ToWorld(dir, 0f), request, 16f, 0.55f,

          new Color(1f, 0.72f, 0.22f, 1f), 14f, hitRadius: 0.22f);

      }



      _overload01 = 0f;

    }



    static Vector2 ResolveAimDirection()

    {

      var registry = CombatRoot.EnemyRegistry;

      var origin = GameObject.FindWithTag("Player");

      if (origin == null)

        return Vector2.right;



      var pos = GameplayPlane.Position2D(origin.transform);

      if (registry != null)

      {

        var best = float.MaxValue;

        Vector2 bestDir = Vector2.right;

        foreach (var enemy in registry.GetInRange(pos, 18f))

        {

          if (enemy == null)

            continue;

          var delta = GameplayPlane.Position2D(enemy.transform) - pos;

          var sqr = delta.sqrMagnitude;

          if (sqr < best && sqr > 0.01f)

          {

            best = sqr;

            bestDir = delta.normalized;

          }

        }



        if (best < float.MaxValue)

          return bestDir;

      }



      return Vector2.right;

    }



    static Vector2 Rotate2D(Vector2 v, float degrees)

    {

      var rad = degrees * Mathf.Deg2Rad;

      var cos = Mathf.Cos(rad);

      var sin = Mathf.Sin(rad);

      return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);

    }

  }

}


