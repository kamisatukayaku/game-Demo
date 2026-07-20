using UnityEngine;

namespace Game.Shared.Enemy.Death
{
  /// <summary>执行 enemies.json ?on_death 效果?/summary>
  public static class EnemyDeathEffects
  {
    public static void Execute(string[] effects, Vector3 position, GameObject source)
    {
      if (effects == null || effects.Length == 0)
        return;

      foreach (var effect in effects)
      {
        if (string.IsNullOrEmpty(effect))
          continue;

        if (effect != "leave_pollution_puddle")
          Debug.LogWarning($"[EnemyDeathEffects] Unknown on_death effect: {effect}");
      }
    }
  }
}