using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>B7: Per-build L5/L10/L15 identity milestones loaded from build_milestones.json.</summary>
  public static class BuildMilestoneDatabase
  {
    static readonly Dictionary<string, List<MilestoneDef>> s_byBuild = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      s_byBuild.Clear();

      JsonDataLoader.TryParse("progression/build_milestones", json =>
      {
        try
        {
          var root = JsonUtility.FromJson<MilestoneRoot>(json);
          if (root?.builds == null)
            return;

          AddBuild("mage", root.builds.mage);
          AddBuild("shooter", root.builds.shooter);
          AddBuild("warrior", root.builds.warrior);
          AddBuild("unified", root.builds.unified);
        }
        catch (Exception e)
        {
          Debug.LogError($"[BuildMilestoneDatabase] Parse failed: {e.Message}");
        }
      });
    }

    static void AddBuild(string buildId, BuildMilestones build)
    {
      if (build?.milestones == null || build.milestones.Length == 0)
        return;
      s_byBuild[buildId] = new List<MilestoneDef>(build.milestones);
    }

    public static MilestoneDef Find(string buildId, int level)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(buildId) || !s_byBuild.TryGetValue(buildId, out var list))
        return null;

      foreach (var milestone in list)
      {
        if (milestone != null && milestone.level == level)
          return milestone;
      }

      return null;
    }

    public static string ResolveBuildId(string selectedBuildId) => selectedBuildId switch
    {
      ArenaBuildBootstrap.Unified => "unified",
      ArenaBuildBootstrap.Shooter => "shooter",
      ArenaBuildBootstrap.Contact => "warrior",
      ArenaBuildBootstrap.Support => "unified",
      ArenaBuildBootstrap.Mage => "mage",
      _ => "unified"
    };

    [Serializable]
    sealed class MilestoneRoot
    {
      public BuildsDef builds;
    }

    [Serializable]
    sealed class BuildsDef
    {
      public BuildMilestones mage;
      public BuildMilestones shooter;
      public BuildMilestones warrior;
      public BuildMilestones unified;
    }

    [Serializable]
    sealed class BuildMilestones
    {
      public MilestoneDef[] milestones;
    }

    [Serializable]
    public sealed class MilestoneDef
    {
      public int level;
      public string id;
      public string display_name;
      public string description;
      public LevelUpChoiceDatabase.StatModifier[] modifiers;
    }
  }
}
