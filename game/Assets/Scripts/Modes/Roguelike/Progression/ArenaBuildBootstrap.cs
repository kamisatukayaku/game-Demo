using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Runtime;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Applies run bootstrap before the first wave (unified or legacy build).</summary>
  public static class ArenaBuildBootstrap
  {
    public const string Unified = UnifiedBuildBootstrap.BuildId;
    public const string Mage = "mage";
    public const string Shooter = "shooter";
    public const string Contact = "contact";
    // Compatibility key for old saves and historical result records; never selectable.
    public const string Support = "support";

    public static string SelectedBuildId { get; private set; } = Unified;

    public static bool IsUnifiedBuild =>
      SelectedBuildId == Unified
      || string.IsNullOrEmpty(SelectedBuildId)
      || SelectedBuildId == Support;

    public static void BindSession()
    {
      var selected = GameSessionConfig.SelectedBuildDirectionId;
      if (string.IsNullOrEmpty(selected) || selected == Support)
        SelectedBuildId = Unified;
      else if (selected == Unified)
        SelectedBuildId = Unified;
      else
        SelectedBuildId = selected; // legacy save compat
    }

    public static void ApplySelectedBuild()
    {
      BindSession();
      LevelUpChoiceDatabase.EnsureLoaded();

      if (IsUnifiedBuild)
      {
        UnifiedBuildBootstrap.ApplyBaseline();
        return;
      }

      switch (SelectedBuildId)
      {
        case Shooter:
          ApplyStarter(LevelUpChoiceDatabase.RangedStarterId);
          break;
        case Contact:
          ApplyStarter(LevelUpChoiceDatabase.ContactStarterId);
          break;
        default:
          ApplyStarter(LevelUpChoiceDatabase.MageStarterId);
          break;
      }

      ArenaDifficultyRuntime.ApplyPlayerModifiers();
    }

    static void ApplyStarter(string starterId)
    {
      var def = LevelUpChoiceDatabase.FindById(starterId);
      if (def == null)
        return;

      RunBuildState.ApplyChoice(def);
    }

    public static string GetDisplayName(string buildId) => buildId switch
    {
      Unified => "自由构筑",
      Shooter => "射击",
      Contact => "冲刺星环",
      Support => "自由构筑",
      _ => "法术"
    };

    public static string GetTagline(string buildId) => buildId switch
    {
      Unified => "融合成长 / 自由组合",
      Shooter => "弹幕压制 / 远程覆盖",
      Contact => "冲刺突破 / 外置武器",
      Support => "融合成长 / 自由组合",
      _ => "法术织网 / 引力控场"
    };

    public static Color GetIdentityColor(string buildId) => buildId switch
    {
      Unified => new Color(0.55f, 0.88f, 1f, 1f),
      Shooter => new Color(1f, 0.55f, 0.12f, 1f),
      Contact => new Color(0.95f, 0.97f, 1f, 1f),
      Support => new Color(0.55f, 0.88f, 1f, 1f),
      _ => new Color(0.35f, 0.72f, 1f, 1f)
    };

    public static Color GetKillFeedbackColor() => GetIdentityColor(SelectedBuildId);

    public static string GetThemeForBuild(string buildId) => buildId switch
    {
      Unified => UnifiedBuildBootstrap.WeaponTheme,
      Shooter => "ranged",
      Contact => "warrior",
      Support => UnifiedBuildBootstrap.WeaponTheme,
      _ => "mage"
    };

    /// <summary>Editor / deterministic tests: apply starter without reading GameSessionConfig.</summary>
    public static void ConfigureForSimulation(string buildId)
    {
      SelectedBuildId = string.IsNullOrEmpty(buildId) ? Unified : buildId;
      LevelUpChoiceDatabase.EnsureLoaded();
      RunBuildState.Reset(GetThemeForBuild(SelectedBuildId));

      if (IsUnifiedBuild)
      {
        UnifiedBuildBootstrap.ApplyBaseline();
        return;
      }

      switch (SelectedBuildId)
      {
        case Shooter:
          ApplyStarter(LevelUpChoiceDatabase.RangedStarterId);
          break;
        case Contact:
          ApplyStarter(LevelUpChoiceDatabase.ContactStarterId);
          break;
        default:
          ApplyStarter(LevelUpChoiceDatabase.MageStarterId);
          break;
      }
    }
  }
}
