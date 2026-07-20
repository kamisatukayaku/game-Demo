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
  /// 六王 Wild Boss — 冲撞 + 护盾 + 幻影瞬移机制。
  ///
  /// 技能列表（按 Priority）：
  ///   P0 shield       — 护盾子部件（免伤 + 破碎损血）
  ///   P1 phantom      — 幻影瞬移冲撞（6 幻影 + 随机传送 + 无蓄力冲撞）
  ///   P2 double_dash  — 二连冲撞（蓄力自由转向）
  ///   P3 trail_dash   — 单次冲撞 + 路径弹幕（蓄力限速转向）
  ///
  /// 被动：
  ///   - Idle 时与玩家保持中等略近距离
  ///   - HP &lt; 50% 时触发（仅一次）：自动召唤 10% HP 护盾 + 清除技能 4 CD
  /// </summary>
  [DisallowMultipleComponent]
  public class WildBossHexKing : BossCore
  {
    // ══════════════════════════════════════════════════
    //  技能 ID 常量
    // ══════════════════════════════════════════════════
    public const string SKILL_DOUBLE_DASH = "double_dash";
    public const string SKILL_TRAIL_DASH  = "trail_dash";
    public const string SKILL_SHIELD      = "shield";
    public const string SKILL_PHANTOM     = "phantom_rush";

    // ══════════════════════════════════════════════════
    //  参数
    // ══════════════════════════════════════════════════
    const float NORMAL_SPEED     = 2.8f;
    const float IDEAL_DIST_MIN   = 3.5f;
    const float IDEAL_DIST_MAX   = 5.5f;

    const string PART_ENEMY_ID    = "hex_king_part";    // 八边形外环 → 护盾
    const string PART_PHANTOM_ID  = "hex_king_phantom";  // 幻影（缩小+半透明）
    const float PART_VISUAL       = 1.0f;

    const float PASSIVE_SHIELD_HP_RATIO = 0.06f;
    const float SKILL3_SHIELD_HP_RATIO  = 0.18f;
    const float  SHIELD_BREAK_COOLDOWN   = 3f;

    // ══════════════════════════════════════════════════
    //  状态
    // ══════════════════════════════════════════════════
    EnemyMovement _movement;
    EnemyAttack   _attackComp;

    // 护盾
    BossPart      _shieldPart;
    float         _shieldBreakCooldownTimer;
    bool          _handlingDamage; // 防重入

    // 幻影（技能 4 专用）
    readonly List<BossPart> _phantoms = new();
    bool _skill4Active;

    // 被动触发
    bool _passiveTriggered;

    // ══════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════

    public float MoveSpeed   => _movement != null ? _movement.MoveSpeed : NORMAL_SPEED;
    public float AttackDmg   => _attackComp != null ? _attackComp.AttackDamage : 6f;
    public float ProjSpeed   => _attackComp != null ? _attackComp.ProjectileSpeed : 6f;
    public float ProjScale   => _attackComp != null ? _attackComp.ProjectileScale : 0.32f;
    public Color ProjColor   => _attackComp != null ? _attackComp.ProjectileColor : new Color(0.95f, 0.35f, 0.85f, 1f);

    // ── 护盾 ──
    public bool HasShield          => _shieldPart != null && !_shieldPart.IsDestroyed;
    public bool ShieldOnCooldown   => _shieldBreakCooldownTimer > 0f;

    // ── 幻影 ──
    public bool IsSkill4Active     => _skill4Active;
    public IReadOnlyList<BossPart> Phantoms => _phantoms;
    public int LivingPhantomCount
    {
      get
      {
        int c = 0;
        for (int i = _phantoms.Count - 1; i >= 0; i--)
          if (_phantoms[i] != null && !_phantoms[i].IsDestroyed) c++;
        return c;
      }
    }

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
    public float GetHpRatio()
    {
      var h = Core?.Health;
      return (h != null && h.MaxHp > 0f) ? h.CurrentHp / h.MaxHp : 1f;
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
      var o = transform.position + d3 * 0.45f;
      EnemyTriangleProjectile.SpawnDirectional(o, d3,
        BuildReq(AttackDmg * dmgMult),
        spd > 0f ? spd : ProjSpeed,
        scale > 0f ? scale : ProjScale, ProjColor,
        range > 0f ? range : 999f, 0f);
    }

    public void SpawnProjFrom(Vector3 origin, Vector2 dir, float dmgMult = 1f, float spd = 0f, float range = 0f)
    {
      var d3 = new Vector3(dir.x, dir.y, 0f);
      if (d3.sqrMagnitude < 0.0001f) d3 = Vector3.right; else d3.Normalize();
      EnemyTriangleProjectile.SpawnDirectional(origin + d3 * 0.45f, d3,
        BuildReq(AttackDmg * dmgMult),
        spd > 0f ? spd : ProjSpeed, ProjScale, ProjColor,
        range > 0f ? range : 999f, 0f);
    }

    public void SelfDamage(float amount)
      => Core?.Health?.TakeDamage(amount);

    // ══════════════════════════════════════════════════
    //  护盾操作
    // ══════════════════════════════════════════════════

    /// <summary>创建护盾子部件（位置与 Boss 完全同步）</summary>
    public BossPart SummonShield(float hpRatio)
    {
      if (HasShield) return _shieldPart;

      float hp = Mathf.Max(1f, (Core?.Health?.MaxHp ?? 100f) * hpRatio);

      _shieldPart = SpawnPart(
        enemyId: PART_ENEMY_ID,
        offset: Vector2.zero,
        damageMode: BossPart.DamageMode.Independent,
        movementMode: BossPart.MovementMode.None, // 位置由我们手动同步
        isAttackOrigin: false,
        visualScale: PART_VISUAL * 1.3f,
        hpMult: hp / 100f
      );
      SetPartMaxHp(_shieldPart, hp);
      ConfigureShieldPartPresentation(_shieldPart);
      return _shieldPart;
    }

    void ConfigureShieldPartPresentation(BossPart shieldPart)
    {
      if (shieldPart == null)
        return;

      var display = shieldPart.GetComponent<DamageDisplay>();
      if (display != null)
        display.ShowHealthBar = false;

      var shieldHealth = shieldPart.GetComponent<Health>();
      if (shieldHealth != null)
        CombatFeedbackManager.HideHealthBar(shieldHealth);
    }

    void SyncBossShieldBar()
    {
      var bossHealth = Core?.Health;
      if (bossHealth == null)
        return;

      if (!HasShield)
      {
        CombatFeedbackManager.UpdateBossShieldOverlay(bossHealth, 0f, bossHealth.MaxHp);
        return;
      }

      var shieldHealth = _shieldPart != null ? _shieldPart.GetComponent<Health>() : null;
      var shieldHp = shieldHealth != null ? shieldHealth.CurrentHp : 0f;
      CombatFeedbackManager.UpdateBossShieldOverlay(bossHealth, shieldHp, bossHealth.MaxHp);
      CombatFeedbackManager.ShowHealthBar(bossHealth);
    }

    /// <summary>同步护盾位置（每帧调用）</summary>
    public void SyncShieldPosition()
    {
      if (_shieldPart != null && !_shieldPart.IsDestroyed)
        _shieldPart.transform.position = transform.position;
    }

    /// <summary>强制销毁护盾（不触发损血惩罚）</summary>
    public void DestroyShieldNoPenalty()
    {
      if (_shieldPart != null)
      {
        DestroyPart(_shieldPart);
        _shieldPart = null;
      }
    }

    // ══════════════════════════════════════════════════
    //  幻影操作
    // ══════════════════════════════════════════════════

    /// <summary>创建 6 个幻影子部件（围绕玩家排列），返回偏移角度数组</summary>
    public float[] CreatePhantoms(int count, float hpPerPhantom, float radius)
    {
      DestroyAllPhantoms();
      _phantoms.Clear();
      _skill4Active = true;

      var playerPos = GetPlayerPos();
      float[] angles = new float[count];
      var previewSprite = Resources.Load<Sprite>("Sprites/Enemies/Bosses/" + PART_PHANTOM_ID);

      for (int i = 0; i < count; i++)
      {
        float angle = (360f / count) * i;
        angles[i] = angle;
        float rad = angle * Mathf.Deg2Rad;
        var offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        var pos = playerPos + offset;

        // ── 召唤前淡入动画 ──
        if (previewSprite != null)
          BossSkillBase.ShowSummonFadeIn(new Vector3(pos.x, pos.y, 0f), previewSprite, Vector3.one * 1.2f, 0.5f);

        var part = SpawnPart(
          enemyId: PART_PHANTOM_ID,
          offset: Vector2.zero,
          damageMode: BossPart.DamageMode.Independent,
          movementMode: BossPart.MovementMode.None, // 手动同步位置
          isAttackOrigin: false,
          visualScale: PART_VISUAL * 0.8f,
          hpMult: 1f
        );
        part.transform.position = new Vector3(pos.x, pos.y, part.transform.position.z);
        SetPartMaxHp(part, hpPerPhantom);
        _phantoms.Add(part);
      }
      return angles;
    }

    /// <summary>同步幻影位置到玩家周围（每帧由技能调用）</summary>
    public void SyncPhantomPositions(float[] initialAngles, float radius)
    {
      var playerPos = GetPlayerPos();
      for (int i = 0; i < _phantoms.Count && i < initialAngles.Length; i++)
      {
        var p = _phantoms[i];
        if (p == null || p.IsDestroyed) continue;
        float rad = initialAngles[i] * Mathf.Deg2Rad;
        var offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        p.transform.position = new Vector3(playerPos.x + offset.x, playerPos.y + offset.y, p.transform.position.z);
      }
    }

    /// <summary>随机选一个存活幻影，返回其位置并立即销毁</summary>
    public bool PopRandomPhantom(out Vector3 position)
    {
      position = Vector3.zero;
      // 收集存活幻影
      var alive = new List<BossPart>();
      for (int i = _phantoms.Count - 1; i >= 0; i--)
      {
        if (_phantoms[i] != null && !_phantoms[i].IsDestroyed)
          alive.Add(_phantoms[i]);
        else
          _phantoms.RemoveAt(i);
      }
      if (alive.Count == 0) return false;

      int idx = Random.Range(0, alive.Count);
      var chosen = alive[idx];
      position = chosen.transform.position;
      _phantoms.Remove(chosen);
      DestroyPart(chosen);
      return true;
    }

    /// <summary>销毁所有幻影</summary>
    public void DestroyAllPhantoms()
    {
      _skill4Active = false;
      for (int i = _phantoms.Count - 1; i >= 0; i--)
      {
        if (_phantoms[i] != null) DestroyPart(_phantoms[i]);
      }
      _phantoms.Clear();
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
      // 订阅伤害事件（护盾免伤）
      var health = Core?.Health;
      if (health != null) health.Damaged += OnBossDamaged;

      RegisterSkill(new HexKingSkill_Shield());
      RegisterSkill(new HexKingSkill_PhantomRush());
      RegisterSkill(new HexKingSkill_DoubleDash());
      RegisterSkill(new HexKingSkill_TrailDash());
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      // ── 护盾冷却计时 ──
      if (_shieldBreakCooldownTimer > 0f)
        _shieldBreakCooldownTimer -= dt;

      // ── 护盾位置同步 ──
      SyncShieldPosition();
      SyncBossShieldBar();

      // ── 被动：HP < 50% 触发（仅一次）──
      if (!_passiveTriggered && GetHpRatio() < 0.5f)
      {
        _passiveTriggered = true;
        SummonShield(PASSIVE_SHIELD_HP_RATIO);      // 10% HP 护盾
        SetSkillCooldown(SKILL_PHANTOM, 0f);         // 清除技能 4 CD
      }

      // ── Idle 移动：维持中等略近距离 ──
      if (ActiveSkill == null)
        HandleIdleMovement(dt);
    }

    public override void OnPartDestroyed(BossPart part)
    {
      // 护盾被击破
      if (part == _shieldPart)
      {
        _shieldPart = null;
        _shieldBreakCooldownTimer = SHIELD_BREAK_COOLDOWN;
        SyncBossShieldBar();

        // 护盾破碎后自身流失 10% 最大生命（至少保留 1）
        var h = Core?.Health;
        if (h != null)
        {
          float penalty = Mathf.Max(1f, h.MaxHp * 0.10f);
          float maxAllowed = Mathf.Max(0f, h.CurrentHp - 1f);
          if (penalty > 0f && maxAllowed > 0f)
            SelfDamage(Mathf.Min(penalty, maxAllowed));
        }
      }

      // 幻影被击破（技能 4 期间）
      _phantoms.Remove(part);
    }

    protected override void OnDestroy()
    {
      var health = Core?.Health;
      if (health != null) health.Damaged -= OnBossDamaged;
      base.OnDestroy();
    }

    protected override Transform SelectAttackOrigin() => transform;

    // ══════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════

    void HandleIdleMovement(float dt)
    {
      float dist = DistToPlayer();
      if (dist < IDEAL_DIST_MIN)
      {
        // 太近 → 后退
        var away = (Position - GetPlayerPos()).normalized;
        MoveInDir(away, NORMAL_SPEED, dt);
      }
      else if (dist > IDEAL_DIST_MAX)
      {
        // 太远 → 靠近
        MoveInDir(DirToPlayer(), NORMAL_SPEED, dt);
      }
      // 在理想范围内不动
    }

    void OnBossDamaged(float amount)
    {
      if (_handlingDamage) return;
      if (!HasShield) return;

      _handlingDamage = true;
      var h = Core?.Health;
      if (h != null && !h.IsDead)
        h.Heal(amount);
      // 注：致命一击（damage ≥ currentHp）会穿透护盾直接致死，
      // 因 Health.Heal 在 _dead=true 时直接返回。
      // 此边缘情况极少发生（Boss 血量较高），可接受。
      _handlingDamage = false;
    }

    static void SetPartMaxHp(BossPart part, float maxHp)
    {
      if (part == null) return;
      var h = part.GetComponent<Health>();
      if (h != null) h.Configure(Mathf.Max(1f, maxHp));
    }
  }
}
