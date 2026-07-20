#if UNITY_EDITOR
using Game.Modes.Roguelike.BossRush;
using Game.Shared.Enemy.AI;
using Game.Shared.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.DebugTools
{
  public static class BossRushDebugMenu
  {
    const string Root = "Tools/Boss Rush Debug/";

    [MenuItem(Root + "Show Current Phase")]
    static void ShowPhase()
    {
      if (BossRushDirector.Instance == null)
      {
        Debug.Log("[BossRushDebug] No active BossRushDirector.");
        return;
      }

      var d = BossRushDirector.Instance;
      Debug.Log($"[BossRushDebug] phase={d.Phase} encounter={d.CurrentEncounterIndex} defeated={d.DefeatedBossCount} elapsed={d.RunElapsedSeconds:F1}s");
    }

    [MenuItem(Root + "Force Defeat Current Boss")]
    static void ForceDefeat()
    {
      BossRushDirector.Instance?.EditorForceDefeatBoss();
    }

    [MenuItem(Root + "Open Reward Phase")]
    static void OpenReward()
    {
      BossRushDirector.Instance?.EditorOpenRewardPhase();
    }

    [MenuItem(Root + "Advance Encounter Index")]
    static void AdvanceEncounter()
    {
      BossRushDirector.Instance?.EditorAdvanceEncounter();
    }

    [MenuItem(Root + "Cleanup Battlefield")]
    static void Cleanup()
    {
      BossRushDatabase.EnsureLoaded();
      BossRushCombatService.CleanupBattlefield(BossRushDatabase.Settings);
    }

    [MenuItem(Root + "Count Active Bosses/Parts")]
    static void CountEntities()
    {
      var bosses = Object.FindObjectsOfType<BossCore>().Length;
      var cores = Object.FindObjectsOfType<EnemyCore>().Length;
      Debug.Log($"[BossRushDebug] BossCore={bosses} EnemyCore={cores} mode={GameSessionConfig.SelectedMode}");
    }
  }
}
#endif
