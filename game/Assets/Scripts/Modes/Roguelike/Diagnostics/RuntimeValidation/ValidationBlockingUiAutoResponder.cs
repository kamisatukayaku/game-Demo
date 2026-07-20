#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Auto-dismisses blocking arena UI during Play Mode validation.</summary>
  public static class ValidationBlockingUiAutoResponder
  {
    static float s_frozenSince = -1f;

    public static void Tick()
    {
      // Dismiss direct timeScale blockers before level-up pause restore.
      ArenaCorruptionPickUI.ValidationAutoPickFirst();
      ArenaRelicPickUI.ValidationAutoPickFirst();
      LevelUpController.ValidationAutoPickFirst();
      EnsureSimulationAdvancing();
    }

    static void EnsureSimulationAdvancing()
    {
      if (Time.timeScale > 0.01f)
      {
        s_frozenSince = -1f;
        return;
      }

      if (ArenaCorruptionPickUI.IsShowing || ArenaRelicPickUI.IsShowing || LevelUpController.IsWaiting)
      {
        s_frozenSince = -1f;
        return;
      }

      if (CombatTimePause.IsPaused)
        CombatTimePause.ForceResumeValidation();

      if (Time.timeScale > 0.01f)
      {
        s_frozenSince = -1f;
        return;
      }

      if (RuntimeValidationSettings.ActiveTimeScale > RuntimeValidationSettings.NormalTimeScale)
        Time.timeScale = RuntimeValidationSettings.ActiveTimeScale;
    }

    public static float GetFrozenDurationSeconds()
    {
      if (Time.timeScale > 0.01f || s_frozenSince < 0f)
        return 0f;

      return Time.unscaledTime - s_frozenSince;
    }

    public static void TrackFrozenState()
    {
      if (Time.timeScale <= 0.01f
          && !ArenaCorruptionPickUI.IsShowing
          && !ArenaRelicPickUI.IsShowing
          && !LevelUpController.IsWaiting
          && !CombatTimePause.IsPaused)
      {
        if (s_frozenSince < 0f)
          s_frozenSince = Time.unscaledTime;
      }
      else
      {
        s_frozenSince = -1f;
      }
    }
  }
}

#endif
