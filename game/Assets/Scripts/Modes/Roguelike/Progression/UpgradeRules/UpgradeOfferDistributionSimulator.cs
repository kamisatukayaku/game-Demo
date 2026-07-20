using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Runtime;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Deterministic sampling of level-up offer category distribution.</summary>
  public static class UpgradeOfferDistributionSimulator
  {
    public enum RunPhase
    {
      EarlyRun = 1,
      DevelopingRun = 2,
      MidBuild = 3,
      LateBuild = 4,
      Exhaustion = 5
    }

    public readonly struct GroupCounts
    {
      public readonly int TotalCards;
      public readonly int Gameplay;
      public readonly int Player;
      public readonly int Detached;
      public readonly int Numeric;
      public readonly int Mechanic;
      public readonly int Evolution;
      public readonly int MaxConsecutiveOffersWithoutPlayer;
      public readonly int MaxConsecutiveDetachedOffers;
      public readonly int MaxConsecutiveOffersWithoutDetached;
      public readonly int OffersWithoutPlayerCard;
      public readonly int IllegalCandidates;
      public readonly int DuplicateInOffer;
      public readonly int MaxLevelRepeats;
      public readonly int EmptyOffers;
      public readonly int PityForced;
      public readonly int FallbackFills;
      public readonly int EarlyCards;
      public readonly int DevelopingCards;
      public readonly int MidCards;
      public readonly int LateCards;

      public readonly int PrimaryGameplay;
      public readonly int PrimaryPlayer;
      public readonly int PrimaryDetached;
      public readonly int PrimaryNumeric;

      public readonly int EligiblePrimaryGameplay;
      public readonly int EligiblePrimaryPlayer;
      public readonly int EligiblePrimaryDetached;
      public readonly int EligiblePrimaryNumeric;

      public readonly int EligibleEarlyGameplay;
      public readonly int EligibleEarlyPlayer;
      public readonly int EligibleEarlyDetached;
      public readonly int EligibleEarlyNumeric;

      public GroupCounts(
        int totalCards,
        int gameplay,
        int player,
        int detached,
        int numeric,
        int mechanic,
        int evolution,
        int maxConsecutiveOffersWithoutPlayer,
        int maxConsecutiveDetachedOffers,
        int maxConsecutiveOffersWithoutDetached,
        int offersWithoutPlayerCard,
        int illegalCandidates,
        int duplicateInOffer,
        int maxLevelRepeats,
        int emptyOffers,
        int pityForced,
        int fallbackFills,
        int earlyCards,
        int developingCards,
        int midCards,
        int lateCards,
        int primaryGameplay,
        int primaryPlayer,
        int primaryDetached,
        int primaryNumeric,
        int eligiblePrimaryGameplay,
        int eligiblePrimaryPlayer,
        int eligiblePrimaryDetached,
        int eligiblePrimaryNumeric,
        int eligibleEarlyGameplay,
        int eligibleEarlyPlayer,
        int eligibleEarlyDetached,
        int eligibleEarlyNumeric)
      {
        TotalCards = totalCards;
        Gameplay = gameplay;
        Player = player;
        Detached = detached;
        Numeric = numeric;
        Mechanic = mechanic;
        Evolution = evolution;
        MaxConsecutiveOffersWithoutPlayer = maxConsecutiveOffersWithoutPlayer;
        MaxConsecutiveDetachedOffers = maxConsecutiveDetachedOffers;
        MaxConsecutiveOffersWithoutDetached = maxConsecutiveOffersWithoutDetached;
        OffersWithoutPlayerCard = offersWithoutPlayerCard;
        IllegalCandidates = illegalCandidates;
        DuplicateInOffer = duplicateInOffer;
        MaxLevelRepeats = maxLevelRepeats;
        EmptyOffers = emptyOffers;
        PityForced = pityForced;
        FallbackFills = fallbackFills;
        EarlyCards = earlyCards;
        DevelopingCards = developingCards;
        MidCards = midCards;
        LateCards = lateCards;
        PrimaryGameplay = primaryGameplay;
        PrimaryPlayer = primaryPlayer;
        PrimaryDetached = primaryDetached;
        PrimaryNumeric = primaryNumeric;
        EligiblePrimaryGameplay = eligiblePrimaryGameplay;
        EligiblePrimaryPlayer = eligiblePrimaryPlayer;
        EligiblePrimaryDetached = eligiblePrimaryDetached;
        EligiblePrimaryNumeric = eligiblePrimaryNumeric;
        EligibleEarlyGameplay = eligibleEarlyGameplay;
        EligibleEarlyPlayer = eligibleEarlyPlayer;
        EligibleEarlyDetached = eligibleEarlyDetached;
        EligibleEarlyNumeric = eligibleEarlyNumeric;
      }

      public float PlayerRatio => Ratio(Player);
      public float GameplayRatio => Ratio(Gameplay);
      public float DetachedRatio => Ratio(Detached);
      public float NumericRatio => Ratio(Numeric);
      public float MechanicRatio => Ratio(Mechanic);
      public float EvolutionRatio => Ratio(Evolution);

      public int PrimaryPhaseTotal => EarlyCards + DevelopingCards + MidCards;
      public float PrimaryGameplayRatio => PrimaryRatio(PrimaryGameplay);
      public float PrimaryPlayerRatio => PrimaryRatio(PrimaryPlayer);
      public float PrimaryDetachedRatio => PrimaryRatio(PrimaryDetached);
      public float PrimaryNumericRatio => PrimaryRatio(PrimaryNumeric);

      public int EligiblePrimaryPhaseTotal =>
        EligiblePrimaryGameplay + EligiblePrimaryPlayer + EligiblePrimaryDetached + EligiblePrimaryNumeric;

      public float EligiblePrimaryGameplayRatio => EligibleRatio(EligiblePrimaryGameplay);
      public float EligiblePrimaryPlayerRatio => EligibleRatio(EligiblePrimaryPlayer);
      public float EligiblePrimaryDetachedRatio => EligibleRatio(EligiblePrimaryDetached);
      public float EligiblePrimaryNumericRatio => EligibleRatio(EligiblePrimaryNumeric);

      public int EligibleEarlyPhaseTotal =>
        EligibleEarlyGameplay + EligibleEarlyPlayer + EligibleEarlyDetached + EligibleEarlyNumeric;

      public float EligibleEarlyGameplayRatio => EligibleEarlyRatio(EligibleEarlyGameplay);
      public float EligibleEarlyPlayerRatio => EligibleEarlyRatio(EligibleEarlyPlayer);
      public float EligibleEarlyDetachedRatio => EligibleEarlyRatio(EligibleEarlyDetached);
      public float EligibleEarlyNumericRatio => EligibleEarlyRatio(EligibleEarlyNumeric);

      float Ratio(int count) => TotalCards > 0 ? count / (float)TotalCards : 0f;
      float PrimaryRatio(int count) => PrimaryPhaseTotal > 0 ? count / (float)PrimaryPhaseTotal : 0f;
      float EligibleRatio(int count) =>
        EligiblePrimaryPhaseTotal > 0 ? count / (float)EligiblePrimaryPhaseTotal : 0f;
      float EligibleEarlyRatio(int count) =>
        EligibleEarlyPhaseTotal > 0 ? count / (float)EligibleEarlyPhaseTotal : 0f;
    }

    sealed class TestArenaLayout : IArenaLayout
    {
      public static readonly TestArenaLayout Instance = new();
      public bool IsActive => true;
      public Vector2 Center => Vector2.zero;
      public float PathRadius => 12f;
      public float FullCombatRange => 999f;
      public float AngleAtPosition(Vector2 position) => 0f;
      public Vector2 PositionAtAngle(float angleRadians) => Vector2.zero;

      public bool ComputeChord(Vector2 enemyPlanar, Vector2 direction, out Vector2 start, out Vector2 end, out float dashDistance)
      {
        start = enemyPlanar;
        end = enemyPlanar;
        dashDistance = 0f;
        return false;
      }

      public Vector2 GetSpawnPointOnCircle(Vector2 hintPos) => hintPos;
    }

    public enum SimulationScenario
    {
      EarlyRun,
      DetachedAcquired,
      EvolutionEligible,
      MidBuild,
      MaxedChains,
      MechanicSlotsFull,
      AllBaseMechanicsOwned,
      ChainMaxed,
      PoolNearExhausted,
      BossRushOpening,
      BossRushPreFinal,
      MultiDetachedEvolution,
      DetachedAtCap,
      MultiLevelBurst,
      PlayerStatsPartialMax,
      RunRestartFirstOffer,
      NoDetachedEntitlement,
      FirstDetachedEntitlement,
      AuxiliaryOfferStress
    }

    public static GroupCounts Run(string buildId, int rolls, int seed) =>
      Run(buildId, rolls, seed, SimulationScenario.EarlyRun);

    public static GroupCounts Run(string buildId, int rolls, int seed, SimulationScenario scenario)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      LevelUpChoiceDatabase.Reseed(seed);
      UpgradeOfferPityTracker.ResetForNewRun();
      UpgradeOfferBuildTelemetry.Reset();
      ArenaBuildBootstrap.ConfigureForSimulation(buildId);
      ApplyScenarioSetup(scenario, buildId);
      return RunSimulationLoop(rolls, seed, scenario == SimulationScenario.MaxedChains, scenario);
    }

    static void ApplyScenarioSetup(SimulationScenario scenario, string buildId)
    {
      switch (scenario)
      {
        case SimulationScenario.EarlyRun:
          return;
        case SimulationScenario.DetachedAcquired:
          RunBuildState.AddStat("detached_part_count", 1f);
          TryApplyUpgrade("num_ranger_detached_slot_01");
          return;
        case SimulationScenario.EvolutionEligible:
          if (buildId == ArenaBuildBootstrap.Contact)
            RunBuildState.AddStat("detached_contact_level", 1f);
          else
            RunBuildState.AddStat("detached_part_count", 1f);
          TryApplyUpgrade("num_ranger_detached_slot_01");
          return;
        case SimulationScenario.MidBuild:
          RunBuildState.AddStat("detached_part_count", 1f);
          TryApplyUpgrade("num_player_max_hp_01");
          TryApplyUpgrade("num_player_move_speed_01");
          TryApplyUpgrade("num_common_damage_01");
          TryApplyUpgrade("num_common_attack_speed_01");
          return;
        case SimulationScenario.MaxedChains:
          for (var level = 1; level <= 5; level++)
          {
            TryApplyUpgrade($"num_common_damage_{level:00}");
            TryApplyUpgrade($"num_player_max_hp_{level:00}");
          }
          return;
        case SimulationScenario.MechanicSlotsFull:
          RunBuildState.AddStat("mechanic_slots", BuildProgressionState.MaxMechanicSlots - BuildProgressionState.BaseMechanicSlots);
          for (var i = 0; i < BuildProgressionState.MechanicSlotCount; i++)
            TryApplyUpgrade(i % 2 == 0 ? "num_ranged_pierce_01" : "num_mage_gravity_well_01");
          return;
        case SimulationScenario.AllBaseMechanicsOwned:
          TryApplyUpgrade("num_ranged_pierce_01");
          TryApplyUpgrade("num_mage_gravity_well_01");
          TryApplyUpgrade("num_warrior_orbit_speed_01");
          return;
        case SimulationScenario.ChainMaxed:
          for (var level = 1; level <= 5; level++)
            TryApplyUpgrade($"num_common_damage_{level:00}");
          return;
        case SimulationScenario.PoolNearExhausted:
          for (var level = 1; level <= 5; level++)
          {
            TryApplyUpgrade($"num_common_damage_{level:00}");
            TryApplyUpgrade($"num_common_attack_speed_{level:00}");
            TryApplyUpgrade($"num_player_max_hp_{level:00}");
            TryApplyUpgrade($"num_player_move_speed_{level:00}");
          }
          return;
        case SimulationScenario.BossRushOpening:
          GameSessionConfig.Configure(buildId, System.Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, buildId);
          if (BossRushDatabase.Encounters.Count > 0)
            BossRushEncounterRuntime.BeginEncounter(BossRushDatabase.Encounters[0], 0);
          else
            BossRushEncounterRuntime.BeginEncounter(null, 0);
          return;
        case SimulationScenario.BossRushPreFinal:
          GameSessionConfig.Configure(buildId, System.Array.Empty<string>(), "normal", GameSessionConfig.GameMode.BossRush, buildId);
          var preFinalIndex = Mathf.Max(0, BossRushDatabase.Encounters.Count - 2);
          if (BossRushDatabase.Encounters.Count > 0)
            BossRushEncounterRuntime.BeginEncounter(BossRushDatabase.Encounters[preFinalIndex], preFinalIndex);
          else
            BossRushEncounterRuntime.BeginEncounter(null, 4);
          return;
        case SimulationScenario.MultiDetachedEvolution:
          RunBuildState.AddStat("detached_part_count", 2f);
          TryApplyUpgrade("num_ranger_detached_slot_01");
          TryApplyUpgrade("num_ranger_detached_slot_02");
          return;
        case SimulationScenario.DetachedAtCap:
          RunBuildState.AddStat("detached_part_count", 6f);
          TryApplyUpgrade("num_ranger_detached_slot_01");
          return;
        case SimulationScenario.MultiLevelBurst:
          RunBuildState.AddStat("detached_part_count", 1f);
          TryApplyUpgrade("num_player_max_hp_01");
          return;
        case SimulationScenario.PlayerStatsPartialMax:
          for (var level = 1; level <= 5; level++)
            TryApplyUpgrade($"num_player_max_hp_{level:00}");
          return;
        case SimulationScenario.RunRestartFirstOffer:
          RunBuildState.Reset(ArenaBuildBootstrap.GetThemeForBuild(buildId));
          ArenaBuildBootstrap.ConfigureForSimulation(buildId);
          return;
        case SimulationScenario.NoDetachedEntitlement:
          return;
        case SimulationScenario.FirstDetachedEntitlement:
          RunBuildState.AddStat("detached_part_count", 1f);
          TryApplyUpgrade("num_ranger_detached_slot_01");
          return;
        case SimulationScenario.AuxiliaryOfferStress:
          TryApplyUpgrade(LevelUpChoiceDatabase.RangedStarterId);
          return;
      }
    }

    static void TryApplyUpgrade(string id)
    {
      var upgrade = LevelUpChoiceDatabase.FindById(id);
      if (upgrade != null)
        RunBuildState.ApplyChoice(upgrade);
    }

    static GroupCounts RunSimulationLoop(
      int rolls,
      int seed,
      bool exhaustionMode,
      SimulationScenario scenario)
    {
      var gameplay = 0;
      var player = 0;
      var detached = 0;
      var numeric = 0;
      var mechanic = 0;
      var evolution = 0;
      var earlyCards = 0;
      var developingCards = 0;
      var midCards = 0;
      var lateCards = 0;
      var primaryGameplay = 0;
      var primaryPlayer = 0;
      var primaryDetached = 0;
      var primaryNumeric = 0;
      var eligiblePrimaryGameplay = 0;
      var eligiblePrimaryPlayer = 0;
      var eligiblePrimaryDetached = 0;
      var eligiblePrimaryNumeric = 0;
      var eligibleEarlyGameplay = 0;
      var eligibleEarlyPlayer = 0;
      var eligibleEarlyDetached = 0;
      var eligibleEarlyNumeric = 0;
      var consecutiveWithoutPlayer = 0;
      var consecutiveDetachedOffers = 0;
      var consecutiveWithoutDetached = 0;
      var maxConsecutiveWithoutPlayer = 0;
      var maxConsecutiveDetachedOffers = 0;
      var maxConsecutiveWithoutDetached = 0;
      var offersWithoutPlayer = 0;
      var illegalCandidates = 0;
      var duplicateInOffer = 0;
      var maxLevelRepeats = 0;
      var emptyOffers = 0;
      var simRng = new System.Random(seed);

      var previousLayout = ArenaLayoutLocator.Layout;
      ArenaLayoutLocator.Register(TestArenaLayout.Instance);

      try
      {
        for (var i = 0; i < rolls; i++)
        {
          var level = i + 1;
          var phase = ResolvePhase(level, exhaustionMode);
          var offer = RunBuildState.GetPendingOffer();
          var poolSnapshot = UpgradeOfferBuildTelemetry.LastPoolSnapshot;
          var eligibleSample = IsEligibleDistributionSample(scenario, poolSnapshot)
                               && offer.choices != null
                               && offer.choices.Length > 0;
          if (offer.choices == null || offer.choices.Length == 0)
            emptyOffers++;

          var hadPlayer = false;
          var hadDetached = false;
          var ids = new HashSet<string>();

          if (offer.choices != null)
          {
            foreach (var choice in offer.choices)
            {
              if (choice == null)
                continue;

              if (!string.IsNullOrEmpty(choice.id))
              {
                if (!ids.Add(choice.id))
                  duplicateInOffer++;
              }

              if (IsIllegalCandidate(choice))
                illegalCandidates++;
              else if (UpgradeEligibilityRules.IsBlockedByPickHistory(choice, RunBuildState.PickStacks))
                maxLevelRepeats++;

              if (phase != RunPhase.Exhaustion)
              {
                var group = UpgradeOfferGroupPolicy.Resolve(choice);
                switch (group)
                {
                  case UpgradeOfferGroup.Gameplay: gameplay++; break;
                  case UpgradeOfferGroup.Player:
                    player++;
                    hadPlayer = true;
                    break;
                  case UpgradeOfferGroup.Detached:
                    detached++;
                    hadDetached = true;
                    break;
                  case UpgradeOfferGroup.Numeric: numeric++; break;
                }

                switch (phase)
                {
                  case RunPhase.EarlyRun: earlyCards++; break;
                  case RunPhase.DevelopingRun: developingCards++; break;
                  case RunPhase.MidBuild: midCards++; break;
                  case RunPhase.LateBuild: lateCards++; break;
                }

                if (phase is RunPhase.EarlyRun or RunPhase.DevelopingRun or RunPhase.MidBuild)
                {
                  switch (group)
                  {
                    case UpgradeOfferGroup.Gameplay: primaryGameplay++; break;
                    case UpgradeOfferGroup.Player: primaryPlayer++; break;
                    case UpgradeOfferGroup.Detached: primaryDetached++; break;
                    case UpgradeOfferGroup.Numeric: primaryNumeric++; break;
                  }

                  if (eligibleSample)
                  {
                    switch (group)
                    {
                      case UpgradeOfferGroup.Gameplay: eligiblePrimaryGameplay++; break;
                      case UpgradeOfferGroup.Player: eligiblePrimaryPlayer++; break;
                      case UpgradeOfferGroup.Detached: eligiblePrimaryDetached++; break;
                      case UpgradeOfferGroup.Numeric: eligiblePrimaryNumeric++; break;
                    }

                    if (phase == RunPhase.EarlyRun)
                    {
                      switch (group)
                      {
                        case UpgradeOfferGroup.Gameplay: eligibleEarlyGameplay++; break;
                        case UpgradeOfferGroup.Player: eligibleEarlyPlayer++; break;
                        case UpgradeOfferGroup.Detached: eligibleEarlyDetached++; break;
                        case UpgradeOfferGroup.Numeric: eligibleEarlyNumeric++; break;
                      }
                    }
                  }
                }

                if (IsMechanicUpgrade(choice))
                  mechanic++;
                if (IsEvolutionUpgrade(choice))
                  evolution++;
              }
            }
          }

          if (offer.choices != null && offer.choices.Length > 0)
          {
            var pick = offer.choices[simRng.Next(offer.choices.Length)];
            if (pick != null)
              RunBuildState.ApplyChoice(pick);
          }

          if (hadPlayer)
            consecutiveWithoutPlayer = 0;
          else
          {
            offersWithoutPlayer++;
            consecutiveWithoutPlayer++;
            if (consecutiveWithoutPlayer > maxConsecutiveWithoutPlayer)
              maxConsecutiveWithoutPlayer = consecutiveWithoutPlayer;
          }

          if (hadDetached)
          {
            consecutiveDetachedOffers++;
            consecutiveWithoutDetached = 0;
            if (consecutiveDetachedOffers > maxConsecutiveDetachedOffers)
              maxConsecutiveDetachedOffers = consecutiveDetachedOffers;
          }
          else
          {
            consecutiveDetachedOffers = 0;
            if (DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())
            {
              consecutiveWithoutDetached++;
              if (consecutiveWithoutDetached > maxConsecutiveWithoutDetached)
                maxConsecutiveWithoutDetached = consecutiveWithoutDetached;
            }
          }
        }
      }
      finally
      {
        ArenaLayoutLocator.Register(previousLayout);
      }

      var total = gameplay + player + detached + numeric;
      return new GroupCounts(
        total,
        gameplay,
        player,
        detached,
        numeric,
        mechanic,
        evolution,
        maxConsecutiveWithoutPlayer,
        maxConsecutiveDetachedOffers,
        maxConsecutiveWithoutDetached,
        offersWithoutPlayer,
        illegalCandidates,
        duplicateInOffer,
        maxLevelRepeats,
        emptyOffers,
        UpgradeOfferBuildTelemetry.PityForcedCount,
        UpgradeOfferBuildTelemetry.FallbackFillCount,
        earlyCards,
        developingCards,
        midCards,
        lateCards,
        primaryGameplay,
        primaryPlayer,
        primaryDetached,
        primaryNumeric,
        eligiblePrimaryGameplay,
        eligiblePrimaryPlayer,
        eligiblePrimaryDetached,
        eligiblePrimaryNumeric,
        eligibleEarlyGameplay,
        eligibleEarlyPlayer,
        eligibleEarlyDetached,
        eligibleEarlyNumeric);
    }

    static bool IsEligibleDistributionSample(
      SimulationScenario scenario,
      UpgradeOfferBuildTelemetry.PoolSnapshot snapshot)
    {
      if (snapshot.Gameplay <= 0 || snapshot.Player <= 0 || snapshot.Numeric <= 0)
        return false;

      if (scenario is SimulationScenario.DetachedAcquired
          or SimulationScenario.MidBuild
          or SimulationScenario.EvolutionEligible)
        return snapshot.Detached > 0;

      return true;
    }

    static RunPhase ResolvePhase(int level, bool exhaustionMode)
    {
      if (exhaustionMode)
        return RunPhase.Exhaustion;
      if (level <= 5)
        return RunPhase.EarlyRun;
      if (level <= 12)
        return RunPhase.DevelopingRun;
      if (level <= 20)
        return RunPhase.MidBuild;
      return RunPhase.LateBuild;
    }

    static bool IsMechanicUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>
      def != null && (
        string.Equals(def.category, "mechanic", System.StringComparison.OrdinalIgnoreCase)
        || def.introduces_mechanic);

    static bool IsEvolutionUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>
      def != null
      && !string.IsNullOrEmpty(def.id)
      && def.id.StartsWith("evo_", System.StringComparison.OrdinalIgnoreCase);

    static bool IsIllegalCandidate(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null || string.IsNullOrEmpty(def.id))
        return true;

      if (!UpgradeOfferWeightPolicy.IsDetachedWeaponUpgrade(def)
          || DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())
        return false;

      if (def.id.StartsWith("num_part_", System.StringComparison.Ordinal))
        return true;

      if (def.requires_ids == null)
        return false;

      foreach (var req in def.requires_ids)
      {
        if (!string.IsNullOrEmpty(req) && req.StartsWith("part_", System.StringComparison.Ordinal))
          return true;
      }

      return false;
    }

    public static string FormatReport(string buildId, GroupCounts counts, int rolls, int seed)
    {
      var sb = new StringBuilder();
      sb.AppendLine($"[UpgradeOfferSimulation] build={buildId} rolls={rolls} seed={seed}");
      sb.AppendLine($"  Gameplay: {counts.Gameplay} ({counts.GameplayRatio:P1}) target ~40%");
      sb.AppendLine($"  Player:   {counts.Player} ({counts.PlayerRatio:P1}) target ~25%");
      sb.AppendLine($"  Detached: {counts.Detached} ({counts.DetachedRatio:P1}) target ~20%");
      sb.AppendLine($"  Numeric:  {counts.Numeric} ({counts.NumericRatio:P1}) target ~15%");
      sb.AppendLine($"  Mechanic: {counts.Mechanic} ({counts.MechanicRatio:P1})");
      sb.AppendLine($"  Evolution:{counts.Evolution} ({counts.EvolutionRatio:P1})");
      sb.AppendLine($"  Phase cards: early={counts.EarlyCards} dev={counts.DevelopingCards} mid={counts.MidCards} late={counts.LateCards}");
      sb.AppendLine(
        $"  Eligible primary: G={counts.EligiblePrimaryGameplay} P={counts.EligiblePrimaryPlayer} D={counts.EligiblePrimaryDetached} N={counts.EligiblePrimaryNumeric} total={counts.EligiblePrimaryPhaseTotal}");
      sb.AppendLine(
        $"  Eligible early: G={counts.EligibleEarlyGameplay} P={counts.EligibleEarlyPlayer} D={counts.EligibleEarlyDetached} N={counts.EligibleEarlyNumeric} total={counts.EligibleEarlyPhaseTotal}");
      sb.AppendLine($"  Offers without player card: {counts.OffersWithoutPlayerCard}/{rolls}");
      sb.AppendLine($"  Max consecutive offers without player: {counts.MaxConsecutiveOffersWithoutPlayer}");
      sb.AppendLine($"  Max consecutive detached offers: {counts.MaxConsecutiveDetachedOffers}");
      sb.AppendLine($"  Max consecutive offers without detached: {counts.MaxConsecutiveOffersWithoutDetached}");
      sb.AppendLine($"  Illegal candidates in offers: {counts.IllegalCandidates}");
      sb.AppendLine($"  Duplicate ids in same offer: {counts.DuplicateInOffer}");
      sb.AppendLine($"  Re-offered max-level ids: {counts.MaxLevelRepeats}");
      sb.AppendLine($"  Empty offers: {counts.EmptyOffers}");
      sb.AppendLine($"  Pity forced: {counts.PityForced}");
      sb.AppendLine($"  Fallback fills: {counts.FallbackFills}");
      return sb.ToString();
    }
  }
}
