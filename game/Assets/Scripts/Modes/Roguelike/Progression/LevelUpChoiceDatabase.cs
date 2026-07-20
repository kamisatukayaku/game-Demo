using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Archetypes.Warrior;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Shared.Core;
using Game.Shared.Projectile;
using Game.Shared.Gameplay.Bridges;

using Game.Shared.Data;
namespace Game.Modes.Roguelike.Progression
{
  public static class LevelUpChoiceDatabase
  {
    public const int DefaultChoicesPerLevel = 3;
    public const string RangedStarterId = "starter_ranged";
    public const string MageStarterId = "starter_mage";
    public const string ContactStarterId = "starter_contact";
    public const string SupportStarterId = "starter_support";

    static LevelUpConfig s_config;
    static readonly List<UpgradeDef> s_playerUpgrades = new();
    static readonly Dictionary<string, List<UpgradeDef>> s_classUpgrades = new();
    static bool s_loaded;
    static System.Random s_rng = new();

    static readonly UpgradeDef[] StarterChoices =
    {
      new UpgradeDef
      {
        tier = 1,
        route = "starter",
        id = RangedStarterId,
        display_name = "选择射手能力",
        description = "获得自动锁敌射击；攻击速度提高 10%，每次攻击额外发射 1 枚弹道。",
        modifiers = new[]
        {
          new StatModifier { stat = StatKeys.WeaponAttackSpeedMult, op = "add", value = 0.1f },
          new StatModifier { stat = StatKeys.WeaponExtraProjectile, op = "add", value = 1f }
        },
        classes = new[] { "all" },
        tags = new[] { "starter_ranged", "projectile", "rapid" },
        category = "gameplay",
        offer_weight = 1f
      },
      new UpgradeDef
      {
        tier = 1,
        route = "starter",
        id = MageStarterId,
        display_name = "选择法师能力",
        description = "获得自动释放的奥术飞弹，并解锁引力井与潮汐脉冲。",
        modifiers = new[]
        {
          new StatModifier { stat = StatKeys.SkillGravityWellUnlock, op = "add", value = 1f },
          new StatModifier { stat = StatKeys.SkillTidalPulseUnlock, op = "add", value = 1f }
        },
        classes = new[] { "all" },
        tags = new[] { "starter_mage", "arcane", "control" },
        category = "gameplay",
        offer_weight = 1f
      },
      new UpgradeDef
      {
        tier = 1,
        route = "starter",
        id = ContactStarterId,
        display_name = "接触构筑",
        description = "星环刃贴身旋转，并优先解锁外置接触武器进化。",
        modifiers = Array.Empty<StatModifier>(),
        classes = new[] { "all" },
        tags = new[] { "starter_contact", "contact", "dash", "melee", "orbit", "detached_weapon" },
        category = "gameplay",
        offer_weight = 1f
      },
      new UpgradeDef
      {
        tier = 1,
        route = "starter",
        id = SupportStarterId,
        display_name = "支援构筑",
        description = "解锁潮汐脉冲与击杀回复，强化控场与续航。",
        modifiers = new[]
        {
          new StatModifier { stat = StatKeys.HealOnKillPct, op = "add", value = 0.02f }
        },
        classes = new[] { "all" },
        tags = Array.Empty<string>(),
        category = "gameplay",
        offer_weight = 1f
      }
    };

    static LevelUpChoiceDatabase()
    {
      NormalizeStarterFallbacks();
    }

    static void NormalizeStarterFallbacks()
    {
      if (StarterChoices.Length < 3)
        return;

      StarterChoices[0].display_name = "射击核心";
      StarterChoices[0].description = "获得自动索敌射击；攻击速度提高 10%，每次攻击额外发射 1 条弹道。";
      StarterChoices[0].tags = new[] { "starter_ranged", "projectile", "rapid" };

      StarterChoices[1].display_name = "法术核心";
      StarterChoices[1].description = "解锁引力井与潮汐脉冲，获得聚怪、控制与范围压制能力。";
      StarterChoices[1].tags = new[] { "starter_mage", "arcane", "control", "gravity", "tidal" };

      StarterChoices[2].display_name = "冲刺星环";
      StarterChoices[2].description = "冲刺开始具备进攻价值，并获得 1 个可进化的外置星环武器。";
      StarterChoices[2].modifiers = new[]
      {
        new StatModifier { stat = "detached_part_count", op = "add", value = 1f },
        new StatModifier { stat = "detached_contact_level", op = "add", value = 1f }
      };
      StarterChoices[2].tags = new[] { "starter_contact", "contact", "dash", "melee", "orbit", "detached_weapon" };

      if (StarterChoices.Length > 3)
      {
        StarterChoices[3].id = "__deprecated_support_starter";
        StarterChoices[3].display_name = "已废弃";
        StarterChoices[3].description = "旧支援开局已废弃。";
        StarterChoices[3].modifiers = Array.Empty<StatModifier>();
        StarterChoices[3].tags = Array.Empty<string>();
        StarterChoices[3].offer_weight = 0f;
      }
    }

    public static XpCurveDef Curve
    {
      get
      {
        EnsureLoaded();
        return s_config?.level_curve ?? new XpCurveDef { xp_base = 60, xp_growth = 1.15f, max_level = 50 };
      }
    }

    public static SurvivalTuningDef SurvivalTuning
    {
      get
      {
        EnsureLoaded();
        return s_config?.survival_tuning ?? new SurvivalTuningDef();
      }
    }

    public static int ChoicesPerLevel
    {
      get
      {
        EnsureLoaded();
        return s_config?.offer_config != null && s_config.offer_config.choices_per_level > 0
          ? s_config.offer_config.choices_per_level
          : DefaultChoicesPerLevel;
      }
    }

    public static float AuxiliaryOfferChance
    {
      get
      {
        EnsureLoaded();
        var chance = s_config?.offer_config?.auxiliary_offer_chance ?? 0f;
        return chance > 0f ? chance : 0.25f;
      }
    }

    public static void ResetForTests()
    {
      s_loaded = false;
      s_config = null;
      s_playerUpgrades.Clear();
      s_classUpgrades.Clear();
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_playerUpgrades.Clear();
      s_classUpgrades.Clear();

      if (!JsonDataLoader.TryParse("progression/level_up_config", ParseConfig))
      {
        Debug.LogWarning("[LevelUpChoiceDatabase] level_up_config.json not found, trying legacy level_up_choices.");
        if (JsonDataLoader.TryParse("progression/level_up_choices", ParseLegacy))
          return;
        Debug.LogWarning("[LevelUpChoiceDatabase] No roguelike upgrade data loaded.");
        return;
      }

      if (s_config?.upgrade_files == null)
        return;

      foreach (var fileStem in s_config.upgrade_files)
      {
        if (string.IsNullOrEmpty(fileStem))
          continue;

        JsonDataLoader.TryParse(fileStem, json => ParseClassFile(fileStem, json));
      }

      UnifiedGrowthDatabase.EnsureLoaded();
      s_playerUpgrades.AddRange(UnifiedGrowthDatabase.All);

      WarriorProgressionDatabase.EnsureLoaded();
      if (WarriorProgressionDatabase.IsValid)
        s_classUpgrades["warrior"] = new List<UpgradeDef>(WarriorProgressionDatabase.Upgrades);

      Debug.Log(
        $"[LevelUpChoiceDatabase] Loaded player={s_playerUpgrades.Count}, " +
        $"melee={CountClass("melee")}, ranged={CountClass("ranged")}, " +
        $"mage={CountClass("mage")}, warrior={CountClass("warrior")}");
    }

    static int CountClass(string classId) =>
      s_classUpgrades.TryGetValue(classId, out var list) ? list.Count : 0;

    public static UpgradeDef FindById(string id)
    {
      if (string.IsNullOrEmpty(id))
        return null;
      if (id == SupportStarterId)
        return null;

      EnsureLoaded();

      foreach (var starter in StarterChoices)
      {
        if (starter.id == id)
          return starter;
      }

      foreach (var def in s_playerUpgrades)
      {
        if (def != null && def.id == id)
          return def;
      }

      foreach (var list in s_classUpgrades.Values)
      {
        if (list == null)
          continue;

        foreach (var def in list)
        {
          if (def != null && def.id == id)
            return def;
        }
      }

      foreach (var def in UnifiedGrowthDatabase.ExhaustionFallbacks)
      {
        if (def != null && def.id == id)
          return def;
      }

      return null;
    }

    public static List<UpgradeDef> GetExhaustionFallbackCandidates(
      IReadOnlyDictionary<string, int> pickStacks)
    {
      EnsureLoaded();
      var result = new List<UpgradeDef>();
      foreach (var u in UnifiedGrowthDatabase.ExhaustionFallbacks)
      {
        if (u == null || string.IsNullOrEmpty(u.id))
          continue;
        if (UpgradeEligibilityRules.IsBlockedByPickHistory(u, pickStacks))
          continue;
        result.Add(u);
      }

      return result;
    }

    /// <summary>
    /// 获取指定职业的所有升级条目（原始数据，未经过滤）?
    /// 用于预览面板等需要展示全量升级的场景?
    /// </summary>
    public static Dictionary<string, List<UpgradeDef>> GetAllUpgradesForClass(string playerClass)
    {
      EnsureLoaded();
      var result = new Dictionary<string, List<UpgradeDef>>();

      // 通用属性升级（所有职业共享）
      result["player"] = new List<UpgradeDef>(s_playerUpgrades);

      // equipment 升级：按 weaponTheme 匹配
      if (s_classUpgrades.TryGetValue(playerClass, out var eqList))
        result["equipment"] = new List<UpgradeDef>(eqList);

      // skill 升级：仅 mage 职业
      if (playerClass == "mage" && s_classUpgrades.TryGetValue("mage", out var skList))
        result["skill"] = new List<UpgradeDef>(skList);

      return result;
    }

    public static void Reseed(int seed) => s_rng = new System.Random(seed);

    public static string ResolveRoute(UpgradeDef def)
    {
      if (def == null)
        return null;

      if (!string.IsNullOrEmpty(def.route))
        return def.route;

      if (!string.IsNullOrEmpty(def.id))
      {
        if (def.id.StartsWith("pl_", StringComparison.Ordinal))
          return "player";
        if (def.id.StartsWith("sk_", StringComparison.Ordinal))
          return "skill";
      }

      if (!string.IsNullOrEmpty(def.weapon_theme))
        return "equipment";

      return "player";
    }

    public static string GetRouteDisplayName(string routeId)
    {
      EnsureLoaded();
      if (s_config?.routes != null)
      {
        foreach (var route in s_config.routes)
        {
          if (route != null && route.id == routeId)
            return route.display_name;
        }
      }

      return routeId switch
      {
        "starter" => "初始能力",
        "equipment" => "武装",
        "skill" => "技能",
        "player" => "属性",
        _ => routeId ?? ""
      };
    }

    public static List<UpgradeDef> GetCandidates(
      string routeType,
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks,
      string playerClass = null)
    {
      EnsureLoaded();
      var result = new List<UpgradeDef>();
      playerClass ??= weaponTheme;
      var arenaMode = ArenaLayoutLocator.Layout.IsActive;

      if (arenaMode && !IsArenaClassRouteAllowed(routeType, playerClass))
        return result;

      var source = GetSourceUpgrades(routeType, weaponTheme, playerClass);
      if (source == null || source.Count == 0)
        return result;

      var nextTier = routeType switch
      {
        "equipment" => equipmentTier + 1,
        "skill" => skillTier + 1,
        "player" => playerTier + 1,
        _ => 0
      };

      foreach (var u in source)
      {
        if (u == null || string.IsNullOrEmpty(u.id))
          continue;

        if (routeType == "equipment" && !string.IsNullOrEmpty(u.weapon_theme) && u.weapon_theme != weaponTheme)
          continue;

        if (!MatchesPlayerClass(u, playerClass))
          continue;

        if (IsBlockedArenaUpgrade(u, weaponTheme))
          continue;

        if (IsBlockedDetachedEnhancement(u))
          continue;

        if (DetachedWeaponSlotRules.IsEvolutionOfferBlocked(u))
          continue;

        if (EvolutionBuildGatesDatabase.IsUpgradeBlocked(u, ArenaBuildBootstrap.SelectedBuildId, pickStacks))
          continue;

        if (UpgradeEligibilityRules.IsBlockedByPickHistory(u, pickStacks))
          continue;

        if (UpgradeEligibilityRules.IsChainComplete(u, source, pickStacks))
          continue;

        if (HasPickedExclusiveGroup(u, source, pickStacks))
        {
          continue;
        }
        else if (u.tier > 0 && !arenaMode && routeType != "equipment" && u.tier != nextTier)
        {
          continue;
        }

        if (!UpgradeEligibilityRules.MeetsUpgradeRequirements(u, pickStacks))
          continue;

        if (u.min_wave > 0)
        {
          var wave = WaveDirector.Instance != null ? WaveDirector.Instance.CurrentWave : 0;
          if (wave < u.min_wave)
            continue;
        }

        if (u.min_level > 0 && ExperienceSystem.Level < u.min_level)
          continue;

        if (u.introduces_mechanic
            && !BuildProgressionState.HasMechanic(u.mechanic_id)
            && BuildProgressionState.MechanicCount >= BuildProgressionState.MechanicSlotCount)
          continue;

        if (!CheckTagRequirements(u))
          continue;

        if (!UpgradeEligibilityRules.MeetsStatPrerequisite(u.prerequisite))
          continue;

        if (IsDuplicateFlagUpgrade(u))
          continue;

        if (IsExhaustionFallbackUpgrade(u))
          continue;

        result.Add(u);
      }

      return result;
    }

    static bool HasPickedExclusiveGroup(
      UpgradeDef candidate,
      IReadOnlyList<UpgradeDef> source,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (candidate == null
          || string.IsNullOrEmpty(candidate.exclusive_group)
          || source == null
          || pickStacks == null)
        return false;

      foreach (var upgrade in source)
      {
        if (upgrade == null
            || upgrade.id == candidate.id
            || upgrade.exclusive_group != candidate.exclusive_group)
          continue;
        if (pickStacks.TryGetValue(upgrade.id, out var count) && count > 0)
          return true;
      }

      return false;
    }

    static bool IsArenaClassRouteAllowed(string routeType, string playerClass)
    {
      if (playerClass == "melee")
        return false;

      return routeType switch
      {
        "player" => true,
        "equipment" => playerClass is "ranged" or "warrior",
        "skill" => playerClass == "mage",
        _ => false
      };
    }

    static List<UpgradeDef> GetSourceUpgrades(string routeType, string weaponTheme, string playerClass)
    {
      if (routeType == "player")
        return s_playerUpgrades;

      var classId = routeType switch
      {
        "equipment" => weaponTheme,
        "skill" => "mage",
        _ => playerClass
      };

      return s_classUpgrades.TryGetValue(classId, out var list) ? list : null;
    }

    /// <summary>检查组合传奇升级的标签前置条件。玩家必须已拥有 requires_tags 中的所有标签?/summary>
    static bool MatchesUnifiedClassEntitlement(string classId) => classId switch
    {
      "ranged" => RunBuildState.HasTag("projectile"),
      "warrior" => RunBuildState.HasTag("orbit"),
      "mage" => RunBuildState.HasTag("spell")
             || RunBuildState.HasTag("gravity")
             || RunBuildState.HasTag("tidal"),
      _ => true
    };

    static bool CheckTagRequirements(UpgradeDef def)
    {
      if (def == null)
        return true;

      foreach (var tag in def.requires_tags ?? Array.Empty<string>())
      {
        if (string.IsNullOrEmpty(tag))
          continue;

        if (!RunBuildState.HasTag(tag))
          return false;
      }

      return string.IsNullOrEmpty(def.requires_tag)
             || RunBuildState.GetTagStack(def.requires_tag) >= Mathf.Max(1, def.requires_tag_stacks);
    }

    static bool IsBlockedArenaUpgrade(UpgradeDef def, string weaponTheme)
    {
      if (!ArenaLayoutLocator.Layout.IsActive || def == null)
        return false;

      return RunBuildState.GetProjectileWeakHoming() > 0.5f
             && UpgradeGrantsAnyStat(def, "projectile_weak_homing");
    }

    static bool IsBlockedDetachedEnhancement(UpgradeDef def)
    {
      if (def == null || DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement())
        return false;

      if (HasUpgradeTag(def, "part_spawn"))
        return false;

      if (HasUpgradeTag(def, "detached_weapon"))
        return true;

      if (!string.IsNullOrEmpty(def.id)
          && def.id.StartsWith("num_part_", StringComparison.Ordinal))
        return true;

      return false;
    }

    static bool IsExhaustionFallbackUpgrade(UpgradeDef def) =>
      HasUpgradeTag(def, "exhaustion_fallback");

    static bool HasUpgradeTag(UpgradeDef def, string tag)
    {
      if (def?.tags == null || string.IsNullOrEmpty(tag))
        return false;

      foreach (var t in def.tags)
      {
        if (t == tag)
          return true;
      }

      return false;
    }

    static bool UpgradeGrantsAnyStat(UpgradeDef def, params string[] stats)
    {
      if (def?.modifiers == null || stats == null || stats.Length == 0)
        return false;

      foreach (var mod in def.modifiers)
      {
        if (mod == null || string.IsNullOrEmpty(mod.stat))
          continue;

        foreach (var stat in stats)
        {
          if (mod.stat == stat)
            return true;
        }
      }

      return false;
    }

    static bool MatchesPlayerClass(UpgradeDef def, string playerClass)
    {
      if (def?.classes == null || def.classes.Length == 0)
        return InferLegacyClassMatch(def, playerClass);

      foreach (var c in def.classes)
      {
        if (c == "all" || c == playerClass)
          return true;
      }

      return false;
    }

    static bool InferLegacyClassMatch(UpgradeDef def, string playerClass)
    {
      if (def == null || string.IsNullOrEmpty(playerClass))
        return true;

      var route = ResolveRoute(def);
      return route switch
      {
        "skill" => playerClass == "mage",
        "equipment" => def.weapon_theme == playerClass,
        _ => true
      };
    }

    static bool IsDuplicateFlagUpgrade(UpgradeDef def)
    {
      if (def?.modifiers == null)
        return false;

      foreach (var mod in def.modifiers)
      {
        if (mod == null || string.IsNullOrEmpty(mod.stat))
          continue;

        switch (mod.stat)
        {
          case "projectile_weak_homing" when RunBuildState.GetProjectileWeakHoming() > 0.5f:
          case "projectile_heavy_shot" when RunBuildState.GetProjectileHeavyShot() > 0.5f:
          case "projectile_side_shed" when RunBuildState.GetProjectileSideShed() > 0.5f:
            return true;
        }
      }

      return false;
    }

    public static LevelUpOffer BuildOffer(
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      var arenaMode = ArenaLayoutLocator.Layout.IsActive;

      if (arenaMode)
        return BuildArenaOffer(weaponTheme, equipmentTier, skillTier, playerTier, pickStacks);

      return BuildExploreOffer(weaponTheme, equipmentTier, skillTier, playerTier, pickStacks);
    }

    static LevelUpOffer BuildArenaOffer(
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (IsUnifiedArenaRun(weaponTheme))
        return BuildUnifiedArenaOffer(weaponTheme, equipmentTier, skillTier, playerTier, pickStacks);

      if (!HasStarterChoice(pickStacks))
        return new LevelUpOffer { choices = Array.Empty<UpgradeDef>() };

      var total = ChoicesPerLevel;
      var picked = new List<UpgradeDef>();
      var pickedIds = new HashSet<string>();

      var classRoute = weaponTheme switch
      {
        "ranged" or "warrior" => "equipment",
        "mage" => "skill",
        _ => null
      };

      var classPool = classRoute != null
        ? GetCandidates(classRoute, weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme)
        : new List<UpgradeDef>();
      var playerPool = GetCandidates("player", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme);

      var all = new List<UpgradeDef>();
      all.AddRange(classPool);
      all.AddRange(playerPool);

      var groupPools = new Dictionary<UpgradeOfferGroup, List<UpgradeDef>>
      {
        [UpgradeOfferGroup.Gameplay] = new(),
        [UpgradeOfferGroup.Player] = new(),
        [UpgradeOfferGroup.Detached] = new(),
        [UpgradeOfferGroup.Numeric] = new()
      };

      foreach (var def in all)
      {
        var group = UpgradeOfferGroupPolicy.Resolve(def);
        groupPools[group].Add(def);
      }

      UpgradeOfferBuildTelemetry.RecordPoolSnapshot(
        groupPools[UpgradeOfferGroup.Gameplay].Count,
        groupPools[UpgradeOfferGroup.Player].Count,
        groupPools[UpgradeOfferGroup.Detached].Count,
        groupPools[UpgradeOfferGroup.Numeric].Count);

      var weightContext = CreateWeightContext(pickStacks);
      var hasDetached = DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement();
      var cfg = s_config?.offer_config?.group_targets;
      var targets = UpgradeOfferBudgetPolicy.ResolveTargets(
        hasDetached,
        cfg?.gameplay ?? 0.40f,
        cfg?.player ?? 0.25f,
        cfg?.detached ?? 0.20f,
        cfg?.numeric ?? 0.15f);
      var slotPlan = UpgradeOfferBudgetPolicy.BuildSlotPlan(
        s_rng,
        total,
        targets,
        UpgradeOfferPityTracker.ShouldForcePlayerSlot(),
        groupPools);

      foreach (var group in slotPlan)
      {
        if (picked.Count >= total)
          break;
        if (UpgradeFallbackPolicy.TryPickFromGroupOnly(s_rng, group, groupPools, picked, pickedIds, weightContext))
          continue;

        var plannedCounts = UpgradeOfferBudgetPolicy.CountPlannedGroups(slotPlan);
        var actualCounts = UpgradeOfferBudgetPolicy.CountActualGroups(picked);
        var filled = false;
        foreach (var substitute in UpgradeOfferBudgetPolicy.GetBudgetSubstitutes(group, hasDetached))
        {
          if (actualCounts.GetValueOrDefault(substitute) >= plannedCounts.GetValueOrDefault(substitute))
            continue;
          if (!UpgradeFallbackPolicy.TryPickFromGroupOnly(
                s_rng, substitute, groupPools, picked, pickedIds, weightContext))
            continue;
          UpgradeOfferBuildTelemetry.RecordFallbackFill();
          filled = true;
          break;
        }

        if (filled)
          continue;
      }

      UpgradeOfferBudgetPolicy.FillDeficits(
        s_rng,
        total,
        picked,
        pickedIds,
        groupPools,
        weightContext,
        slotPlan,
        targets,
        hasDetached);

      UpgradeOfferDiversityPolicy.EnsureOfferDiversity(s_rng, picked, pickedIds, all, weightContext);

      AuxiliaryOfferPolicy.TryInjectAuxiliaryUpgrade(
        s_rng,
        weaponTheme,
        picked,
        pickedIds,
        groupPools[UpgradeOfferGroup.Numeric],
        weightContext);

      EnsureChoiceCount(total, weaponTheme, picked, pickedIds, all, weightContext);

      RemoveChoicesWithLockedPrerequisites(picked, pickStacks);

      var offer = new LevelUpOffer { choices = picked.ToArray() };
      UpgradeOfferPityTracker.OnOfferBuilt(offer);
      return offer;
    }

    static bool IsUnifiedArenaRun(string weaponTheme) =>
      weaponTheme == UnifiedBuildBootstrap.WeaponTheme || ArenaBuildBootstrap.IsUnifiedBuild;

    static LevelUpOffer BuildUnifiedArenaOffer(
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      var total = ChoicesPerLevel;
      var picked = new List<UpgradeDef>();
      var pickedIds = new HashSet<string>();
      var all = CollectUnifiedCandidates(weaponTheme, equipmentTier, skillTier, playerTier, pickStacks);

      var groupPools = new Dictionary<UpgradeOfferGroup, List<UpgradeDef>>
      {
        [UpgradeOfferGroup.Gameplay] = new(),
        [UpgradeOfferGroup.Player] = new(),
        [UpgradeOfferGroup.Detached] = new(),
        [UpgradeOfferGroup.Numeric] = new()
      };

      foreach (var def in all)
      {
        var group = UpgradeOfferGroupPolicy.Resolve(def);
        groupPools[group].Add(def);
      }

      UpgradeOfferBuildTelemetry.RecordPoolSnapshot(
        groupPools[UpgradeOfferGroup.Gameplay].Count,
        groupPools[UpgradeOfferGroup.Player].Count,
        groupPools[UpgradeOfferGroup.Detached].Count,
        groupPools[UpgradeOfferGroup.Numeric].Count);

      var weightContext = CreateWeightContext(pickStacks);
      var hasDetached = DetachedWeaponSpawnRules.HasDetachedWeaponEntitlement();
      var cfg = s_config?.offer_config?.group_targets;
      var targets = UpgradeOfferBudgetPolicy.ResolveTargets(
        hasDetached,
        cfg?.gameplay ?? 0.40f,
        cfg?.player ?? 0.25f,
        cfg?.detached ?? 0.20f,
        cfg?.numeric ?? 0.15f);
      var slotPlan = UpgradeOfferBudgetPolicy.BuildSlotPlan(
        s_rng,
        total,
        targets,
        UpgradeOfferPityTracker.ShouldForcePlayerSlot(),
        groupPools);

      foreach (var group in slotPlan)
      {
        if (picked.Count >= total)
          break;
        if (UpgradeFallbackPolicy.TryPickFromGroupOnly(s_rng, group, groupPools, picked, pickedIds, weightContext))
          continue;

        var plannedCounts = UpgradeOfferBudgetPolicy.CountPlannedGroups(slotPlan);
        var actualCounts = UpgradeOfferBudgetPolicy.CountActualGroups(picked);
        var filled = false;
        foreach (var substitute in UpgradeOfferBudgetPolicy.GetBudgetSubstitutes(group, hasDetached))
        {
          if (actualCounts.GetValueOrDefault(substitute) >= plannedCounts.GetValueOrDefault(substitute))
            continue;
          if (!UpgradeFallbackPolicy.TryPickFromGroupOnly(
                s_rng, substitute, groupPools, picked, pickedIds, weightContext))
            continue;
          UpgradeOfferBuildTelemetry.RecordFallbackFill();
          filled = true;
          break;
        }

        if (filled)
          continue;
      }

      UpgradeOfferBudgetPolicy.FillDeficits(
        s_rng,
        total,
        picked,
        pickedIds,
        groupPools,
        weightContext,
        slotPlan,
        targets,
        hasDetached);

      UpgradeOfferDiversityPolicy.EnsureOfferDiversity(s_rng, picked, pickedIds, all, weightContext);
      FoundationOfferPolicy.EnsureEarlyBuildChoices(s_rng, picked, pickedIds, all, weightContext);

      AuxiliaryOfferPolicy.TryInjectAuxiliaryUpgrade(
        s_rng,
        weaponTheme,
        picked,
        pickedIds,
        groupPools[UpgradeOfferGroup.Numeric],
        weightContext);

      EnsureChoiceCount(total, weaponTheme, picked, pickedIds, all, weightContext);

      RemoveChoicesWithLockedPrerequisites(picked, pickStacks);

      var offer = new LevelUpOffer { choices = picked.ToArray() };
      UpgradeOfferPityTracker.OnOfferBuilt(offer);
      return offer;
    }

    static void RemoveChoicesWithLockedPrerequisites(
      List<UpgradeDef> choices,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (choices == null)
        return;

      for (var i = choices.Count - 1; i >= 0; i--)
      {
        var choice = choices[i];
        if (choice == null
            || !UpgradeEligibilityRules.MeetsUpgradeRequirements(choice, pickStacks)
            || !CheckTagRequirements(choice))
          choices.RemoveAt(i);
      }
    }

    static void EnsureChoiceCount(
      int total,
      string weaponTheme,
      List<UpgradeDef> picked,
      HashSet<string> pickedIds,
      List<UpgradeDef> legalCandidates,
      UpgradeOfferWeightPolicy.WeightContext weightContext)
    {
      var guard = 0;
      while (picked.Count < total && guard++ < total * 4)
      {
        var before = picked.Count;
        UpgradeWeightedPicker.PickWeightedUnique(
          s_rng, legalCandidates, 1, picked, pickedIds, weightContext);

        if (picked.Count == before)
          UpgradeFallbackPolicy.TryPickFromAllRoutes(
            s_rng, weaponTheme, picked, pickedIds, weightContext);

        if (picked.Count == before)
          UpgradeFallbackPolicy.TryPickExhaustionFallback(
            s_rng, picked, pickedIds, weightContext);

        if (picked.Count == before)
          break;
      }

      if (picked.Count == 0)
        UpgradeOfferBuildTelemetry.RecordEmptyOffer();
    }

    static List<UpgradeDef> CollectUnifiedCandidates(
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      var all = new List<UpgradeDef>();
      all.AddRange(GetCandidates("player", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme));
      all.AddRange(GetCandidatesForClass("equipment", "ranged", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks));
      all.AddRange(GetCandidatesForClass("equipment", "warrior", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks));
      all.AddRange(GetCandidatesForClass("skill", "mage", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks));
      return all;
    }

    static List<UpgradeDef> GetCandidatesForClass(
      string routeType,
      string classId,
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      EnsureLoaded();
      var result = new List<UpgradeDef>();
      var source = s_classUpgrades.TryGetValue(classId, out var list) ? list : null;
      if (source == null || source.Count == 0)
        return result;

      foreach (var u in source)
      {
        if (u == null || string.IsNullOrEmpty(u.id))
          continue;

        if (routeType == "equipment"
            && !string.IsNullOrEmpty(u.weapon_theme)
            && u.weapon_theme != classId)
          continue;

        if (IsUnifiedArenaRun(weaponTheme) && !MatchesUnifiedClassEntitlement(classId))
          continue;

        if (IsBlockedArenaUpgrade(u, weaponTheme))
          continue;

        if (IsBlockedDetachedEnhancement(u))
          continue;

        if (DetachedWeaponSlotRules.IsEvolutionOfferBlocked(u))
          continue;

        if (EvolutionBuildGatesDatabase.IsUpgradeBlocked(u, ArenaBuildBootstrap.SelectedBuildId, pickStacks))
          continue;

        if (UpgradeEligibilityRules.IsBlockedByPickHistory(u, pickStacks))
          continue;

        if (UpgradeEligibilityRules.IsChainComplete(u, source, pickStacks))
          continue;

        if (HasPickedExclusiveGroup(u, source, pickStacks))
          continue;

        if (!UpgradeEligibilityRules.MeetsUpgradeRequirements(u, pickStacks))
          continue;

        if (u.min_wave > 0)
        {
          var wave = WaveDirector.Instance != null ? WaveDirector.Instance.CurrentWave : 0;
          if (wave < u.min_wave)
            continue;
        }

        if (u.min_level > 0 && ExperienceSystem.Level < u.min_level)
          continue;

        if (u.introduces_mechanic
            && !BuildProgressionState.HasMechanic(u.mechanic_id)
            && BuildProgressionState.MechanicCount >= BuildProgressionState.MechanicSlotCount)
          continue;

        if (!CheckTagRequirements(u))
          continue;

        if (!UpgradeEligibilityRules.MeetsStatPrerequisite(u.prerequisite))
          continue;

        if (IsDuplicateFlagUpgrade(u))
          continue;

        if (IsExhaustionFallbackUpgrade(u))
          continue;

        result.Add(u);
      }

      return result;
    }

    static List<UpgradeOfferGroup> BuildGroupSlotPlan(int total, OfferGroupTargets targets, bool forcePlayer)
    {
      var plan = new List<UpgradeOfferGroup>();
      var weights = new List<(UpgradeOfferGroup group, float weight)>
      {
        (UpgradeOfferGroup.Gameplay, Mathf.Max(0.01f, targets.gameplay)),
        (UpgradeOfferGroup.Player, Mathf.Max(0.01f, targets.player)),
        (UpgradeOfferGroup.Detached, Mathf.Max(0.01f, targets.detached)),
        (UpgradeOfferGroup.Numeric, Mathf.Max(0.01f, targets.numeric))
      };

      if (forcePlayer)
        plan.Add(UpgradeOfferGroup.Player);

      while (plan.Count < total)
      {
        var roll = (float)s_rng.NextDouble();
        var sum = 0f;
        foreach (var entry in weights)
          sum += entry.weight;
        roll *= sum;

        UpgradeOfferGroup chosen = weights[weights.Count - 1].group;
        foreach (var entry in weights)
        {
          roll -= entry.weight;
          if (roll <= 0f)
          {
            chosen = entry.group;
            break;
          }
        }

        if (plan.Count > 0 && plan[plan.Count - 1] == chosen && plan.Count >= 2 && plan[^2] == chosen)
          continue;

        plan.Add(chosen);
      }

      return plan;
    }

    static UpgradeOfferWeightPolicy.WeightContext CreateWeightContext(IReadOnlyDictionary<string, int> pickStacks)
    {
      var config = s_config?.offer_config;
      return new UpgradeOfferWeightPolicy.WeightContext(
        pickStacks,
        config?.gameplay_weight ?? 1.35f,
        config?.attribute_weight ?? 0.28f,
        config?.build_tag_bonus_per_stack ?? 0.55f,
        config?.chain_continuation_bonus ?? 2.4f,
        BuildMovieArcDirector.CapstoneWeightBoost,
        ArenaNarrativeEventDirector.CapstoneBoostActive,
        RunBuildState.GetTagStack,
        MatchesActiveCapstone);
    }

    static bool MatchesActiveCapstone(UpgradeDef def)
    {
      if (def?.tags == null)
        return false;

      var arc = BuildMovieArcDatabase.GetForBuild(ArenaBuildBootstrap.SelectedBuildId);
      if (arc?.capstone_tags != null)
      {
        foreach (var tag in def.tags)
        foreach (var capstoneTag in arc.capstone_tags)
          if (!string.IsNullOrEmpty(tag) && tag == capstoneTag)
            return true;
      }

      return UpgradeOfferWeightPolicy.BuildHasDetachedWeaponFocus(def);
    }

    static bool HasStarterChoice(IReadOnlyDictionary<string, int> pickStacks)
    {
      if (pickStacks == null)
        return false;

      return HasStarterPick(pickStacks, RangedStarterId)
             || HasStarterPick(pickStacks, MageStarterId)
             || HasStarterPick(pickStacks, ContactStarterId);
    }

    static bool HasStarterPick(IReadOnlyDictionary<string, int> pickStacks, string id) =>
      pickStacks.TryGetValue(id, out var count) && count > 0;

    static LevelUpOffer BuildExploreOffer(
      string weaponTheme,
      int equipmentTier,
      int skillTier,
      int playerTier,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      var eqCandidates = GetCandidates("equipment", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme);
      var skCandidates = GetCandidates("skill", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme);
      var plCandidates = GetCandidates("player", weaponTheme, equipmentTier, skillTier, playerTier, pickStacks, weaponTheme);

      var routePools = new List<(string route, List<UpgradeDef> list)>
      {
        ("equipment", eqCandidates),
        ("skill", skCandidates),
        ("player", plCandidates)
      };

      for (int i = routePools.Count - 1; i > 0; i--)
      {
        var j = s_rng.Next(i + 1);
        (routePools[i], routePools[j]) = (routePools[j], routePools[i]);
      }

      var picked = new List<UpgradeDef>();
      var total = ChoicesPerLevel;

      foreach (var (_, list) in routePools)
      {
        if (picked.Count >= total) break;
        if (list.Count == 0) continue;

        var idx = s_rng.Next(list.Count);
        picked.Add(list[idx]);
        list.RemoveAt(idx);
      }

      if (picked.Count < total)
      {
        var remaining = new List<UpgradeDef>();
        foreach (var (_, list) in routePools)
          remaining.AddRange(list);

        var pickedIds = new HashSet<string>();
        foreach (var p in picked)
          if (!string.IsNullOrEmpty(p.id))
            pickedIds.Add(p.id);

        var filtered = new List<UpgradeDef>();
        foreach (var r in remaining)
        {
          if (r == null || pickedIds.Contains(r.id)) continue;
          filtered.Add(r);
        }

        while (picked.Count < total && filtered.Count > 0)
        {
          var idx = s_rng.Next(filtered.Count);
          picked.Add(filtered[idx]);
          filtered.RemoveAt(idx);
        }
      }

      return new LevelUpOffer { choices = picked.ToArray() };
    }

    static LevelUpOffer PickFromPool(List<UpgradeDef> pool, int count)
    {
      var picked = new List<UpgradeDef>();
      var working = new List<UpgradeDef>(pool);
      while (picked.Count < count && working.Count > 0)
      {
        var idx = s_rng.Next(working.Count);
        picked.Add(working[idx]);
        working.RemoveAt(idx);
      }

      return new LevelUpOffer { choices = picked.ToArray() };
    }

    static void ParseConfig(string json)
    {
      try
      {
        s_config = JsonUtility.FromJson<LevelUpConfig>(json);
      }
      catch (Exception e)
      {
        Debug.LogError($"[LevelUpChoiceDatabase] Config parse failed: {e.Message}");
      }
    }

    static void ParseClassFile(string stem, string json)
    {
      try
      {
        var file = JsonUtility.FromJson<ClassUpgradeFile>(json);
        if (file?.upgrades == null)
          return;

        if (stem.EndsWith("player_upgrades", StringComparison.Ordinal))
        {
          NormalizeUpgradeMetadata(file.upgrades, file.class_id);
          s_playerUpgrades.AddRange(file.upgrades);
          return;
        }

        var classId = file.class_id;
        if (string.IsNullOrEmpty(classId))
          return;

        if (!s_classUpgrades.TryGetValue(classId, out var list))
        {
          list = new List<UpgradeDef>();
          s_classUpgrades[classId] = list;
        }

        NormalizeUpgradeMetadata(file.upgrades, classId);
        list.AddRange(file.upgrades);
      }
      catch (Exception e)
      {
        Debug.LogError($"[LevelUpChoiceDatabase] Parse {stem} failed: {e.Message}");
      }
    }

    static void NormalizeUpgradeMetadata(UpgradeDef[] upgrades, string classId)
    {
      if (upgrades == null)
        return;

      foreach (var def in upgrades)
      {
        if (def == null)
          continue;

        if (def.tags == null || def.tags.Length == 0)
          def.tags = InferTags(def, classId);

        if (string.IsNullOrEmpty(def.category))
          def.category = classId == "player" && def.repeatable
            && (def.requires_ids == null || def.requires_ids.Length == 0)
            ? "attribute"
            : "gameplay";
      }
    }

    static string[] InferTags(UpgradeDef def, string classId)
    {
      var id = def?.id ?? "";
      if (classId == "ranged")
      {
        if (RangedLegacyUpgradeMigration.TryInferTags(id, out var legacyTags))
          return legacyTags;

        if (id.StartsWith("eq_ranged_sp_", StringComparison.Ordinal))
          return new[] { "projectile", "spread", "volley" };
        if (id.StartsWith("eq_ranged_pc_", StringComparison.Ordinal))
          return new[] { "projectile", "pierce", "line_clear" };
        if (id.StartsWith("eq_ranged_ex_", StringComparison.Ordinal))
          return new[] { "projectile", "explosion", "auxiliary_shot" };
        if (id.StartsWith("eq_ranged_lt_", StringComparison.Ordinal))
          return new[] { "projectile", "lightning", "chain", "auxiliary_shot" };
      }

      if (classId == "mage")
      {
        if (id.StartsWith("mg_ar_", StringComparison.Ordinal)) return new[] { "arcane", "projectile" };
        if (id.StartsWith("mg_el_", StringComparison.Ordinal)) return new[] { "fire", "ice", "lightning" };
        if (id.StartsWith("mg_gr_", StringComparison.Ordinal)) return new[] { "gravity", "zone" };
        if (id.StartsWith("mg_tm_", StringComparison.Ordinal)) return new[] { "time", "ice" };
        if (id.StartsWith("mg_core_", StringComparison.Ordinal)) return new[] { "mage" };
      }

      return new[] { classId == "player" ? "attribute" : classId };
    }

    static void ParseLegacy(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<LegacyLevelUpRoot>(json);
        s_config = new LevelUpConfig
        {
          level_curve = root.level_curve,
          offer_config = root.offer_config,
          routes = root.routes
        };

        if (root.player_upgrades != null)
          s_playerUpgrades.AddRange(root.player_upgrades);

        AddLegacyClass("melee", root.equipment_upgrades, "melee");
        AddLegacyClass("ranged", root.equipment_upgrades, "ranged");
        if (root.skill_upgrades != null)
          s_classUpgrades["mage"] = new List<UpgradeDef>(root.skill_upgrades);
      }
      catch (Exception e)
      {
        Debug.LogError($"[LevelUpChoiceDatabase] Legacy parse failed: {e.Message}");
      }
    }

    static void AddLegacyClass(string classId, UpgradeDef[] equipment, string themeFilter)
    {
      if (equipment == null)
        return;

      var list = new List<UpgradeDef>();
      foreach (var u in equipment)
      {
        if (u != null && u.weapon_theme == themeFilter)
          list.Add(u);
      }

      if (list.Count > 0)
        s_classUpgrades[classId] = list;
    }

    public struct LevelUpOffer
    {
      public UpgradeDef[] choices;
      public bool HasAny => choices != null && choices.Length > 0;
    }

    [Serializable]
    class LevelUpConfig
    {
      public XpCurveDef level_curve;
      public OfferConfigDef offer_config;
      public SurvivalTuningDef survival_tuning;
      public RouteDef[] routes;
      public string[] upgrade_files;
    }

    [Serializable]
    class ClassUpgradeFile
    {
      public string class_id;
      public string route;
      public UpgradeDef[] upgrades;
    }

    [Serializable]
    class LegacyLevelUpRoot
    {
      public XpCurveDef level_curve;
      public OfferConfigDef offer_config;
      public RouteDef[] routes;
      public UpgradeDef[] equipment_upgrades;
      public UpgradeDef[] skill_upgrades;
      public UpgradeDef[] player_upgrades;
    }

    [Serializable]
    class OfferGroupTargetsDef
    {
      public float gameplay = 0.40f;
      public float player = 0.25f;
      public float detached = 0.20f;
      public float numeric = 0.15f;
    }

    readonly struct OfferGroupTargets
    {
      public readonly float gameplay;
      public readonly float player;
      public readonly float detached;
      public readonly float numeric;

      public OfferGroupTargets(float gameplay, float player, float detached, float numeric)
      {
        this.gameplay = gameplay;
        this.player = player;
        this.detached = detached;
        this.numeric = numeric;
      }

      public OfferGroupTargets(OfferGroupTargetsDef def)
      {
        gameplay = def?.gameplay ?? 0.40f;
        player = def?.player ?? 0.25f;
        detached = def?.detached ?? 0.20f;
        numeric = def?.numeric ?? 0.15f;
      }
    }

    [Serializable]
    class OfferConfigDef
    {
      public int choices_per_level = DefaultChoicesPerLevel;
      public float auxiliary_offer_chance = 0.25f;
      public float gameplay_weight = 1.35f;
      public float attribute_weight = 0.28f;
      public float build_tag_bonus_per_stack = 0.55f;
      public float chain_continuation_bonus = 2.4f;
      public OfferGroupTargetsDef group_targets;
    }

    [Serializable]
    class RouteDef
    {
      public string id;
      public string display_name;
    }

    [Serializable]
    public class SurvivalTuningDef
    {
      public float heal_on_kill_budget_per_second = 4f;
      public float heal_on_kill_budget_window_seconds = 1f;
    }

    [Serializable]
    public class XpCurveDef
    {
      public int xp_base = 60;
      public float xp_growth = 1.15f;
      public int max_level = 50;
    }

    [Serializable]
    public class UpgradeDef
    {
      public int tier;
      public string route;
      public string weapon_theme;
      public string id;
      public string display_name;
      public string description;
      public bool repeatable;
      public int max_stacks;
      public StatModifier[] modifiers;
      public PrerequisiteDef prerequisite;
      public string[] requires_ids;
      public string[] requires_any_ids;
      public string[] classes;
      public string category;
      public string offer_group;
      public float offer_weight;
      public string exclusive_group;
      public string requires_tag;
      public int requires_tag_stacks;
      public string[] tags;                // 天赋树标签，妀""survival", "berserk", "critical"
      public string[] requires_tags;       // 组合传奇升级的前置标签要汀"
      public string mechanic_id;
      public bool introduces_mechanic;
      public int min_wave;
      public int min_level;
    }

    [Serializable]
    public class StatModifier
    {
      public string stat;
      public string op;
      public float value;
    }

    [Serializable]
    public class PrerequisiteDef
    {
      public string stat;
      public string op;
      public float value;
    }
  }
}
