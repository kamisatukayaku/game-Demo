using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Core
{
  public sealed class ArchetypeContext
  {
    public GameObject Player { get; }

    public ArchetypeContext(GameObject player)
    {
      Player = player;
    }
  }
}
