using UnityEngine;

using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>Wave chests, first-kill bonuses, and boss reward bursts.</summary>
  public sealed class ArenaWaveRewards : MonoBehaviour
  {
    static ArenaWaveRewards s_instance;

    readonly System.Collections.Generic.HashSet<string> _seenEnemyTypes = new();
    EventListenerHandle _waveFinishedHandle;
    EventListenerHandle _enemyKilledHandle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaWaveRewards");
      DontDestroyOnLoad(go);
      go.AddComponent<ArenaWaveRewards>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      _waveFinishedHandle = GameEventBus.Subscribe<WaveFinishedEvent>(OnWaveFinished);
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDestroy()
    {
      if (_waveFinishedHandle.Valid)
        GameEventBus.Unsubscribe(_waveFinishedHandle);
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void OnWaveFinished(WaveFinishedEvent evt)
    {
      var player = GameObject.FindWithTag("Player");
      if (player == null)
        return;

      var pos = player.transform.position;
      var chestXp = ArenaDifficultyRuntime.ScaleXp(
        (WaveDirector.Instance != null && WaveDirector.Instance.IsBossWave(evt.WaveNumber) ? 55 : 35)
        + evt.WaveNumber * 8);
      LootService.SpawnXpPickup(pos + Vector3.left * 0.8f, chestXp);
      ArenaRewardVfx.PlayChest(pos);
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (evt.Enemy == null || string.IsNullOrEmpty(evt.EnemyId))
        return;

      if (!_seenEnemyTypes.Add(evt.EnemyId))
        return;

      var bonus = evt.IsBoss ? 280 : 18;
      bonus = ArenaDifficultyRuntime.ScaleXp(bonus);
      LootService.SpawnXpPickup(evt.Enemy.transform.position, bonus);
      ArenaRewardVfx.PlayFirstKill(evt.Enemy.transform.position, evt.IsBoss);
    }
  }
}
