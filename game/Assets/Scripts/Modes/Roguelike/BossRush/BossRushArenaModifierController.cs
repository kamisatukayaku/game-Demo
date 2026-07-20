using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Gameplay.Player;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  /// <summary>Applies encounter arena_modifier gameplay effects without WaveDirector.</summary>
  public static class BossRushArenaModifierController
  {
    static string s_activeId = "standard";

    public static string ActiveModifierId => s_activeId;

    public static void Apply(string modifierId)
    {
      s_activeId = string.IsNullOrEmpty(modifierId) ? "standard" : modifierId;
      var entry = WaveModifierDatabase.Get(s_activeId);
      if (entry == null && s_activeId != "standard")
      {
        Debug.LogWarning($"[BossRush] Unknown arena_modifier '{s_activeId}'; falling back to standard.");
        s_activeId = "standard";
        entry = WaveModifierDatabase.Get("standard");
      }

      PlayerDashController.SetDashBlocked(s_activeId == "no_dash_wave");
      CircleArenaController.SetEdgeHazardMult(s_activeId == "night" ? 1.12f : 1f);
      WaveModifierRuntime.SetBossRushPresentation(s_activeId == "night", s_activeId == "frenzy");
    }

    public static void Clear()
    {
      s_activeId = "standard";
      PlayerDashController.SetDashBlocked(false);
      CircleArenaController.SetEdgeHazardMult(1f);
      WaveModifierRuntime.SetBossRushPresentation(false, false);
    }
  }
}
