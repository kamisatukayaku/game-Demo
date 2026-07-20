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
  /// 五柱巨像 Wild Boss — 最大 2 个子部件（左右对称），独立血量。
  ///
  /// 技能列表（按 Priority）：
  ///   P0 summon       — 召唤子部件（不存在部件时可释放）
  ///   P1 melee        — 近战 + 子部件环形弹幕
  ///   P2 shotgun      — 子部件霰弹 + 减速（仅存在部件时可释放）
  ///   P3 chase        — 转弯限速追逐近战
  ///   P4 heal         — 完全治疗子部件 + 自损
  ///   P5 burst        — 蓄力减伤 → 多轮环形高伤弹幕
  ///
  /// 被动：
  ///   - 登场时自动召唤 2 个子部件（血量 = Boss 生命上限 × 20%）
  ///   - 每个存活子部件提供移速加成
  /// </summary>
  [DisallowMultipleComponent]
  public class WildBossPentColossus : BossCore
  {
    // ══════════════════════════════════════════════════
    //  技能 ID 常量
    // ══════════════════════════════════════════════════
    public const string SKILL_MELEE   = "melee_barrage";
    public const string SKILL_SHOTGUN = "subpart_shotgun";
    public const string SKILL_CHASE   = "charge_chase";
    public const string SKILL_HEAL    = "heal_parts";
    public const string SKILL_BURST   = "charged_burst";
    public const string SKILL_SUMMON  = "summon_parts";

    // ══════════════════════════════════════════════════
    //  子部件配置
    // ══════════════════════════════════════════════════
    const string PART_ENEMY_ID  = "pent_colossus_part";
    const float  PART_OFFSET_X  = 1.3f;     // 左右偏移（缩短）
    const float  PART_HP_RATIO  = 0.20f;    // 部件血量 = Boss 最大生命 × 20%
    const float  PART_VISUAL_SCALE = 1.0f;

    // ══════════════════════════════════════════════════
    //  移动参数
    // ══════════════════════════════════════════════════
    const float BASE_SPEED          = 2.5f;
    const float SPEED_PER_PART      = 0.75f;  // 每个存活部件额外提速
    const float MELEE_RANGE         = 2.0f;   // 近战判定距离
    const float IDEAL_DISTANCE      = 5f;

    // ══════════════════════════════════════════════════
    //  状态
    // ══════════════════════════════════════════════════
    EnemyMovement _movement;
    EnemyAttack   _attackComp;
    BossPart      _partLeft;
    BossPart      _partRight;
    bool          _initialPartsSpawned;

    // ══════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════

    /// <summary>当前移动速度（基础 + 部件加成）</summary>
    public float MoveSpeed
    {
      get
      {
        float baseVal = _movement != null ? _movement.MoveSpeed : BASE_SPEED;
        return baseVal + LivingPartCount * SPEED_PER_PART;
      }
    }

    /// <summary>存活子部件数量</summary>
    public int LivingPartCount
    {
      get
      {
        int count = 0;
        if (_partLeft  != null && !_partLeft.IsDestroyed)  count++;
        if (_partRight != null && !_partRight.IsDestroyed) count++;
        return count;
      }
    }

    /// <summary>是否存在存活子部件</summary>
    public bool HasLivingParts => LivingPartCount > 0;

    /// <summary>获取所有存活子部件</summary>
    public List<BossPart> GetLivingParts()
    {
      var list = new List<BossPart>();
      if (_partLeft  != null && !_partLeft.IsDestroyed)  list.Add(_partLeft);
      if (_partRight != null && !_partRight.IsDestroyed) list.Add(_partRight);
      return list;
    }

    /// <summary>获取所有存活子部件的位置</summary>
    public List<Vector3> GetLivingPartPositions()
    {
      var list = new List<Vector3>();
      if (_partLeft  != null && !_partLeft.IsDestroyed)  list.Add(_partLeft.transform.position);
      if (_partRight != null && !_partRight.IsDestroyed) list.Add(_partRight.transform.position);
      return list;
    }

    public EnemyAttack AttackComp => _attackComp;
    public float AttackDamage    => _attackComp != null ? _attackComp.AttackDamage : 6f;
    public float ProjectileSpeed => _attackComp != null ? _attackComp.ProjectileSpeed : 6f;
    public float ProjectileScale => _attackComp != null ? _attackComp.ProjectileScale : 0.32f;
    public Color ProjectileColor => _attackComp != null ? _attackComp.ProjectileColor : new Color(0.95f, 0.35f, 0.85f, 1f);

    public DamageRequest BuildDamageReq(float damage, string damageType = "physical", string source = "monster")
      => DamageRequest.Direct(damage, damageType, source, gameObject);

    public Vector2 GetPlayerPos()
    {
      var target = Core?.ChaseTarget;
      if (target != null) return GameplayPlane.Position2D(target);
      var playerGO = GameObject.FindGameObjectWithTag("Player");
      return playerGO != null ? GameplayPlane.Position2D(playerGO.transform) : Vector2.zero;
    }

    public Vector2 DirToPlayer()
    {
      var to = GetPlayerPos() - Position;
      return to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.right;
    }

    public float DistToPlayer() => Vector2.Distance(Position, GetPlayerPos());

    public float GetHpRatio()
    {
      var h = Core?.Health;
      if (h == null || h.MaxHp <= 0f) return 1f;
      return h.CurrentHp / h.MaxHp;
    }

    public Vector3 AttackOrigin => _attackComp != null && _attackComp.AttackOrigin != null
      ? _attackComp.AttackOrigin.position : transform.position;

    /// <summary>按方向移动</summary>
    public void MoveInDirection(Vector2 dir, float speed, float dt)
    {
      if (dir.sqrMagnitude < 0.0001f) return;
      transform.position += (Vector3)(dir.normalized * speed * dt);
    }

    /// <summary>在指定方向生成三角弹</summary>
    public void SpawnDirectionalProjectile(Vector2 direction, float damageMult = 1f,
      float speedOverride = 0f, float rangeOverride = 0f, float scaleOverride = 0f)
    {
      var dir3 = new Vector3(direction.x, direction.y, 0f);
      if (dir3.sqrMagnitude < 0.0001f) dir3 = Vector3.right;
      else dir3.Normalize();

      float speed = speedOverride > 0f ? speedOverride : ProjectileSpeed;
      float scale = scaleOverride > 0f ? scaleOverride : ProjectileScale;
      float range = rangeOverride > 0f ? rangeOverride : 999f;
      var origin = AttackOrigin + dir3 * 0.45f;
      var req = BuildDamageReq(AttackDamage * damageMult);

      EnemyTriangleProjectile.SpawnDirectional(origin, dir3, req, speed, scale, ProjectileColor, range, 0f);
    }

    /// <summary>从指定世界坐标发射三角弹</summary>
    public void SpawnProjectileFrom(Vector3 worldOrigin, Vector2 direction, float damageMult = 1f,
      float speedOverride = 0f, float rangeOverride = 0f, float scaleOverride = 0f)
    {
      var dir3 = new Vector3(direction.x, direction.y, 0f);
      if (dir3.sqrMagnitude < 0.0001f) dir3 = Vector3.right;
      else dir3.Normalize();

      float speed = speedOverride > 0f ? speedOverride : ProjectileSpeed;
      float scale = scaleOverride > 0f ? scaleOverride : ProjectileScale;
      float range = rangeOverride > 0f ? rangeOverride : 999f;
      var origin = worldOrigin + dir3 * 0.45f;
      var req = BuildDamageReq(AttackDamage * damageMult);

      EnemyTriangleProjectile.SpawnDirectional(origin, dir3, req, speed, scale, ProjectileColor, range, 0f);
    }

    /// <summary>对自身造成伤害</summary>
    public void SelfDamage(float amount)
    {
      Core?.Health?.TakeDamage(amount);
    }

    /// <summary>复活 / 满血治疗一个子部件</summary>
    public bool FullHealPart(BossPart part)
    {
      if (part == null || part.IsDestroyed) return false;
      var h = part.GetComponent<Health>();
      if (h == null) return false;
      h.Configure(h.MaxHp); // 重置为满血 + 清除死亡标记
      return true;
    }

    /// <summary>对玩家目标造成直接伤害（用于近战攻击）</summary>
    public void MeleeHit(float damageMult = 1f, string damageType = "physical")
    {
      var target = Core?.ChaseTarget;
      if (target == null)
        return;

      BossContactDamage.ApplyPlayerMeleeHit(
        gameObject, target, AttackDamage * damageMult, damageType, "boss_melee",
        attackInstanceId: ActiveAttackInstanceId);
    }

    /// <summary>应用减速 Buff 到玩家</summary>
    public void ApplySlowToPlayer(string buffId, float slowAmount, float duration)
    {
      var target = Core?.ChaseTarget;
      if (target == null) return;
      var bc = target.GetComponent<Combat.Buff.BuffContainer>();
      if (bc == null) return;
      bc.ApplyBuff(buffId, new Combat.Buff.BuffContainer.BuffApplyContext
      {
        sourceEntity = gameObject,
        sourceKind = "monster",
        abilityId = SKILL_SHOTGUN,
        stacks = 1,
        customSlowAmount = slowAmount,
        customDuration = duration
      });
    }

    // ══════════════════════════════════════════════════
    //  BossCore 重写
    // ══════════════════════════════════════════════════

    protected override void Awake()
    {
      base.Awake();
      _movement   = GetComponent<EnemyMovement>();
      _attackComp = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      // 被动：登场时召唤 2 个子部件
      SpawnInitialParts();

      // 注册技能
      RegisterSkill(new PentColossusSkill_SummonParts());
      RegisterSkill(new PentColossusSkill_MeleeBarrage());
      RegisterSkill(new PentColossusSkill_SubpartShotgun());
      RegisterSkill(new PentColossusSkill_ChargeChase());
      RegisterSkill(new PentColossusSkill_HealParts());
      RegisterSkill(new PentColossusSkill_ChargedBurst());
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      // 基础移动（技能 1 部分逻辑）：仅在 Idle 时朝玩家靠近
      if (ActiveSkill == null && DistToPlayer() > IDEAL_DISTANCE)
      {
        var dir = DirToPlayer();
        MoveInDirection(dir, MoveSpeed, dt);
      }

      // 清理无效部件引用（在 BossCore.Update 中已清理 _parts 列表）
      if (_partLeft  != null && _partLeft.IsDestroyed)  _partLeft  = null;
      if (_partRight != null && _partRight.IsDestroyed) _partRight = null;
    }

    public override void OnPartDestroyed(BossPart part)
    {
      // BossCore 在 BossPart.OnPartDied() 中回调此方法
      if (part == _partLeft)  _partLeft  = null;
      if (part == _partRight) _partRight = null;
    }

    protected override Transform SelectAttackOrigin() => transform;

    // ══════════════════════════════════════════════════
    //  子部件管理
    // ══════════════════════════════════════════════════

    void SpawnInitialParts()
    {
      if (_initialPartsSpawned) return;
      _initialPartsSpawned = true;

      float partHp = (Core?.Health?.MaxHp ?? 100f) * PART_HP_RATIO;

      _partLeft = SpawnPart(
        enemyId: PART_ENEMY_ID,
        offset: new Vector2(-PART_OFFSET_X, 0f),
        damageMode: BossPart.DamageMode.Independent,
        movementMode: BossPart.MovementMode.FixedOffset,
        isAttackOrigin: true,
        visualScale: PART_VISUAL_SCALE,
        hpMult: partHp / 100f // 近似，实际由 Health.Configure 控制
      );

      _partRight = SpawnPart(
        enemyId: PART_ENEMY_ID,
        offset: new Vector2(PART_OFFSET_X, 0f),
        damageMode: BossPart.DamageMode.Independent,
        movementMode: BossPart.MovementMode.FixedOffset,
        isAttackOrigin: true,
        visualScale: PART_VISUAL_SCALE,
        hpMult: partHp / 100f
      );

      // 精确设置部件血量
      SetPartMaxHp(_partLeft,  partHp);
      SetPartMaxHp(_partRight, partHp);
    }

    /// <summary>召唤 2 个新子部件（技能 6 调用）</summary>
    public bool SummonNewParts(float hpPerPart)
    {
      if (HasLivingParts) return false;

      _partLeft = SpawnPart(
        enemyId: PART_ENEMY_ID,
        offset: new Vector2(-PART_OFFSET_X, 0f),
        damageMode: BossPart.DamageMode.Independent,
        movementMode: BossPart.MovementMode.FixedOffset,
        isAttackOrigin: true,
        visualScale: PART_VISUAL_SCALE,
        hpMult: 1f // 会被覆盖
      );

      _partRight = SpawnPart(
        enemyId: PART_ENEMY_ID,
        offset: new Vector2(PART_OFFSET_X, 0f),
        damageMode: BossPart.DamageMode.Independent,
        movementMode: BossPart.MovementMode.FixedOffset,
        isAttackOrigin: true,
        visualScale: PART_VISUAL_SCALE,
        hpMult: 1f
      );

      SetPartMaxHp(_partLeft,  hpPerPart);
      SetPartMaxHp(_partRight, hpPerPart);
      return true;
    }

    void SetPartMaxHp(BossPart part, float maxHp)
    {
      if (part == null) return;
      var h = part.GetComponent<Health>();
      if (h != null) h.Configure(Mathf.Max(1f, maxHp));
    }
  }
}
