using UnityEngine;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Projectile;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Mini-Boss W13 — 螺旋弹幕，中途翻转方向。
  /// </summary>
  [DisallowMultipleComponent]
  public class MiniBossStarChorus : BossCore
  {
    public const string SKILL_SPIRAL_BARRAGE = "spiral_barrage";

    const float IDEAL_DISTANCE = 5f;
    const float TOO_CLOSE = 3f;
    const float TOO_FAR = 7f;
    const float NORMAL_SPEED = 2.4f;

    EnemyMovement _movement;
    EnemyAttack _attack;

    public float MoveSpeed => _movement != null ? _movement.MoveSpeed : NORMAL_SPEED;
    public float AttackDamage => _attack != null ? _attack.AttackDamage : 6f;
    public float ProjectileSpeed => _attack != null ? _attack.ProjectileSpeed : 6f;
    public float ProjectileScale => _attack != null ? _attack.ProjectileScale : 0.3f;
    public Color ProjectileColor => _attack != null ? _attack.ProjectileColor : new Color(1f, 0.75f, 0.35f, 1f);

    public Vector3 AttackOrigin => _attack != null && _attack.AttackOrigin != null
      ? _attack.AttackOrigin.position
      : transform.position;

    public DamageRequest BuildDamageReq(float damage, string damageType = "physical")
      => DamageRequest.Direct(damage, damageType, "monster", gameObject);

    public Vector2 GetPlayerPos()
    {
      var target = Core?.ChaseTarget;
      if (target != null) return GameplayPlane.Position2D(target);
      var playerGO = GameObject.FindGameObjectWithTag("Player");
      return playerGO != null ? GameplayPlane.Position2D(playerGO.transform) : Vector2.zero;
    }

    public Vector2 DirToPlayer()
    {
      var toTarget = GetPlayerPos() - Position;
      return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
    }

    public void MoveInDirection(Vector2 dir, float speed, float dt)
    {
      if (dir.sqrMagnitude < 0.0001f) return;
      transform.position += (Vector3)(dir.normalized * speed * dt);
    }

    public void SpawnDirectionalProjectile(Vector2 direction, float damageMult = 1f, float speedOverride = 0f)
    {
      var dir3 = new Vector3(direction.x, direction.y, 0f);
      if (dir3.sqrMagnitude < 0.0001f) dir3 = Vector3.right;
      else dir3.Normalize();

      float speed = speedOverride > 0f ? speedOverride : ProjectileSpeed;
      var origin = AttackOrigin + dir3 * 0.45f;
      var req = BuildDamageReq(AttackDamage * damageMult);

      EnemyTriangleProjectile.SpawnDirectional(
        origin, dir3, req, speed, ProjectileScale, ProjectileColor,
        maxRange: 22f, hitRadius: 0f);
    }

    protected override void Awake()
    {
      base.Awake();
      _movement = GetComponent<EnemyMovement>();
      _attack = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      RegisterSkill(new StarChorusSkill_SpiralBarrage());
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      if (ActiveSkill != null) return;

      float dist = Vector2.Distance(Position, GetPlayerPos());
      if (dist < TOO_CLOSE)
        MoveInDirection((Position - GetPlayerPos()).normalized, MoveSpeed, dt);
      else if (dist > TOO_FAR)
        MoveInDirection(DirToPlayer(), MoveSpeed, dt);
    }

    protected override Transform SelectAttackOrigin() => transform;
  }
}
