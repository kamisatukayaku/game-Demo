using UnityEngine;

using Game.Modes.Roguelike.UI;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B2: Offers Corruption pick every 5 waves after wave completion.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaCorruptionDirector : MonoBehaviour
  {
    static readonly int[] CorruptionWaves = { 5, 10, 15 };

    static ArenaCorruptionDirector s_instance;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaCorruptionDirector");
      go.AddComponent<ArenaCorruptionDirector>();
    }

    public static void BeginRun()
    {
      EnsureExists();
      CorruptionRuntime.ResetRun();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      CorruptionDatabase.EnsureLoaded();
      WaveDirector.WaveCompleted += OnWaveCompleted;
    }

    void OnDestroy()
    {
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      if (s_instance == this)
        s_instance = null;
    }

    static void OnWaveCompleted(int waveNumber)
    {
      if (!IsCorruptionWave(waveNumber))
        return;

      ArenaCorruptionPickUI.ShowOffer($"Corruption — 第 {waveNumber} 波", "Corruption");
    }

    static bool IsCorruptionWave(int waveNumber)
    {
      foreach (var wave in CorruptionWaves)
      {
        if (wave == waveNumber)
          return true;
      }

      return false;
    }
  }
}
