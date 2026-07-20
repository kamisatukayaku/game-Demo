using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>Data-driven boss balance for Ring Arena. JSON: roguelike/enemies/boss_balance</summary>
  public static class BossBalanceDatabase
  {
    [Serializable]
    public class DefaultsDef
    {
      public float contact_hit_interval_sec = 0.85f;
      public float player_laser_tick_interval_sec = 0.30f;
      public float global_recovery_light_sec = 0.35f;
      public float global_recovery_pressure_sec = 0.55f;
      public int max_consecutive_pressure = 2;
      public float intro_grace_wild_sec = 2.6f;
      public float intro_grace_mini_sec = 1.4f;
      public float intro_grace_final_sec = 2.8f;
    }

    [Serializable]
    public class AoeResistanceDef
    {
      public float instant_explosion = 0.72f;
      public float detached_explosion = 0.72f;
      public float detached_pulse = 0.78f;
      public float pulse = 0.78f;
      public float detached_trail = 0.82f;
      public float trail = 0.82f;
      public float arena_pulse = 0.58f;
      public float aoe = 0.70f;
      public float detached_nuclear = 0.65f;
      public float chain_falloff = 0.55f;
      public float default_mult = 1f;
    }

    [Serializable]
    public class QuadrantBlockDef
    {
      public float warning_sec = 1.2f;
      public float duration_sec = 28f;
    }

    [Serializable]
    public class WaveOverrideDef
    {
      public float hp_mult_bonus = 1f;
      public float damage_mult_bonus = 1f;
      public float intro_grace_sec;
      public string[] disabled_skills;
    }

    [Serializable]
    public class BossEntryDef
    {
      public Dictionary<string, WaveOverrideDef> wave_overrides;
    }

    [Serializable]
    class RootDef
    {
      public int schema_version;
      public DefaultsDef defaults;
      public AoeResistanceDef aoe_resistance;
      public QuadrantBlockDef quadrant_block;
      public Dictionary<string, WaveOverrideDef> bosses_flat;
    }

    [Serializable]
    class BossWaveRootEntry
    {
      public WaveOverrideDef[] entries;
    }

    static DefaultsDef s_defaults = new();
    static AoeResistanceDef s_aoe = new();
    static QuadrantBlockDef s_quadrant = new();
    static readonly Dictionary<string, Dictionary<int, WaveOverrideDef>> s_overrides = new();
    static bool s_loaded;

    public static DefaultsDef Defaults
    {
      get { EnsureLoaded(); return s_defaults; }
    }

    public static QuadrantBlockDef QuadrantBlock
    {
      get { EnsureLoaded(); return s_quadrant; }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      if (!JsonDataLoader.TryParse("roguelike/enemies/boss_balance", ParseJson)
          && !JsonDataLoader.TryParse("enemies/boss_balance", ParseJson))
      {
        Debug.LogWarning("[BossBalanceDatabase] boss_balance.json not found — using code defaults.");
      }
    }

    static void ParseJson(string json)
    {
      var root = JsonUtility.FromJson<RootDef>(json);
      if (root?.defaults != null)
        s_defaults = root.defaults;
      if (root?.aoe_resistance != null)
        s_aoe = root.aoe_resistance;
      if (root?.quadrant_block != null)
        s_quadrant = root.quadrant_block;

      s_overrides.Clear();
      ParseBossEntries(json);
    }

    static void ParseBossEntries(string json)
    {
      var bossesIdx = json.IndexOf("\"bosses\"", StringComparison.Ordinal);
      if (bossesIdx < 0)
        return;

      var slice = json.Substring(bossesIdx);
      foreach (var bossId in new[]
      {
        "wild_boss_hex_king", "mini_boss_hex_sentinel", "wild_boss_star_hive",
        "mini_boss_star_chorus", "wild_boss_pent_colossus", "mini_boss_square_jailer",
        "final_boss_prism_nexus"
      })
      {
        ParseBossWaveOverrides(slice, bossId);
      }
    }

    static void ParseBossWaveOverrides(string bossesJson, string bossId)
    {
      var key = $"\"{bossId}\"";
      var idx = bossesJson.IndexOf(key, StringComparison.Ordinal);
      if (idx < 0)
        return;

      var waveKey = "\"wave_overrides\"";
      var waveIdx = bossesJson.IndexOf(waveKey, idx, StringComparison.Ordinal);
      if (waveIdx < 0 || waveIdx > idx + 800)
        return;

      var map = new Dictionary<int, WaveOverrideDef>();
      for (var wave = 1; wave <= 20; wave++)
      {
        var waveToken = $"\"{wave}\"";
        var wIdx = bossesJson.IndexOf(waveToken, waveIdx, StringComparison.Ordinal);
        if (wIdx < 0 || wIdx > waveIdx + 1200)
          continue;

        var def = ParseWaveOverrideAt(bossesJson, wIdx);
        if (def != null)
          map[wave] = def;
      }

      if (map.Count > 0)
        s_overrides[bossId] = map;
    }

    static WaveOverrideDef ParseWaveOverrideAt(string json, int waveKeyIdx)
    {
      var brace = json.IndexOf('{', waveKeyIdx);
      if (brace < 0)
        return null;

      var depth = 0;
      for (var i = brace; i < json.Length; i++)
      {
        if (json[i] == '{') depth++;
        else if (json[i] == '}')
        {
          depth--;
          if (depth == 0)
          {
            var chunk = json.Substring(brace, i - brace + 1);
            return JsonUtility.FromJson<WaveOverrideDef>(chunk);
          }
        }
      }

      return null;
    }

    public static WaveOverrideDef GetWaveOverride(string bossId, int wave)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(bossId))
        return null;

      if (!s_overrides.TryGetValue(bossId, out var map))
        return null;

      return map.TryGetValue(wave, out var def) ? def : null;
    }

    public static float GetAoeMultiplier(string sourceId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(sourceId))
        return 1f;

      var lower = sourceId.ToLowerInvariant();

      if (lower.Contains("nuclear"))
        return s_aoe.detached_nuclear;
      if (lower.Contains("detached_explosion") || (lower.Contains("explosion") && lower.Contains("detached")))
        return s_aoe.detached_explosion;
      if (lower.Contains("explosion"))
        return s_aoe.instant_explosion;
      if (lower.Contains("detached_pulse"))
        return s_aoe.detached_pulse;
      if (lower.Contains("pulse"))
        return s_aoe.pulse;
      if (lower.Contains("detached_trail"))
        return s_aoe.detached_trail;
      if (lower.Contains("trail"))
        return s_aoe.trail;
      if (lower.Contains("arena_pulse"))
        return s_aoe.arena_pulse;
      if (lower.Contains("chain"))
        return s_aoe.chain_falloff;
      if (lower.Contains("aoe"))
        return s_aoe.aoe;

      return s_aoe.default_mult;
    }

    public static bool IsSkillDisabled(string bossId, int wave, string skillId)
    {
      var ov = GetWaveOverride(bossId, wave);
      if (ov?.disabled_skills == null || string.IsNullOrEmpty(skillId))
        return false;

      foreach (var id in ov.disabled_skills)
      {
        if (id == skillId)
          return true;
      }

      return false;
    }
  }
}
