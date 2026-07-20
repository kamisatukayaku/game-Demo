using System.Collections.Generic;
using System;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式世界等级系统 — 全地图公共等级的核心管理模块。
  ///
  /// 实现 IWorldSystem，由 WorldManager 驱动生命周期。
  ///
  /// 核心规则（参见 docs/design.md §3.4）：
  ///
  ///   1. DangerValue = Σ max(CampLevel - WorldLevel - MinLevelDiff, 0)
  ///      所有活跃营地按等级贡献危险度。营地等级超过世界等级越多，危险越高。
  ///
  ///   2. WorldExp += DangerValue × expRate × deltaTime
  ///      每帧累积经验。活跃营地越多/越强 → 积累越快。
  ///
  ///   3. WorldExp ≥ xp_threshold → WorldLevel++
  ///      达到阈值升级。阈值来自 WorldDatabase.WorldLevelDef.xp_threshold。
  ///
  ///   4. 营地被摧毁 → WorldExp -= penalty
  ///      经验可降至负数，但不会降级 WorldLevel。摧毁营地是唯一主动降压手段。
  ///
  ///   5. WorldLevel 决定：
  ///      - 野外怪物基础等级 (enemy_level_base)
  ///      - 野外怪物可用种类 (unlock_boss_ids)
  ///      - 野外怪物全局属性倍率 (enemy_stat_mult)
  ///
  /// 与 CampController 联动：
  ///   - Tick 时遍历 WorldRuntimeContext.Camps 聚合 DangerValue
  ///   - 监听 WorldEventBus.CampDestroyed 扣减 WorldExp
  ///   - WorldLevel 变化通过 WorldEventBus.WorldLevelChanged 广播
  /// </summary>
  public class WorldLevelSystem : IWorldSystem, IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置（可由 WorldManager 或 Inspector 设置）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// MinLevelDiff — 营地等级需要超过世界等级多少才开始贡献 DangerValue。
    /// 例如 MinLevelDiff=2 时，WorldLevel=3 的营地需要 CampLevel≥6 才有危险。
    /// 默认值可调；建议 1~3。
    /// </summary>
    public int MinLevelDiff { get; set; } = 2;

    /// <summary>
    /// DangerValue → WorldExp 的转化速率（每秒每点 Danger 产生的经验值）。
    /// 默认 1.0 表示每秒危险度 5 产生 5 经验。值越高升级越快。
    /// </summary>
    public float DangerToExpRate { get; set; } = 1.0f;

    /// <summary>
    /// 营地被摧毁时扣除的 WorldExp。
    /// 公式：penalty = campDestroyedExpPenalty × campLevel。
    /// </summary>
    public float CampDestroyedExpPenalty { get; set; } = 10f;
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态（只读）
    // ══════════════════════════════════════════════════════

    /// <summary>当前世界等级（≥1）</summary>
    public int WorldLevel { get; private set; } = 1;

    /// <summary>当前累积的世界经验</summary>
    public float WorldExp { get; private set; }

    /// <summary>当前全地图 DangerValue（每帧计算）</summary>
    public float DangerValue { get; private set; }

    /// <summary>升到下一级所需的经验阈值（来自配表）</summary>
    public float ExpToNextLevel { get; private set; }

    /// <summary>当前世界等级对应的配表定义</summary>
    public WorldDatabase.WorldLevelDef CurrentLevelDef => WorldDatabase.GetWorldLevel(WorldLevel);

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    bool _initialized;
    bool _paused;
    float _lastDangerCalcTime;
    const float DangerCalcInterval = 0.5f; // 每 0.5 秒重新计算一次 DangerValue

    // ══════════════════════════════════════════════════════
    //  IWorldSystem — Initialize
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      WorldDatabase.EnsureLoaded();

      // 设置初始阈值
      var def = WorldDatabase.GetWorldLevel(WorldLevel);
      ExpToNextLevel = GetXpThreshold(def, WorldLevel);

      // 监听营地摧毁事件
      WorldEventBus.CampDestroyed += OnCampDestroyed;

      _initialized = true;

      if (DebugLog)
        Debug.Log($"[WorldLevelSystem] Initialized. LV.{WorldLevel} " +
                  $"next={ExpToNextLevel:F0}exp minDiff={MinLevelDiff}");
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem — Tick
    // ══════════════════════════════════════════════════════

    public void Tick(float deltaTime)
    {
      if (!_initialized || _paused) return;

      // Step 1: 定期重新计算 DangerValue（不需要每帧都扫所有营地）
      _lastDangerCalcTime += deltaTime;
      if (_lastDangerCalcTime >= DangerCalcInterval)
      {
        DangerValue = CalculateDangerValue();
        _lastDangerCalcTime = 0f;
      }

      // Step 2: DangerValue → WorldExp（每帧累积）
      if (DangerValue > 0f)
      {
        var expGain = DangerValue * DangerToExpRate * deltaTime;
        WorldExp += expGain;
      }

      // Step 3: 检查升级
      CheckLevelUp();
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem — 其他
    // ══════════════════════════════════════════════════════

    public void OnPause()  => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      WorldEventBus.CampDestroyed -= OnCampDestroyed;
      _initialized = false;
      if (DebugLog) Debug.Log("[WorldLevelSystem] Shut down.");
    }

    public void Dispose() { Shutdown(); }

    // ══════════════════════════════════════════════════════
    //  DangerValue 计算
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 遍历所有活跃营地，聚合 DangerValue。
    ///
    /// 公式：DangerValue = Σ max(CampLevel - WorldLevel - MinLevelDiff, 0)
    ///   - 营地等级低于 WorldLevel+MinLevelDiff → 贡献 0（不构成威胁）
    ///   - 营地等级远高于 WorldLevel → 贡献大（危险信号）
    /// </summary>
    float CalculateDangerValue()
    {
      var camps = WorldRuntimeContext.Camps;
      if (camps == null || camps.Count == 0)
        return 0f;

      float total = 0f;
      foreach (var kv in camps)
      {
        var camp = kv.Value;

        // 已摧毁的营地不计入（理论上已 Unregister，但防御性检查）
        if (camp.IsDestroyed) continue;

        var diff = camp.CampLevel - WorldLevel - MinLevelDiff;
        if (diff > 0)
          total += diff;
      }

      return total;
    }

    // ══════════════════════════════════════════════════════
    //  营地摧毁回调
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 营地被摧毁 → 扣除 WorldExp（可降至负数）。
    /// 不会直接降级 WorldLevel。降级只能通过自然衰减（未实现）或特殊事件。
    /// </summary>
    void OnCampDestroyed(string campId, WorldCampData campData)
    {
      if (!_initialized) return;

      var penalty = CampDestroyedExpPenalty * Mathf.Max(1, campData.CampLevel);
      WorldExp -= penalty;

      if (DebugLog)
        Debug.Log($"[WorldLevelSystem] Camp '{campId}' destroyed. " +
                  $"Exp -{penalty:F0} (LV.{campData.CampLevel}) → total={WorldExp:F0}");
    }

    // ══════════════════════════════════════════════════════
    //  升级检测
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 检查 WorldExp 是否达到升级阈值。
    /// 可连续升级（一次 Tick 中多次升级）。</summary>
    void CheckLevelUp()
    {
      while (WorldExp >= ExpToNextLevel && ExpToNextLevel > 0f)
      {
        WorldExp -= ExpToNextLevel;

        var oldLevel = WorldLevel;
        WorldLevel++;

        // 广播升级事件
        WorldEventBus.FireWorldLevelChanged(oldLevel, WorldLevel);

        // 同时更新 WorldRuntimeContext（兼容旧 API）
        WorldRuntimeContext.SyncWorldLevel(WorldLevel);

        // 获取新的阈值
        var def = WorldDatabase.GetWorldLevel(WorldLevel);
        ExpToNextLevel = GetXpThreshold(def, WorldLevel);

        if (DebugLog)
          Debug.Log($"[WorldLevelSystem] LEVEL UP! LV.{oldLevel} → LV.{WorldLevel} " +
                    $"next={ExpToNextLevel:F0}exp remaining={WorldExp:F0}exp");
      }
    }

    // ══════════════════════════════════════════════════════
    //  阈值查询
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 获取从 level 升到 level+1 所需的 WorldExp。
    /// 优先从配表读取 xp_threshold；未配置时用 time_to_next_level 推算。
    /// </summary>
    static float GetXpThreshold(WorldDatabase.WorldLevelDef def, int level)
    {
      if (def != null && def.xp_threshold > 0f)
        return def.xp_threshold;

      // 回退公式：阈 = time_to_next_level × 5（假设默认 DangerValue≈5）
      var time = def?.time_to_next_level ?? 60f;
      return time * 5f;
    }

    public int GetWildEnemyBaseLevel()
    {
      var def = CurrentLevelDef;
      return def != null ? def.enemy_level_base : WorldLevel;
    }

    public float GetWildEnemyStatMult()
    {
      var def = CurrentLevelDef;
      return def != null ? def.enemy_stat_mult : 1f;
    }

    /// <summary>当前世界等级可用的野外 Boss 类型</summary>
    public IReadOnlyList<string> GetAvailableBossTypes()
    {
      var def = CurrentLevelDef;
      return def?.unlock_boss_ids ?? Array.Empty<string>();
    }

    // ══════════════════════════════════════════════════════
    //  测试/调试
    // ══════════════════════════════════════════════════════

    /// <summary>手动设置世界等级（测试用）</summary>
    public void Debug_SetWorldLevel(int level)
    {
      var old = WorldLevel;
      WorldLevel = Mathf.Max(1, level);
      WorldExp = 0f;
      DangerValue = 0f;
      var def = WorldDatabase.GetWorldLevel(WorldLevel);
      ExpToNextLevel = GetXpThreshold(def, WorldLevel);

      if (old != WorldLevel)
      {
        WorldEventBus.FireWorldLevelChanged(old, WorldLevel);
        WorldRuntimeContext.SyncWorldLevel(WorldLevel);
      }
    }

    /// <summary>手动添加经验（测试用）</summary>
    public void Debug_AddExp(float amount)
    {
      WorldExp += amount;
      CheckLevelUp();
    }
  }
}
