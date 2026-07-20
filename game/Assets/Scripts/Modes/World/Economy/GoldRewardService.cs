using UnityEngine;
using Game.Shared.Combat.Events;

namespace Game.World
{
  /// <summary>
  /// 金币奖励服务 ?根据游戏事件计算并发放金币奖励?
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期?
  ///
  /// 奖励来源（均通过事件总线监听）：
  ///   1. 击杀怪物     ?CombatEventBus.OnKill（检?Killer == Player?
  ///   2. 摧毁营地     ?WorldEventBus.CampDestroyed（奖?× CampLevel?
  ///   3. 完成事件     ?WorldEventBus.RandomEventTriggered
  ///
  /// 所有奖励通过 GoldWallet.AddGold() 发放?
  ///
  /// 使用方式?
  ///   var rewardService = new GoldRewardService(wallet);
  ///   rewardService.Initialize(null);
  ///   WorldManager.AddSystem(rewardService);
  ///
  /// 参见：docs/design.md §7.1（商店），?.2（随机事件）
  /// </summary>
  public class GoldRewardService : IWorldSystem, System.IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>击杀普通怪物奖励金币</summary>
    public int GoldPerKill { get; set; } = 1;

    /// <summary>击杀精英怪物的金币倍率（相对普通）</summary>
    public float EliteKillGoldMult { get; set; } = 3f;

    /// <summary>击杀 Boss 的金币倍率</summary>
    public float BossKillGoldMult { get; set; } = 10f;

    /// <summary>摧毁营地的基础金币（再乘以营地等级?/summary>
    public int GoldPerCampDestroyed { get; set; } = 10;

    /// <summary>完成事件的基础金币</summary>
    public int GoldPerEventCompleted { get; set; } = 15;

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部引用
    // ══════════════════════════════════════════════════════

    readonly GoldWallet _wallet;
    bool _initialized;
    bool _paused;

    // ══════════════════════════════════════════════════════
    //  构速"
    // ══════════════════════════════════════════════════════

    public GoldRewardService(GoldWallet wallet)
    {
      _wallet = wallet;
    }

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      // 订阅击杀事件（CombatEventBus ??Arena 共享?
      CombatEventBus.OnKill += HandleCombatKill;

      // 订阅 World 事件
      WorldEventBus.CampDestroyed += HandleCampDestroyed;
      WorldEventBus.RandomEventTriggered += HandleRandomEvent;

      _initialized = true;

      if (DebugLog)
        Debug.Log("[GoldRewardService] Initialized. Listening for kill/camp/event rewards.");
    }

    public void Tick(float deltaTime)
    {
      // 完全事件驱动，无需每帧逻辑
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      CombatEventBus.OnKill -= HandleCombatKill;
      WorldEventBus.CampDestroyed -= HandleCampDestroyed;
      WorldEventBus.RandomEventTriggered -= HandleRandomEvent;

      _initialized = false;

      if (DebugLog)
        Debug.Log("[GoldRewardService] Shut down.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  奖励来源 1 ?击杀怪物
    // ══════════════════════════════════════════════════════

    void HandleCombatKill(CombatEventBus.KillArgs args)
    {
      if (!_initialized || _paused) return;
      if (_wallet == null) return;
      if (args.IsPlayer) return;

      // 判断是否为玩家击杀
      var killer = args.Killer;
      if (killer == null) return;

      var isPlayerKill = killer.CompareTag("Player") || killer.name == "Player";
      if (!isPlayerKill)
      {
        var name = killer.name;
        if (!name.StartsWith("Player", System.StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("Proj_Player", System.StringComparison.OrdinalIgnoreCase))
          return;
      }

      // 根据目标类型计算金币
      var gold = GoldPerKill;
      var victimId = args.VictimId ?? "";
      if (victimId.Contains("elite"))
        gold = Mathf.RoundToInt(gold * EliteKillGoldMult);
      else if (victimId.Contains("boss") || victimId.Contains("Boss"))
        gold = Mathf.RoundToInt(gold * BossKillGoldMult);

      _wallet.AddGold(gold);

      if (DebugLog)
        Debug.Log($"[GoldRewardService] Kill '{victimId}' +{gold}G");
    }

    // ══════════════════════════════════════════════════════
    //  奖励来源 2 ?营地摧毁
    // ══════════════════════════════════════════════════════

    void HandleCampDestroyed(string campId, WorldCampData campData)
    {
      if (!_initialized || _paused) return;
      if (_wallet == null) return;
      if (!campData.IsDestroyed) return;

      var gold = GoldPerCampDestroyed * Mathf.Max(1, campData.CampLevel);
      _wallet.AddGold(gold);

      if (DebugLog)
        Debug.Log($"[GoldRewardService] Camp '{campId}' destroyed +{gold}G (LV.{campData.CampLevel})");
    }

    // ══════════════════════════════════════════════════════
    //  奖励来源 3 ?随机事件完成
    // ══════════════════════════════════════════════════════

    void HandleRandomEvent(string eventId, int choiceIndex)
    {
      if (!_initialized || _paused) return;
      if (_wallet == null) return;

      _wallet.AddGold(GoldPerEventCompleted);

      if (DebugLog)
        Debug.Log($"[GoldRewardService] Event '{eventId}' +{GoldPerEventCompleted}G");
    }

    // ── 辅助 ──────────────────────────────────────────

    static class Mathf
    {
      public static int Max(int a, int b) => a > b ? a : b;
      public static int RoundToInt(float v) => (int)(v + 0.5f);
    }
  }
}
