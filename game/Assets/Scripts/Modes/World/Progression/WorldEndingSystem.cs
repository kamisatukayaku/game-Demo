using UnityEngine;
using Game.Shared.Combat.Events;

namespace Game.World
{
  /// <summary>
  /// World 模式结局判定系统。
  ///
  /// 三种结局（优先级从高到低）：
  ///   1. Clear2 — 击杀所有Boss（立即触发，不等待死亡）
  ///   2. Clear1 — 摧毁所有非Boss营地后玩家死亡
  ///   3. Failure — 玩家死亡且未达成其它条件
  ///
  /// 结局类型影响 MetaProgressionSystem.FinalizeRun() 中的分数倍率：
  ///   - Failure → victory=false（无 500 分加成）
  ///   - Clear1  → victory=true（+500 分）
  ///   - Clear2  → victory=true（+500 分）
  ///
  /// 流程：
  ///   世界生成后 → 统计营地/Boss 总数
  ///   战斗中 → 追踪营地摧毁/Boss击杀
  ///   玩家死亡/Boss全灭 → 写入 RunStats → 调用 FinalizeRun → 广播结局
  /// </summary>
  public class WorldEndingSystem : IWorldSystem, System.IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    int _totalNonBossCamps;
    int _destroyedNonBossCamps;
    int _totalBosses;
    int _killedBosses;
    bool _initialized;
    bool _paused;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      // 世界生成完成后统计总数
      WorldEventBus.WorldMapGenerated += OnWorldGenerated;

      // 营地摧毁
      WorldEventBus.CampDestroyed += OnCampDestroyed;

      // 玩家死亡 / Boss击杀 → CombatEventBus
      CombatEventBus.OnKill += OnCombatKill;

      _initialized = true;

      if (DebugLog)
        Debug.Log("[WorldEndingSystem] Initialized. Listening for end conditions.");
    }

    public void Tick(float deltaTime)
    {
      // 完全事件驱动
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      WorldEventBus.WorldMapGenerated -= OnWorldGenerated;
      WorldEventBus.CampDestroyed -= OnCampDestroyed;
      CombatEventBus.OnKill -= OnCombatKill;
      _initialized = false;

      if (DebugLog)
        Debug.Log("[WorldEndingSystem] Shut down.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  世界生成 → 统计营地/Boss 总数
    // ══════════════════════════════════════════════════════

    void OnWorldGenerated()
    {
      if (!_initialized || _paused) return;
      if (WorldRuntimeContext.MapData == null) return;

      _totalNonBossCamps = 0;
      _totalBosses = 0;

      foreach (var placement in WorldRuntimeContext.MapData.Placements)
      {
        if (placement == null || string.IsNullOrEmpty(placement.Category)) continue;

        if (placement.Category == "Camp" &&
            !placement.TypeId.Contains("Boss", System.StringComparison.OrdinalIgnoreCase))
        {
          _totalNonBossCamps++;
        }
        else if (placement.Category == "WildBoss" ||
                 placement.TypeId.Contains("Boss", System.StringComparison.OrdinalIgnoreCase))
        {
          _totalBosses++;
        }
      }

      WorldRuntimeContext.SetTotalCamps(_totalNonBossCamps, _totalBosses);

      if (DebugLog)
        Debug.Log($"[WorldEndingSystem] World generated: " +
                  $"nonBossCamps={_totalNonBossCamps}, bosses={_totalBosses}");
    }

    // ══════════════════════════════════════════════════════
    //  营地摧毁 → 记录进度
    // ══════════════════════════════════════════════════════

    void OnCampDestroyed(string campId, WorldCampData data)
    {
      if (!_initialized || _paused) return;
      if (data.IsDestroyed) return; // 已摧毁
      if (WorldRuntimeContext.IsRunEnded) return;

      var isBossCamp = data.CampTypeId?.Contains("Boss", System.StringComparison.OrdinalIgnoreCase) ?? false;

      WorldRuntimeContext.RecordCampDestroyed(isBossCamp);

      if (isBossCamp)
      {
        _killedBosses++;
        if (DebugLog)
          Debug.Log($"[WorldEndingSystem] Boss camp destroyed: {campId} ({_killedBosses}/{_totalBosses})");

        // Boss 营地也算作 Boss 击杀
        CheckBossClear();
      }
      else
      {
        _destroyedNonBossCamps++;
        if (DebugLog)
          Debug.Log($"[WorldEndingSystem] Non-boss camp destroyed: {campId} ({_destroyedNonBossCamps}/{_totalNonBossCamps})");
      }
    }

    // ══════════════════════════════════════════════════════
    //  击杀事件 → 检测玩家死亡 + Boss击杀
    // ══════════════════════════════════════════════════════

    void OnCombatKill(CombatEventBus.KillArgs args)
    {
      if (!_initialized || _paused) return;
      if (WorldRuntimeContext.IsRunEnded) return;

      if (args.IsPlayer)
      {
        // 玩家死亡 → 判定结局
        OnPlayerDied();
        return;
      }

      // 检查是否是玩家击杀的 Boss 怪物
      var isPlayerKill = IsPlayerKill(args);
      if (!isPlayerKill) return;

      var victimId = args.VictimId ?? "";
      if (victimId.Contains("Boss", System.StringComparison.OrdinalIgnoreCase) ||
          victimId.Contains("boss", System.StringComparison.OrdinalIgnoreCase))
      {
        _killedBosses++;
        if (DebugLog)
          Debug.Log($"[WorldEndingSystem] Boss killed: {victimId} ({_killedBosses}/{_totalBosses})");
        CheckBossClear();
      }
    }

    /// <summary>检查是否达成 Clear2（所有Boss击杀）</summary>
    void CheckBossClear()
    {
      if (_totalBosses <= 0) return;
      if (_killedBosses >= _totalBosses)
        EndRun(WorldEndType.Clear2);
    }

    /// <summary>玩家死亡 → 判定 Clear1 或 Failure</summary>
    void OnPlayerDied()
    {
      WorldEventBus.FirePlayerDied();

      // 检查是否所有非Boss营地已摧毁
      if (_totalNonBossCamps > 0 && _destroyedNonBossCamps >= _totalNonBossCamps)
      {
        EndRun(WorldEndType.Clear1);
      }
      else
      {
        EndRun(WorldEndType.Failure);
      }
    }

    // ══════════════════════════════════════════════════════
    //  结局判定 → 结算
    // ══════════════════════════════════════════════════════

    void EndRun(WorldEndType endType)
    {
      if (WorldRuntimeContext.IsRunEnded) return;

      bool victory = endType != WorldEndType.Failure;

      // 写入 RunStats（现有字段但从未被填充）
      var metaSys = WorldManager.Instance?.GetSystem<MetaProgressionSystem>();
      if (metaSys != null)
      {
        var stats = metaSys.CurrentStats;
        stats.CampsDestroyed = _destroyedNonBossCamps + _killedBosses;
        stats.BossesKilled = _killedBosses;
        stats.MaxWorldLevelReached = WorldRuntimeContext.WorldLevel;
        stats.MaxPlayerLevelReached = WorldRuntimeContext.WorldPlayerLevel;
        stats.GoldCollected = WorldRuntimeContext.WorldGold;

        // 调用结算
        var battleExp = metaSys.FinalizeRun(victory);
        WorldRuntimeContext.MarkRunEnded(endType, metaSys.CurrentRunScore, battleExp);

        if (DebugLog)
          Debug.Log($"[WorldEndingSystem] Run ended: {endType} victory={victory} " +
                    $"score={metaSys.CurrentRunScore:F0} battleExp={battleExp:F0}");
      }
      else
      {
        WorldRuntimeContext.MarkRunEnded(endType, 0f, 0f);
        if (DebugLog)
          Debug.LogWarning($"[WorldEndingSystem] MetaProgressionSystem not found, cannot finalize run.");
      }

      // 广播结局事件（UI 层监听）
      WorldEventBus.FireRunEnded(endType);
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    static bool IsPlayerKill(CombatEventBus.KillArgs args)
    {
      var killer = args.Killer;
      if (killer == null) return false;
      if (killer.CompareTag("Player") || killer.name == "Player") return true;
      var name = killer.name;
      return name.StartsWith("Player", System.StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("Proj_Player", System.StringComparison.OrdinalIgnoreCase);
    }
  }
}
