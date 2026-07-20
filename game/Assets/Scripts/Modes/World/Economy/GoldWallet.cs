using System;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式金币钱包 ?持有金币余额，支持存取与事件通知?
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期?
  ///
  /// 规则?
  ///   - 金币仅本局有效（单局经济，不跨局继承?
  ///   - AddGold / SpendGold 双向校验
  ///   - OnGoldChanged 事件通知 UI 或其他系绀"
  ///
  /// 同步?
  ///   - 余额变化时自动同步到 WorldRuntimeContext（兼容旧 API?
  ///   - 同时广播 WorldEventBus.GoldChanged
  ///
  /// 使用方式?
  ///   var wallet = WorldManager.Instance.GetSystem<GoldWallet>();
  ///   wallet.AddGold(100);
  ///   if (wallet.SpendGold(50)) { ... }
  ///   wallet.OnGoldChanged += (old, cur, delta) => UpdateUI(cur);
  /// </summary>
  public class GoldWallet : IWorldSystem
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态"
    // ══════════════════════════════════════════════════════

    /// <summary>当前金币余额</summary>
    public int Balance { get; private set; }

    /// <summary>
    /// 金币余额变化事件?oldBalance, newBalance, delta)
    /// delta 为正=获得，为?消费
    /// </summary>
    public event Action<int, int, int> OnGoldChanged;

    // ══════════════════════════════════════════════════════
    //  内部状态"
    // ══════════════════════════════════════════════════════

    bool _initialized;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;
      Balance = 0;
      _initialized = true;

      if (DebugLog)
        Debug.Log("[GoldWallet] Initialized. Balance=0");
    }

    public void Tick(float deltaTime)
    {
      // 金币是事件驱动，无需每帧逻辑
    }

    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      _initialized = false;
      if (DebugLog)
        Debug.Log($"[GoldWallet] Shut down. Final balance={Balance}");
    }

    // ══════════════════════════════════════════════════
    //  API ?存取
    // ══════════════════════════════════════════════════

    /// <summary>增加金币?/summary>
    /// <param name="amount">数量（必?&gt; 0?/param>
    /// <returns>实际增加量（0 表示无效操作?/returns>
    public int AddGold(int amount)
    {
      if (!_initialized || amount <= 0) return 0;

      var old = Balance;
      Balance += amount;

      FireChanged(old, amount);

      if (DebugLog)
        Debug.Log($"[GoldWallet] +{amount} Gold ({old} {Balance})");

      return amount;
    }

    /// <summary>消费金币?/summary>
    /// <param name="amount">消费数量（必?&gt; 0?/param>
    /// <returns>是否消费成功（余额不足时返回 false?/returns>
    public bool SpendGold(int amount)
    {
      if (!_initialized || amount <= 0) return false;
      if (Balance < amount) return false;

      var old = Balance;
      Balance -= amount;

      FireChanged(old, -amount);

      if (DebugLog)
        Debug.Log($"[GoldWallet] -{amount} Gold ({old} {Balance})");

      return true;
    }

    /// <summary>检查余额是否足?/summary>
    public bool CanAfford(int amount)
    {
      return _initialized && amount > 0 && Balance >= amount;
    }

    // ══════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════

    void FireChanged(int oldBalance, int delta)
    {
      OnGoldChanged?.Invoke(oldBalance, Balance, delta);

      // 同步?WorldRuntimeContext（兼容旧 API?
      WorldRuntimeContext.SyncGold(Balance);

      // 广播?WorldEventBus（供其他 World 系统监听?
      WorldEventBus.FireGoldChanged(oldBalance, Balance, delta);
    }
  }
}
