using UnityEngine.EventSystems;
using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>
  /// 确保运行时存?EventSystem，否?UGUI Button 无法点击?
  /// </summary>
  public static class UiBootstrap
  {
    /// <summary>?CombatRoot 统一调用，确?EventSystem 存在?/summary>
    public static void EnsureEventSystem()
    {
      if (Object.FindAnyObjectByType<EventSystem>() != null)
        return;

      var go = new GameObject("EventSystem");
      go.AddComponent<EventSystem>();
      go.AddComponent<StandaloneInputModule>();
      Object.DontDestroyOnLoad(go);
    }
  }
}