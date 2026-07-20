using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Boss 抽象基类。提供部件生命周期管理、攻击源选择、阶段切换、
  /// 以及状态机驱动的技能调度系统。
  ///
  /// <b>技能调度（新增）</b>：
  ///   子类在 OnBossStart 中 RegisterSkill(BossSkillBase) 注册技能，
  ///   引擎自动管理冷却 → 优先级排序 → 条件判定 → OnEnter/OnUpdate/OnExit 生命周期。
  ///   未注册技能时保持旧版兼容（走 OnBossUpdate）。
  ///
  /// 部件存活管理：
  ///   - DamageMode.Independent/Shared → 通过 EnemySpawner 生成为结构敌人，注册到 EnemyRegistry
  ///   - DamageMode.None → 创建普通 GameObject（无 EnemyCore/Health），不注册
  /// </summary>
  [DisallowMultipleComponent]
  public abstract class BossCore : MonoBehaviour
  {
    [Header("Phases")]
    [SerializeField] protected float[] _phaseHpThresholds = { 0.5f, 0.25f };

    EnemyCore _core;
    EnemyAttack _attack;
    readonly List<BossPart> _parts = new();
    readonly List<Transform> _attackOrigins = new();

    int _currentPhase;
    bool _initialised;

    // ══════════════════════════════════════════════════
    //  技能调度系统
    // ══════════════════════════════════════════════════
    readonly List<BossSkillBase> _skills = new();
    readonly Dictionary<string, float> _cooldowns = new();
    BossSkillBase _activeSkill;
    float _skillElapsed;
    float _globalCooldownMult = 1f;
    float _skillIntroGrace;
    int _activeAttackInstanceId;
    string _lastSkillId;
    BossSkillCategory _lastSkillCategory;
    float _globalRecoveryTimer;
    int _consecutivePressureCount;

    public int ActiveAttackInstanceId => _activeAttackInstanceId;

    public EnemyCore Core => _core;
    public int CurrentPhase => _currentPhase;
    public Vector2 Position => GameplayPlane.Position2D(transform);

    /// <summary>是否正在使用技能系统（注册了技能则为 true）</summary>
    protected bool IsUsingSkillSystem => _skills.Count > 0;

    /// <summary>当前执行中的技能（Idle 时为 null）</summary>
    protected BossSkillBase ActiveSkill => _activeSkill;

    public string ActiveSkillId => _activeSkill?.Id;

    // ══════════════════════════════════════════════════
    //  子类重写
    // ══════════════════════════════════════════════════

    protected abstract void OnBossStart();
    protected abstract void OnBossUpdate(float dt);
    protected abstract Transform SelectAttackOrigin();
    protected virtual void OnPhaseChanged(int fromPhase, int toPhase) { }
    protected virtual void OnPartDamaged(BossPart part, float amount) { }
    public virtual void OnPartDestroyed(BossPart part) { }

    // ══════════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════════

    protected virtual void Awake()
    {
      _core = GetComponent<EnemyCore>();
      _attack = GetComponent<EnemyAttack>();
    }

    protected virtual void Start()
    {
      if (_core?.Health != null)
        _core.Health.Damaged += OnCoreDamaged;

      OnBossStart();
      ApplyWaveContextTuning();

      // 子类在 OnBossStart 中注册技能后，禁用 EnemyCore 自主 AI
      if (IsUsingSkillSystem && _core != null)
        _core.enabled = false;

      _initialised = true;
    }

    protected virtual void Update()
    {
      if (!_initialised || _core?.Health == null || _core.Health.IsDead) return;

      float dt = Time.deltaTime;

      // 更新部件位置
      for (int i = _parts.Count - 1; i >= 0; i--)
      {
        var p = _parts[i];
        if (p == null || p.IsDestroyed) { _parts.RemoveAt(i); continue; }
        p.TickPart(dt);
      }

      // 阶段检测
      CheckPhaseTransition();

      if (IsUsingSkillSystem)
      {
        UpdateSkillCooldowns(dt);
        OnPassiveUpdate(dt);
        UpdateSkillFSM(dt);
      }
      else
      {
        // 向后兼容：子类未注册技能时保持原有行为
        OnBossUpdate(dt);
      }

      // 攻击源设置
      if (_attack != null)
      {
        var origin = SelectAttackOrigin();
        _attack.AttackOrigin = origin;
      }
    }

    protected virtual void OnDestroy()
    {
      if (_core?.Health != null)
        _core.Health.Damaged -= OnCoreDamaged;

      // 清理技能
      if (_activeSkill != null)
      {
        _activeSkill.OnExit(this);
        _activeSkill = null;
      }
      foreach (var skill in _skills)
        skill.OnDestroy(this);
      _skills.Clear();
      _cooldowns.Clear();

      DestroyAllParts();
    }

    // ══════════════════════════════════════════════════
    //  部件工厂
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 生成一个部件。
    ///   Independent/Shared → 通过 EnemySpawner 生成结构敌人，有 EnemyCore+Health。
    ///   None → 创建纯视觉 GO（无碰撞/无 EnemyCore/无注册）。
    /// </summary>
    protected BossPart SpawnPart(
      string enemyId,
      Vector2 offset,
      BossPart.DamageMode damageMode,
      BossPart.MovementMode movementMode,
      bool isAttackOrigin = false,
      float visualScale = 1.5f,
      float orbitRadius = 3f,
      float orbitSpeed = 45f,
      float orbitStartAngle = 0f,
      float hpMult = 1f)
    {
      BossPart part;

      if (damageMode == BossPart.DamageMode.None)
      {
        // 纯视觉部件 — 无 EnemyCore，不注册，不可被命中
        var go = new GameObject($"BossPart_None_{_parts.Count}");
        go.transform.position = (Vector3)(Position + offset);
        // 添加基本视觉
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>($"Sprites/Enemies/Bosses/{enemyId}");
        if (sr.sprite == null)
          sr.sprite = Resources.Load<Sprite>($"Sprites/Enemies/Minions/{enemyId}");
        sr.color = new Color(0.7f, 0.3f, 0.2f, 0.8f); // 红褐色调的部件
        go.transform.localScale = Vector3.one * visualScale;
        part = go.AddComponent<BossPart>();
      }
      else
      {
        // 可命中部件 — 通过 EnemySpawner 生成结构敌人
        var spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
          Debug.LogError("[BossCore] No EnemySpawner found for part spawn!");
          return null;
        }
        var go = spawner.SpawnEnemyForBossSystems(enemyId, Position + offset);
        if (go == null) return null;

        // 结构敌人不需要词条（覆盖之前可能生成的默认词条）
        var ec = go.GetComponent<EnemyCore>();
        if (ec != null)
        {
          ec.DisableAiStyle = true;
          ec.SetAffixSet(null); // 清除默认词条
        }

        part = go.AddComponent<BossPart>();
        go.transform.position = (Vector3)(Position + offset);
      }

      // 配置 BossPart
      part.Configure(damageMode, movementMode, offset, orbitRadius, orbitSpeed, orbitStartAngle);
      part.Initialize(this);
      part.IsAttackOrigin = isAttackOrigin;

      if (damageMode == BossPart.DamageMode.Independent && hpMult > 0f)
      {
        var h = part.GetComponent<Health>();
        if (h != null) h.Configure(h.MaxHp * hpMult);
      }

      _parts.Add(part);
      if (isAttackOrigin) _attackOrigins.Add(part.transform);

      return part;
    }

    // ══════════════════════════════════════════════════
    //  部件查询
    // ══════════════════════════════════════════════════

    protected IReadOnlyList<Transform> GetAttackOrigins() => _attackOrigins;

    protected bool TryGetPart(int index, out BossPart part)
    {
      if (index >= 0 && index < _parts.Count)
      {
        part = _parts[index];
        return part != null && !part.IsDestroyed;
      }
      part = null;
      return false;
    }

    // ══════════════════════════════════════════════════
    //  伤害转发
    // ══════════════════════════════════════════════════

    /// <summary>接收 Shared 模式部件的转发伤害。</summary>
    public void ForwardDamage(float amount)
    {
      _core?.Health?.TakeDamage(amount);
    }

    void OnCoreDamaged(float amount)
    {
      CheckPhaseTransition();
    }

    // ══════════════════════════════════════════════════
    //  阶段切换
    // ══════════════════════════════════════════════════

    void CheckPhaseTransition()
    {
      if (_core?.Health == null || _core.Health.IsDead) return;
      if (_phaseHpThresholds == null || _phaseHpThresholds.Length == 0) return;

      float hpRatio = _core.Health.CurrentHp / _core.Health.MaxHp;
      int newPhase = 0;
      for (int i = 0; i < _phaseHpThresholds.Length; i++)
      {
        if (hpRatio <= _phaseHpThresholds[i])
          newPhase = i + 1;
      }

      if (newPhase != _currentPhase)
      {
        int old = _currentPhase;
        _currentPhase = newPhase;
        OnPhaseChanged(old, newPhase);
      }
    }

    protected void TransitionToPhase(int phase)
    {
      if (phase != _currentPhase)
      {
        int old = _currentPhase;
        _currentPhase = phase;
        OnPhaseChanged(old, phase);
      }
    }

    // ══════════════════════════════════════════════════
    //  技能调度系统
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 注册一个技能。按 Priority 升序插入列表（数值越小越优先）。
    /// 子类在 OnBossStart 中调用。
    /// </summary>
    protected void RegisterSkill(BossSkillBase skill)
    {
      if (skill == null) return;
      _cooldowns[skill.Id] = 0f;
      // 按 priority 升序插入
      int idx = 0;
      for (; idx < _skills.Count; idx++)
        if (_skills[idx].Priority > skill.Priority) break;
      _skills.Insert(idx, skill);
    }

    /// <summary>查询技能剩余冷却秒数。未注册的技能返回 0。</summary>
    protected float GetSkillCooldown(string id) =>
      _cooldowns.TryGetValue(id, out float v) ? v : 0f;

    /// <summary>强制设置技能剩余冷却秒数（供阶段切换或被动逻辑使用）</summary>
    public void SetSkillCooldown(string id, float remaining)
    {
      if (_cooldowns.ContainsKey(id))
        _cooldowns[id] = Mathf.Max(0f, remaining);
    }

    /// <summary>强制中断当前技能，切回 Idle</summary>
    protected void CancelActiveSkill()
    {
      if (_activeSkill == null) return;
      _activeSkill.OnExit(this);
      if (_activeAttackInstanceId > 0)
        BossAttackHitTracker.ClearAttackInstance(_activeAttackInstanceId);
      _activeAttackInstanceId = 0;
      _activeSkill = null;
    }

    /// <summary>技能是否在冷却中</summary>
    protected bool IsSkillCoolingDown(string id) => GetSkillCooldown(id) > 0f;

    /// <summary>Boss 全局冷却倍率（1=正常，0.5=双倍冷却速度）</summary>
    protected float GlobalCooldownMult
    {
      get => _globalCooldownMult;
      set => _globalCooldownMult = Mathf.Max(0.1f, value);
    }

    public void SetSkillIntroGrace(float seconds) =>
      _skillIntroGrace = Mathf.Max(_skillIntroGrace, seconds);

    protected void ApplyWaveContextTuning()
    {
      var ctx = GetComponent<BossWaveContext>();
      if (ctx == null)
        return;

      if (ctx.EncounterCooldownMult > 0f)
        GlobalCooldownMult = ctx.EncounterCooldownMult;
    }

    public bool IsSkillEnabled(string skillId)
    {
      var ctx = GetComponent<BossWaveContext>();
      return ctx == null || ctx.IsSkillEnabled(skillId);
    }

    // ── 内部实现 ──────────────────────────────

    void UpdateSkillCooldowns(float dt)
    {
      var keys = new List<string>(_cooldowns.Keys);
      foreach (var id in keys)
        if (_cooldowns[id] > 0f)
          _cooldowns[id] -= dt;
    }

    void UpdateSkillFSM(float dt)
    {
      if (_skillIntroGrace > 0f)
      {
        _skillIntroGrace -= dt;
        return;
      }

      if (_activeSkill != null)
      {
        var state = _activeSkill.OnUpdate(this, dt, _skillElapsed);
        _skillElapsed += dt;
        if (state == BossSkillBase.State.Completed)
          CompleteActiveSkill();
        return;
      }

      if (_globalRecoveryTimer > 0f)
      {
        _globalRecoveryTimer -= dt;
        return;
      }

      var defaults = BossBalanceDatabase.Defaults;
      BossSkillBase repeatFallback = null;
      for (int i = 0; i < _skills.Count; i++)
      {
        var skill = _skills[i];
        if (_cooldowns[skill.Id] > 0f) continue;
        if (!IsSkillEnabled(skill.Id)) continue;
        if (!skill.CanTrigger(this)) continue;
        if (!CanScheduleSkill(skill, defaults))
        {
          if (skill.Id == _lastSkillId)
            repeatFallback = skill;
          continue;
        }

        BeginSkill(skill);
        return;
      }

      // 仅剩一个可触发技能且被「不可连放」规则挡住时，允许重复释放，避免护盾期间技能链卡死。
      if (repeatFallback != null)
        BeginSkill(repeatFallback);
    }

    void BeginSkill(BossSkillBase skill)
    {
      _activeSkill = skill;
      _skillElapsed = 0f;
      _activeAttackInstanceId = BossAttackHitTracker.NewAttackInstanceId();
      BossCombatDebugLog.SetActiveSkill(skill.Id, _activeAttackInstanceId);
      skill.OnEnter(this);
    }

    void CompleteActiveSkill()
    {
      if (_activeSkill == null)
        return;

      var finished = _activeSkill;
      finished.OnExit(this);
      _cooldowns[finished.Id] = finished.Cooldown * _globalCooldownMult;

      if (finished.IsPressureSkill)
      {
        _consecutivePressureCount++;
        _globalRecoveryTimer = Mathf.Max(
          _globalRecoveryTimer,
          BossBalanceDatabase.Defaults.global_recovery_pressure_sec);
      }
      else
      {
        _consecutivePressureCount = 0;
        _globalRecoveryTimer = Mathf.Max(
          _globalRecoveryTimer,
          BossBalanceDatabase.Defaults.global_recovery_light_sec);
      }

      _lastSkillId = finished.Id;
      _lastSkillCategory = finished.Category;

      if (_activeAttackInstanceId > 0)
        BossAttackHitTracker.ClearAttackInstance(_activeAttackInstanceId);

      _activeAttackInstanceId = 0;
      _activeSkill = null;
    }

    bool CanScheduleSkill(BossSkillBase skill, BossBalanceDatabase.DefaultsDef defaults)
    {
      if (!string.IsNullOrEmpty(_lastSkillId) && skill.Id == _lastSkillId)
        return false;

      if (skill.IsPressureSkill
          && _lastSkillCategory == BossSkillCategory.AreaDenial
          && _consecutivePressureCount >= defaults.max_consecutive_pressure)
        return false;

      if (skill.IsPressureSkill
          && (_lastSkillCategory == BossSkillCategory.Ultimate
              || _lastSkillCategory == BossSkillCategory.Summon)
          && _consecutivePressureCount >= defaults.max_consecutive_pressure)
        return false;

      return true;
    }

    // ══════════════════════════════════════════════════
    //  被动逻辑钩子（每帧 Idle 和 Executing 均调用）
    //  默认调用旧版 OnBossUpdate 保持向后兼容
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 常驻被动逻辑钩子。每帧 Update 中调用（无论 Idle/Executing）。
    /// 子类可重写以处理血量检测、跨技能变量维护、Buff 更新等。
    /// 默认为兼容旧版子类而调用 OnBossUpdate。
    /// </summary>
    protected virtual void OnPassiveUpdate(float dt) => OnBossUpdate(dt);

    // ══════════════════════════════════════════════════
    //  清理
    // ══════════════════════════════════════════════════

    void DestroyAllParts()
    {
      foreach (var p in _parts)
        if (p != null) Destroy(p.gameObject);
      _parts.Clear();
      _attackOrigins.Clear();
    }

    public void DestroyPart(BossPart part)
    {
      if (part == null) return;
      _parts.Remove(part);
      _attackOrigins.Remove(part.transform);
      Destroy(part.gameObject);
    }
  }
}
