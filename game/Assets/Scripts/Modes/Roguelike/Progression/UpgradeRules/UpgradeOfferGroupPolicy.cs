using System;

namespace Game.Modes.Roguelike.Progression.UpgradeRules
{
  public enum UpgradeOfferGroup
  {
    Gameplay,
    Player,
    Detached,
    Numeric
  }

  /// <summary>Classifies upgrades for category-budget offer building.</summary>
  public static class UpgradeOfferGroupPolicy
  {
    public static UpgradeOfferGroup Resolve(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return UpgradeOfferGroup.Gameplay;

      if (!string.IsNullOrEmpty(def.offer_group))
      {
        return def.offer_group switch
        {
          "player" => UpgradeOfferGroup.Player,
          "detached" => UpgradeOfferGroup.Detached,
          "numeric" => UpgradeOfferGroup.Numeric,
          "gameplay" => UpgradeOfferGroup.Gameplay,
          _ => InferLegacyGroup(def)
        };
      }

      if (def.id != null && def.id.StartsWith("evo_", StringComparison.OrdinalIgnoreCase))
        return UpgradeOfferGroup.Detached;

      return InferLegacyGroup(def);
    }

    static UpgradeOfferGroup InferLegacyGroup(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (UpgradeOfferWeightPolicy.IsDetachedWeaponUpgrade(def))
        return UpgradeOfferGroup.Detached;
      if (string.Equals(def.category, "player", StringComparison.OrdinalIgnoreCase))
        return UpgradeOfferGroup.Player;
      if (string.Equals(def.category, "numeric", StringComparison.OrdinalIgnoreCase)
          || string.Equals(def.category, "attribute", StringComparison.OrdinalIgnoreCase))
        return UpgradeOfferGroup.Numeric;
      if (string.Equals(def.category, "mechanic", StringComparison.OrdinalIgnoreCase)
          || def.introduces_mechanic)
        return UpgradeOfferGroup.Gameplay;
      return UpgradeOfferGroup.Gameplay;
    }

    public static bool IsPlayerUpgrade(LevelUpChoiceDatabase.UpgradeDef def) =>
      Resolve(def) == UpgradeOfferGroup.Player;

    public static string GetDisplayLabel(string offerGroup) => offerGroup switch
    {
      "player" => "角色强化",
      "gameplay" => "职业强化",
      "detached" => "外置武器强化",
      "numeric" => "数值强化",
      _ => "通用"
    };

    public static string GetDisplayLabel(UpgradeOfferGroup group) => group switch
    {
      UpgradeOfferGroup.Player => "角色强化",
      UpgradeOfferGroup.Gameplay => "职业强化",
      UpgradeOfferGroup.Detached => "外置武器强化",
      UpgradeOfferGroup.Numeric => "数值强化",
      _ => "通用"
    };
  }
}
