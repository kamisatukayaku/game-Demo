using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Progression
{
  public readonly struct LevelUpEvent : IGameEvent
  {
    public readonly int OldLevel;
    public readonly int NewLevel;
    public readonly int TotalXp;

    public LevelUpEvent(int oldLevel, int newLevel, int totalXp)
    {
      OldLevel = oldLevel;
      NewLevel = newLevel;
      TotalXp = totalXp;
    }
  }

  public readonly struct WaveStartedEvent : IGameEvent
  {
    public readonly int WaveNumber;

    public WaveStartedEvent(int waveNumber) => WaveNumber = waveNumber;
  }

  public readonly struct WaveFinishedEvent : IGameEvent
  {
    public readonly int WaveNumber;
    public readonly bool AllWavesComplete;

    public WaveFinishedEvent(int waveNumber, bool allWavesComplete)
    {
      WaveNumber = waveNumber;
      AllWavesComplete = allWavesComplete;
    }
  }

}
