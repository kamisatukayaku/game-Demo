using System.Collections.Generic;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.UI;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>B7: Applies build milestones at L5/L10/L15 and shows ArenaMomentUI banner.</summary>
  [DisallowMultipleComponent]
  public sealed class BuildMilestoneRuntime : MonoBehaviour
  {
    static BuildMilestoneRuntime s_instance;
    static readonly HashSet<string> s_applied = new();

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_BuildMilestoneRuntime");
      DontDestroyOnLoad(go);
      go.AddComponent<BuildMilestoneRuntime>();
    }

    public static void ResetSession()
    {
      s_applied.Clear();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
      ExperienceSystem.LevelUp += OnLevelUp;
    }

    void OnDisable()
    {
      ExperienceSystem.LevelUp -= OnLevelUp;
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void OnLevelUp(int fromLevel, int toLevel)
    {
      BuildMilestoneDatabase.EnsureLoaded();
      var buildId = BuildMilestoneDatabase.ResolveBuildId(ArenaBuildBootstrap.SelectedBuildId);
      var color = ArenaBuildBootstrap.GetIdentityColor(ArenaBuildBootstrap.SelectedBuildId);

      for (var level = fromLevel + 1; level <= toLevel; level++)
        TryApply(buildId, level, color);
    }

    static void TryApply(string buildId, int level, Color bannerColor)
    {
      var milestone = BuildMilestoneDatabase.Find(buildId, level);
      if (milestone == null || string.IsNullOrEmpty(milestone.id))
        return;

      if (!s_applied.Add(milestone.id))
        return;

      ApplyModifiers(milestone.modifiers);
      RunBuildState.NotifyChanged();

      var title = string.IsNullOrEmpty(milestone.display_name)
        ? $"Build 里程碑 Lv.{level}"
        : milestone.display_name;
      ArenaMomentUI.ShowBanner(title, bannerColor);
      Debug.Log($"[BuildMilestone] Lv.{level} unlocked [{title}] for build={buildId}");
    }

    static void ApplyModifiers(LevelUpChoiceDatabase.StatModifier[] mods)
    {
      if (mods == null)
        return;

      foreach (var mod in mods)
      {
        if (mod == null || string.IsNullOrEmpty(mod.stat))
          continue;
        RunBuildState.AddStat(mod.stat, mod.value);
      }
    }
  }
}
