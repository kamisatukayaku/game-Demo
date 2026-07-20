using UnityEngine;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.BossRush;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>C1: Picks arena layout at run start and configures CircleArenaController.</summary>
  public static class ArenaLayoutController
  {
    static ArenaLayoutDatabase.LayoutEntry s_active;

    public static string ActiveLayoutId => s_active?.id ?? "default";
    public static string ActiveDisplayName => s_active?.display_name ?? "标准环";
    public static ArenaLayoutDatabase.LayoutEntry Active => s_active;

    public static void BeginRun()
    {
      ArenaLayoutDatabase.EnsureLoaded();
      if (GameSessionConfig.IsBossRush)
      {
        BossRushDatabase.EnsureLoaded();
        var layoutId = BossRushDatabase.Settings.arena_layout_id;
        s_active = !string.IsNullOrEmpty(layoutId) ? ArenaLayoutDatabase.Get(layoutId) : null;
        s_active ??= ArenaLayoutDatabase.PickLegacyCompactLayout();
      }
      else
      {
        s_active = ArenaLayoutDatabase.PickForRun();
      }

      if (s_active == null)
        return;

      CircleArenaController.ApplyLayout(s_active);
    }

    public static void Reset()
    {
      s_active = null;
    }
  }
}
