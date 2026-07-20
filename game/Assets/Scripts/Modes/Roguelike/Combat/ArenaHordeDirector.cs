using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Runtime;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// Maintains local enemy density during active waves by pulling spawns from the wave quota
  /// when nearby count drops (same pool as WaveDirector regular spawns).
  /// </summary>
  [DisallowMultipleComponent]
  public class ArenaHordeDirector : MonoBehaviour
  {
    const float CheckInterval = 0.42f;
    const float NearbyRadius = ArenaCombatScale.MinHordeNearbyRadius;

    static ArenaHordeDirector s_instance;
    float _checkTimer;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      if (GameSessionConfig.SelectedMode != GameSessionConfig.GameMode.Arena)
        return;

      var go = new GameObject("_ArenaHordeDirector");
      s_instance = go.AddComponent<ArenaHordeDirector>();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      var director = WaveDirector.Instance;
      if (director == null || director.CurrentPhase != WaveDirector.Phase.WaveActive)
        return;

      _checkTimer -= Time.deltaTime;
      if (_checkTimer > 0f)
        return;
      _checkTimer = CheckInterval;

      var wave = director.CurrentWave;
      if (director.IsWaveSpawnQuotaMet)
        return;

      var minNearby = 10 + wave / 2 + Mathf.FloorToInt(wave / 4f);
      var nearby = CountNearbyEnemies();
      if (nearby >= minNearby)
        return;

      var deficit = minNearby - nearby;
      var packSize = Mathf.Clamp(deficit / 4 + 1, 1, 4);
      director.SpawnHordeReinforcement(packSize);
    }

    static int CountNearbyEnemies()
    {
      var player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return 0;

      var center = (Vector2)player.transform.position;
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return 0;

      var count = 0;
      var radiusSq = NearbyRadius * NearbyRadius;
      foreach (var core in registry.AllEnemies)
      {
        if (core == null || !core.gameObject.activeInHierarchy)
          continue;
        if (EnemySpawnMetadata.IsBossEnemy(core.gameObject))
          continue;

        var pos = (Vector2)core.transform.position;
        if ((pos - center).sqrMagnitude <= radiusSq)
          count++;
      }

      return count;
    }
  }
}
