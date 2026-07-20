using System;
using System.Collections.Generic;

using UnityEngine;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Combat.Events;

namespace Game.World
{
  /// <summary>
  /// World 模式独立玩家等级系统?
  ///
  /// ?Arena 模式?ExperienceSystem + 升级三选一 并行独立运行?
  ///
  /// 设计原则?
  ///   - 不修?Arena 的经验球升级系统
  ///   - 不修?Player 代码（通过属性接口对外暴露加成）
  ///   - 通过事件总线接收 XP 来源（击杀/营地摧毁/事件?
  ///   - 仅提供攻?生命/防御三项基础属性加戀"
  ///
  /// XP 来源?
  ///   1. 击杀怪物     ?CombatEventBus.OnKill（检?Killer 是否为玩家）
  ///   2. 摧毁营地     ?WorldEventBus.CampDestroyed
  ///   3. 完成事件     ?WorldEventBus.RandomEventTriggered
  ///
  /// 属性加成接口（?PlayerAttackDirector / Health 等读取）?
  ///   - GetAttackMult()   ?攻击力倍率
  ///   - GetHpMult()       ?最大生命值倍率
  ///   - GetDefenseMult()  ?防御减伤倍率?~1?
  ///
  /// 配表：data/world/player_levels.json ?WorldDatabase.PlayerLevelDef
  /// </summary>
  public class PlayerLevelSystem : IWorldSystem, IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>击杀普通怪物获得?XP</summary>
    public float XpPerKill { get; set; } = 5f;

    /// <summary>击杀精英怪物获得?XP 倍率（相对普通）</summary>
    public float EliteKillXpMult { get; set; } = 3f;

    /// <summary>击杀 Boss 获得?XP 倍率</summary>
    public float BossKillXpMult { get; set; } = 10f;

    /// <summary>摧毁营地的基础 XP（再乘以营地等级?/summary>
    public float XpPerCampDestroyed { get; set; } = 20f;

    /// <summary>完成事件的基础 XP</summary>
    public float XpPerEventCompleted { get; set; } = 15f;

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态（只读?
    // ══════════════════════════════════════════════════════

    /// <summary>当前玩家等级（≥1?/summary>
    public int Level { get; private set; } = 1;

    /// <summary>当前累积?XP</summary>
    public float TotalXp { get; private set; }

    /// <summary>升到下一级所需?XP</summary>
    public float XpToNextLevel { get; private set; }

    /// <summary>当前等级的配表定?/summary>
    public WorldDatabase.PlayerLevelDef CurrentLevelDef =>
      WorldDatabase.GetPlayerLevel(Level);

    // ══════════════════════════════════════════════════════
    //  内部状态"
    // ══════════════════════════════════════════════════════

    bool _initialized;
    bool _paused;
    int _maxLevel;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Initialize
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      WorldDatabase.EnsureLoaded();
      _maxLevel = WorldDatabase.PlayerMaxLevel;

      // 初始阈值"
      var def = WorldDatabase.GetPlayerLevel(Level + 1);
      XpToNextLevel = def != null ? def.xp_required : 100f;

      // 订阅击杀事件（通过 CombatEventBus ??Arena 共享击杀通道?
      CombatEventBus.OnKill += OnCombatKill;

      // 订阅 World 事件
      WorldEventBus.CampDestroyed += OnCampDestroyed;
      WorldEventBus.RandomEventTriggered += OnRandomEventTriggered;

      _initialized = true;

      // 初始等级属性加成
      SyncToAttributeManager();

      if (DebugLog)
        Debug.Log($"[PlayerLevelSystem] Initialized. LV.{Level} " +
                  $"next={XpToNextLevel:F0}xp maxLV={_maxLevel}");
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Tick（无需每帧逻辑，但保留接口完整性）
    // ══════════════════════════════════════════════════════

    public void Tick(float deltaTime)
    {
      // XP 完全由事件驱动，无需每帧累积
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem ?Pause/Resume/Shutdown
    // ══════════════════════════════════════════════════════

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      CombatEventBus.OnKill -= OnCombatKill;
      WorldEventBus.CampDestroyed -= OnCampDestroyed;
      WorldEventBus.RandomEventTriggered -= OnRandomEventTriggered;

      _initialized = false;

      if (DebugLog)
        Debug.Log("[PlayerLevelSystem] Shut down.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  XP 来源 1 ?击杀怪物（CombatEventBus?
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 接收 Arena 的击杀事件。仅处理玩家击杀的非玩家目标?
    /// 不干?Arena ?ExperienceSystem（两者独立监听同一事件）?
    /// </summary>
    void OnCombatKill(CombatEventBus.KillArgs args)
    {
      if (!_initialized || _paused) return;
      if (args.IsPlayer) return; // 玩家被杀 ?忽略

      // 判断击杀者是否为玩家
      var killer = args.Killer;
      if (killer == null) return;

      var isPlayerKill = killer.CompareTag("Player") || killer.name == "Player";
      if (!isPlayerKill)
      {
        // 也检查是否为玩家的弹体（名字否""PlayerProjectile" 等）
        var killerName = killer.name;
        if (!killerName.StartsWith("Player", StringComparison.OrdinalIgnoreCase) &&
            !killerName.StartsWith("Proj_Player", StringComparison.OrdinalIgnoreCase))
          return;
      }

      // 根据击杀目标类型计算 XP
      var xp = XpPerKill;
      var victimId = args.VictimId ?? "";
      if (victimId.Contains("elite"))
        xp *= EliteKillXpMult;
      else if (victimId.Contains("boss") || victimId.Contains("Boss"))
        xp *= BossKillXpMult;

      AddXp(xp);

      if (DebugLog)
        Debug.Log($"[PlayerLevelSystem] Kill '{victimId}' +{xp:F0}XP ?total={TotalXp:F0}");
    }

    // ══════════════════════════════════════════════════════
    //  XP 来源 2 ?营地摧毁（WorldEventBus?
    // ══════════════════════════════════════════════════════

    void OnCampDestroyed(string campId, WorldCampData campData)
    {
      if (!_initialized || _paused) return;
      if (campData.IsDestroyed == false) return;

      var xp = XpPerCampDestroyed * Mathf.Max(1, campData.CampLevel);
      AddXp(xp);

      if (DebugLog)
        Debug.Log($"[PlayerLevelSystem] Camp '{campId}' destroyed +{xp:F0}XP " +
                  $"(LV.{campData.CampLevel}) ?total={TotalXp:F0}");
    }

    // ══════════════════════════════════════════════════════
    //  XP 来源 3 ?随机事件完成（WorldEventBus?
    // ══════════════════════════════════════════════════════

    void OnRandomEventTriggered(string eventId, int choiceIndex)
    {
      if (!_initialized || _paused) return;

      var xp = XpPerEventCompleted;
      AddXp(xp);

      if (DebugLog)
        Debug.Log($"[PlayerLevelSystem] Event '{eventId}' (choice={choiceIndex}) +{xp:F0}XP " +
                  $"?total={TotalXp:F0}");
    }

    // ══════════════════════════════════════════════════════
    //  XP 累积与升纀"
    // ══════════════════════════════════════════════════════

    /// <summary>添加经验值并检查升级（正值为获得，负值为扣除?/summary>
    public void AddXp(float amount)
    {
      if (!_initialized || amount == 0f) return;
      if (amount < 0f)
      {
        // 扣除 XP（可降至负数），不降纀"
        TotalXp += amount;
        if (TotalXp < 0f) TotalXp = 0f;
        return;
      }
      if (Level >= _maxLevel)
      {
        TotalXp += amount;
        return; // 已达上限，只累积 XP 不升纀"
      }

      var maxLevel = _maxLevel;
      TotalXp += amount;

      // 连续升级检柀"
      while (Level < maxLevel)
      {
        var nextDef = WorldDatabase.GetPlayerLevel(Level + 1);
        var threshold = nextDef?.xp_required ?? float.MaxValue;

        if (TotalXp < threshold) break;

        LevelUp();
        XpToNextLevel = WorldDatabase.GetPlayerLevel(Level + 1)?.xp_required ?? 0f;
      }
    }

    void LevelUp()
    {
      var oldLevel = Level;
      Level++;

      // 同步?WorldRuntimeContext
      WorldRuntimeContext.SyncPlayerLevel(Level, TotalXp);

      // 同步属性加成到 AttributeManager
      SyncToAttributeManager();

      // 广播事件
      WorldEventBus.FireWorldPlayerLevelUp(oldLevel, Level);

      if (DebugLog)
      {
        var def = CurrentLevelDef;
        Debug.Log($"[PlayerLevelSystem] LEVEL UP! LV.{oldLevel} ?LV.{Level} " +
                  $"ATK×{def?.attack_mult ?? 1f:F2} " +
                  $"HP×{def?.hp_mult ?? 1f:F2} " +
                  $"DEF={def?.defense_mult ?? 0f:F2}");
      }
    }

    // ══════════════════════════════════════════════════════
    //  属性加成接口（?Player / Combat 系统读取?
    //
    // 使用方式示例?
    //   var sys = WorldManager.Instance.GetSystem<PlayerLevelSystem>();
    //   var atkMult = sys.GetAttackMult();
    //   var hpMult  = sys.GetHpMult();
    //   var defMult = sys.GetDefenseMult();
    //
    // 不直接修?Player 代码 ??RunBuildApplier 或类似统合层读取?
    // ══════════════════════════════════════════════════════

    /// <summary>当前玩家等级的攻击力倍率（叠乘到最终伤害）</summary>
    public float GetAttackMult()
    {
      var def = CurrentLevelDef;
      return def?.attack_mult ?? 1f;
    }

    /// <summary>当前玩家等级的最大生命值倍率</summary>
    public float GetHpMult()
    {
      var def = CurrentLevelDef;
      return def?.hp_mult ?? 1f;
    }

    /// <summary>
    /// 当前玩家等级的防御减伤比例（0~1）?
    /// 例如 0.2 表示减免 20% 伤害?
    /// </summary>
    public float GetDefenseMult()
    {
      var def = CurrentLevelDef;
      return Mathf.Clamp01(def?.defense_mult ?? 0f);
    }

    /// <summary>
    /// 将当前玩家等级的属性加成同步到 AttributeManager。
    /// 在初始化、升级时自动调用。
    ///
    /// 映射关系：
    ///   attack_mult  → 属性 "attack_mult"  (Mult 操作，值 = defVal - 1.0)
    ///   hp_mult      → 属性 "max_hp_mult"   (Mult 操作，值 = defVal - 1.0)
    ///   defense_mult → 属性 "defense"       (Add 操作，值 = defVal)
    /// </summary>
    void SyncToAttributeManager()
    {
      var def = CurrentLevelDef;
      if (def == null) return;

      var attrMgr = WorldManager.Instance?.Attributes;
      if (attrMgr == null) return;

      var mods = new List<AttributeManager.ModifierEntry>
      {
        new AttributeManager.ModifierEntry("attack_mult", AttributeManager.ModifierOp.Mult,
          def.attack_mult - 1f),
        new AttributeManager.ModifierEntry("max_hp_mult", AttributeManager.ModifierOp.Mult,
          def.hp_mult - 1f),
        new AttributeManager.ModifierEntry("defense", AttributeManager.ModifierOp.Add,
          def.defense_mult * 100f),
        new AttributeManager.ModifierEntry("crit_chance", AttributeManager.ModifierOp.Add,
          def.crit_chance_bonus),
        new AttributeManager.ModifierEntry("move_speed_mult", AttributeManager.ModifierOp.Add,
          def.move_speed_bonus)
      };

      attrMgr.ApplyModifiers("player_level", mods);

      if (DebugLog)
        Debug.Log($"[PlayerLevelSystem] Synced LV.{Level} modifiers to AttributeManager: " +
                  $"atk+{(def.attack_mult - 1f) * 100f:F0}% hp+{(def.hp_mult - 1f) * 100f:F0}% " +
                  $"def+{def.defense_mult * 100f:F0} crit+{def.crit_chance_bonus:F2} spd+{def.move_speed_bonus:F2}");
    }

    /// <summary>获取指定等级的属性定义（用于 UI 预览?/summary>
    public static WorldDatabase.PlayerLevelDef GetLevelDef(int level)
    {
      WorldDatabase.EnsureLoaded();
      return WorldDatabase.GetPlayerLevel(level);
    }

    // ══════════════════════════════════════════════════════
    //  测试/调试
    // ══════════════════════════════════════════════════════

    /// <summary>手动设置等级（测试用?/summary>
    public void Debug_SetLevel(int level)
    {
      var old = Level;
      Level = Mathf.Max(1, Mathf.Min(level, _maxLevel));
      TotalXp = WorldDatabase.GetPlayerLevel(Level)?.xp_required ?? 0f;
      XpToNextLevel = WorldDatabase.GetPlayerLevel(Level + 1)?.xp_required ?? 0f;

      if (old != Level)
      {
        WorldRuntimeContext.SyncPlayerLevel(Level, TotalXp);
        SyncToAttributeManager();
        WorldEventBus.FireWorldPlayerLevelUp(old, Level);
      }
    }

    /// <summary>手动添加经验（测试用?/summary>
    public void Debug_AddXp(float amount)
    {
      AddXp(amount);
    }

    // ── 辅助 ──────────────────────────────────────────

    static class Mathf
    {
      public static int Max(int a, int b) => a > b ? a : b;
      public static int Min(int a, int b) => a < b ? a : b;
      public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
  }
}
