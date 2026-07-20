#if UNITY_EDITOR
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class HealOnKillBudgetTests
  {
    const string MenuPath = "Tools/Validation/Run Heal On Kill Budget Tests";

    [MenuItem(MenuPath)]
    public static void RunAll()
    {
      LevelUpChoiceDatabase.ResetForTests();
      LevelUpChoiceDatabase.EnsureLoaded();
      HealOnKillBudget.EditorResetForTests();

      var budget = HealOnKillBudget.BudgetPerSecond;
      Require(budget > 0f, "budget must be positive");

      HealOnKillBudget.EditorSetWindowStart(100f);
      var first = HealOnKillBudget.RequestHeal(2f);
      Require(Mathf.Approximately(first, 2f), "first heal should pass");

      var second = HealOnKillBudget.RequestHeal(budget);
      Require(second <= budget - 2f + 0.001f, "second heal should respect remaining budget");

      var blocked = HealOnKillBudget.RequestHeal(10f);
      Require(blocked <= 0.001f, "over-budget heal should be blocked");

      HealOnKillBudget.EditorSetWindowStart(200f);
      var afterWindow = HealOnKillBudget.RequestHeal(1f);
      Require(afterWindow >= 1f, "budget window should reset");

      foreach (var kps in new[] { 1, 5, 10, 30 })
      {
        HealOnKillBudget.EditorResetForTests();
        HealOnKillBudget.EditorSetWindowStart(0f);
        var total = 0f;
        for (var i = 0; i < kps; i++)
          total += HealOnKillBudget.RequestHeal(3f);

        Require(total <= budget + 0.01f, $"kps={kps} exceeded budget ({total}>{budget})");
      }

      Debug.Log("[HealOnKillBudgetTests] PASS");
    }

    static void Require(bool ok, string message)
    {
      if (!ok)
        throw new System.InvalidOperationException(message);
    }
  }
}
#endif
