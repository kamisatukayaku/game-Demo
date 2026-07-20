#if UNITY_EDITOR
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
  public static class UpgradeOfferDistributionSimulatorEditor
  {
    const int DistributionRolls = 1000;
    const int StressRolls = 1000;
    const int DefaultSeed = 20260705;
    const float Tolerance = 0.05f;

    [MenuItem("Tools/Ring Arena/Simulate Upgrade Offer Distribution")]
    public static void RunAllStarters()
    {
      foreach (UpgradeOfferDistributionSimulator.SimulationScenario scenario in System.Enum.GetValues(
                 typeof(UpgradeOfferDistributionSimulator.SimulationScenario)))
      {
        RunStarter(ArenaBuildBootstrap.Mage, "Mage", scenario);
        RunStarter(ArenaBuildBootstrap.Shooter, "Ranger", scenario);
        RunStarter(ArenaBuildBootstrap.Contact, "Dash", scenario);
      }
    }

    public static void RunBatchAndQuit()
    {
      RunAllStarters();
      EditorApplication.Exit(0);
    }

    static void RunStarter(
      string buildId,
      string label,
      UpgradeOfferDistributionSimulator.SimulationScenario scenario)
    {
      var rolls = IsStressScenario(scenario) ? StressRolls : DistributionRolls;
      var counts = UpgradeOfferDistributionSimulator.Run(buildId, rolls, DefaultSeed, scenario);
      var report = UpgradeOfferDistributionSimulator.FormatReport(
        $"{label}/{scenario}", counts, rolls, DefaultSeed);
      Debug.Log(report);

      if (counts.IllegalCandidates > 0 || counts.DuplicateInOffer > 0 || counts.MaxLevelRepeats > 0)
      {
        throw new System.InvalidOperationException(
          $"Upgrade sim failed for {label}/{scenario}: illegal={counts.IllegalCandidates}, duplicate={counts.DuplicateInOffer}, maxed={counts.MaxLevelRepeats}");
      }

      if (IsStressScenario(scenario))
      {
        if (counts.EmptyOffers > 0 && scenario != UpgradeOfferDistributionSimulator.SimulationScenario.PoolNearExhausted)
          throw new System.InvalidOperationException(
            $"Boundary scenario returned empty offers: {counts.EmptyOffers}");
        return;
      }

      ValidateDistribution(label, scenario, counts);
    }

    static bool IsStressScenario(UpgradeOfferDistributionSimulator.SimulationScenario scenario) =>
      scenario != UpgradeOfferDistributionSimulator.SimulationScenario.EarlyRun
      && scenario != UpgradeOfferDistributionSimulator.SimulationScenario.DetachedAcquired
      && scenario != UpgradeOfferDistributionSimulator.SimulationScenario.EvolutionEligible
      && scenario != UpgradeOfferDistributionSimulator.SimulationScenario.MidBuild;

    static void ValidateDistribution(
      string label,
      UpgradeOfferDistributionSimulator.SimulationScenario scenario,
      UpgradeOfferDistributionSimulator.GroupCounts counts)
    {
      if (counts.EligiblePrimaryPhaseTotal <= 0)
        return;

      if (scenario == UpgradeOfferDistributionSimulator.SimulationScenario.EarlyRun)
      {
        if (counts.EligiblePrimaryPhaseTotal <= 0)
          return;

        const float earlyTolerance = 0.09f;
        RequireNear(label, "Early Detached", counts.EligiblePrimaryDetachedRatio, 0f, 0.01f);
        RequireNear(label, "Early Gameplay", counts.EligiblePrimaryGameplayRatio, 0.50f, earlyTolerance);
        RequireNear(label, "Early Player", counts.EligiblePrimaryPlayerRatio, 0.30f, earlyTolerance);
        RequireNear(label, "Early Numeric", counts.EligiblePrimaryNumericRatio, 0.20f, earlyTolerance);
        return;
      }

      if (scenario == UpgradeOfferDistributionSimulator.SimulationScenario.DetachedAcquired
          || scenario == UpgradeOfferDistributionSimulator.SimulationScenario.MidBuild
          || scenario == UpgradeOfferDistributionSimulator.SimulationScenario.EvolutionEligible)
      {
        RequireRange(label, "Gameplay", counts.EligiblePrimaryGameplayRatio, 0.35f, 0.45f);
        RequireRange(label, "Player", counts.EligiblePrimaryPlayerRatio, 0.18f, 0.35f);
        RequireRange(label, "Detached", counts.EligiblePrimaryDetachedRatio, 0.12f, 0.28f);
        RequireRange(label, "Numeric", counts.EligiblePrimaryNumericRatio, 0.10f, 0.20f);
      }
    }

    static void RequireNear(string label, string metric, float actual, float target, float tolerance)
    {
      const float epsilon = 0.001f;
      if (actual < target - tolerance - epsilon || actual > target + tolerance + epsilon)
        throw new System.InvalidOperationException(
          $"{label} {metric}: got {actual:P1}, expected {target:P1} ±{tolerance:P0}");
    }

    static void RequireRange(string label, string metric, float actual, float min, float max)
    {
      if (actual < min || actual > max)
        throw new System.InvalidOperationException(
          $"{label} {metric}: got {actual:P1}, expected {min:P0}–{max:P0}");
    }
  }
}
#endif
