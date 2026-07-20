using System.Collections.Generic;

namespace Game.Shared.Stats
{
  /// <summary>开局 UI ?单局构筑配置（由 Roguelike 模式实现）?/summary>
  public interface IRunSessionConfigurator
  {
    void ConfigureRun(string weaponTheme, IEnumerable<string> talentIds);
  }

  public static class RunSessionConfiguratorLocator
  {
    static IRunSessionConfigurator s_configurator;

    public static IRunSessionConfigurator Configurator => s_configurator;

    public static void Register(IRunSessionConfigurator configurator) => s_configurator = configurator;

    public static void Clear() => s_configurator = null;
  }
}