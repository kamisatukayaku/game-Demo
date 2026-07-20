using UnityEngine;

namespace Game.Shared.UI
{
  /// <summary>
  /// 游戏模式描述符基类。
  /// 子类（RoguelikeGameMode / WorldGameMode）通过 RuntimeInitializeOnLoadMethod
  /// 自动注册到 StartGameUIShared.RegisteredModes 列表中。
  /// </summary>
  public abstract class GameModeDescriptor
  {
    /// <summary>模式唯一标识，如 "arena"、"explore"</summary>
    public abstract string ModeId { get; }

    /// <summary>在模式选择界面显示的名称</summary>
    public abstract string DisplayName { get; }

    /// <summary>简短描述文本</summary>
    public abstract string Description { get; }

    /// <summary>模式按钮的主题色</summary>
    public abstract Color ThemeColor { get; }

    /// <summary>
    /// 构建该模式的特有 UI（难度、装备、天赋等）。
    /// 由 StartGameUIShared 在用户选中该模式后调用。
    /// </summary>
    /// <param name="parent">模式 UI 的父级 Transform</param>
    /// <param name="host">宿主 StartGameUIShared，提供字体/导航等</param>
    public abstract void BuildModeUI(Transform parent, StartGameUIShared host);

    /// <summary>销毁模式 UI（切换模式时调用）</summary>
    public virtual void TeardownModeUI() { }

    /// <summary>用户点击"开始游戏"后的启动逻辑</summary>
    public abstract void OnStart();
  }
}
