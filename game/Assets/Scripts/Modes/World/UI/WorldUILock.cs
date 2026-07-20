using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式 UI 互斥锁 — 确保同一时间只有一个非常驻 UI 面板打开。
  ///
  /// 常驻 UI（HUD/Subtitle/InteractionHint/ItemSlotBar）不参与互斥。
  /// 非常驻 UI：地图、背包、商人、事件、MetaProgression、键位设置。
  ///
  /// 用法：
  ///   打开: if (!WorldUILock.TryAcquire("id")) return;
  ///   关闭: WorldUILock.Release("id");
  /// </summary>
  public static class WorldUILock
  {
    static string s_currentOwner;
    static System.Action<string> s_onForceClose;

    /// <summary>当前持有锁的 UI 标识（null = 无 UI 打开）</summary>
    public static string CurrentOwner => s_currentOwner;

    /// <summary>是否有 UI 锁定中</summary>
    public static bool IsLocked => !string.IsNullOrEmpty(s_currentOwner);

    /// <summary>
    /// 当另一个 UI 强制关闭当前 UI 时触发（id = 被关闭的 UI 标识）。
    /// 各 UI 在 Open 时应注册此事件以响应外部关闭。
    /// </summary>
    public static event System.Action<string> OnForceClose
    {
      add => s_onForceClose += value;
      remove => s_onForceClose -= value;
    }

    /// <summary>尝试获取互斥锁。若已被占用则返回 false 并可选关闭占用方。</summary>
    public static bool TryAcquire(string uiId)
    {
      if (string.IsNullOrEmpty(s_currentOwner))
      {
        s_currentOwner = uiId;
        return true;
      }

      if (s_currentOwner == uiId)
        return true; // 同 UI 重入

      // 强制关闭当前 UI
      var prev = s_currentOwner;
      s_currentOwner = uiId;
      s_onForceClose?.Invoke(prev);

      return true;
    }

    /// <summary>释放互斥锁。</summary>
    public static void Release(string uiId)
    {
      if (s_currentOwner == uiId)
        s_currentOwner = null;
    }

    /// <summary>强行释放（不检查 id）。</summary>
    public static void ForceRelease()
    {
      s_currentOwner = null;
    }
  }
}
