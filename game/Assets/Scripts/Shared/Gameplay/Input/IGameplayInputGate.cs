using Game.Shared.Gameplay.Input;
namespace Game.Shared.Gameplay.Input
{
  /// <summary>阻塞玩家输入?UI 状态（键位面板、背包等）?/summary>
  public interface IGameplayInputGate
  {
    bool BlocksPlayerInput { get; }
  }

  public static class GameplayInputGateLocator
  {
    static IGameplayInputGate s_gate = NullGameplayInputGate.Instance;

    public static IGameplayInputGate Gate => s_gate;

    public static bool BlocksPlayerInput => s_gate.BlocksPlayerInput;

    public static void Register(IGameplayInputGate gate) =>
      s_gate = gate ?? NullGameplayInputGate.Instance;

    public static void Clear() => s_gate = NullGameplayInputGate.Instance;
  }

  sealed class NullGameplayInputGate : IGameplayInputGate
  {
    public static readonly NullGameplayInputGate Instance = new();
    public bool BlocksPlayerInput => false;
  }
}