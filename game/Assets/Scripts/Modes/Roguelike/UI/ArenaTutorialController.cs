using Game.Modes.Roguelike.Tutorial;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>Legacy entry point; forwards to <see cref="RoguelikeTutorialDirector"/>.</summary>
  public sealed class ArenaTutorialController
  {
    public static void EnsureExists() => RoguelikeTutorialDirector.EnsureExists();

    public static void NotifyEnemySpawned(string enemyId) =>
      RoguelikeTutorialDirector.Instance?.NotifyEnemySpawned(enemyId);
  }
}
