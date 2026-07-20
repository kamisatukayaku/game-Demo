using UnityEngine;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Combat;
using Game.Shared.Runtime;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Gameplay.Player;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.Presentation.Audio;
using Game.Shared.Core;
using Game.Shared.Player;
using Game.Shared.Gameplay.Bridges;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
#endif
using Game.Shared.UI;
using Game.UI;

namespace Game.Modes.Roguelike.Gameplay
{
  public sealed class RoguelikeCombatSceneBootstrap : ICombatSceneBootstrap
  {
    public static readonly RoguelikeCombatSceneBootstrap Instance = new();

    public void InitializeCombatSystems()
    {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationSettings.ResetForNormalPlay();
#endif
      MageZonePool.ResetAll();
      KeyBindingsUI.EnsureExists();

      ExperienceSystem.EnsureExists();
      HealOnKillBudget.Reset();
      LevelUpController.EnsureExists();
      BuildMilestoneRuntime.EnsureExists();
      BuildMilestoneRuntime.ResetSession();
      LootManager.EnsureExists();

      PlayerHUD.EnsureExists();
      if (!GameSessionConfig.IsBossRush)
        WaveHUD.EnsureExists();
      MageSkillCooldownHUD.EnsureExists();
      KillStreakFeedbackUI.EnsureExists();
      ArenaMomentUI.EnsureExists();
      RunDecisionMomentListener.EnsureExists();
      ArenaBgmController.EnsureExists();
      ArenaQuadrantBlocker.EnsureExists();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      BossCombatDebugOverlay.EnsureExists();
#endif
      PlayerStatPanelUI.EnsureExists();
      RoguelikeEnemyDeathFeedbackSystem.EnsureExists();
      RoguelikeEnemyCollisionPolicy.EnsureExists();
      DetachedWeaponPresentationSystem.EnsureExists();
      ContactDashPresentationSystem.EnsureExists();
      PlayerDashController.ResetRunState();
      RangedOverloadRuntime.ResetForNewRun();

      ClearLegacySceneEnemies();

      RunDeathSummary.MarkRunStart();
      ArenaMetaProgress.RecordBuildRun(ArenaBuildBootstrap.SelectedBuildId);
      ArenaLayoutController.BeginRun();
      EnsureCameraFollow();
      CircleArenaController.EnsureExists();
      CombatEventSubscriber.EnsureExists();
      ArenaBuildIdentityUI.EnsureExists();
      ArenaBuildIdentityUI.ShowForCurrentBuild();
      ArenaBuildBootstrap.ApplySelectedBuild();
      RoguelikeSkillSystem.Instance.RefreshFromBuild();

      if (GameSessionConfig.IsBossRush)
      {
        WaveDirector.ShutdownForAlternateMode();
        BossRushHUD.EnsureExists();
        BossRushVictoryUI.EnsureExists();
        BossRushFailureUI.EnsureExists();
        BossRushDirector.BeginRun();
        return;
      }

      WaveDirector.BeginRun();
      HuntContractRuntime.EnsureExists();
      SupportPanicRuntime.EnsureExists();
      ArenaRareEnemySpawner.EnsureExists();
      ArenaXpZoneController.EnsureExists();
      ArenaMidWaveEventDirector.EnsureExists();
      ArenaCorruptionDirector.EnsureExists();
      WaveModifierRuntime.EnsureExists();
      ArenaWaveRewards.EnsureExists();
      ArenaTutorialController.EnsureExists();
      RoguelikeTutorialDirector.EnsureExists();
      TutorialEventListener.EnsureExists();
      TutorialPromptUI.EnsureExists();
      GroundZoneInfoPresenter.EnsureExists();
      GroundZoneProximityTracker.EnsureExists();
      RoguelikeTutorialSandboxUI.EnsureExistsIfSandbox();
      ArenaDamageLog.EnsureExists();
      ArenaVictoryUI.EnsureExists();
      EvolutionMomentUI.EnsureExists();
      PlayerCapstoneVfx.EnsureExists();
      ArenaObstacleController.EnsureExists();
      ArenaRelicPickUI.EnsureExists();
      ArenaCorruptionPickUI.EnsureExists();
      ArenaHordeDirector.EnsureExists();
      BuildMovieArcDirector.EnsureExists();
      BuildMovieArcDirector.BeginRun();
      ArenaNarrativeEventDirector.EnsureExists();
      ArenaNarrativeEventDirector.BeginRun();
      ArenaHazardController.EnsureExists();
      ArenaScenePresentation.Apply();
    }

    public void ApplyPlayerComponents(GameObject playerGo)
    {
      if (playerGo == null)
        return;

      var autoAttack = playerGo.GetComponent<PlayerAutoAttack>();
      if (autoAttack == null)
        autoAttack = playerGo.AddComponent<PlayerAutoAttack>();
      autoAttack.enabled = true;

      if (playerGo.GetComponent<RunBuildApplier>() == null)
        playerGo.AddComponent<RunBuildApplier>();
      else
        playerGo.GetComponent<RunBuildApplier>().Apply();

      PlayerActiveSkillController.Ensure(playerGo);
      PlayerDashController.Ensure(playerGo);
      PlayerStateMachine.Ensure(playerGo);
      DetachedWeaponPresentationSystem.EnsureExists();
      DetachedWeaponSystem.Ensure(playerGo);
      TutorialGameplayEventPublisher.EnsureOn(playerGo);

      var camera = Camera.main;
      if (camera != null)
      {
        var follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
          follow = camera.gameObject.AddComponent<CameraFollow2D>();
        follow.SetTarget(playerGo.transform);
      }

      ArenaScenePresentation.Apply(playerGo);
    }

    static void ClearLegacySceneEnemies()
    {
      var root = GameObject.Find("Enemies");
      if (root == null)
        return;

      for (var i = root.transform.childCount - 1; i >= 0; i--)
        Object.Destroy(root.transform.GetChild(i).gameObject);
    }

    static void EnsureCameraFollow()
    {
      var camera = Camera.main;
      if (camera == null)
        return;

      ArenaCombatCamera.Apply(camera);
    }
  }
}
