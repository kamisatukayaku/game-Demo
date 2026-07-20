using UnityEngine;

namespace Game.Shared.Laser
{
  /// <summary>怪物持续激光的可配置参数?/summary>
  [System.Serializable]
  public struct LaserBeamSettings
  {
    public float MaxRange;
    public float Duration;
    public float DamageTickInterval;

    [Header("LineRenderer 宽度（世界单位）")]
    public float CoreWidth;
    public float GlowWidth;

    [Header("颜色")]
    public Color CoreColor;
    public Color GlowColor;

    [Header("脉冲闪烁")]
    public float PulseSpeed;
    public float PulseAmount;

    public static LaserBeamSettings DefaultMob(Color beamTint, float maxRange, float beamHalfWidth)
    {
      var shell = beamTint;
      if (shell.a <= 0.01f)
        shell = new Color(1f, 0.28f, 0.08f, 0.72f);

      // 整体比普通射线更粗；白芯?80%~90%，红/橙外壳仅 10%~20% 窄包边?
      var totalWidth = Mathf.Clamp(beamHalfWidth * 2.2f, 0.34f, 0.58f);
      var coreBody = Mathf.Clamp(totalWidth * 0.18f, 0.045f, 0.085f);
      var glowWidth = totalWidth;

      return new LaserBeamSettings
      {
        MaxRange = Mathf.Max(0.5f, maxRange),
        Duration = 0.65f,
        DamageTickInterval = 0.15f,
        CoreWidth = coreBody,
        GlowWidth = glowWidth,
        CoreColor = new Color(1f, 0.82f, 0.72f, 0.74f),
        GlowColor = new Color(
          Mathf.Max(shell.r, 0.95f),
          Mathf.Min(shell.g, 0.08f),
          Mathf.Min(shell.b, 0.04f),
          Mathf.Max(shell.a, 0.62f)),
        PulseSpeed = 8f,
        PulseAmount = 0.05f
      };
    }

    public static LaserBeamSettings FromProfile(
      Color beamTint,
      float maxRange,
      float beamHalfWidth,
      float duration,
      float tickInterval)
    {
      var settings = DefaultMob(beamTint, maxRange, beamHalfWidth);
      if (duration > 0f)
        settings.Duration = duration;
      if (tickInterval > 0f)
        settings.DamageTickInterval = tickInterval;
      return settings;
    }

    /// <summary>反弹激光：沿用原束?脉冲，核心与外壳改为金色?/summary>
    public static LaserBeamSettings ToReflectedGolden(in LaserBeamSettings source)
    {
      var settings = source;
      settings.CoreColor = new Color(1f, 0.9f, 0.38f, 1f);
      settings.GlowColor = new Color(1f, 0.74f, 0.06f, 0.78f);
      return settings;
    }
  }
}
