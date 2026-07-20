using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B14: Killing a support enemy triggers arena-wide panic, XP burst, and moment banner (W8+).</summary>
  [DisallowMultipleComponent]
  public sealed class SupportPanicRuntime : MonoBehaviour
  {
    const int MinWave = 8;
    const float PanicDuration = 2f;
    const float PanicSpeedMult = 1.65f;
    const int BonusXp = 72;

    static SupportPanicRuntime s_instance;

    EventListenerHandle _enemyKilledHandle;
    Coroutine _panicRoutine;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_SupportPanicRuntime");
      DontDestroyOnLoad(go);
      go.AddComponent<SupportPanicRuntime>();
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
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDestroy()
    {
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (evt.Enemy == null || !PlayerCombatAttribution.IsPlayerOrOwned(evt.Killer))
        return;

      var agent = evt.Enemy.GetComponent<MonsterEcosystemAgent>();
      if (agent == null || agent.Role != "supporter")
        return;

      var director = WaveDirector.Instance;
      if (director == null || director.CurrentWave < MinWave)
        return;

      TriggerPanic(evt.Position);
    }

    void TriggerPanic(Vector3 position)
    {
      if (_panicRoutine != null)
        StopCoroutine(_panicRoutine);

      _panicRoutine = StartCoroutine(PanicRoutine(position));
    }

    IEnumerator PanicRoutine(Vector3 position)
    {
      var registry = CombatRoot.EnemyRegistry;
      var toRevert = new List<EnemyMovement>();
      if (registry != null)
      {
        foreach (var enemy in registry.AllEnemies)
        {
          if (enemy == null)
            continue;
          var movement = enemy.GetComponent<EnemyMovement>();
          if (movement == null)
            continue;

          if (!movement.IsSprinting)
            toRevert.Add(movement);
          movement.SetSprintState(true, PanicSpeedMult);
        }
      }

      var xp = ArenaDifficultyRuntime.ScaleXp(BonusXp);
      LootService.SpawnXpPickup(position + Vector3.up * 0.35f, xp);
      LootService.SpawnXpPickup(position + Vector3.left * 0.45f, Mathf.RoundToInt(xp * 0.55f));
      ArenaMomentUI.ShowBanner("战术击溃", new Color(1f, 0.45f, 0.28f, 1f));
      RunTimelineRecorder.Record("战术击溃", "击杀支援");

      yield return new WaitForSeconds(PanicDuration);

      foreach (var movement in toRevert)
      {
        if (movement != null)
          movement.SetSprintState(false);
      }

      _panicRoutine = null;
    }
  }
}
