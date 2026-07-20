using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>Adapts numeric lines and detached-weapon evolutions to the common offer model.</summary>
  public static class UnifiedGrowthDatabase
  {
    static readonly List<LevelUpChoiceDatabase.UpgradeDef> Upgrades = new();
    static bool s_loaded;

    public static IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> All
    {
      get { EnsureLoaded(); return Upgrades; }
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      Upgrades.Clear();
      ExhaustionUpgrades.Clear();

      LoadNumeric("upgrades/pools/CommonStats");
      LoadNumeric("upgrades/pools/PlayerStats");
      LoadNumeric("upgrades/pools/MageStats");
      LoadNumeric("upgrades/pools/RangerStats");
      LoadNumeric("upgrades/pools/PartStats");
      LoadNumeric("upgrades/pools/ExhaustionRewards", exhaustionOnly: true);
      LoadMechanics("upgrades/pools/Mechanics");
      LoadMechanics("upgrades/pools/DashMeleeMechanics");
      LoadMechanics("upgrades/pools/OrbitBladeMechanics");
      LoadMechanics("upgrades/pools/Foundation");

      foreach (var id in new[] { "laser", "missile", "explosion", "pulse", "boomerang", "trail" })
        JsonDataLoader.TryParse($"weapons/evolutions/{id}_evolution", ParseEvolution);
    }

    static readonly List<LevelUpChoiceDatabase.UpgradeDef> ExhaustionUpgrades = new();

    public static IReadOnlyList<LevelUpChoiceDatabase.UpgradeDef> ExhaustionFallbacks
    {
      get { EnsureLoaded(); return ExhaustionUpgrades; }
    }

    static void LoadNumeric(string path, bool exhaustionOnly = false) =>
      JsonDataLoader.TryParse(path, json => ParseNumeric(path, json, exhaustionOnly));

    static void LoadMechanics(string path) =>
      JsonDataLoader.TryParse(path, json => ParseUpgradeFile(path, json));

    static void ParseUpgradeFile(string path, string json)
    {
      var root = JsonUtility.FromJson<UpgradeRoot>(json);
      if (root?.upgrades == null)
        return;
      string previousMechanicId = null;
      foreach (var upgrade in root.upgrades)
      {
        if (upgrade == null || string.IsNullOrEmpty(upgrade.id))
          continue;
        upgrade.route = "player";
        upgrade.classes = new[] { "all" };
        if (string.IsNullOrEmpty(upgrade.requires_tag) && !string.IsNullOrEmpty(root.requires_tag))
        {
          upgrade.requires_tag = root.requires_tag;
          upgrade.requires_tag_stacks = root.requires_tag_stacks > 0 ? root.requires_tag_stacks : 1;
        }
        // Mechanic tier files are strict linear trees. Preserve that invariant even if
        // an older/corrupted JSON copy loses requires_ids during migration.
        if (upgrade.tier > 1 && !string.IsNullOrEmpty(previousMechanicId))
          upgrade.requires_ids = new[] { previousMechanicId };
        if (upgrade.tier > 0)
          previousMechanicId = upgrade.id;
        Upgrades.Add(upgrade);
      }
    }

    static void ParseNumeric(string path, string json, bool exhaustionOnly = false)
    {
      var root = JsonUtility.FromJson<NumericRoot>(json);
      if (root?.upgrades == null)
        return;

      foreach (var source in root.upgrades)
      {
        var hasModifierList = source?.modifiers != null && source.modifiers.Length > 0;
        if (source == null || string.IsNullOrEmpty(source.id) || (!hasModifierList && string.IsNullOrEmpty(source.stat)))
          continue;

        if (exhaustionOnly)
        {
          var value = source.values != null && source.values.Length > 0
            ? source.values[0]
            : source.value_per_stack;
          var offerGroup = !string.IsNullOrEmpty(source.offer_group)
            ? source.offer_group
            : "player";
          var isPlayer = offerGroup == "player";
          var def = new LevelUpChoiceDatabase.UpgradeDef
          {
            id = source.id,
            tier = 0,
            route = "player",
            display_name = source.display_name,
            description = source.description,
            category = isPlayer ? "player" : "numeric",
            offer_group = offerGroup,
            offer_weight = source.offer_weight > 0f ? source.offer_weight : 0.05f,
            repeatable = true,
            max_stacks = Mathf.Max(1, source.max_stacks),
            classes = new[] { "all" },
            tags = MergeTags(source.tags, root.tags, isPlayer ? "player" : "numeric", root.pool),
            modifiers = hasModifierList
              ? source.modifiers
              : new[] { new LevelUpChoiceDatabase.StatModifier
                { stat = source.stat, op = "add", value = value } }
          };
          ExhaustionUpgrades.Add(def);
          continue;
        }

        if (string.IsNullOrEmpty(source.stat))
          continue;

        var levels = Mathf.Clamp(source.max_stacks, 1, 5);
        string previousId = null;
        for (var level = 1; level <= levels; level++)
        {
          var id = $"{source.id}_{level:00}";
          var value = source.values != null && source.values.Length >= level
            ? source.values[level - 1]
            : source.value_per_stack * Mathf.Lerp(1f, 0.45f, (level - 1f) / Mathf.Max(1f, levels - 1f));
          var offerGroup = !string.IsNullOrEmpty(source.offer_group)
            ? source.offer_group
            : root.pool == "player_stats" ? "player" : "numeric";
          var isPlayer = offerGroup == "player";
          Upgrades.Add(new LevelUpChoiceDatabase.UpgradeDef
          {
            id = id,
            tier = level,
            route = "player",
            display_name = $"{source.display_name} {ToRoman(level)}",
            description = source.description,
            category = isPlayer ? "player" : "numeric",
            offer_group = offerGroup,
            offer_weight = source.offer_weight > 0f ? source.offer_weight : 0.65f,
            classes = new[] { "all" },
            requires_ids = previousId == null ? Array.Empty<string>() : new[] { previousId },
            requires_tag = !string.IsNullOrEmpty(source.requires_tag)
              ? source.requires_tag
              : root.requires_tag,
            requires_tag_stacks = string.IsNullOrEmpty(source.requires_tag) && string.IsNullOrEmpty(root.requires_tag)
              ? 0
              : 1,
            tags = MergeTags(source.tags, root.tags, isPlayer ? "player" : "numeric", root.pool),
            modifiers = new[] { new LevelUpChoiceDatabase.StatModifier
              { stat = source.stat, op = "add", value = value } }
          });
          previousId = id;
        }
      }
    }

    static void ParseEvolution(string json)
    {
      var root = JsonUtility.FromJson<EvolutionRoot>(json);
      var evolution = root?.evolution;
      if (evolution?.tiers == null || string.IsNullOrEmpty(evolution.id))
        return;

      string previousId = null;
      foreach (var tier in evolution.tiers)
      {
        if (tier == null || tier.tier <= 0)
          continue;
        var id = $"evo_{evolution.id}_{tier.tier:00}_{tier.id}";
        var copy = GetEvolutionCopy(evolution.id, tier.id);
        Upgrades.Add(new LevelUpChoiceDatabase.UpgradeDef
        {
          id = id,
          tier = tier.tier,
          route = "player",
          display_name = copy.name,
          description = copy.description,
          category = "mechanic",
          mechanic_id = evolution.id,
          introduces_mechanic = false,
          offer_weight = tier.tier == 1 ? 0.95f : 1.05f,
          classes = new[] { "all" },
          requires_ids = previousId == null
            ? Array.Empty<string>()
            : new[] { previousId },
          requires_any_ids = previousId == null
            ? new[] { "foundation_detached_origin", "num_part_count_01", "num_ranger_detached_slot_01" }
            : null,
          tags = new[] { "mechanic", "detached_weapon", evolution.id },
          modifiers = new[] { new LevelUpChoiceDatabase.StatModifier
            { stat = $"detached_{evolution.id}_tier", op = "add", value = 1f } }
        });
        previousId = id;
      }
    }

    // Evolution JSON from the early prototype contains damaged Chinese copy. Keep the
    // player-facing text beside the runtime adapter so every growth-route consumer gets
    // the same truthful, current-tier-only description.
    static (string name, string description) GetEvolutionCopy(string evolutionId, string tierId)
    {
      var key = $"{evolutionId}/{tierId}";
      return key switch
      {
        "laser/focused_beam" => ("聚焦光束", "获得 1 道持续光束，每 0.12 秒造成 1 次伤害。"),
        "laser/twin_refraction" => ("双向折射", "光束数量 +1，可同时攻击 2 个目标。"),
        "laser/prism_edge" => ("棱镜锋刃", "光束命中宽度由 0.16 提高至 0.50（+213%）。"),
        "laser/orbital_sweep" => ("轨道扫射", "单次照射时间由 0.48 秒提高至 1.05 秒（+119%），并横扫 56°。"),
        "laser/prism_array" => ("棱镜阵列", "主光束数量 +1；命中后额外折射 1 次，造成 65% 伤害。"),

        "missile/single_launch" => ("单发飞弹", "每轮发射 1 枚飞弹，造成 100% 飞弹伤害。"),
        "missile/salvo_launch" => ("齐射协议", "每轮飞弹数量由 1 枚提高至 3 枚（+200%）。"),
        "missile/hunter_guidance" => ("猎手制导", "飞弹获得自动追踪，追踪率提高至 100%。"),
        "missile/split_warhead" => ("分裂弹头", "主飞弹命中后额外分裂，子弹头各造成 55% 伤害。"),
        "missile/missile_swarm" => ("飞弹蜂群", "每轮飞弹数量由 3 枚提高至 6 枚（+100%），发射间隔缩短至 0.08 秒。"),

        "explosion/impact_burst" => ("冲击爆破", "攻击获得 1 次范围爆炸。"),
        "explosion/expanding_charge" => ("扩容装药", "爆炸作用范围提高 1 个等级。"),
        "explosion/chain_detonation" => ("连锁引爆", "爆炸额外触发 1 次连锁引爆。"),
        "explosion/death_charge" => ("死亡装药", "被爆炸击杀的敌人额外触发 1 次爆炸。"),
        "explosion/nuclear_chain" => ("核爆链", "连锁爆炸数量提高至 5 次。"),

        "pulse/pulse_ring" => ("脉冲环", "周期性释放 1 道环形脉冲。"),
        "pulse/double_pulse" => ("双重脉冲", "每轮脉冲数量由 1 道提高至 2 道（+100%）。"),
        "pulse/propagating_wave" => ("扩散波", "脉冲扩散距离提高 1 个等级。"),
        "pulse/resonance_ring" => ("共振环", "每轮额外产生 1 道共振脉冲。"),
        "pulse/arena_pulse" => ("竞技场脉冲", "脉冲覆盖范围提高至全场级，覆盖等级 +1。"),

        "boomerang/short_cast" => ("短距回旋", "获得 1 枚回旋刃，可往返命中敌人。"),
        "boomerang/long_cast" => ("长距投掷", "回旋刃最大飞行距离提高至长距档（+1 档）。"),
        "boomerang/piercing_arc" => ("穿透弧线", "回旋刃不再因首次命中折返，可额外穿透目标。"),
        "boomerang/multi_return" => ("多重折返", "每次投掷额外折返至少 1 次。"),
        "boomerang/boomerang_storm" => ("回旋风暴", "同时投出的回旋刃数量提高至最大配置数量。"),

        "trail/short_trail" => ("短暂尾迹", "移动路径生成伤害尾迹，每次造成 36% 基础伤害。"),
        "trail/long_trail" => ("延长尾迹", "尾迹持续时间提高至长效档（+1 档）。"),
        "trail/persistent_path" => ("持久路径", "尾迹持续时间提高至持久档（+1 档）。"),
        "trail/forked_path" => ("分叉路径", "转向超过 32° 时额外生成 1 条分叉尾迹。"),
        "trail/trail_network" => ("尾迹网络", "相邻尾迹额外连接 1 条伤害线，造成 28% 基础伤害。"),
        _ => ("进化强化", "当前形态等级 +1。")
      };
    }

    static string[] MergeTags(string[] a, string[] b, params string[] extra)
    {
      var result = new List<string>();
      void Add(string[] values)
      {
        if (values == null) return;
        foreach (var value in values)
          if (!string.IsNullOrEmpty(value) && !result.Contains(value)) result.Add(value);
      }
      Add(a); Add(b); Add(extra);
      return result.ToArray();
    }

    static string ToRoman(int value) => value switch
    {
      1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => value.ToString()
    };

    [Serializable] sealed class UpgradeRoot
    {
      public string requires_tag;
      public int requires_tag_stacks;
      public LevelUpChoiceDatabase.UpgradeDef[] upgrades;
    }
    [Serializable] sealed class NumericRoot
    {
      public string pool;
      public string requires_tag;
      public string[] tags;
      public NumericDef[] upgrades;
    }
    [Serializable] sealed class NumericDef
    {
      public string id;
      public string display_name;
      public string description;
      public string stat;
      public float value_per_stack;
      public float[] values;
      public int max_stacks;
      public float offer_weight;
      public string offer_group;
      public string[] tags;
      public string requires_tag;
      public LevelUpChoiceDatabase.StatModifier[] modifiers;
    }
    [Serializable] sealed class EvolutionRoot { public EvolutionDef evolution; }
    [Serializable] sealed class EvolutionDef { public string id; public EvolutionTier[] tiers; }
    [Serializable] sealed class EvolutionTier
    {
      public int tier;
      public string id;
      public string name;
      public string description;
    }
  }
}
