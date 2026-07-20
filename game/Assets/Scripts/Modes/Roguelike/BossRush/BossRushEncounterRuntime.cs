using Game.Modes.Roguelike.Combat;
using Game.Shared.Gameplay.Events;
using Game.Shared.Enemy.AI;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  /// <summary>Active Boss Rush encounter context for gameplay + presentation wiring.</summary>
  public static class BossRushEncounterRuntime
  {
    public static BossRushEncounterDef ActiveEncounter { get; private set; }
    public static int ActiveEncounterIndex { get; private set; }
    public static int LivingMinionCount { get; private set; }
    public static bool IsNearFinalEncounter =>
      ActiveEncounterIndex >= BossRushDatabase.Encounters.Count - 1;

    public static void BeginEncounter(BossRushEncounterDef encounter, int index)
    {
      ActiveEncounter = encounter;
      ActiveEncounterIndex = index;
      LivingMinionCount = 0;
      ApplyArenaModifier(encounter);
      PublishPresentation(encounter, index);
    }

    public static void Clear()
    {
      ActiveEncounter = null;
      ActiveEncounterIndex = 0;
      LivingMinionCount = 0;
      BossRushArenaModifierController.Clear();
    }

    public static bool CanSpawnMinion()
    {
      if (ActiveEncounter == null)
        return true;

      if (!ActiveEncounter.allow_minions)
        return false;

      var cap = ActiveEncounter.max_minions > 0 ? ActiveEncounter.max_minions : 8;
      return LivingMinionCount < cap;
    }

    public static void RegisterMinionSpawned() => LivingMinionCount++;

    public static void RegisterMinionRemoved()
    {
      LivingMinionCount = Mathf.Max(0, LivingMinionCount - 1);
    }

    public static void RefreshLivingMinionCount(GameObject activeBoss)
    {
      var count = 0;
      var enemies = Object.FindObjectsByType<Shared.Enemy.AI.EnemyCore>(FindObjectsSortMode.None);
      foreach (var core in enemies)
      {
        if (core == null)
          continue;

        var go = core.gameObject;
        if (go.GetComponent<Shared.Enemy.AI.BossCore>() != null)
          continue;
        if (activeBoss != null && go == activeBoss)
          continue;

        count++;
      }

      LivingMinionCount = count;
    }

    static void ApplyArenaModifier(BossRushEncounterDef encounter)
    {
      var modifierId = string.IsNullOrEmpty(encounter?.arena_modifier) ? "standard" : encounter.arena_modifier;
      BossRushArenaModifierController.Apply(modifierId);
    }

    static void PublishPresentation(BossRushEncounterDef encounter, int index)
    {
      if (encounter == null)
        return;

      GameEventBus.Publish(new BossRushEncounterStartedEvent
      {
        EncounterIndex = index,
        BossId = encounter.boss_id,
        DisplayName = encounter.display_name,
        MusicLayer = encounter.music_layer,
        BackgroundIntensity = encounter.background_intensity,
        TargetDurationSeconds = encounter.target_duration_seconds
      });
    }
  }

  public struct BossRushEncounterStartedEvent : IGameEvent
  {
    public int EncounterIndex;
    public string BossId;
    public string DisplayName;
    public string MusicLayer;
    public float BackgroundIntensity;
    public float TargetDurationSeconds;
  }
}
