using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Core
{
  /// <summary>Represents a complete Roguelike archetype rather than one mechanic.</summary>
  public interface IArchetype
  {
    string Id { get; }

    bool IsActive { get; }

    void Initialize(GameObject player);

    void Shutdown();

    void OnLevelUp();

    void OnEnemyKilled();

    void Tick(float deltaTime);
  }
}
