#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using System;

namespace Game.Shared.Diagnostics
{
  /// <summary>Optional hooks for Play Mode combat-chain validation (no Roguelike dependency).</summary>
  public static class CombatValidationHooks
  {
    public static Action OnEnemyRegistered;
    public static Action OnTargetAcquired;
    public static Action OnAttackAttempt;
    public static Action<string> OnAttackBlocked;
    public static Action OnPlayerProjectileSpawned;

    public static void Reset()
    {
      OnEnemyRegistered = null;
      OnTargetAcquired = null;
      OnAttackAttempt = null;
      OnAttackBlocked = null;
      OnPlayerProjectileSpawned = null;
    }
  }
}
#endif
