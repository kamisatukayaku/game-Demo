#if UNITY_EDITOR
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class BuildMilestoneContactTests
  {
    const string MenuPath = "Tools/Validation/Run Contact Milestone Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      BuildMilestoneDatabase.EnsureLoaded();
      RunBuildState.Reset();
      BuildStatRepository.Clear();

      var beforeDamage = RunBuildState.GetStat("detached_part_damage_mult");
      var beforeCount = RunBuildState.GetStat("detached_part_count");
      var beforeRadius = RunBuildState.GetStat("detached_contact_radius_mult");

      ApplyMilestone("warrior", 10);
      Require(
        RunBuildState.GetStat("detached_part_damage_mult") > beforeDamage,
        "L10 warrior milestone must increase detached_part_damage_mult");

      ApplyMilestone("warrior", 15);
      Require(
        RunBuildState.GetStat("detached_part_count") > beforeCount,
        "L15 warrior milestone must increase detached_part_count");
      Require(
        RunBuildState.GetStat("detached_contact_radius_mult") > beforeRadius,
        "L15 warrior milestone must increase detached_contact_radius_mult");

      Debug.Log("[BuildMilestoneContactTests] PASS");
    }

    static void ApplyMilestone(string buildId, int level)
    {
      var milestone = BuildMilestoneDatabase.Find(buildId, level);
      if (milestone?.modifiers == null)
        throw new System.InvalidOperationException($"Missing milestone {buildId} L{level}");

      foreach (var mod in milestone.modifiers)
      {
        if (mod != null && !string.IsNullOrEmpty(mod.stat))
          RunBuildState.AddStat(mod.stat, mod.value);
      }
    }

    static void Require(bool ok, string message)
    {
      if (!ok)
        throw new System.InvalidOperationException(message);
    }
  }
}
#endif
