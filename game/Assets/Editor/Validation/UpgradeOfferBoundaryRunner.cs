#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Validation
{
  public static class UpgradeOfferBoundaryRunner
  {
    const int RollsPerScenario = 1000;
    const int DefaultSeed = 20260708;

    static readonly UpgradeOfferDistributionSimulator.SimulationScenario[] BoundaryScenarios =
    {
      UpgradeOfferDistributionSimulator.SimulationScenario.MechanicSlotsFull,
      UpgradeOfferDistributionSimulator.SimulationScenario.AllBaseMechanicsOwned,
      UpgradeOfferDistributionSimulator.SimulationScenario.ChainMaxed,
      UpgradeOfferDistributionSimulator.SimulationScenario.PoolNearExhausted,
      UpgradeOfferDistributionSimulator.SimulationScenario.BossRushOpening,
      UpgradeOfferDistributionSimulator.SimulationScenario.BossRushPreFinal,
      UpgradeOfferDistributionSimulator.SimulationScenario.MultiDetachedEvolution,
      UpgradeOfferDistributionSimulator.SimulationScenario.DetachedAtCap,
      UpgradeOfferDistributionSimulator.SimulationScenario.MultiLevelBurst,
      UpgradeOfferDistributionSimulator.SimulationScenario.PlayerStatsPartialMax,
      UpgradeOfferDistributionSimulator.SimulationScenario.RunRestartFirstOffer,
      UpgradeOfferDistributionSimulator.SimulationScenario.NoDetachedEntitlement,
      UpgradeOfferDistributionSimulator.SimulationScenario.FirstDetachedEntitlement,
      UpgradeOfferDistributionSimulator.SimulationScenario.AuxiliaryOfferStress,
      UpgradeOfferDistributionSimulator.SimulationScenario.MaxedChains
    };

    [MenuItem("Tools/Validation/Run Upgrade Boundary Tests")]
    public static void RunAll()
    {
      var results = new List<ScenarioResult>();
      foreach (var scenario in BoundaryScenarios)
      {
        foreach (var build in new[] { ArenaBuildBootstrap.Mage, ArenaBuildBootstrap.Shooter, ArenaBuildBootstrap.Contact })
        {
          var counts = UpgradeOfferDistributionSimulator.Run(build, RollsPerScenario, DefaultSeed, scenario);
          var pass = counts.IllegalCandidates == 0
                     && counts.DuplicateInOffer == 0
                     && counts.MaxLevelRepeats == 0;
          results.Add(new ScenarioResult(build, scenario.ToString(), counts, pass));
          if (!pass)
          {
            throw new System.InvalidOperationException(
              $"Boundary failed {build}/{scenario}: illegal={counts.IllegalCandidates}, duplicate={counts.DuplicateInOffer}, maxed={counts.MaxLevelRepeats}, empty={counts.EmptyOffers}");
          }
        }
      }

      WriteReports(results);
      Debug.Log($"[UpgradeOfferBoundaryRunner] PASS ({results.Count} scenarios x 1000 rolls)");
    }

    static void WriteReports(List<ScenarioResult> results)
    {
      var json = new StringBuilder();
      json.AppendLine("{");
      json.AppendLine($"  \"generated_at\": \"{RuntimeValidationReportWriter.TimestampUtc()}\",");
      json.AppendLine("  \"rolls_per_scenario\": 1000,");
      json.AppendLine("  \"scenarios\": [");
      for (var i = 0; i < results.Count; i++)
      {
        var r = results[i];
        json.Append("    {")
          .Append($"\"build\":\"{r.BuildId}\",")
          .Append($"\"scenario\":\"{r.Scenario}\",")
          .Append($"\"illegal\":{r.Counts.IllegalCandidates},")
          .Append($"\"duplicate\":{r.Counts.DuplicateInOffer},")
          .Append($"\"maxed_reoffer\":{r.Counts.MaxLevelRepeats},")
          .Append($"\"empty_offers\":{r.Counts.EmptyOffers},")
          .Append($"\"status\":\"{(r.Pass ? "PASS" : "FAIL")}\"")
          .Append(i == results.Count - 1 ? "}" : "},");
        json.AppendLine();
      }

      json.AppendLine("  ]");
      json.AppendLine("}");

      RuntimeValidationReportWriter.WriteJson("upgrade_boundary_results.json", json.ToString());
      RuntimeValidationReportWriter.WriteText(
        "UPGRADE_BOUNDARY_RESULTS.md",
        "# Upgrade Boundary Results\n\nStatus: PASS\n\nAll boundary scenarios illegal=0 duplicate=0 maxed=0\n");
    }

    readonly struct ScenarioResult
    {
      public readonly string BuildId;
      public readonly string Scenario;
      public readonly UpgradeOfferDistributionSimulator.GroupCounts Counts;
      public readonly bool Pass;

      public ScenarioResult(string buildId, string scenario, UpgradeOfferDistributionSimulator.GroupCounts counts, bool pass)
      {
        BuildId = buildId;
        Scenario = scenario;
        Counts = counts;
        Pass = pass;
      }
    }
  }
}
#endif
