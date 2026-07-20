using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.Player
{
  public static class DashCombatConfig
  {
    static BaseDef s_base;
    static bool s_loaded;

    public static BaseDef Base
    {
      get
      {
        EnsureLoaded();
        return s_base;
      }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      s_base = new BaseDef();
      JsonDataLoader.TryParse("combat/dash_combat", json =>
      {
        try
        {
          var root = JsonUtility.FromJson<Root>(json);
          if (root?.dash_combat?.@base != null)
            s_base = root.dash_combat.@base;
        }
        catch (System.Exception e)
        {
          Debug.LogError($"[DashCombatConfig] Parse failed: {e.Message}");
        }
      });
    }

    [System.Serializable]
    sealed class Root
    {
      public DashCombatDef dash_combat;
    }

    [System.Serializable]
    sealed class DashCombatDef
    {
      public BaseDef @base;
    }

    [System.Serializable]
    public sealed class BaseDef
    {
      public float distance = 5f;
      public float duration = 0.15f;
      public float cooldown = 2f;
      public float minimum_cooldown = 0.35f;
      public float invincible_time = 0.15f;
      public float strike_damage = 18f;
      public float strike_width = 0.72f;
      public float knockback;
      public float cooldown_refund_cap_ratio = 0.5f;
      public float aftershock_damage_ratio = 0.65f;
      public float aftershock_radius;
      public float aftershock_knockback = 5f;
      public float input_grace_seconds = 0.15f;
      public float pursuit_hit_threshold = 3f;
      public float pursuit_window_seconds = 2.5f;
      public float pursuit_distance_mult = 0.85f;
      public float boss_knockback_scale = 0.12f;
      public float elite_knockback_scale = 0.55f;
    }
  }
}
