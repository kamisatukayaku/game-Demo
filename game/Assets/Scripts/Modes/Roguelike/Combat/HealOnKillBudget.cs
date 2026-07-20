using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>Rate-limits heal-on-kill so high kill density cannot out-heal wave pressure.</summary>
  public static class HealOnKillBudget
  {
    static float s_windowStart;
    static float s_consumed;

    public static float BudgetPerSecond =>
      LevelUpChoiceDatabase.SurvivalTuning.heal_on_kill_budget_per_second > 0f
        ? LevelUpChoiceDatabase.SurvivalTuning.heal_on_kill_budget_per_second
        : 4f;

    public static float WindowSeconds =>
      LevelUpChoiceDatabase.SurvivalTuning.heal_on_kill_budget_window_seconds > 0f
        ? LevelUpChoiceDatabase.SurvivalTuning.heal_on_kill_budget_window_seconds
        : 1f;

    public static void Reset()
    {
      s_windowStart = Time.time;
      s_consumed = 0f;
    }

    public static float RequestHeal(float requestedAmount)
    {
      if (requestedAmount <= 0f)
        return 0f;

      AdvanceWindow();
      var budget = BudgetPerSecond * WindowSeconds;
      var remaining = Mathf.Max(0f, budget - s_consumed);
      var granted = Mathf.Min(requestedAmount, remaining);
      s_consumed += granted;
      return granted;
    }

    public static float RemainingInWindow()
    {
      AdvanceWindow();
      var budget = BudgetPerSecond * WindowSeconds;
      return Mathf.Max(0f, budget - s_consumed);
    }

    static void AdvanceWindow()
    {
      var now = Time.time;
      if (s_windowStart > now || now - s_windowStart >= WindowSeconds)
      {
        s_windowStart = now;
        s_consumed = 0f;
      }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void EditorResetForTests()
    {
      s_windowStart = 0f;
      s_consumed = 0f;
    }

    public static void EditorSetWindowStart(float time) => s_windowStart = time;

    public static float EditorConsumed => s_consumed;
#endif
  }
}
