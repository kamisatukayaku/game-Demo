#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using Game.Shared.Combat.Health;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>
  /// Full-run validation only: temporarily boosts player HP and ignores damage so auto-player
  /// survival does not block integration coverage. Restored when the test coroutine ends.
  /// </summary>
  public static class RuntimeValidationPlayerSurvival
  {
    public const float BoostMaxHp = 999_999f;

    struct Snapshot
    {
      public bool Active;
      public float MaxHp;
      public float CurrentHp;
      public bool IgnoreDamage;
      public bool NeverDie;
    }

    static Snapshot s_snapshot;
    static GameObject s_player;

    public static bool IsActive => s_snapshot.Active;

    public static void ApplyForFullRun()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      Apply(player);
    }

    public static void Apply(GameObject player)
    {
      if (player == null)
        return;

      var health = player.GetComponent<Health>();
      if (health == null)
        return;

      if (!s_snapshot.Active)
      {
        s_snapshot = new Snapshot
        {
          Active = true,
          MaxHp = health.MaxHp,
          CurrentHp = health.CurrentHp,
          IgnoreDamage = health.SandboxIgnoreDamage,
          NeverDie = health.SandboxNeverDie,
        };
        s_player = player;
      }

      Reapply(health);

      if (player.GetComponent<ValidationPlayerSurvivalGuard>() == null)
        player.AddComponent<ValidationPlayerSurvivalGuard>();
    }

    internal static void Reapply(Health health)
    {
      if (health == null || !s_snapshot.Active)
        return;

      if (health.MaxHp < BoostMaxHp * 0.5f)
        health.Configure(BoostMaxHp);

      health.SandboxIgnoreDamage = true;
      health.SandboxNeverDie = true;

      if (health.CurrentHp < health.MaxHp * 0.99f)
        health.Heal(health.MaxHp - health.CurrentHp);
    }

    public static void Restore()
    {
      if (!s_snapshot.Active)
        return;

      if (s_player != null)
      {
        var guard = s_player.GetComponent<ValidationPlayerSurvivalGuard>();
        if (guard != null)
          Object.Destroy(guard);

        var health = s_player.GetComponent<Health>();
        if (health != null)
        {
          health.SandboxIgnoreDamage = s_snapshot.IgnoreDamage;
          health.SandboxNeverDie = s_snapshot.NeverDie;
          health.Configure(Mathf.Max(1f, s_snapshot.MaxHp));

          var missing = Mathf.Max(0f, s_snapshot.CurrentHp - health.CurrentHp);
          if (missing > 0f)
            health.Heal(missing);
        }
      }

      s_snapshot = default;
      s_player = null;
      RuntimeValidationSettings.ClearPlayerSurvivalBoost();
    }

    [DisallowMultipleComponent]
    sealed class ValidationPlayerSurvivalGuard : MonoBehaviour
    {
      Health _health;

      void Awake() => _health = GetComponent<Health>();

      void LateUpdate()
      {
        if (!IsActive || _health == null)
          return;

        Reapply(_health);
      }
    }
  }
}

#endif
