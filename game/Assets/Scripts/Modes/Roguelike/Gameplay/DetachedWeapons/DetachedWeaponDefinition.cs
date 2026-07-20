using System;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  [Serializable]
  public sealed class DetachedWeaponDefinition
  {
    public string id;
    public string display_name;
    public string description;
    public string visual_id;
    public string[] attack_modes;
    public float base_damage = 12f;
    public float orbit_radius = 2.2f;
    public float orbit_speed = 120f;
    public float hit_cooldown = 0.35f;
    public float wander_radius = 5f;
    public float wander_speed = 3.5f;
    public float attack_range = 14f;
    public float attack_cooldown = 2.2f;
    public float contact_radius = 0.75f;
    public float projectile_hit_radius = 0.48f;
    public float effect_radius = 1.35f;
    public float secondary_radius = 2.2f;
    public float secondary_delay = 0.16f;
    public int finisher_threshold = 5;
    public float finisher_radius = 5.2f;
    public float pulse_speed = 8f;
    public float pulse_thickness = 0.7f;
    public float pulse_max_radius = 7f;
    public float pulse_zone_duration = 1.8f;
    public float boomerang_speed = 12f;
    public float boomerang_short_distance = 5f;
    public float boomerang_long_distance = 9f;
    public int boomerang_recasts = 2;
    public int boomerang_storm_count = 5;
    public float trail_sample_distance = 0.5f;
    public float trail_short_lifetime = 0.7f;
    public float trail_long_lifetime = 1.3f;
    public float trail_persistent_lifetime = 2.8f;
    public float trail_width = 0.45f;
    public float trail_fork_length = 2.4f;
    public float trail_network_distance = 4.5f;
  }

  [Serializable]
  public sealed class DetachedWeaponAttackModeDefinition
  {
    public string id;
    public string display_name;
    public string description;
  }
}
