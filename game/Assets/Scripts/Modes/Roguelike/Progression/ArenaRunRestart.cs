using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Archetypes.Ranged;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
#endif
using Game.Modes.Roguelike.Build.Integration;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.UI;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Projectile;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.UI;
using Game.Shared.UI;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>死亡/胜利后「重新开始」时重置 DontDestroyOnLoad 单局状态。</summary>
  public static class ArenaRunRestart
  {
    public static void PrepareForNewRun()
    {
      Time.timeScale = 1f;
      CombatTimePause.ForceResume();
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationSettings.ResetForNormalPlay();
#else
      CombatTimePause.SetValidationResumeScale(1f);
#endif

      ResetRunProgression();
      ResetArenaFieldState();

      ExperienceSystem.ResetToDefault();
      CombatEventSubscriber.ResetForNewRun();
      LevelUpController.ResetForNewRun();
      UpgradeOfferPityTracker.ResetForNewRun();
      RangedOverloadRuntime.ResetForNewRun();
      LootService.ResetRunLootState();
      RunDeathSummary.MarkRunStart();
      BossAttackHitTracker.ResetRun();
      ArenaQuadrantBlocker.Clear();
      if (GameSessionConfig.IsBossRush)
        BossRushDirector.ResetForNewRun();
      else
      {
        ArenaDifficultyRuntime.BindSession();
        HuntContractRuntime.BeginRun(ArenaDifficultyRuntime.TotalWaves);
      }
      GroundZoneProximityTracker.ResetForNewRun();
      RoguelikeTutorialDirector.ResetForNewRun();
      DetachedWeaponPresentationSystem.ResetPoolsForNewRun();
      ActiveProjectileRegistry.DespawnAllActive();
      XpPickup.ResetPoolForNewRun();
      MageZonePool.ResetAll();
      HealOnKillBudget.Reset();
      BuildMilestoneRuntime.ResetSession();
      CorruptionRuntime.ResetRun();
      ArenaLayoutController.Reset();

      ClearOrphanedDetachedWeapons();
    }

    static void ResetRunProgression()
    {
      if (GameSessionConfig.RunConfigured)
      {
        RoguelikeRunSessionConfigurator.Instance.ConfigureRun(
          GameSessionConfig.SelectedWeaponTheme,
          GameSessionConfig.SelectedTalentIds);
        return;
      }

      var buildId = string.IsNullOrEmpty(GameSessionConfig.SelectedBuildDirectionId)
        ? ArenaBuildBootstrap.Unified
        : GameSessionConfig.SelectedBuildDirectionId;
      var theme = ArenaBuildBootstrap.GetThemeForBuild(buildId);
      RunBuildState.Reset(theme);
      ArenaBuildBootstrap.ApplySelectedBuild();
    }

    static void ResetArenaFieldState()
    {
      WaveDirector.PrepareArenaRestart();
      ArenaObstacleController.ResetForNewRun();
      ArenaHazardController.ResetForNewRun();
    }

    public static void ReloadMainScene()
    {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationTelemetry.RecordRunRestart();
#endif
      PrepareForNewRun();
      CombatRoot.ResetMainSceneInitialization();
      HideRunEndOverlays();
      SceneManager.LoadScene("MainScene");
    }

    /// <summary>Reset DontDestroyOnLoad run state and return to the StartGame menu.</summary>
    public static void ReturnToMainMenu()
    {
      PrepareForLeavingRun();
      CombatRoot.ResetMainSceneInitialization();
      HideRunEndOverlays();
      GameSessionConfig.AbandonCurrentRun();
      GameSceneTransitionCurtain.LoadScene("StartGame");
    }

    static void PrepareForLeavingRun()
    {
      Time.timeScale = 1f;
      CombatTimePause.ForceResume();
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationSettings.ResetForNormalPlay();
#else
      CombatTimePause.SetValidationResumeScale(1f);
#endif

      ResetArenaFieldState();
      CombatEventSubscriber.ResetForNewRun();
      LevelUpController.ResetForNewRun();
      RangedOverloadRuntime.ResetForNewRun();
      LootService.ResetRunLootState();
      BossAttackHitTracker.ResetRun();
      ArenaQuadrantBlocker.Clear();
      GroundZoneProximityTracker.ResetForNewRun();
      RoguelikeTutorialDirector.ResetForNewRun();
      DetachedWeaponPresentationSystem.ResetPoolsForNewRun();
      ActiveProjectileRegistry.DespawnAllActive();
      XpPickup.ResetPoolForNewRun();
      MageZonePool.ResetAll();
      HealOnKillBudget.Reset();
      BuildMilestoneRuntime.ResetSession();
      CorruptionRuntime.ResetRun();
      ArenaLayoutController.Reset();
      ClearOrphanedDetachedWeapons();
    }

    static void HideRunEndOverlays()
    {
      PlayerStatPanelUI.HideIfVisible();
      PlayerDeathFailureUI.HideIfVisible();
      ArenaVictoryUI.HideIfVisible();
      RunShareCardUI.HideIfVisible();
    }

    static void ClearOrphanedDetachedWeapons()
    {
      foreach (var weapon in Object.FindObjectsOfType<DetachedWeaponController>())
      {
        if (weapon == null)
          continue;
#if UNITY_EDITOR
        if (!Application.isPlaying)
          Object.DestroyImmediate(weapon.gameObject);
        else
#endif
          Object.Destroy(weapon.gameObject);
      }
    }
  }
}
