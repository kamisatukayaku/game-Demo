using Game.Modes.Roguelike.Build.Runtime;

using Game.Modes.Roguelike.Combat;

using Game.Modes.Roguelike.Progression;

using UnityEngine;



namespace Game.Modes.Roguelike.UI

{

  /// <summary>Run summary snapshot for death and victory screens.</summary>

  public static class RunDeathSummary

  {

    static float s_runStartUnscaledTime;



    public static int WaveReached { get; private set; }

    public static int PlayerLevel { get; private set; }

    public static int TotalKills { get; private set; }

    public static int TotalXp { get; private set; }

    public static string WeaponTheme { get; private set; } = "mage";

    public static string BuildDirection { get; private set; } = ArenaBuildBootstrap.Mage;

    public static float SurviveSeconds { get; private set; }

    public static bool WasVictory { get; private set; }



    public static void MarkRunStart()

    {

      s_runStartUnscaledTime = Time.unscaledTime;

      WasVictory = false;
      RunTimelineRecorder.Reset();

    }



    public static void CaptureAtDeath()

    {

      WasVictory = false;

      CaptureInternal();

    }



    public static void CaptureAtVictory()

    {

      WasVictory = true;

      CaptureInternal();

    }



    static void CaptureInternal()

    {

      var director = WaveDirector.Instance;

      WaveReached = director != null ? director.CurrentWave : 0;

      PlayerLevel = ExperienceSystem.Exists ? ExperienceSystem.Level : 1;

      TotalKills = CombatEventSubscriber.Instance != null ? CombatEventSubscriber.Instance.TotalKills : 0;

      TotalXp = ExperienceSystem.Exists ? ExperienceSystem.TotalXp : 0;

      WeaponTheme = RunBuildState.WeaponTheme ?? "mage";

      BuildDirection = ArenaBuildBootstrap.SelectedBuildId;

      SurviveSeconds = Mathf.Max(0f, Time.unscaledTime - s_runStartUnscaledTime);

    }



    public static string FormatThemeLabel(string theme) => theme switch

    {

      "ranged" => "射手",

      "mage" => "法师",

      "warrior" => "接触",

      "melee" => "近战",

      _ => theme

    };



    public static string FormatSurviveTime(float seconds)

    {

      var total = Mathf.Max(0, Mathf.FloorToInt(seconds));

      var minutes = total / 60;

      var secs = total % 60;

      return minutes > 0 ? $"{minutes}:{secs:00}" : $"{secs}s";

    }

  }

}


