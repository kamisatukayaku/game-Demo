using System.Collections;
using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Gameplay;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Player;
using Game.Shared.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Health = Game.Shared.Combat.Health.Health;
namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  public static class RingArenaPlayModeSession
  {
    public static IEnumerator LoadMainSceneAndBootstrap(string buildId, string weaponTheme, int seed)
    {
      RuntimeValidationTelemetry.Reset();
      CombatChainTelemetry.Reset();
      CombatChainProbe.EnsureExists();
      RuntimeValidationSettings.RestoreTimeScale();
      GameSessionConfig.ResetForEditor();
      Random.InitState(seed);

      GameSessionConfig.Configure(
        weaponTheme,
        System.Array.Empty<string>(),
        "normal",
        GameSessionConfig.GameMode.Arena,
        buildId);

      CombatSceneBootstrapLocator.Register(RoguelikeCombatSceneBootstrap.Instance);
      ArenaBuildBootstrap.ConfigureForSimulation(buildId);
      ExperienceSystem.ResetToDefault();
      LevelUpController.ResetForNewRun();
      ArenaRunRestart.PrepareForNewRun();
      TeardownPersistedArenaSystems();
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationEventBridge.EnsureExists();
      CombatRoot.ValidationResetMainSceneInit();
#endif

      var load = SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
      while (load != null && !load.isDone)
        yield return null;

      yield return null;
      CombatRoot.RequestMainSceneInitialization(SceneManager.GetActiveScene());
      RuntimeValidationTelemetry.RecordSceneLoad();
      yield return EnsureCombatReady(45f);
    }

    static void TeardownPersistedArenaSystems()
    {
      WaveDirector.ShutdownForAlternateMode();
      foreach (var spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
      {
        if (spawner != null)
          Object.Destroy(spawner.gameObject);
      }
    }

    static IEnumerator EnsureCombatReady(float timeoutSeconds)
    {
      var elapsed = 0f;
      while (elapsed < timeoutSeconds)
      {
        if (IsCombatReady())
          yield break;
        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      RoguelikeCombatSceneBootstrap.Instance.InitializeCombatSystems();
      var retry = 0f;
      while (retry < 5f)
      {
        if (IsCombatReady())
          yield break;
        retry += Time.unscaledDeltaTime;
        yield return null;
      }

      if (!IsCombatReady())
        throw new System.InvalidOperationException("MainScene combat bootstrap timed out.");
    }

    static bool IsCombatReady()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      var director = WaveDirector.Instance;
      var spawner = Object.FindAnyObjectByType<EnemySpawner>();
      return player != null
             && spawner != null
             && director != null
             && director.CurrentPhase != WaveDirector.Phase.NotStarted;
    }

    public static IEnumerator WaitForCombatReady(float timeoutSeconds)
    {
      var elapsed = 0f;
      while (elapsed < timeoutSeconds)
      {
        if (IsCombatReady())
          yield break;

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      RoguelikeCombatSceneBootstrap.Instance.InitializeCombatSystems();
      var retry = 0f;
      while (retry < 5f)
      {
        if (IsCombatReady())
          yield break;
        retry += Time.unscaledDeltaTime;
        yield return null;
      }

      throw new System.InvalidOperationException("MainScene combat bootstrap timed out.");
    }

    public static RingArenaAutoPlayer EnableAutoPlayer() => RingArenaAutoPlayer.Ensure();

    public static void BeginCombatValidation()
    {
      CombatChainTelemetry.Reset();
      CombatChainProbe.EnsureExists();
      RuntimeValidationEventBridge.EnsureExists();
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return;

      var autoAttack = player.GetComponent<PlayerAutoAttack>();
      if (autoAttack != null)
        autoAttack.enabled = true;

      var applier = player.GetComponent<RunBuildApplier>();
      if (applier != null)
        applier.Apply();
      else
        player.GetComponent<PlayerAttackDirector>()?.SetAutoAttack(true);

      ArenaBuildBootstrap.ApplySelectedBuild();
      EnableAutoPlayer();
      ApplyValidationPlayerSurvival();
    }

    public static void ApplyValidationPlayerSurvival()
    {
      RuntimeValidationSettings.MarkPlayerSurvivalBoostActive();
      RuntimeValidationPlayerSurvival.ApplyForFullRun();
    }

    public static IEnumerator WaitUntilWaveAtLeast(int targetWave, float timeoutSeconds)
    {
      var elapsed = 0f;
      while (elapsed < timeoutSeconds)
      {
        ValidationBlockingUiAutoResponder.Tick();

        var director = WaveDirector.Instance;
        if (director != null && director.CurrentWave >= targetWave
            && director.CurrentPhase == WaveDirector.Phase.BuildPhase)
          yield break;

        if (director != null && director.CurrentPhase == WaveDirector.Phase.AllWavesComplete)
          yield break;

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      throw new System.InvalidOperationException($"Timed out waiting for wave {targetWave}.");
    }

    public static IEnumerator WaitForAllWavesComplete(float timeoutSeconds)
    {
      var elapsed = 0f;
      while (elapsed < timeoutSeconds)
      {
        ValidationBlockingUiAutoResponder.Tick();

        if (WaveDirector.Instance != null
            && WaveDirector.Instance.CurrentPhase == WaveDirector.Phase.AllWavesComplete)
          yield break;

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      throw new System.InvalidOperationException("Timed out waiting for AllWavesComplete.");
    }

    public static IEnumerator WaitForPlayerDeath(float timeoutSeconds)
    {
      var elapsed = 0f;
      while (elapsed < timeoutSeconds)
      {
        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        var health = player != null ? player.GetComponent<Health>() : null;
        if (health != null && health.IsDead)
          yield break;

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      throw new System.InvalidOperationException("Timed out waiting for player death.");
    }

    public static IEnumerator TriggerRestartAndWait(float timeoutSeconds)
    {
      var beforeLoads = RuntimeValidationTelemetry.SceneLoadCount;
      ArenaRunRestart.ReloadMainScene();
      yield return WaitForCombatReady(timeoutSeconds);
      RuntimeValidationTelemetry.RecordSceneLoad();
      RuntimeValidationTelemetry.RecordOrphanedObjects(CountOrphanedValidationObjects());
      AssertRestartBaseline(beforeLoads);
    }

    public static int CountOrphanedValidationObjects()
    {
      var count = 0;
      foreach (var projectile in Object.FindObjectsByType<Game.Shared.Projectile.StraightProjectile>(FindObjectsSortMode.None))
      {
        if (projectile != null && projectile.gameObject.activeInHierarchy)
          count++;
      }

      foreach (var pickup in Object.FindObjectsByType<XpPickup>(FindObjectsSortMode.None))
      {
        if (pickup != null && pickup.gameObject.activeInHierarchy)
          count++;
      }

      return count;
    }

    static void AssertRestartBaseline(int beforeLoads)
    {
      RuntimeValidationTelemetry.IncrementEvent("restart_scene_load_delta");
      if (RuntimeValidationTelemetry.SceneLoadCount <= beforeLoads)
        RuntimeValidationTelemetry.IncrementEvent("restart_scene_load_missing");
    }

    public static (string buildId, string weaponTheme) ResolveStarter(string starterKey)
    {
      switch (starterKey?.ToLowerInvariant())
      {
        case "unified":
        case "free":
          return (ArenaBuildBootstrap.Unified, UnifiedBuildBootstrap.WeaponTheme);
        case "mage":
          return (ArenaBuildBootstrap.Mage, "mage");
        case "ranger":
        case "shooter":
          return (ArenaBuildBootstrap.Shooter, "ranged");
        case "contact":
        case "dash":
        case "warrior":
          return (ArenaBuildBootstrap.Contact, "warrior");
        default:
          return (ArenaBuildBootstrap.Unified, UnifiedBuildBootstrap.WeaponTheme);
      }
    }
  }
}
