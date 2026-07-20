using System.Collections.Generic;
using System;
using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>
  /// 可自定义的游戏键位系统（PlayerPrefs 持久化）。
  ///
  /// InputAction 从 enum 改为 struct（含字符串 Id），支持动态注册新动作。
  /// 内置预定义动作（MoveUp/Down/Left/Right/Attack/Interact/Inventory/KeySettings）
  /// 保持常量字段访问，外部代码无需修改即可兼容。
  ///
  /// 动态注册：GameInputBindings.Register("OpenMap", KeyCode.M, "打开地图");
  /// 动态删除：GameInputBindings.Unregister("OpenMap");
  /// </summary>
  public static class GameInputBindings
  {
    // ══════════════════════════════════════════════════════
    //  InputAction（struct，替代 enum）
    // ══════════════════════════════════════════════════════

    public struct InputAction : IEquatable<InputAction>
    {
      public readonly string Id;

      public InputAction(string id)
      {
        Id = id ?? throw new ArgumentNullException(nameof(id));
      }

      // ── 预定义常量（保持与旧 enum 兼容）──
      public static readonly InputAction MoveUp      = new("MoveUp");
      public static readonly InputAction MoveDown    = new("MoveDown");
      public static readonly InputAction MoveLeft    = new("MoveLeft");
      public static readonly InputAction MoveRight   = new("MoveRight");
      public static readonly InputAction Attack      = new("Attack");
      public static readonly InputAction Interact     = new("Interact");
      public static readonly InputAction Inventory   = new("Inventory");
      public static readonly InputAction KeySettings = new("KeySettings");

      // ── IEquatable ──
      public bool Equals(InputAction other) => Id == other.Id;
      public override bool Equals(object obj) => obj is InputAction a && Equals(a);
      public override int GetHashCode() => Id.GetHashCode();
      public override string ToString() => Id;

      public static bool operator ==(InputAction a, InputAction b) => a.Id == b.Id;
      public static bool operator !=(InputAction a, InputAction b) => a.Id != b.Id;
    }

    // ══════════════════════════════════════════════════════
    //  预定义动作 ID 集合（用于 EnsureLoaded 逐项检查）
    // ══════════════════════════════════════════════════════

    static readonly (string id, KeyCode key, string name)[] s_builtin = new[]
    {
      ("MoveUp",      KeyCode.W,      "向上移动"),
      ("MoveDown",    KeyCode.S,      "向下移动"),
      ("MoveLeft",    KeyCode.A,      "向左移动"),
      ("MoveRight",   KeyCode.D,      "向右移动"),
      ("Attack",      KeyCode.Mouse0, "攻击"),
      ("Interact",    KeyCode.F,      "交互"),
      ("Inventory",   KeyCode.I,      "背包"),
      ("KeySettings", KeyCode.K,      "键位设置"),
    };

    // ══════════════════════════════════════════════════════
    //  事件 & 状态
    // ══════════════════════════════════════════════════════

    public static event Action Changed;
    public static bool IsRebinding { get; private set; }
    public static InputAction RebindTarget { get; private set; }

    // ══════════════════════════════════════════════════════
    //  内部存储
    // ══════════════════════════════════════════════════════

    static readonly Dictionary<string, KeyCode> s_defaults = new();
    static readonly Dictionary<string, string> s_displayNames = new();
    static readonly Dictionary<string, KeyCode> s_bindings = new();
    static readonly List<InputAction> s_registered = new();
    static bool s_loaded;

    // ══════════════════════════════════════════════════════
    //  公开查询
    // ══════════════════════════════════════════════════════

    /// <summary>所有已注册的动作（按注册顺序）。</summary>
    public static IReadOnlyList<InputAction> RegisteredActions
    {
      get { EnsureLoaded(); return s_registered; }
    }

    /// <summary>所有当前绑定（ID → KeyCode）。</summary>
    public static IReadOnlyDictionary<string, KeyCode> All => s_bindings;

    // ══════════════════════════════════════════════════════
    //  注册 & 加载
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 动态注册一个新的 InputAction。
    /// 可在 EnsureLoaded 之前或之后调用（内部自动处理加载状态差异）。
    /// 加载后注册会自动触发 Changed 事件使 UI 重建。
    /// 如果 ID 已注册，仅更新默认键和显示名称（不覆盖已有绑定）。
    /// </summary>
    public static InputAction Register(string actionId, KeyCode defaultKey, string displayName)
    {
      if (string.IsNullOrEmpty(actionId))
        throw new ArgumentNullException(nameof(actionId));

      s_defaults[actionId] = defaultKey;
      s_displayNames[actionId] = displayName ?? actionId;

      bool alreadyRegistered = false;
      for (int i = 0; i < s_registered.Count; i++)
        if (s_registered[i].Id == actionId) { alreadyRegistered = true; break; }

      if (!alreadyRegistered)
      {
        s_registered.Add(new InputAction(actionId));

        // 已加载后注册：需加载持久化绑定并通知 UI
        if (s_loaded)
        {
          LoadBinding(actionId, defaultKey);
          Changed?.Invoke();
        }
      }

      return new InputAction(actionId);
    }

    /// <summary>
    /// 取消注册一个 InputAction（从列表移除，但保留绑定记录和默认键）。
    /// 已从 PlayerPrefs 加载的绑定不会丢失，重新注册同 ID 时恢复。
    /// </summary>
    public static void Unregister(string actionId)
    {
      if (string.IsNullOrEmpty(actionId)) return;
      for (int i = s_registered.Count - 1; i >= 0; i--)
        if (s_registered[i].Id == actionId)
          s_registered.RemoveAt(i);

      s_displayNames.Remove(actionId);
      // 保留 s_defaults 和 s_bindings（绑定持久化记录不删除）

      if (s_loaded)
        Changed?.Invoke();
    }

    public static void EnsureLoaded()
    {
      if (s_loaded) return;
      s_loaded = true;

      // 逐项检查内置动作是否已注册（而非只检查总数）
      foreach (var (id, key, name) in s_builtin)
      {
        bool alreadyRegistered = false;
        for (int i = 0; i < s_registered.Count; i++)
          if (s_registered[i].Id == id) { alreadyRegistered = true; break; }
        if (!alreadyRegistered)
          Register(id, key, name);
      }

      s_bindings.Clear();
      foreach (var kv in s_defaults)
        LoadBinding(kv.Key, kv.Value);
    }

    static void LoadBinding(string actionId, KeyCode defaultKey)
    {
      var prefKey = PrefKey(actionId);
      var saved = PlayerPrefs.GetString(prefKey, defaultKey.ToString());
      if (Enum.TryParse(saved, out KeyCode code))
        s_bindings[actionId] = code;
      else
        s_bindings[actionId] = defaultKey;
    }

    // ══════════════════════════════════════════════════════
    //  Get / Set
    // ══════════════════════════════════════════════════════

    public static KeyCode Get(InputAction action)
    {
      if (string.IsNullOrEmpty(action.Id)) return KeyCode.None;
      EnsureLoaded();
      return s_bindings.TryGetValue(action.Id, out var code) ? code
           : (s_defaults.TryGetValue(action.Id, out var def) ? def : KeyCode.None);
    }

    public static void Set(InputAction action, KeyCode code)
    {
      EnsureLoaded();
      s_bindings[action.Id] = code;
      PlayerPrefs.SetString(PrefKey(action.Id), code.ToString());
      PlayerPrefs.Save();
      Changed?.Invoke();
    }

    public static void ResetToDefaults()
    {
      EnsureLoaded();
      foreach (var kv in s_defaults)
      {
        s_bindings[kv.Key] = kv.Value;
        PlayerPrefs.DeleteKey(PrefKey(kv.Key));
      }
      PlayerPrefs.Save();
      Changed?.Invoke();
    }

    // ══════════════════════════════════════════════════════
    //  改键流程
    // ══════════════════════════════════════════════════════

    public static void BeginRebind(InputAction action)
    {
      EnsureLoaded();
      IsRebinding = true;
      RebindTarget = action;
    }

    public static void CancelRebind()
    {
      IsRebinding = false;
      RebindTarget = default;
    }

    /// <summary>UI Update 中调用，捕获下一颗按键。</summary>
    public static bool TryCompleteRebind()
    {
      if (!IsRebinding)
        return false;

      if (Input.GetKeyDown(KeyCode.Escape))
      {
        CancelRebind();
        return true;
      }

      foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
      {
        if (code == KeyCode.None || code == KeyCode.Escape)
          continue;

        if (!Input.GetKeyDown(code))
          continue;

        Set(RebindTarget, code);
        CancelRebind();
        return true;
      }

      return false;
    }

    // ══════════════════════════════════════════════════════
    //  输入读取（外部 API，签名不变）
    // ══════════════════════════════════════════════════════

    public static bool IsHeld(InputAction action) => IsKeyHeld(Get(action));

    public static bool WasPressed(InputAction action) => IsKeyDown(Get(action));

    public static Vector2 ReadMoveVector()
    {
#if DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS
      if (s_useSyntheticMove)
        return s_syntheticMove;
#endif
      EnsureLoaded();
      float x = 0f;
      float y = 0f;

      if (IsKeyHeld(Get(InputAction.MoveLeft)))  x -= 1f;
      if (IsKeyHeld(Get(InputAction.MoveRight))) x += 1f;
      if (IsKeyHeld(Get(InputAction.MoveDown)))  y -= 1f;
      if (IsKeyHeld(Get(InputAction.MoveUp)))    y += 1f;

      var v = new Vector2(x, y);
      return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    // ══════════════════════════════════════════════════════
    //  显示
    // ══════════════════════════════════════════════════════

    public static string GetDisplayName(InputAction action)
    {
      if (string.IsNullOrEmpty(action.Id)) return "?";
      EnsureLoaded();
      return s_displayNames.TryGetValue(action.Id, out var name) ? name : action.Id;
    }

    public static string FormatKey(KeyCode code)
    {
      if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6)
        return code switch
        {
          KeyCode.Mouse0 => "鼠标左键",
          KeyCode.Mouse1 => "鼠标右键",
          KeyCode.Mouse2 => "鼠标中键",
          _ => code.ToString()
        };

      return code switch
      {
        KeyCode.LeftShift => "Left Shift",
        KeyCode.RightShift => "Right Shift",
        KeyCode.LeftControl => "Left Ctrl",
        KeyCode.RightControl => "Right Ctrl",
        KeyCode.Space => "Space",
        _ => code.ToString()
      };
    }

    // ══════════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════════

    static string PrefKey(string actionId) => $"input_{actionId}";

    static bool IsKeyHeld(KeyCode code)
    {
      if (IsMouseKey(code))
        return Input.GetMouseButton(MouseIndex(code));
      return Input.GetKey(code);
    }

    static bool IsKeyDown(KeyCode code)
    {
      if (IsMouseKey(code))
        return Input.GetMouseButtonDown(MouseIndex(code));
      return Input.GetKeyDown(code);
    }

    static bool IsMouseKey(KeyCode code) => code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6;
    static int MouseIndex(KeyCode code) => code - KeyCode.Mouse0;

#if DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS
    static bool s_useSyntheticMove;
    static Vector2 s_syntheticMove;
    static bool s_syntheticDashPressed;

    public static void SetSyntheticMoveVector(Vector2 move)
    {
      s_syntheticMove = move.sqrMagnitude > 1f ? move.normalized : move;
      s_useSyntheticMove = true;
    }

    public static void ClearSyntheticInput()
    {
      s_useSyntheticMove = false;
      s_syntheticMove = Vector2.zero;
      s_syntheticDashPressed = false;
    }

    public static void QueueSyntheticDash() => s_syntheticDashPressed = true;

    public static bool ConsumeSyntheticDash()
    {
      if (!s_syntheticDashPressed)
        return false;
      s_syntheticDashPressed = false;
      return true;
    }
#endif
  }
}
