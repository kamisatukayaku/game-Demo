using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Player;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.World
{
  /// <summary>
  /// World 模式玩家攻击桥接层。
  ///
  /// 职责：
  ///   1. Q/E 切换近战/远程攻击模式（默认近战）
  ///   2. R 切换自动攻击开关
  ///   3. 从 AttributeManager 读取属性 → 注入 PlayerAttackDirector / Health / PlayerSphereController
  ///   4. 管理 HP 同步、HP 回复、移速同步等基础属性
  ///
  /// 挂载方式：挂载在 Player GameObject 上，
  /// 由 WorldCombatSceneBootstrap.ApplyPlayerComponents() 创建。
  /// </summary>
  [DisallowMultipleComponent]
  public class WorldPlayerAttackBridge : MonoBehaviour
  {
    [Header("Refresh")]
    [SerializeField] float _refreshInterval = 0.3f;

    [Header("HP Regen")]
    [SerializeField] float _hpRegenInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] bool _debugLog;

    PlayerAttackDirector _director;
    Health _health;
    PlayerSphereController _controller;
    float _refreshTimer;
    float _hpRegenTimer;
    bool _isRanged;
    bool _autoAttack;
    bool _hpInitialized;

    // ══════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════

    void Awake()
    {
      _director = GetComponent<PlayerAttackDirector>();
      if (_director == null)
      {
        Debug.LogWarning("[WorldPlayerAttackBridge] No PlayerAttackDirector on this GameObject.");
        enabled = false;
        return;
      }

      _health = GetComponent<Health>();
      _controller = GetComponent<PlayerSphereController>();

      // 默认近战
      SetAttackMode(false);

      if (_debugLog)
        Debug.Log("[WorldPlayerAttackBridge] Initialized. Mode=melee, AutoAttack=off");
    }

    void Update()
    {
      if (_director == null || !WorldRuntimeContext.IsWorldModeActive) return;

      // 模式切换
      HandleModeSwitch();

      // 自动攻击切换
      HandleAutoAttackToggle();

      // 周期性属性刷新（0.3s 间隔）
      _refreshTimer += Time.deltaTime;
      if (_refreshTimer >= _refreshInterval)
      {
        _refreshTimer -= _refreshInterval;
        RefreshFromAttributes();
      }

      // HP 回复（0.25s 间隔）
      _hpRegenTimer += Time.deltaTime;
      if (_hpRegenTimer >= _hpRegenInterval)
      {
        _hpRegenTimer -= _hpRegenInterval;
        ApplyHpRegen();
      }
    }

    void OnDestroy()
    {
      // 恢复默认
      if (_director != null)
      {
        _director.SetAutoAttack(false);
        SetAttackMode(false);
      }
    }

    // ══════════════════════════════════════════════════════
    //  HP 回复
    // ══════════════════════════════════════════════════════

    void ApplyHpRegen()
    {
      if (_health == null || _health.IsDead) return;

      var attr = WorldManager.Instance?.Attributes;
      if (attr == null) return;

      var regenPerSec = attr.GetValue("hp_regen", 0f);
      if (regenPerSec <= 0f) return;

      _health.Heal(regenPerSec * _hpRegenInterval);
    }

    // ══════════════════════════════════════════════════════
    //  模式切换 (Q/E)
    // ══════════════════════════════════════════════════════

    void HandleModeSwitch()
    {
      if (Input.GetKeyDown(KeyCode.Q))
      {
        if (_isRanged)
          SetAttackMode(false);
      }
      else if (Input.GetKeyDown(KeyCode.E))
      {
        if (!_isRanged)
          SetAttackMode(true);
      }
    }

    void SetAttackMode(bool ranged)
    {
      _isRanged = ranged;

      if (_director == null) return;

      // 通过设置 attack profile 切换交付方式
      // melee: 使用 hybrid 模式（近距离近战，远距离自动切弹体）
      // ranged: 使用 projectile 模式（始终发射弹体）
      var profileId = ranged ? "weapon_starter_bolt" : "weapon_starter_melee";

      // PlayerAutoAttack 是 legacy 封装，若存在则通过它切换
      var legacy = GetComponent<PlayerAutoAttack>();
      if (legacy != null)
      {
        legacy.SetAttackModeFromProfile(profileId);
      }
      else
      {
        // 直接通过反射或公开字段设置（attackProfileId 是 serialized field）
        var profileField = typeof(PlayerAttackDirector).GetField("attackProfileId",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        profileField?.SetValue(_director, profileId);
      }

      if (_debugLog)
        Debug.Log($"[WorldPlayerAttackBridge] Attack mode: {(ranged ? "ranged" : "melee")} (profile={profileId})");
    }

    // ══════════════════════════════════════════════════════
    //  自动攻击切换 (R)
    // ══════════════════════════════════════════════════════

    void HandleAutoAttackToggle()
    {
      if (GameInputBindings.WasPressed(WorldInputKeys.AutoAttack))
      {
        _autoAttack = !_autoAttack;
        _director?.SetAutoAttack(_autoAttack);

        if (_debugLog)
          Debug.Log($"[WorldPlayerAttackBridge] Auto-attack: {(_autoAttack ? "ON" : "OFF")}");
      }
    }

    // ══════════════════════════════════════════════════════
    //  属性表 → 各系统注入
    // ══════════════════════════════════════════════════════

    void RefreshFromAttributes()
    {
      if (_director == null) return;

      var attr = WorldManager.Instance?.Attributes;
      if (attr == null) return;

      // ══ 1. 最大生命值同步 ══
      SyncMaxHp(attr);

      // ══ 2. 移动速度同步 ══
      SyncMoveSpeed(attr);

      // ══ 3. SetEquipmentModifiers ══
      SyncEquipmentModifiers(attr);

      // ══ 4. SetBuildModifiers ══
      SyncBuildModifiers(attr);
    }

    /// <summary>同步最大生命值到 Health 组件，保持当前血量比例。</summary>
    void SyncMaxHp(AttributeManager attr)
    {
      if (_health == null) return;

      var maxHp = attr.GetValue("max_hp", 100f);
      var maxHpMult = attr.GetValue("max_hp_mult", 1f);
      var finalMaxHp = maxHp * maxHpMult;

      if (!_hpInitialized)
      {
        _health.Configure(finalMaxHp);
        _hpInitialized = true;
        return;
      }

      var currentPercent = _health.HpPercent;
      _health.ApplyEquipmentMaxHp(finalMaxHp, currentPercent);
    }

    /// <summary>同步移动速度到 PlayerSphereController。</summary>
    void SyncMoveSpeed(AttributeManager attr)
    {
      if (_controller == null) return;

      var moveSpeedMult = attr.GetValue("move_speed_mult", 1f);
      _controller.SetBuildMoveSpeedMultipliers(moveSpeedMult, 0f);
    }

    /// <summary>注入装备基础属性到 PlayerAttackDirector。</summary>
    void SyncEquipmentModifiers(AttributeManager attr)
    {
      var attackMult   = attr.GetValue("attack_mult", 1f);
      var allDmgMult   = attr.GetValue("all_damage_mult", 1f);
      var attackFlat   = attr.GetValue("attack", 0f);
      var critChance   = attr.GetValue("crit_chance", 0.05f);
      var critDmgMult  = attr.GetValue("crit_damage_mult", 1.5f);
      var lifesteal    = attr.GetValue("lifesteal", 0f);
      var rangedRange  = attr.GetValue("ranged_range", float.MaxValue);

      // 攻速：从 AttributeManager 读取，默认 1.0
      var attackSpeed  = attr.GetValue("attack_speed_mult", 1f);
      var cooldownMult = 1f;

      // 射程加成：包含近战范围加成（对 melee/hybrid 模式生效）
      var rangeAdd     = attr.GetValue("melee_range_add", 0f);

      var effectiveAttackMult = attackMult * allDmgMult;
      if (rangedRange <= 0f) rangedRange = float.MaxValue;

      _director.SetEquipmentModifiers(
        effectiveAttackMult,
        attackSpeed,
        rangeAdd,
        critChance,
        critDmgMult,
        lifesteal,
        cooldownMult,
        attackFlatAdd: attackFlat,
        rangedRange: rangedRange
      );
    }

    /// <summary>注入构筑特效属性到 PlayerAttackDirector。</summary>
    void SyncBuildModifiers(AttributeManager attr)
    {
      // 追踪角度：>0 时启用追踪
      var homingAngle = attr.GetValue("homing_angle", 0f);
      var homingEnabled = homingAngle > 0f;
      var turnRate = homingEnabled ? homingAngle : 0f;

      // 近战角度：75° 基准 + 加成
      var meleeAngleAdd = attr.GetValue("melee_angle_add", 0f);
      var meleeArcHalfAngle = 75f + meleeAngleAdd;

      var mods = new PlayerAttackDirector.BuildModifiers
      {
        // ── 弹体通用 ──
        extraProjectiles   = CeilAttr(attr, "projectile_count"),
        pierceCount        = CeilAttr(attr, "pierce"),
        chainCount         = CeilAttr(attr, "chain_count"),
        explosionRadius    = attr.GetValue("explosion_radius"),
        explosionDamageMult = attr.GetValue("explosion_dmg_mult"),
        chainJumpRange     = attr.GetValue("chain_range"),
        chainDamageRatio    = attr.GetValue("chain_dmg_ratio"),
        projectileSpeedMult = attr.GetValue("projectile_speed_mult"),
        slowChance         = attr.GetValue("slow_chance"),
        slowAmount         = attr.GetValue("slow_amount"),
        burnDps            = attr.GetValue("burn_dps"),
        burnDuration       = attr.GetValue("burn_duration"),
        eliteDamageMult    = attr.GetValue("elite_dmg_mult"),
        bossDamageMult     = attr.GetValue("boss_dmg_mult"),
        healOnHitPct       = attr.GetValue("heal_on_hit"),

        // ── 追踪 ──
        homing             = homingEnabled,
        weakHoming         = homingEnabled,
        homingTurnRate     = turnRate,

        // ── 连发 ──
        burstCount         = CeilAttr(attr, "burst_count"),

        // ── 近战专属 ──
        meleeExplosionRadius  = attr.GetValue("melee_explosion_radius"),
        meleeKnockbackChance  = attr.GetValue("melee_knockback_chance"),
        meleeSlowChance       = attr.GetValue("melee_slow_chance"),
        meleeSlowAmount       = attr.GetValue("melee_slow_amount"),
        meleeBleedDps         = attr.GetValue("melee_bleed_dps"),
        meleeBleedDuration    = attr.GetValue("melee_bleed_duration"),
        meleeBurnDps          = attr.GetValue("melee_burn_dps"),
        meleeBurnDuration     = attr.GetValue("melee_burn_duration"),
        meleeComboCount       = CeilAttr(attr, "melee_combo_count"),
        meleeArcHalfAngle     = meleeArcHalfAngle,

        // ── 技能专属 ──
        skillExtraProjectiles = CeilAttr(attr, "skill_projectile_count"),
        skillCritChance       = attr.GetValue("skill_crit_chance"),
        skillCritDamage       = attr.GetValue("skill_crit_dmg"),
        skillChainCount       = CeilAttr(attr, "skill_chain_count"),
        skillPierce           = CeilAttr(attr, "skill_pierce"),
        skillExplosionRadius  = attr.GetValue("skill_explosion_radius"),
        skillExplosionRatio   = attr.GetValue("skill_explosion_ratio"),
        skillDamageMult       = attr.GetValue("skill_dmg_mult"),
        skillRangeMult        = attr.GetValue("skill_range_mult"),
      };

      _director.SetBuildModifiers(mods);
    }

    // ══════════════════════════════════════════════════════
    //  类型转换辅助
    // ══════════════════════════════════════════════════════

    /// <summary>从 AttributeManager 读取 float 属性并向上取整为 int。</summary>
    static int CeilAttr(AttributeManager attr, string key)
    {
      var v = attr.GetValue(key, 0f);
      return v > 0f ? Mathf.CeilToInt(v) : 0;
    }

    static class Mathf
    {
      public static int CeilToInt(float v) => (int)System.Math.Ceiling(v);
    }
  }
}
