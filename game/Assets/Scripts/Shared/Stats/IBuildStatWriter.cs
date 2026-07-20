namespace Game.Shared.Stats
{
  /// <summary>向当?Run 构筑写入 stat（World 等模式通过 Locator 调用，不引用 Roguelike）?/summary>
  public interface IBuildStatWriter
  {
    void AddStat(string key, float value);
  }
}