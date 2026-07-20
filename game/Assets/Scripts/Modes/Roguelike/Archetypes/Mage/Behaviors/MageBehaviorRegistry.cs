using System.Collections.Generic;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  public static class MageBehaviorRegistry
  {
    static readonly List<IMageBehavior> s_behaviors = new();

    public static void Register(IMageBehavior behavior)
    {
      if (behavior != null)
        s_behaviors.Add(behavior);
    }

    public static IReadOnlyList<IMageBehavior> All => s_behaviors;
  }
}
