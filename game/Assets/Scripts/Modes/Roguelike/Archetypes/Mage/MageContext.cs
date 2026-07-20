
namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>Mage 模块统一访问 SkillSystemLocator（SkillContext ?Mage 运行时快照）?/summary>
  public static class MageSystemLocator
  {
    public static ISkillSystem System => SkillSystemLocator.System;

    public static SkillContext Context => System.Context;
  }
}
