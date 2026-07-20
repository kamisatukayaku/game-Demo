using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Data;

namespace Game.Modes.Roguelike.Tutorial
{
  public static class TutorialStepDatabase
  {
    const string Stem = "tutorial/tutorial_steps";

    [Serializable]
    class Root
    {
      public StepDef[] steps;
    }

    [Serializable]
    public class StepDef
    {
      public string id;
      public string message;
      public int priority;
      public string anchor;
    }

    static readonly Dictionary<string, StepDef> s_steps = new();
    static bool s_loaded;

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_steps.Clear();

      if (!JsonDataLoader.TryParse(Stem, json =>
          {
            var root = JsonUtility.FromJson<Root>(json);
            if (root?.steps == null)
              return;
            foreach (var step in root.steps)
            {
              if (step == null || string.IsNullOrEmpty(step.id))
                continue;
              s_steps[step.id] = step;
            }
          }))
      {
        ApplyDefaults();
      }
    }

    static void ApplyDefaults()
    {
      AddDefault("move", "WASD 移动", 3, "bottom");
      AddDefault("dash", "按左 Shift 冲刺，可短暂规避伤害", 3, "bottom");
      AddDefault("auto_attack", "武器会自动寻找并攻击附近敌人", 3, "bottom");
      AddDefault("xp_pickup", "靠近能量核心以吸收经验", 3, "bottom");
      AddDefault("level_up", "选择强化，逐步构建本局能力", 4, "level_up_footer");
      AddDefault("detached_weapon", "外置核心会独立行动，并可进化为不同攻击形态", 3, "bottom");
    }

    static void AddDefault(string id, string message, int priority, string anchor) =>
      s_steps[id] = new StepDef { id = id, message = message, priority = priority, anchor = anchor };

    public static bool TryGet(string stepId, out StepDef def)
    {
      EnsureLoaded();
      return s_steps.TryGetValue(stepId, out def);
    }

    public static IEnumerable<string> AllStepIds
    {
      get
      {
        EnsureLoaded();
        return s_steps.Keys;
      }
    }

    public static string GetMessage(string stepId, string fallback) =>
      TryGet(stepId, out var def) && !string.IsNullOrEmpty(def.message) ? def.message : fallback;
  }
}
