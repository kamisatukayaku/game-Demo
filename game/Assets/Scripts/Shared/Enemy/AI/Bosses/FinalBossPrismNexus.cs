using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 棱镜核心 Final Boss — 6 无血量子部件 + 三阶段变换机制。
  ///
  /// 阶段：
  ///   Phase 0 (HP ≥ 60%) — 基础形态，所有部件存在
  ///   Phase 1 (HP 20%~60%) — 强化技能，部件仍存在
  ///   Phase 2 (HP &lt; 20%) — 销毁所有部件，解锁新技能，随机移动加速
  ///
  /// 被动 Idle 移动：随机方向 / 缓慢移动 / 方向刷新间隔随阶段变化
  /// </summary>
  [DisallowMultipleComponent]
  public class FinalBossPrismNexus : BossCore
  {
    // ══════════════════════════════════════════════════
    //  技能 ID
    // ══════════════════════════════════════════════════
    public const string SK_DASH    = "dash";
    public const string SK_MELEE   = "melee";
    public const string SK_SHOTGUN = "part_shotgun";
    public const string SK_LASER   = "part_laser";
    public const string SK_HOMING  = "homing_bullet";
    public const string SK_REPOS   = "reposition";
    public const string SK_SUMMON  = "summon_minions";
    public const string SK_SHIELD  = "pshield";
    public const string SK_BOMBS   = "random_bombs";

    // ══════════════════════════════════════════════════
    //  子部件配置
    // ══════════════════════════════════════════════════
    const int    PART_COUNT     = 6;
    const float  PART_RADIUS    = 5.5f;
    const string PART_RING_ID   = "prism_nexus_ring";   // 三角形弹幕子部件
    const string PART_ENEMY_ID  = "prism_nexus_part";    // 12边型护盾部件
    const float  PART_SCALE     = 0.9f;

    // ══════════════════════════════════════════════════
    //  Idle 移动参数（按阶段）
    // ══════════════════════════════════════════════════
    const float IDLE_SPEED_P0 = 1.5f;
    const float IDLE_SPEED_P1 = 2.0f;
    const float IDLE_SPEED_P2 = 2.8f;
    const float IDLE_DIR_INTERVAL_P0 = 2.0f;
    const float IDLE_DIR_INTERVAL_P1 = 2.0f;
    const float IDLE_DIR_INTERVAL_P2 = 0.5f;

    // ══════════════════════════════════════════════════
    //  状态
    // ══════════════════════════════════════════════════
    EnemyMovement _movement;
    EnemyAttack   _attackComp;

    // 子部件（DamageMode.None，纯视觉/攻击源）
    readonly BossPart[] _subParts = new BossPart[PART_COUNT];
    readonly float[]    _partAngles = new float[PART_COUNT]; // 固定角度

    // Idle 随机移动
    Vector2 _idleDir;
    float   _idleDirTimer;

    // 护盾（技能 8）
    BossPart _shieldPart;
    bool     _shieldProtecting;

    // 技能封锁
    float _skillLockoutTimer;

    // ══════════════════════════════════════════════════
    //  公开属性
    // ══════════════════════════════════════════════════

    /// <summary>当前游戏阶段 0/1/2</summary>
    public int GamePhase => CurrentPhase;

    /// <summary>是否有存活子部件</summary>
    public bool HasParts
    {
      get
      {
        for (int i = 0; i < PART_COUNT; i++)
          if (_subParts[i] != null && !_subParts[i].IsDestroyed)
            return true;
        return false;
      }
    }
    /// <summary>子部件数组（可能含 null/已销毁项）</summary>
    public BossPart[] Parts => _subParts;

    public bool HasShield       => _shieldPart != null && !_shieldPart.IsDestroyed;
    public bool IsSkillLocked   => _skillLockoutTimer > 0f;

    public float MoveSpeedVal   => _movement != null ? _movement.MoveSpeed : 2.5f;
    public float AttackDmg      => _attackComp != null ? _attackComp.AttackDamage : 8f;
    public float ProjSpeed      => _attackComp != null ? _attackComp.ProjectileSpeed : 6f;
    public float ProjScale      => _attackComp != null ? _attackComp.ProjectileScale : 0.32f;
    public Color ProjColor      => _attackComp != null ? _attackComp.ProjectileColor : new Color(0.95f, 0.35f, 0.85f, 1f);

    // ══════════════════════════════════════════════════
    //  阶段参数
    // ══════════════════════════════════════════════════

    public float IdleSpeed =>
      GamePhase == 2 ? IDLE_SPEED_P2 : (GamePhase == 1 ? IDLE_SPEED_P1 : IDLE_SPEED_P0);

    public float IdleDirInterval =>
      GamePhase == 2 ? IDLE_DIR_INTERVAL_P2 : IDLE_DIR_INTERVAL_P0;

    public int   DashCount       => GamePhase >= 1 ? 3 : 2;
    public float DashInterval    => GamePhase == 2 ? 0.15f : 0.25f;
    public bool  DashHasTeleport => GamePhase == 2;

    // ══════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════

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
    public float HpRatio()
    {
      var h = Core?.Health;
      return (h != null && h.MaxHp > 0f) ? h.CurrentHp / h.MaxHp : 1f;
    }

    /// <summary>获取玩家速度（用于传送预判）</summary>
    public Vector2 GetPlayerVelocity()
    {
      var go = Core?.ChaseTarget?.gameObject;
      if (go == null) go = GameObject.FindGameObjectWithTag("Player");
      if (go == null) return Vector2.zero;
      var rb = go.GetComponent<Rigidbody2D>();
      return rb != null ? rb.velocity : Vector2.zero;
    }

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

    public void SpawnProj(Vector2 dir, float dmgMult = 1f, float spd = 0f, float range = 0f, float scale = 0f)
    {
      var d3 = new Vector3(dir.x, dir.y, 0f);
      if (d3.sqrMagnitude < 0.0001f) d3 = Vector3.right; else d3.Normalize();
      EnemyTriangleProjectile.SpawnDirectional(transform.position + d3 * 0.45f, d3,
        BuildReq(AttackDmg * dmgMult), spd > 0f ? spd : ProjSpeed,
        scale > 0f ? scale : ProjScale, ProjColor, range > 0f ? range : 999f, 0f);
    }

    public void SpawnProjFrom(Vector3 origin, Vector2 dir, float dmgMult = 1f, float spd = 0f, float range = 0f)
    {
      var d3 = new Vector3(dir.x, dir.y, 0f);
      if (d3.sqrMagnitude < 0.0001f) d3 = Vector3.right; else d3.Normalize();
      EnemyTriangleProjectile.SpawnDirectional(origin + d3 * 0.45f, d3,
        BuildReq(AttackDmg * dmgMult), spd > 0f ? spd : ProjSpeed, ProjScale, ProjColor,
        range > 0f ? range : 999f, 0f);
    }

    /// <summary>发射失锁追踪弹</summary>
    public void SpawnLockLossHoming(Vector3 origin, float dmgMult, float speed,
      float turnRate, float lockLossAngle)
    {
      var target = Core?.ChaseTarget;
      if (target == null) return;
      ProjectileFactory.SpawnLockLossHoming(origin, target,
        BuildReq(AttackDmg * dmgMult), speed, turnRate, lockLossAngle,
        ProjScale, ProjColor, "EnemyLockLossProjectile", 0f);
    }

    public void SelfDamage(float amount) => Core?.Health?.TakeDamage(amount);

    /// <summary>在玩家周围中远距离随机选取位置</summary>
    public Vector2 RandomMidFarPos(float minDist = 5f, float maxDist = 9f)
    {
      var playerPos = GetPlayerPos();
      float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
      float dist  = Random.Range(minDist, maxDist);
      return playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

    // ══════════════════════════════════════════════════
    //  子部件操作
    // ══════════════════════════════════════════════════

    void CreateSubParts()
    {
      for (int i = 0; i < PART_COUNT; i++)
      {
        float angle = (360f / PART_COUNT) * i;
        _partAngles[i] = angle;
        float rad = angle * Mathf.Deg2Rad;
        var offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * PART_RADIUS;

        var part = SpawnPart(PART_RING_ID, offset,
          BossPart.DamageMode.None, BossPart.MovementMode.None,
          isAttackOrigin: true, visualScale: PART_SCALE);
        _subParts[i] = part;
      }
    }

    /// <summary>同步子部件到固定角度位置（每帧调用）</summary>
    public void SyncPartsToAngles(float[] angles = null, float radiusMult = 1f)
    {
      float r = PART_RADIUS * radiusMult;
      var ang = angles ?? _partAngles;
      for (int i = 0; i < PART_COUNT && i < ang.Length; i++)
      {
        if (_subParts[i] == null || _subParts[i].IsDestroyed) continue;
        float rad = ang[i] * Mathf.Deg2Rad;
        var offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r;
        _subParts[i].transform.position = new Vector3(
          Position.x + offset.x, Position.y + offset.y,
          _subParts[i].transform.position.z);
      }
    }

    /// <summary>销毁所有子部件</summary>
    public void DestroyAllSubParts()
    {
      for (int i = 0; i < PART_COUNT; i++)
      {
        if (_subParts[i] != null) { DestroyPart(_subParts[i]); _subParts[i] = null; }
      }
    }

    // ══════════════════════════════════════════════════
    //  护盾操作
    // ══════════════════════════════════════════════════

    public BossPart SummonShield(float hpRatio)
    {
      if (HasShield) return _shieldPart;
      float hp = Mathf.Max(1f, (Core?.Health?.MaxHp ?? 100f) * hpRatio);
      _shieldPart = SpawnPart(PART_ENEMY_ID, Vector2.zero,
        BossPart.DamageMode.Independent, BossPart.MovementMode.None,
        isAttackOrigin: false, visualScale: PART_SCALE * 1.4f, hpMult: hp / 100f);
      var h = _shieldPart?.GetComponent<Health>();
      if (h != null) h.Configure(hp);
      _shieldProtecting = true;
      return _shieldPart;
    }

    public void SyncShieldPos()
    {
      if (_shieldPart != null && !_shieldPart.IsDestroyed)
        _shieldPart.transform.position = transform.position;
    }

    // ══════════════════════════════════════════════════
    //  BossCore 重写
    // ══════════════════════════════════════════════════

    protected override void Awake()
    {
      // 设置阶段阈值：60%, 20%
      _phaseHpThresholds = new float[] { 0.6f, 0.2f };
      base.Awake();
      _movement   = GetComponent<EnemyMovement>();
      _attackComp = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      CreateSubParts();
      _idleDir      = Random.insideUnitCircle.normalized;
      _idleDirTimer = 0f;

      var health = Core?.Health;
      if (health != null) health.Damaged += OnBossDamaged;

      // 注册技能（P0→P8，低数字先触发）
      RegisterSkill(new PrismNexusSkill_Shield());       // P0
      RegisterSkill(new PrismNexusSkill_Melee());        // P1
      RegisterSkill(new PrismNexusSkill_Dash());         // P2
      RegisterSkill(new PrismNexusSkill_Reposition());   // P3
      RegisterSkill(new PrismNexusSkill_RandomBombs());  // P4
      RegisterSkill(new PrismNexusSkill_HomingBullet()); // P5
      RegisterSkill(new PrismNexusSkill_SummonMinions());// P6
      RegisterSkill(new PrismNexusSkill_PartShotgun());  // P7
      RegisterSkill(new PrismNexusSkill_PartLaser());    // P8
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      // 技能封锁计时
      if (_skillLockoutTimer > 0f) _skillLockoutTimer -= dt;
      // 护盾位置同步
      SyncShieldPos();

      // Idle 随机移动
      if (ActiveSkill == null)
        HandleIdleMovement(dt);

      // 子部件位置同步（仅在部件存在时）
      if (HasParts && GamePhase < 2)
        SyncPartsToAngles();
    }

    protected override void OnPhaseChanged(int from, int to)
    {
      if (to == 1)
        GameEventBus.Publish(new BossPhaseChangedEvent(gameObject, "prism_nexus", from, to, "棱镜分化"));
      else if (to >= 2)
        GameEventBus.Publish(new BossPhaseChangedEvent(gameObject, "prism_nexus", from, to, "终末棱镜"));

      // 进入二/三阶段 → 清除技能 7 CD
      if (to >= 1)
        SetSkillCooldown(SK_SUMMON, 0f);

      // 进入三阶段 → 销毁所有子部件 + 象限封锁 + 再清除一次技能 7 CD
      if (to >= 2)
      {
        CancelActiveSkill();
        SetSkillIntroGrace(1.4f);
        DestroyAllSubParts();
        SetSkillCooldown(SK_SUMMON, 0f);
      }
    }

    public override void OnPartDestroyed(BossPart part)
    {
      if (part == _shieldPart)
      {
        _shieldPart = null;
        _shieldProtecting = false;

        // 护盾破裂 → 中断当前技能 + 技能封锁
        CancelActiveSkill();
        _skillLockoutTimer = 1.5f;
      }
    }

    protected override void OnDestroy()
    {
      var health = Core?.Health;
      if (health != null) health.Damaged -= OnBossDamaged;
      base.OnDestroy();
    }

    protected override Transform SelectAttackOrigin() => transform;

    // ══════════════════════════════════════════════════
    //  Idle 移动
    // ══════════════════════════════════════════════════

    void HandleIdleMovement(float dt)
    {
      _idleDirTimer += dt;
      if (_idleDirTimer >= IdleDirInterval)
      {
        _idleDirTimer -= IdleDirInterval;
        _idleDir = Random.insideUnitCircle.normalized;
      }
      MoveInDir(_idleDir, IdleSpeed, dt);
    }

    // ══════════════════════════════════════════════════
    //  护盾免伤回调
    // ══════════════════════════════════════════════════

    void OnBossDamaged(float amount)
    {
      if (!_shieldProtecting || HasShield == false) return;
      var h = Core?.Health;
      if (h != null && !h.IsDead) h.Heal(amount);
    }
  }
}
