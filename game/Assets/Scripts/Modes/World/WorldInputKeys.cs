using Game.Shared.Core;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式特有的键位注册/删除管理。
  /// World 模式启动时注册，Shutdown 时删除，确保 Arena 模式不受影响。
  /// </summary>
  public static class WorldInputKeys
  {
    public static GameInputBindings.InputAction ClosePanel  { get; private set; }
    public static GameInputBindings.InputAction OpenMap     { get; private set; }
    public static GameInputBindings.InputAction AutoAttack  { get; private set; }
    public static GameInputBindings.InputAction UseItem     { get; private set; }
    public static GameInputBindings.InputAction SelectSlot1 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot2 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot3 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot4 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot5 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot6 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot7 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot8 { get; private set; }
    public static GameInputBindings.InputAction SelectSlot9 { get; private set; }

    /// <summary>所有格选键（1-9），按索引访问。</summary>
    public static GameInputBindings.InputAction[] SelectSlots => s_slots;
    static GameInputBindings.InputAction[] s_slots;

    static bool s_registered;

    /// <summary>注册 World 模式特有键位（World 启动时调用）。</summary>
    public static void RegisterWorldKeys()
    {
      if (s_registered) return;
      s_registered = true;

      ClosePanel  = GameInputBindings.Register("ClosePanel",  KeyCode.Escape, "关闭面板");
      OpenMap     = GameInputBindings.Register("OpenMap",     KeyCode.M,      "打开地图");
      AutoAttack  = GameInputBindings.Register("AutoAttack",  KeyCode.R,      "自动攻击");
      UseItem     = GameInputBindings.Register("UseItem",     KeyCode.G,      "使用道具");

      s_slots = new GameInputBindings.InputAction[9];
      for (int i = 0; i < 9; i++)
      {
        var id = $"SelectSlot{i + 1}";
        var key = KeyCode.Alpha1 + i;
        s_slots[i] = GameInputBindings.Register(id, key, $"选中物品栏第{i + 1}格");
      }
      SelectSlot1 = s_slots[0]; SelectSlot2 = s_slots[1]; SelectSlot3 = s_slots[2];
      SelectSlot4 = s_slots[3]; SelectSlot5 = s_slots[4]; SelectSlot6 = s_slots[5];
      SelectSlot7 = s_slots[6]; SelectSlot8 = s_slots[7]; SelectSlot9 = s_slots[8];

      Debug.Log("[WorldInputKeys] World mode keys registered (13 actions).");
    }

    /// <summary>删除 World 模式特有键位（World Shutdown 时调用）。</summary>
    public static void UnregisterWorldKeys()
    {
      if (!s_registered) return;
      s_registered = false;

      GameInputBindings.Unregister("ClosePanel");
      GameInputBindings.Unregister("OpenMap");
      GameInputBindings.Unregister("AutoAttack");
      GameInputBindings.Unregister("UseItem");
      for (int i = 1; i <= 9; i++)
        GameInputBindings.Unregister($"SelectSlot{i}");

      ClosePanel  = default;
      OpenMap     = default;
      AutoAttack  = default;
      UseItem     = default;
      SelectSlot1 = default; SelectSlot2 = default; SelectSlot3 = default;
      SelectSlot4 = default; SelectSlot5 = default; SelectSlot6 = default;
      SelectSlot7 = default; SelectSlot8 = default; SelectSlot9 = default;
      s_slots = null;

      Debug.Log("[WorldInputKeys] World mode keys unregistered.");
    }
  }
}
