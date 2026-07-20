using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>升级选牌?UI 期间的战斗时停（恢复先前 timeScale）?/summary>
  public static class CombatTimePause
  {
    static float s_savedTimeScale = 1f;
    static float s_validationResumeScale = 1f;
    static int s_pauseDepth;

    public static bool IsPaused => s_pauseDepth > 0;

    public static void SetValidationResumeScale(float scale) =>
      s_validationResumeScale = scale > 0.01f ? scale : 1f;

    public static void PushPause()
    {
      if (s_pauseDepth == 0)
        s_savedTimeScale = CaptureActiveTimeScale();

      s_pauseDepth++;
      Time.timeScale = 0f;
    }

    public static void PopPause()
    {
      if (s_pauseDepth <= 0)
        return;

      s_pauseDepth--;
      if (s_pauseDepth == 0)
        Time.timeScale = ResolveResumeTimeScale(s_savedTimeScale);
    }

    public static void ForceResume()
    {
      s_pauseDepth = 0;
      s_savedTimeScale = 1f;
      Time.timeScale = 1f;
    }

    public static void ForceResumeValidation()
    {
      s_pauseDepth = 0;
      Time.timeScale = ResolveResumeTimeScale(s_validationResumeScale);
      s_savedTimeScale = Time.timeScale;
    }

    static float CaptureActiveTimeScale() =>
      Time.timeScale > 0.01f ? Time.timeScale : ResolveResumeTimeScale(s_validationResumeScale);

    static float ResolveResumeTimeScale(float preferred) =>
      preferred > 0.01f ? preferred : (s_validationResumeScale > 0.01f ? s_validationResumeScale : 1f);
  }
}