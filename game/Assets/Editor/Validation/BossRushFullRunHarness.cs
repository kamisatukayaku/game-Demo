using System;
using System.Collections.Generic;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Game.Shared.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class BossRushFullRunHarness
  {
    const string MenuPath = "Tools/Validation/Run Boss Rush Full Run Harness";

    [MenuItem(MenuPath)]
    public static void RunHarness()
    {
      BossRushDirector.EditorDestroyInstance();
      WaveDirector.EditorDestroyInstance();
      EnemySpawner.SpawningEnabled = true;
      BossRushDatabase.ResetForTests();
      BossRushDatabase.EnsureLoaded();
      ArenaBuildBootstrap.ConfigureForSimulation("mage");
      GameSessionConfig.Configure("mage", Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, "mage");

      var directorGo = new GameObject("_HarnessDirector");
      var director = directorGo.AddComponent<BossRushDirector>();
      director.EditorPrepareForTests();

      try
      {
        Require(BossRushDatabase.Encounters.Count == 7, "seven encounters configured");
        Require(WaveDirector.Instance == null, "no WaveDirector during harness");

        for (var i = 1; i <= 7; i++)
        {
          var encounter = BossRushDatabase.GetEncounter(i);
          Require(encounter != null, $"encounter {i} missing");

          var mockBoss = new GameObject($"MockBoss_{encounter.boss_id}");
          mockBoss.AddComponent<MiniBossHexSentinel>();

          director.EditorForcePhase(BossRushPhase.BossActive);
          director.EditorBindActiveBoss(mockBoss, encounter);
          GameEventBus.Publish(new BossKilledEvent(mockBoss, null, mockBoss.transform.position, encounter.boss_id));
          Require(director.Phase == BossRushPhase.BossDefeated, "boss defeated");

          if (encounter.reward_count > 0)
          {
            for (var r = 0; r < encounter.reward_count; r++)
            {
              var offer = RunBuildState.GetPendingOffer();
              if (offer.choices == null || offer.choices.Length == 0)
              {
                Debug.LogWarning($"[BossRushFullRunHarness] Encounter {i} reward {r + 1}: empty offer (simulation pool exhaustion).");
                break;
              }

              RunBuildState.ApplyChoice(offer.choices[0]);
            }
          }

          BossRushCombatService.CleanupBattlefield(BossRushDatabase.Settings);
          director.EditorForcePhase(BossRushPhase.NextEncounter);
          UnityEngine.Object.DestroyImmediate(mockBoss);
        }

        director.EditorForcePhase(BossRushPhase.FinalVictory);
        Require(UnityEngine.Object.FindObjectsOfType<BossCore>().Length == 0, "no residual bosses");
        ActiveProjectileRegistry.ResetAll();

        Debug.Log("[BossRushFullRunHarness] PASS");
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(directorGo);
        BossRushDirector.EditorDestroyInstance();
        WaveDirector.EditorDestroyInstance();
        GameSessionConfig.ResetForEditor();
        BossRushDatabase.ResetForTests();
      }
    }

    static void Require(bool condition, string message)
    {
      if (!condition)
        throw new InvalidOperationException(message);
    }
  }
}
