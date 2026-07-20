using Game.Modes.Roguelike.Combat;
using Game.Shared.Core;
using Game.Shared.Player;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Play Mode / runtime validation tuning (no gameplay balance changes).</summary>
  public static class RuntimeValidationSettings
  {
    public const float DefaultAcceleratedTimeScale = 20f;
    public const float FullRunAcceleratedTimeScale = 20f;
    public const float MaxAcceleratedTimeScale = 20f;
    public const float NormalTimeScale = 1f;
    public const float AiSoakDurationSeconds = 60f;
    public const float AiSoakStallThresholdSeconds = 8f;
    public const float BossSampleDurationSeconds = 45f;
    public const int BoundaryRollsPerScenario = 1000;
    public const int WaveBalanceSeedsPerStarter = 10;

    public static float ActiveTimeScale { get; private set; } = NormalTimeScale;
    public static bool UseSyntheticInput { get; private set; }
    public static bool PlayerSurvivalBoostActive { get; private set; }

    public static void MarkPlayerSurvivalBoostActive() => PlayerSurvivalBoostActive = true;

    public static void ClearPlayerSurvivalBoost() => PlayerSurvivalBoostActive = false;

    public static void SetAccelerated(float scale = DefaultAcceleratedTimeScale)
    {
      ActiveTimeScale = Mathf.Clamp(scale, 1f, MaxAcceleratedTimeScale);
      CombatTimePause.SetValidationResumeScale(ActiveTimeScale);
      Time.timeScale = ActiveTimeScale;
      if (ActiveTimeScale >= 6f)
        WaveDirector.ValidationMinimizeBuildPhases();
    }

    public static void SetFullRunAccelerated() => SetAccelerated(FullRunAcceleratedTimeScale);

    /// <summary>Clear validation harness state before normal editor / manual play.</summary>
    public static void ResetForNormalPlay()
    {
      RestoreTimeScale();
      DisableSyntheticInput();
      ClearPlayerSurvivalBoost();
      GameInputBindings.ClearSyntheticInput();
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      RuntimeValidationPlayerSurvival.Restore();
      ClearValidationHarnessFromPlayer();
#endif
    }

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
    static void ClearValidationHarnessFromPlayer()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return;

      var autoPlayer = player.GetComponent<RingArenaAutoPlayer>();
      if (autoPlayer != null)
        Object.Destroy(autoPlayer);
    }
#endif

    public static void RestoreTimeScale()
    {
      ActiveTimeScale = NormalTimeScale;
      CombatTimePause.SetValidationResumeScale(NormalTimeScale);
      Time.timeScale = NormalTimeScale;
    }

    public static void EnableSyntheticInput() => UseSyntheticInput = true;

    public static void DisableSyntheticInput() => UseSyntheticInput = false;
  }
}
