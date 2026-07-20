using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Vfx;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Boss 技能抽象基类。每个技能拥有独立的冷却、优先级、触发条件和多帧生命周期。
  ///
  /// 生命周期：
  ///   CanTrigger() → OnEnter() → OnUpdate()（每帧，返回 Running/Completed）→ OnExit()
  ///
  /// 子类可维护自己的内部状态机（波次计数、计时器、阶段标记等），
  /// 天然支持持续光束、多波弹幕、追踪攻击等需要跨帧运行的技能。
  /// </summary>
  public abstract class BossSkillBase
  {
    /// <summary>技能唯一标识</summary>
    public string Id { get; protected set; }

    /// <summary>优先级（数值越小越优先，冷却就绪后按此顺序搜索）</summary>
    public int Priority { get; protected set; }

    /// <summary>冷却时间（秒）。技能结束后重置为此值 × 全局倍率</summary>
    public float Cooldown { get; protected set; }

    /// <summary>技能分类 — 用于 BossCore 轻量调度</summary>
    public BossSkillCategory Category { get; protected set; } = BossSkillCategory.Projectile;

    public bool IsPressureSkill =>
      Category == BossSkillCategory.AreaDenial
      || Category == BossSkillCategory.Ultimate
      || Category == BossSkillCategory.Summon;

    /// <summary>OnUpdate 返回的状态</summary>
    public enum State { Running, Completed }

    // ══════════════════════════════════════════════════
    //  子类必须实现
    // ══════════════════════════════════════════════════

    /// <summary>返回 true 表示该技能当前可以触发（距离、血量阈值、阶段等）</summary>
    public abstract bool CanTrigger(BossCore boss);

    /// <summary>技能开始执行（仅调用一次）</summary>
    public abstract void OnEnter(BossCore boss);

    /// <summary>
    /// 每帧调用，返回 Running 则下一帧继续，返回 Completed 则结束并切回 Idle。
    /// skillDt 为从 OnEnter 开始累计的时长，dt 为本帧 delta。
    /// </summary>
    public abstract State OnUpdate(BossCore boss, float dt, float skillDt);

    /// <summary>技能结束（正常完成或中断时均会调用）</summary>
    public abstract void OnExit(BossCore boss);

    // ══════════════════════════════════════════════════
    //  可选重写
    // ══════════════════════════════════════════════════

    /// <summary>Boss 销毁时调用（清理资源）</summary>
    public virtual void OnDestroy(BossCore boss) { }

    /// <summary>调试用显示名称</summary>
    public virtual string DebugName => Id;

    // ══════════════════════════════════════════════════
    //  便捷工具方法（子类在 OnEnter/OnUpdate 中调用）
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 用 Boss 的 EnemyAttack 基础属性 × 倍率 重新配置攻击参数。
    /// dmgMult 基于 Boss 当前的 attackDamage；speedMult 影响 cooldown/windup；
    /// rangeOverride > 0 时覆盖射程，否则使用 Boss 默认值。
    /// </summary>
    protected void ConfigAttack(BossCore boss, float dmgMult = 1f, float speedMult = 1f, float rangeOverride = 0f)
    {
      var attack = boss.Core?.Attack;
      if (attack == null) return;

      float baseDamage = attack.AttackDamage;
      float baseCooldown = attack.AttackCooldown;
      float attackWindup = 0f; // EnemyAttack 没有公开 Windup 属性，用 0 表示不改
      float baseRange = attack.AttackKind == EnemyAttackKind.Ranged
        ? attack.RangedAttackRange
        : attack.AttackRange;

      float dmg = baseDamage * dmgMult;
      float cd = baseCooldown / Mathf.Max(0.1f, speedMult);
      float range = rangeOverride > 0f ? rangeOverride : baseRange;

      attack.ApplyScaledStats(dmg, cd, attackWindup, range, 1f);
    }

    /// <summary>
    /// 使用当前 Boss 的 EnemyAttack 配置向目标直接开火（跳过冷却/距离检查）。
    /// target 为 null 时使用当前追逐目标（玩家）。
    /// </summary>
    protected void FireAttack(BossCore boss, Transform target = null)
    {
      boss.Core?.Attack?.ForceFire(target);
    }

    /// <summary>获取玩家当前位置（平面坐标）</summary>
    protected Vector2 GetPlayerPos(BossCore boss)
    {
      var target = boss.Core?.ChaseTarget;
      if (target == null)
      {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) return GameplayPlane.Position2D(playerGO.transform);
        return Vector2.zero;
      }
      return GameplayPlane.Position2D(target);
    }

    /// <summary>获取 Boss 当前血量比例 [0, 1]</summary>
    protected float GetHpRatio(BossCore boss)
    {
      var health = boss.Core?.Health;
      if (health == null || health.MaxHp <= 0f) return 0f;
      return health.CurrentHp / health.MaxHp;
    }

    // ══════════════════════════════════════════════════
    //  共享视觉工具
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 显示激光蓄力瞄准线（红色细线，无伤害）。
    /// 从 origin 沿 direction 延伸 range 距离，持续 duration 秒后自动销毁。
    /// </summary>
    protected static void ShowLaserAimLine(Vector3 origin, Vector2 direction, float range, float duration = 0.5f)
    {
      var go = new GameObject("LaserAimLine");
      var lr = go.AddComponent<LineRenderer>();
      lr.startWidth = 0.04f;
      lr.endWidth   = 0.04f;
      lr.material   = new Material(Shader.Find("Sprites/Default"));
      lr.startColor = new Color(1f, 0.15f, 0.05f, 0.7f);
      lr.endColor   = new Color(1f, 0.15f, 0.05f, 0.7f);
      lr.positionCount = 2;
      lr.sortingOrder  = 5;
      var dir3 = new Vector3(direction.x, direction.y, 0f).normalized;
      lr.SetPosition(0, origin);
      lr.SetPosition(1, origin + dir3 * range);
      Object.Destroy(go, duration);
    }

    /// <summary>
    /// 召唤前淡入动画：在 position 处创建半透明贴图，alpha 从 1→0，
    /// 动画完成后自动销毁。duration 约 0.5s。
    /// 用于子部件/小怪召唤前的提示效果。
    /// </summary>
    public static void ShowSummonFadeIn(Vector3 position, Sprite sprite, Vector3 scale, float duration = 0.5f)
    {
      var go = new GameObject("SummonFadeIn");
      go.transform.position = position;
      go.transform.localScale = scale;
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = sprite;
      sr.sortingOrder = 6;
      sr.color = new Color(1f, 1f, 1f, 1f);

      // 通过协程或简单方案：创建时记录时间，每帧递减 alpha
      var fade = go.AddComponent<SummonFadeInBehaviour>();
      fade.Duration = duration;
    }

    /// <summary>
    /// 显示冲撞蓄力瞄准线（橙黄色宽线 + 末端闪烁警告）。
    /// 持续 duration 秒后自动销毁。
    /// </summary>
    protected static void ShowChargeAimLine(Vector3 origin, Vector2 direction, float range, float duration)
    {
      // Charge skills now telegraph through boss charge spin only.
    }

    /// <summary>
    /// 为 Boss 冲撞显示拖尾粒子特效。onDash 回调中 Begin，OnExit 中调用 End。
    /// </summary>
    protected static void StartChargeTrail(BossCore boss, Vector2 direction)
    {
      if (boss == null) return;
      var trail = boss.GetComponent<ChargeDashTrailEffect>();
      if (trail == null)
        trail = ChargeDashTrailEffect.Ensure(boss.gameObject);
      trail.Begin(direction, 0.6f);
    }

    /// <summary>停止冲撞拖尾粒子。</summary>
    protected static void StopChargeTrail(BossCore boss)
    {
      var trail = boss?.GetComponent<ChargeDashTrailEffect>();
      if (trail != null) trail.End();
    }

    protected static void StartChargeSpin(BossCore boss)
    {
      boss?.Core?.MotionVisual?.UseChargeSpin();
    }

    protected static void StopChargeSpin(BossCore boss)
    {
      boss?.Core?.MotionVisual?.ResetSpinMultiplier();
    }

    /// <summary>Ground warning strip for charge skills — visible lane players can dodge.</summary>
    protected static void ShowGroundChargeTelegraph(
      Vector3 origin,
      Vector2 direction,
      float range,
      float width,
      float duration)
    {
      // Charge skills now telegraph through boss charge spin only.
    }

    /// <summary>Simple alpha-fade then destroy behaviour used by ShowSummonFadeIn.</summary>
    class SummonFadeInBehaviour : MonoBehaviour
    {
      public float Duration = 0.5f;
      float _elapsed;

      void Update()
      {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / Duration);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t);
        if (_elapsed >= Duration) Destroy(gameObject);
      }
    }
  }
}
