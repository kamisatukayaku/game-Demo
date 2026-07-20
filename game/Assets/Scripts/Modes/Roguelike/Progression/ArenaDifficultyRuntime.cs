using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Combat;
using Game.Shared.Runtime;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Reads run_difficulty.json and exposes arena tuning for the active session.</summary>
  public static class ArenaDifficultyRuntime
  {
    const string DefaultId = "normal";

    public static string DifficultyId { get; private set; } = DefaultId;
    public static RunDifficultyDatabase.DifficultyDef Active { get; private set; }

    public static int TotalWaves => Active != null && Active.wave_survive_count > 0
      ? Active.wave_survive_count
      : 15;

    public static float PlayerStatMult => Active?.player_stat_mult > 0f ? Active.player_stat_mult : 1f;
    public static float XpMult => Active?.xp_mult > 0f ? Active.xp_mult : 1f;
    public static float SpawnCountMult => Active?.spawn_count_mult > 0f ? Active.spawn_count_mult : 1f;
    public static float RewardMult => Active?.reward_mult > 0f ? Active.reward_mult : 1f;
    public static float EnemyHpMult => Active?.enemy_hp_mult > 0f ? Active.enemy_hp_mult : 1f;
    public static float EnemyDamageMult => Active?.enemy_damage_mult > 0f ? Active.enemy_damage_mult : 1f;
    public static float BuildPhaseSeconds => Active?.build_phase_seconds > 0f ? Active.build_phase_seconds : 2f;
    public static float BossMinionSpawnMult => Active?.boss_minion_spawn_mult > 0f ? Active.boss_minion_spawn_mult : 0.35f;

    public static bool IsHard => DifficultyId == "hard";

    public static void BindSession()
    {
      DifficultyId = string.IsNullOrEmpty(GameSessionConfig.SelectedDifficultyId)
        ? DefaultId
        : GameSessionConfig.SelectedDifficultyId;

      RunDifficultyDatabase.EnsureLoaded();
      Active = RunDifficultyDatabase.Get(DifficultyId)
               ?? RunDifficultyDatabase.Get(DefaultId)
               ?? new RunDifficultyDatabase.DifficultyDef { id = DefaultId, wave_survive_count = 15, player_stat_mult = 1f };
    }

    public static void ApplyPlayerModifiers()
    {
      var mult = PlayerStatMult;
      if (Mathf.Approximately(mult, 1f))
        return;

      RunBuildState.AddStat(StatKeys.MaxHpMult, mult - 1f);
      RunBuildState.AddStat(StatKeys.WeaponDamageMult, mult - 1f);
      RunBuildState.AddStat(StatKeys.MoveSpeedMult, (mult - 1f) * 0.5f);
    }

    public static int ScaleEnemyCount(int baseCount, bool isBossWave)
    {
      var scaled = Mathf.RoundToInt(baseCount * SpawnCountMult);
      if (isBossWave)
        scaled = Mathf.Max(4, Mathf.RoundToInt(scaled * BossMinionSpawnMult));
      return Mathf.Max(1, scaled);
    }

    public static int ScaleXp(int baseXp) =>
      Mathf.Max(1, Mathf.RoundToInt(baseXp * XpMult * RewardMult * WaveModifierRuntime.GetXpMultiplier()));

    public static float ScaleRewardShards(float baseShards) =>
      baseShards * RewardMult;
  }
}
