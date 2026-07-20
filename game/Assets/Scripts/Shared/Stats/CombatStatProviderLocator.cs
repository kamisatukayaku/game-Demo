namespace Game.Shared.Stats
{
  /// <summary>当前模式注册?<see cref="ICombatStatProvider"/>（Arena ?Roguelike 注册）?/summary>
  public static class CombatStatProviderLocator
  {
    static ICombatStatProvider s_provider = NullCombatStatProvider.Instance;

    public static ICombatStatProvider Provider => s_provider;

    public static void Register(ICombatStatProvider provider)
    {
      s_provider = provider ?? NullCombatStatProvider.Instance;
    }

    public static void Clear()
    {
      s_provider = NullCombatStatProvider.Instance;
    }
  }
}