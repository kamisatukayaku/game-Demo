namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  /// <summary>Guarantees player-body upgrades appear regularly in level-up offers.</summary>
  public static class UpgradeOfferPityTracker
  {
    const int ForcePlayerAfterMisses = 2;

    static int s_offersWithoutPlayerCard;

    public static void ResetForNewRun() => s_offersWithoutPlayerCard = 0;

    public static bool ShouldForcePlayerSlot() =>
      s_offersWithoutPlayerCard >= ForcePlayerAfterMisses;

    public static void OnOfferBuilt(LevelUpChoiceDatabase.LevelUpOffer offer)
    {
      var hadPlayer = false;
      if (offer.choices != null)
      {
        foreach (var choice in offer.choices)
        {
          if (UpgradeOfferGroupPolicy.Resolve(choice) == UpgradeOfferGroup.Player)
          {
            hadPlayer = true;
            break;
          }
        }
      }

      if (hadPlayer)
        s_offersWithoutPlayerCard = 0;
      else
        s_offersWithoutPlayerCard++;
    }

    public static void OnPlayerUpgradePicked() => s_offersWithoutPlayerCard = 0;

    public static int OffersWithoutPlayerCard => s_offersWithoutPlayerCard;

    public static int LevelsWithoutPlayerOffer => OffersWithoutPlayerCard;
  }
}
