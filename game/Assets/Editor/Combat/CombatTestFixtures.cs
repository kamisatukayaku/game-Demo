using System.IO;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using UnityEngine;

namespace Game.Editor
{
  /// <summary>Editor-only combat test JSON; never synced to Resources or loaded in production.</summary>
  public static class CombatTestFixtures
  {
    const string FixtureRoot = "Assets/Editor/TestFixtures/Combat";
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      BuffDatabase.EnsureLoaded();
      AttackProfileDatabase.EnsureLoaded();

      var buffsPath = Path.Combine(FixtureRoot, "buffs_core_tests.json");
      var attacksPath = Path.Combine(FixtureRoot, "attacks_core_tests.json");
      if (File.Exists(buffsPath))
        BuffDatabase.MergeTestDefinitions(File.ReadAllText(buffsPath));
      if (File.Exists(attacksPath))
        AttackProfileDatabase.MergeTestProfiles(File.ReadAllText(attacksPath));

      s_loaded = true;
    }

    public static void Unload()
    {
      if (!s_loaded)
        return;

      BuffDatabase.ReloadProduction();
      AttackProfileDatabase.ReloadProduction();
      s_loaded = false;
    }

    public static bool ProductionContainsTestOnlyId(string buffId, string attackId)
    {
      BuffDatabase.ReloadProduction();
      AttackProfileDatabase.ReloadProduction();
      return BuffDatabase.Exists(buffId) || AttackProfileDatabase.Get(attackId) != null;
    }
  }
}
