using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 星巢 Wild Boss — 无子部件，纯技能驱动型 Boss。
  ///
  /// 技能列表（按 Priority）：
  ///   P0 escape        — 脱离（HP&lt;50% 触发，清 CD 一次）
  ///   P1 scatter       — 散射弹幕（三连霰弹 + 随机角度）
  ///   P2 sweep         — 扫射弹幕（十五连发 + 大角度随机）
  ///   P3 laser         — 双激光旋转（偏左/偏右 → 缓慢旋转至玩家方向）
  ///   P4 orbit         — 环绕弹幕（高速移动到玩家上方 → 绕玩家旋转 + 间歇霰弹）
  ///
  /// 基础移动（技能 1）：OnPassiveUpdate 中实现 — 过近后退 / 过远靠近，技能执行期间由技能接管移动。
  ///
  /// 被动（HP &lt; 50%）：每秒随机为除移动外的任一技能额外减 1 秒冷却。
  /// </summary>
  [DisallowMultipleComponent]
  public class WildBossStarHive : BossCore
  {
    // ══════════════════════════════════════════════════
    //  技能 ID 常量
    // ══════════════════════════════════════════════════
    public const string SKILL_SCATTER = "scatter_barrage";
    public const string SKILL_SWEEP   = "sweep_barrage";
    public const string SKILL_LASER   = "laser";
    public const string SKILL_ORBIT   = "orbiting_barrage";
    public const string SKILL_ESCAPE  = "escape";

    // ══════════════════════════════════════════════════
    //  移动参数
    // ══════════════════════════════════════════════════
    const float IDEAL_DISTANCE = 5f;
    const float TOO_CLOSE      = 3f;
    const float TOO_FAR        = 7f;
    const float NORMAL_SPEED   = 2.5f;

    // ══════════════════════════════════════════════════
    //  状态
    // ══════════════════════════════════════════════════
    EnemyMovement _movement;
    EnemyAttack _attack;
    readonly List<string> _passiveSkillIds = new();
    float _passiveTickTimer;
    bool _escapeUsed;

    // ══════════════════════════════════════════════════
    //  公开 API（供技能调用）
    // ══════════════════════════════════════════════════

    public float MoveSpeed      => _movement != null ? _movement.MoveSpeed : NORMAL_SPEED;
    public bool  IsEscapeUsed   => _escapeUsed;
    public void  MarkEscapeUsed() => _escapeUsed = true;

    public float AttackDamage      => _attack != null ? _attack.AttackDamage : 6f;
    public float ProjectileSpeed   => _attack != null ? _attack.ProjectileSpeed : 6f;
    public float ProjectileScale   => _attack != null ? _attack.ProjectileScale : 0.32f;
    public Color ProjectileColor   => _attack != null ? _attack.ProjectileColor : new Color(0.95f, 0.35f, 0.85f, 1f);

    public DamageRequest BuildDamageReq(float damage, string damageType = "physical")
      => DamageRequest.Direct(damage, damageType, "monster", gameObject);

    /// <summary>按方向移动（世界坐标偏移）</summary>
    public void MoveInDirection(Vector2 dir, float speed, float dt)
    {
      if (dir.sqrMagnitude < 0.0001f) return;
      transform.position += (Vector3)(dir.normalized * speed * dt);
    }

    /// <summary>玩家平面坐标</summary>
    public Vector2 GetPlayerPos()
    {
      var target = Core?.ChaseTarget;
      if (target != null) return GameplayPlane.Position2D(target);
      var playerGO = GameObject.FindGameObjectWithTag("Player");
      return playerGO != null ? GameplayPlane.Position2D(playerGO.transform) : Vector2.zero;
    }

    /// <summary>Boss → 玩家的平面方向（已归一化）</summary>
    public Vector2 DirToPlayer()
    {
      var toTarget = GetPlayerPos() - Position;
      return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
    }

    /// <summary>Boss → 玩家的平面距离</summary>
    public float DistToPlayer() => Vector2.Distance(Position, GetPlayerPos());

    /// <summary>血量比例 [0, 1]</summary>
    public float GetHpRatio()
    {
      var health = Core?.Health;
      if (health == null || health.MaxHp <= 0f) return 1f;
      return health.CurrentHp / health.MaxHp;
    }

    /// <summary>攻击发射点</summary>
    public Vector3 AttackOrigin => _attack != null && _attack.AttackOrigin != null
      ? _attack.AttackOrigin.position : transform.position;

    /// <summary>在指定方向生成三角弹（绕过 EnemyAttack 的协程系统，直接操控方向）</summary>
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

    // ══════════════════════════════════════════════════
    //  BossCore 重写
    // ══════════════════════════════════════════════════

    protected override void Awake()
    {
      base.Awake();
      _movement = GetComponent<EnemyMovement>();
      _attack   = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      // 初始化被动技能池（排除移动）
      _passiveSkillIds.Clear();
      _passiveSkillIds.Add(SKILL_SCATTER);
      _passiveSkillIds.Add(SKILL_SWEEP);
      _passiveSkillIds.Add(SKILL_LASER);
      _passiveSkillIds.Add(SKILL_ORBIT);
      _passiveSkillIds.Add(SKILL_ESCAPE);

      // 注册技能（按 Priority 自动排序：P0 escape, P1 scatter, P2 sweep, P3 laser, P4 orbit）
      RegisterSkill(new StarHiveSkill_Escape());
      RegisterSkill(new StarHiveSkill_ScatterBarrage());
      RegisterSkill(new StarHiveSkill_SweepBarrage());
      RegisterSkill(new StarHiveSkill_Laser());
      RegisterSkill(new StarHiveSkill_OrbitingBarrage());
    }

    protected override void OnBossUpdate(float dt) { /* 技能系统启用时不走此路径 */ }

    protected override void OnPassiveUpdate(float dt)
    {
      // ── 被动：HP < 50% 后，每秒随机为一个技能额外减 1 秒 CD ──
      if (GetHpRatio() < 0.5f)
      {
        _passiveTickTimer += dt;
        if (_passiveTickTimer >= 1f)
        {
          _passiveTickTimer -= 1f;
          int idx = Random.Range(0, _passiveSkillIds.Count);
          string id = _passiveSkillIds[idx];
          float cur = GetSkillCooldown(id);
          if (cur > 0f)
            SetSkillCooldown(id, Mathf.Max(0f, cur - 1f));
        }
      }

      // ── 基础移动（技能 1）：仅在 Idle 时生效（技能执行时由技能接管移动）──
      if (ActiveSkill == null)
        HandleBasicMovement(dt);
    }

    protected override Transform SelectAttackOrigin() => transform;

    protected override void OnPhaseChanged(int fromPhase, int toPhase)
    {
      if (toPhase >= 1)
        GameEventBus.Publish(new BossPhaseChangedEvent(gameObject, "star_hive", fromPhase, toPhase, "星巢分裂"));
    }

    // ══════════════════════════════════════════════════
    //  基础移动（技能 1）
    // ══════════════════════════════════════════════════

    void HandleBasicMovement(float dt)
    {
      var playerPos = GetPlayerPos();
      var myPos     = Position;
      float dist    = Vector2.Distance(myPos, playerPos);

      if (dist < TOO_CLOSE)
      {
        // 后退
        Vector2 away = (myPos - playerPos).normalized;
        MoveInDirection(away, NORMAL_SPEED, dt);
      }
      else if (dist > TOO_FAR)
      {
        // 靠近
        Vector2 toward = (playerPos - myPos).normalized;
        MoveInDirection(toward, NORMAL_SPEED, dt);
      }
      // 在 [TOO_CLOSE, TOO_FAR] 内不动
    }
  }
}
