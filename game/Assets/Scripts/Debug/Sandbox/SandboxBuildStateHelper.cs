using System.Collections.Generic;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;

namespace Game.DevTools.Sandbox
{
  /// <summary>沙盒专用：在不改 Build 核心 API 的前提下重建/切换选项。</summary>
  static class SandboxBuildStateHelper
  {
    struct PickEntry
    {
      public LevelUpChoiceDatabase.UpgradeDef Def;
      public int Stacks;
    }

    public static bool ToggleUpgrade(string weaponTheme, LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;

      var picks = CollectPicks(weaponTheme);
      var index = picks.FindIndex(p => p.Def.id == def.id);
      if (index < 0)
        return RunBuildState.ApplyChoice(def);

      var entry = picks[index];
      if (def.repeatable && entry.Stacks > 1)
      {
        entry.Stacks--;
        picks[index] = entry;
      }
      else
      {
        picks.RemoveAt(index);
      }

      Rebuild(weaponTheme, picks);
      return true;
    }

    static List<PickEntry> CollectPicks(string weaponTheme)
    {
      var picks = new List<PickEntry>();
      foreach (var kv in RunBuildState.PickStacks)
      {
        var def = FindUpgradeDef(weaponTheme, kv.Key);
        if (def == null || kv.Value <= 0)
          continue;

        picks.Add(new PickEntry { Def = def, Stacks = kv.Value });
      }

      return picks;
    }

    static void Rebuild(string weaponTheme, List<PickEntry> picks)
    {
      RunBuildState.Reset(weaponTheme);
      foreach (var pick in picks)
      {
        for (var i = 0; i < pick.Stacks; i++)
          RunBuildState.ApplyChoice(pick.Def);
      }
    }

    static LevelUpChoiceDatabase.UpgradeDef FindUpgradeDef(string weaponTheme, string id)
    {
      if (string.IsNullOrEmpty(id))
        return null;

      var allByRoute = LevelUpChoiceDatabase.GetAllUpgradesForClass(weaponTheme);
      foreach (var kv in allByRoute)
      {
        foreach (var def in kv.Value)
        {
          if (def != null && def.id == id)
            return def;
        }
      }

      return null;
    }
  }
}
