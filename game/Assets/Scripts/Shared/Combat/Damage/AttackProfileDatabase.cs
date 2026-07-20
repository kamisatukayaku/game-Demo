using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Shared.Combat.Damage
{
  /// <summary>读取 data/attacks.json 攻击 profile（伤害类型、基础伤害等）?/summary>
  public static class AttackProfileDatabase
  {
    public class AttackProfile
    {
      public string id;
      public float base_damage;
      public string damage_type;
      public string damage_source;
      public string delivery;
      public string targeting;
      public float windup;
      public float cooldown;
      public float range;
      public float projectile_speed;
      public float projectile_scale;

      // Extended delivery params
      public float projectile_turn_rate_deg;
      public string projectile_homing;  // "none" | "weak" | "lock_loss" | "strong"
      public float lock_loss_angle_deg;
      public float hit_radius;

      // AOE
      public float aoe_radius;
      public float aoe_duration;
      public float aoe_cone_angle_deg;
      public bool aoe_persistent;
      public float aoe_damage_mult;

      // Chain
      public int chain_max_targets;
      public float chain_jump_range;
      public float chain_damage_falloff;

      // Beam
      public bool beam_pierce;
      public float beam_half_width;
      public float beam_duration;
      public float beam_tick_interval;

      // Charge dash
      public bool charge_dash;
      public float dash_speed_mult;
      public float dash_distance;

      // Barrage
      public int projectile_count;
      public float spread_deg;
      public int max_targets;

      // Aura
      public float aura_tick_interval;
      public string aura_apply_to;
      public float aura_heal_per_tick;
      public float aura_ei_per_tick;

      // On hit buffs
      public string[] on_hit_buffs;
    }

    static readonly Dictionary<string, AttackProfile> s_profiles = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_profiles.Clear();
      RegisterCoreFallbackProfiles();
      try
      {
        JsonDataLoader.TryParse("combat/attacks", Parse);
        JsonDataLoader.TryParse("attacks", Parse);
      }
      catch (Exception exception)
      {
        Debug.LogError($"[AttackProfileDatabase] Invalid attacks JSON; using core fallback profiles. {exception.Message}");
      }
    }

    public static void MergeTestProfiles(string json)
    {
      EnsureLoaded();
      Parse(json);
    }

    public static void ReloadProduction()
    {
      s_loaded = false;
      s_profiles.Clear();
      EnsureLoaded();
    }

    public static AttackProfile Get(string profileId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(profileId))
        return null;

      s_profiles.TryGetValue(profileId, out var profile);
      return profile;
    }

    public static string GetDamageType(string profileId, string fallback = "physical") =>
      Get(profileId)?.damage_type ?? fallback;

    public static string GetDamageSource(string profileId, string fallback = "environment") =>
      Get(profileId)?.damage_source ?? fallback;

    static void Parse(string json)
    {
      var root = JsonUtility.FromJson<Root>(json);
      if (root?.definitions == null)
        return;

      foreach (var entry in root.definitions)
      {
        if (entry == null || string.IsNullOrEmpty(entry.id))
          continue;

        var profile = new AttackProfile
        {
          id = entry.id,
          base_damage = entry.base_damage,
          damage_type = string.IsNullOrEmpty(entry.damage_type) ? "physical" : entry.damage_type,
          damage_source = string.IsNullOrEmpty(entry.damage_source) ? "environment" : entry.damage_source,
          delivery = entry.delivery,
          targeting = entry.targeting,
          windup = entry.windup,
          cooldown = entry.cooldown,
          range = entry.range,
          on_hit_buffs = entry.on_hit_buffs ?? new string[0]
        };

        if (entry.delivery_params != null)
        {
          var dp = entry.delivery_params;
          profile.projectile_speed = dp.projectile_speed;
          profile.projectile_scale = dp.projectile_scale;
          profile.projectile_turn_rate_deg = dp.projectile_turn_rate_deg;
          profile.projectile_homing = dp.projectile_homing;
          profile.lock_loss_angle_deg = dp.lock_loss_angle_deg;
          profile.hit_radius = dp.hit_radius;
          profile.aoe_radius = dp.aoe_radius;
          profile.aoe_duration = dp.aoe_duration;
          profile.aoe_cone_angle_deg = dp.aoe_cone_angle_deg;
          profile.aoe_persistent = dp.aoe_persistent;
          profile.aoe_damage_mult = dp.aoe_damage_mult;
          profile.chain_max_targets = dp.chain_max_targets;
          profile.chain_jump_range = dp.chain_jump_range;
          profile.chain_damage_falloff = dp.chain_damage_falloff;
          profile.beam_pierce = dp.beam_pierce;
          profile.beam_half_width = dp.beam_half_width;
          profile.beam_duration = dp.beam_duration;
          profile.beam_tick_interval = dp.beam_tick_interval;
          profile.charge_dash = dp.charge_dash;
          profile.dash_speed_mult = dp.dash_speed_mult;
          profile.dash_distance = dp.dash_distance;
          profile.projectile_count = dp.projectile_count;
          profile.spread_deg = dp.spread_deg;
          profile.max_targets = dp.max_targets;
          profile.aura_tick_interval = dp.aura_tick_interval;
          profile.aura_apply_to = dp.aura_apply_to;
          profile.aura_heal_per_tick = dp.aura_heal_per_tick;
          profile.aura_ei_per_tick = dp.aura_ei_per_tick;
        }

        s_profiles[entry.id] = profile;
      }
    }

    static void RegisterCoreFallbackProfiles()
    {
      RegisterFallback(new AttackProfile
      {
        id = "weapon_theme_melee",
        base_damage = 12f,
        damage_type = "physical",
        damage_source = "weapon",
        delivery = "melee",
        targeting = "nearest_in_range",
        cooldown = 0.45f,
        range = 1.6f,
        on_hit_buffs = Array.Empty<string>()
      });

      RegisterFallback(new AttackProfile
      {
        id = "weapon_theme_ranged",
        base_damage = 7f,
        damage_type = "kinetic",
        damage_source = "weapon",
        delivery = "projectile",
        targeting = "nearest_in_range",
        cooldown = 0.65f,
        range = 16f,
        projectile_speed = 14f,
        projectile_scale = 0.22f,
        projectile_homing = "none",
        on_hit_buffs = Array.Empty<string>()
      });

      RegisterFallback(new AttackProfile
      {
        id = "weapon_theme_mage",
        base_damage = 9f,
        damage_type = "energy",
        damage_source = "skill",
        delivery = "projectile",
        targeting = "nearest_in_range",
        cooldown = 0.58f,
        range = 14f,
        projectile_speed = 16f,
        projectile_scale = 0.2f,
        projectile_turn_rate_deg = 90f,
        projectile_homing = "weak",
        on_hit_buffs = Array.Empty<string>()
      });

      RegisterFallback(new AttackProfile
      {
        id = "skill_mage_arcane_bolt",
        base_damage = 11f,
        damage_type = "energy",
        damage_source = "skill",
        delivery = "projectile",
        targeting = "directional",
        cooldown = 1.2f,
        range = 14f,
        projectile_speed = 18f,
        projectile_scale = 0.22f,
        projectile_turn_rate_deg = 90f,
        projectile_homing = "weak",
        on_hit_buffs = Array.Empty<string>()
      });
    }

    static void RegisterFallback(AttackProfile profile)
    {
      s_profiles[profile.id] = profile;
    }

    [Serializable]
    class Root
    {
      public AttackJson[] definitions;
    }

    [Serializable]
    class AttackJson
    {
      public string id;
      public float base_damage;
      public string damage_type;
      public string damage_source;
      public string delivery;
      public string targeting;
      public float windup;
      public float cooldown;
      public float range;
      public string[] on_hit_buffs;
      public DeliveryParams delivery_params;
    }

    [Serializable]
    class DeliveryParams
    {
      public float projectile_speed;
      public float projectile_scale;
      public float projectile_turn_rate_deg;
      public string projectile_homing;
      public float lock_loss_angle_deg;
      public float hit_radius;
      public float aoe_radius;
      public float aoe_duration;
      public float aoe_cone_angle_deg;
      public bool aoe_persistent;
      public float aoe_damage_mult;
      public int chain_max_targets;
      public float chain_jump_range;
      public float chain_damage_falloff;
      public bool beam_pierce;
      public float beam_half_width;
      public float beam_duration;
      public float beam_tick_interval;
      public bool charge_dash;
      public float dash_speed_mult;
      public float dash_distance;
      public int projectile_count;
      public float spread_deg;
      public int max_targets;
      public float aura_tick_interval;
      public string aura_apply_to;
      public float aura_heal_per_tick;
      public float aura_ei_per_tick;
    }
  }
}
