using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Projectile;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Mini-Boss W8 — 旋转护盾部件 + 冲撞。
  /// </summary>
  [DisallowMultipleComponent]
  public class MiniBossHexSentinel : BossCore
  {
    public const string SKILL_ROTATING_SHIELD = "rotating_shield";
    public const string SKILL_CHARGE = "charge";

    const float NORMAL_SPEED = 2.6f;
    const float IDEAL_DIST_MIN = 3.5f;
    const float IDEAL_DIST_MAX = 5.5f;
    const string PART_ENEMY_ID = "mini_boss_hex_sentinel_part";

    EnemyMovement _movement;
    EnemyAttack _attackComp;
    readonly List<BossPart> _shieldParts = new();

    public float MoveSpeed => _movement != null ? _movement.MoveSpeed : NORMAL_SPEED;
    public float AttackDmg => _attackComp != null ? _attackComp.AttackDamage : 6f;
    public float ProjSpeed => _attackComp != null ? _attackComp.ProjectileSpeed : 6f;
    public float ProjScale => _attackComp != null ? _attackComp.ProjectileScale : 0.32f;
    public Color ProjColor => _attackComp != null ? _attackComp.ProjectileColor : new Color(0.55f, 0.85f, 1f, 1f);

    public int LivingShieldCount
    {
      get
      {
        int count = 0;
        for (int i = _shieldParts.Count - 1; i >= 0; i--)
        {
          var part = _shieldParts[i];
          if (part == null || part.IsDestroyed)
            _shieldParts.RemoveAt(i);
          else
            count++;
        }
        return count;
      }
    }

    public Vector2 GetPlayerPos()
    {
      var t = Core?.ChaseTarget;
      if (t != null) return GameplayPlane.Position2D(t);
      var go = GameObject.FindGameObjectWithTag("Player");
      return go != null ? GameplayPlane.Position2D(go.transform) : Vector2.zero;
    }

    public Vector2 DirToPlayer()
    {
      var d = GetPlayerPos() - Position;
      return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    public float DistToPlayer() => Vector2.Distance(Position, GetPlayerPos());

    public void MoveInDir(Vector2 dir, float speed, float dt)
    {
      if (dir.sqrMagnitude < 0.0001f) return;
      transform.position += (Vector3)(dir.normalized * speed * dt);
    }

    public DamageRequest BuildReq(float dmg, string type = "physical", string src = "monster")
      => DamageRequest.Direct(dmg, type, src, gameObject);

    public void MeleeHit(float dmgMult = 1f, string type = "physical")
    {
      var t = Core?.ChaseTarget;
      if (t == null)
        return;

      BossContactDamage.ApplyPlayerMeleeHit(
        gameObject, t, AttackDmg * dmgMult, type, "boss_melee",
        attackInstanceId: ActiveAttackInstanceId);
    }

    public void SpawnRotatingShields(int count, float orbitRadius, float orbitSpeed)
    {
      ClearShields();
      for (int i = 0; i < count; i++)
      {
        float startAngle = (360f / count) * i;
        var part = SpawnPart(
          PART_ENEMY_ID,
          Vector2.zero,
          BossPart.DamageMode.Independent,
          BossPart.MovementMode.CircularOrbit,
          visualScale: 0.95f,
          orbitRadius: orbitRadius,
          orbitSpeed: orbitSpeed,
          orbitStartAngle: startAngle,
          hpMult: 0.18f);
        if (part != null)
          _shieldParts.Add(part);
      }
    }

    public void ClearShields()
    {
      for (int i = _shieldParts.Count - 1; i >= 0; i--)
      {
        if (_shieldParts[i] != null)
          DestroyPart(_shieldParts[i]);
      }
      _shieldParts.Clear();
    }

    protected override void Awake()
    {
      base.Awake();
      _movement = GetComponent<EnemyMovement>();
      _attackComp = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      RegisterSkill(new HexSentinelSkill_RotatingShield());
      RegisterSkill(new HexSentinelSkill_Charge());
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      if (ActiveSkill == null)
        HandleIdleMovement(dt);
    }

    public override void OnPartDestroyed(BossPart part) => _shieldParts.Remove(part);

    protected override Transform SelectAttackOrigin() => transform;

    void HandleIdleMovement(float dt)
    {
      float dist = DistToPlayer();
      if (dist < IDEAL_DIST_MIN)
        MoveInDir((Position - GetPlayerPos()).normalized, NORMAL_SPEED, dt);
      else if (dist > IDEAL_DIST_MAX)
        MoveInDir(DirToPlayer(), NORMAL_SPEED, dt);
    }
  }
}
