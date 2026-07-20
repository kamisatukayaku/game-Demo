using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Stats;
namespace Game.Shared.Runtime
{
  /// <summary>从开局 UI 写入、MainScene 读取的单局配置?/summary>
  public static class GameSessionConfig
  {
    /// <summary>游戏模式：arena=环形竞技场生存，boss_rush=首领连战，explore=世界探索（独立模式）。</summary>
    public enum GameMode { Arena, BossRush, Explore }

    public static bool IsRingArena => SelectedMode == GameMode.Arena;
    public static bool IsBossRush => SelectedMode == GameMode.BossRush;
    public static bool UsesArenaLayout => IsRingArena || IsBossRush;

    public static bool RunConfigured { get; private set; }
    public static GameMode SelectedMode { get; private set; } = GameMode.Arena;
    public static string SelectedWeaponTheme { get; private set; } = "unified";
    public static string SelectedBuildDirectionId { get; private set; } = "unified";
    public static string SelectedDifficultyId { get; private set; } = "normal";
    public static IReadOnlyList<string> SelectedTalentIds => s_talentIds;

    /// <summary>World 模式开局选择的初始饰品ID（饰品3选1）。</summary>
    public static string WorldStartingAccessory { get; private set; }

    static readonly List<string> s_talentIds = new();

    public static void Configure(
      string weaponTheme,
      IEnumerable<string> talentIds,
      string difficultyId = "normal",
      GameMode mode = GameMode.Arena,
      string buildDirectionId = null)
    {
      SelectedMode = mode;
      SelectedBuildDirectionId = string.IsNullOrEmpty(buildDirectionId) ? "unified" : buildDirectionId;
      SelectedWeaponTheme = string.IsNullOrEmpty(weaponTheme)
        ? SelectedBuildDirectionId
        : weaponTheme;
      SelectedDifficultyId = string.IsNullOrEmpty(difficultyId) ? "normal" : difficultyId;
      s_talentIds.Clear();
      if (talentIds != null)
      {
        foreach (var id in talentIds)
        {
          if (!string.IsNullOrEmpty(id) && !s_talentIds.Contains(id))
            s_talentIds.Add(id);
        }
      }

      RunConfigured = true;

      var configurator = RunSessionConfiguratorLocator.Configurator;
      if (configurator != null)
        configurator.ConfigureRun(SelectedWeaponTheme, s_talentIds);
      else
        Debug.LogWarning("[GameSession] No IRunSessionConfigurator registered; run build not initialized.");

      Debug.Log($"[GameSession] mode={SelectedMode} build={SelectedBuildDirectionId} theme={SelectedWeaponTheme} difficulty={SelectedDifficultyId} talents={s_talentIds.Count}");
    }

    /// <summary>设置 World 模式开局选择的初始饰品。</summary>
    public static void SetWorldStartingAccessory(string itemId)
    {
      WorldStartingAccessory = itemId;
    }

    /// <summary>消费并清除 World 模式开局饰品（防止重复发放）。</summary>
    public static string ConsumeWorldStartingAccessory()
    {
      var id = WorldStartingAccessory;
      WorldStartingAccessory = null;
      return id;
    }

    /// <summary>Leave an in-progress run without starting a new one (e.g. return to main menu).</summary>
    public static void AbandonCurrentRun()
    {
      RunConfigured = false;
    }

    public static void ResetForEditor()
    {
      RunConfigured = false;
      SelectedMode = GameMode.Arena;
      SelectedWeaponTheme = "unified";
      SelectedBuildDirectionId = "unified";
      SelectedDifficultyId = "normal";
      s_talentIds.Clear();
      WorldStartingAccessory = null;
    }
  }
}