using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>主音量设置（PlayerPrefs 持久化）?/summary>
  public static class GameAudioSettings
  {
    const string PrefKey = "game_master_volume";

    static float s_volume = 1f;
    static bool s_applied;

    public static float MasterVolume
    {
      get
      {
        EnsureLoaded();
        return s_volume;
      }
      set
      {
        s_volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKey, s_volume);
        PlayerPrefs.Save();
        Apply();
      }
    }

    public static void EnsureLoaded()
    {
      if (s_applied)
        return;

      s_applied = true;
      s_volume = PlayerPrefs.GetFloat(PrefKey, 1f);
      Apply();
    }

    static void Apply()
    {
      AudioListener.volume = s_volume;
    }
  }
}