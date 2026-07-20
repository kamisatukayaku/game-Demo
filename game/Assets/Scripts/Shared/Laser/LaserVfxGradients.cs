using UnityEngine;

namespace Game.Shared.Laser
{
  /// <summary>共享 Gradient 缓存，避免运行时 GC?/summary>
  static class LaserVfxGradients
  {
    static Gradient s_whiteFade;
    static Gradient s_sparkFade;
    static Gradient s_rainFade;
    static Gradient s_hitSplash;

    public static Gradient WhiteFade
    {
      get
      {
        if (s_whiteFade != null)
          return s_whiteFade;

        s_whiteFade = new Gradient();
        s_whiteFade.SetKeys(
          new[]
          {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
          },
          new[]
          {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0.8f, 0.45f),
            new GradientAlphaKey(0f, 1f)
          });
        return s_whiteFade;
      }
    }

    public static Gradient SparkFade
    {
      get
      {
        if (s_sparkFade != null)
          return s_sparkFade;

        s_sparkFade = new Gradient();
        s_sparkFade.SetKeys(
          new[]
          {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
          },
          new[]
          {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0.75f, 0.4f),
            new GradientAlphaKey(0f, 1f)
          });
        return s_sparkFade;
      }
    }

    public static Gradient RainFade
    {
      get
      {
        if (s_rainFade != null)
          return s_rainFade;

        s_rainFade = new Gradient();
        s_rainFade.SetKeys(
          new[]
          {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 0.55f),
            new GradientColorKey(new Color(1f, 1f, 0.98f), 1f)
          },
          new[]
          {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0.95f, 0.4f),
            new GradientAlphaKey(0f, 1f)
          });
        return s_rainFade;
      }
    }

    public static Gradient HitSplash
    {
      get
      {
        if (s_hitSplash != null)
          return s_hitSplash;

        s_hitSplash = new Gradient();
        s_hitSplash.SetKeys(
          new[]
          {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 0.45f),
            new GradientColorKey(new Color(1f, 0.98f, 0.88f), 1f)
          },
          new[]
          {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0.85f, 0.35f),
            new GradientAlphaKey(0f, 1f)
          });
        return s_hitSplash;
      }
    }
  }
}