using System;
using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>Per-spawn arena context: wave number and balance overrides for a boss instance.</summary>
  [DisallowMultipleComponent]
  public sealed class BossWaveContext : MonoBehaviour
  {
    static readonly string[] SummonSkillIds =
    {
      "summon_minions",
      "summon_parts",
      "rotating_shield"
    };

    /// <summary>Optional live minion counter supplied by mode runtime (Boss Rush).</summary>
    public static Func<int> ExternalLivingMinionCounter { get; set; }

    public int WaveNumber { get; private set; }
    public string BossId { get; private set; }
    public float EncounterCooldownMult { get; private set; } = 1f;
    public bool AllowMinions { get; private set; } = true;
    public int MaxMinions { get; private set; } = 99;

    BossBalanceDatabase.WaveOverrideDef _override;

    public static BossWaveContext Ensure(GameObject boss, string bossId, int waveNumber)
    {
      if (boss == null)
        return null;

      var ctx = boss.GetComponent<BossWaveContext>() ?? boss.AddComponent<BossWaveContext>();
      ctx.Configure(bossId, waveNumber);
      return ctx;
    }

    public void Configure(string bossId, int waveNumber)
    {
      BossId = bossId;
      WaveNumber = waveNumber;
      _override = BossBalanceDatabase.GetWaveOverride(bossId, waveNumber);
    }

    public void ConfigureEncounterTuning(float cooldownMult, bool allowMinions, int maxMinions)
    {
      EncounterCooldownMult = cooldownMult > 0f ? cooldownMult : 1f;
      AllowMinions = allowMinions;
      MaxMinions = maxMinions > 0 ? maxMinions : 8;
    }

    public bool IsSkillEnabled(string skillId)
    {
      if (IsSummonSkillBlocked(skillId))
        return false;

      return !BossBalanceDatabase.IsSkillDisabled(BossId, WaveNumber, skillId);
    }

    public bool IsSummonSkillBlocked(string skillId)
    {
      if (!IsSummonSkill(skillId))
        return false;

      if (!AllowMinions)
        return true;

      var live = ExternalLivingMinionCounter?.Invoke() ?? 0;
      return live >= MaxMinions;
    }

    public static bool IsSummonSkill(string skillId)
    {
      if (string.IsNullOrEmpty(skillId))
        return false;

      foreach (var id in SummonSkillIds)
      {
        if (skillId == id)
          return true;
      }

      return skillId.Contains("summon", StringComparison.Ordinal);
    }

    public float HpMultBonus => _override?.hp_mult_bonus ?? 1f;
    public float DamageMultBonus => _override?.damage_mult_bonus ?? 1f;

    public float IntroGraceOverride =>
      _override != null && _override.intro_grace_sec > 0.01f ? _override.intro_grace_sec : 0f;
  }
}
