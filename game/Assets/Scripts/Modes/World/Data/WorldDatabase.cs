using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式配置数据库?
  ///
  /// 负责加载 World 模式全部 JSON 配表?
  ///   - camp_types.json    ?营地类型定义
  ///   - camp_levels.json   ?营地等级数值曲纀"
  ///   - world_levels.json  ?世界等级定义
  ///   - events.json        ?随机事件定义
  ///   - shops.json         ?商店定义
  ///
  /// 设计原则?
  ///   - 完全配置驱动，禁止硬编码数值"
  ///   - 与现?Database 风格一致（static class + Dictionary 缓存 + EnsureLoaded 懒加载）
  ///   - 提供 Get(id) / TryGet(id) 双通道访问
  ///   - JSON 文件路径：data/world/
  /// </summary>
  public static class WorldDatabase
  {
    // ══════════════════════════════════════════════════════
    //  内部缓存
    // ══════════════════════════════════════════════════════

    static readonly Dictionary<string, CampTypeDef> s_campTypes = new();
    static readonly Dictionary<int, CampLevelDef> s_campLevels = new();
    static readonly Dictionary<int, WorldLevelDef> s_worldLevels = new();
    static readonly Dictionary<string, EventDef> s_events = new();
    static readonly Dictionary<string, ShopDef> s_shops = new();
    static readonly Dictionary<int, PlayerLevelDef> s_playerLevels = new();
    static readonly Dictionary<string, ItemDef> s_items = new();
    static readonly Dictionary<string, AttributeDef> s_attributeDefs = new();
    static readonly Dictionary<string, MonsterDropDef> s_monsterDrops = new();
    static readonly List<AffixTierDef> s_affixTiers = new();
    static readonly List<WildSpawnDef> s_wildSpawns = new();

    static bool s_loaded;

    // ══════════════════════════════════════════════════════
    //  懒加载入发"
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 确保所?World 配表已加载。幂等调用，首次调用时执行全?JSON 解析?
    /// </summary>
    public static void EnsureLoaded()
    {
      if (s_loaded) return;
      s_loaded = true;

      LoadCampTypes();
      LoadCampLevels();
      LoadWorldLevels();
      LoadPlayerLevels();
      LoadEvents();
      LoadShops();
      LoadInventoryItems();
      LoadAttributeDefs();
      LoadMonsterDrops();
      LoadAffixTiers();
      LoadWildSpawns();
      LoadShopBuffs();
    }

    /// <summary>
    /// 强制重新加载所有配表（Editor 热重载或测试用）?
    /// </summary>
    public static void ReloadAll()
    {
      s_loaded = false;
      s_campTypes.Clear();
      s_campLevels.Clear();
      s_worldLevels.Clear();
      s_playerLevels.Clear();
      s_events.Clear();
      s_shops.Clear();
      s_items.Clear();
      s_attributeDefs.Clear();
      s_monsterDrops.Clear();
      s_affixTiers.Clear();
      s_wildSpawns.Clear();
      s_shopBuffs.Clear();
      EnsureLoaded();
    }

    // ══════════════════════════════════════════════════════
    //  CampType ?营地类型
    // ══════════════════════════════════════════════════════

    /// <summary>?ID 获取营地类型定义。未找到返回 null?/summary>
    public static CampTypeDef GetCampType(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id)) return null;
      s_campTypes.TryGetValue(id, out var def);
      return def;
    }

    /// <summary>尝试获取营地类型定义?/summary>
    public static bool TryGetCampType(string id, out CampTypeDef def)
    {
      def = GetCampType(id);
      return def != null;
    }

    /// <summary>所有营地类型?/summary>
    public static IReadOnlyDictionary<string, CampTypeDef> CampTypes
    {
      get { EnsureLoaded(); return s_campTypes; }
    }

    /// <summary>按最小世界等级筛选可用的营地类型?/summary>
    public static List<CampTypeDef> GetCampTypesByWorldLevel(int worldLevel)
    {
      EnsureLoaded();
      var list = new List<CampTypeDef>();
      foreach (var kv in s_campTypes)
        if (kv.Value.min_world_level <= worldLevel)
          list.Add(kv.Value);
      return list;
    }

    // ══════════════════════════════════════════════════════
    //  CampLevel ?营地等级数值"
    // ══════════════════════════════════════════════════════

    /// <summary>按等级获取营地等级数值定义。未找到返回默认值?/summary>
    public static CampLevelDef GetCampLevel(int level)
    {
      EnsureLoaded();
      if (level < 1) level = 1;
      s_campLevels.TryGetValue(level, out var def);
      return def ?? CampLevelDef.Default;
    }

    /// <summary>尝试获取营地等级数值?/summary>
    public static bool TryGetCampLevel(int level, out CampLevelDef def)
    {
      def = GetCampLevel(level);
      return def != null && def.level == level;
    }

    /// <summary>所有营地等级定义?/summary>
    public static IReadOnlyDictionary<int, CampLevelDef> CampLevels
    {
      get { EnsureLoaded(); return s_campLevels; }
    }

    // ══════════════════════════════════════════════════════
    //  WorldLevel ?世界等级
    // ══════════════════════════════════════════════════════

    /// <summary>按等级获取世界等级定义。未找到返回默认值?/summary>
    public static WorldLevelDef GetWorldLevel(int level)
    {
      EnsureLoaded();
      if (level < 1) level = 1;
      s_worldLevels.TryGetValue(level, out var def);
      return def ?? WorldLevelDef.Default;
    }

    /// <summary>尝试获取世界等级定义?/summary>
    public static bool TryGetWorldLevel(int level, out WorldLevelDef def)
    {
      def = GetWorldLevel(level);
      return def != null && def.level == level;
    }

    /// <summary>所有世界等级定义?/summary>
    public static IReadOnlyDictionary<int, WorldLevelDef> WorldLevels
    {
      get { EnsureLoaded(); return s_worldLevels; }
    }

    // ══════════════════════════════════════════════════════
    //  PlayerLevel ?World 模式玩家等级
    // ══════════════════════════════════════════════════════

    /// <summary>按等级获取玩家等级数值定义。未找到返回默认值?/summary>
    public static PlayerLevelDef GetPlayerLevel(int level)
    {
      EnsureLoaded();
      if (level < 1) level = 1;
      s_playerLevels.TryGetValue(level, out var def);
      return def ?? PlayerLevelDef.Default;
    }

    /// <summary>尝试获取玩家等级数值?/summary>
    public static bool TryGetPlayerLevel(int level, out PlayerLevelDef def)
    {
      def = GetPlayerLevel(level);
      return def != null && def.level == level;
    }

    /// <summary>所有玩家等级定义?/summary>
    public static IReadOnlyDictionary<int, PlayerLevelDef> PlayerLevels
    {
      get { EnsureLoaded(); return s_playerLevels; }
    }

    /// <summary>获取玩家等级上限</summary>
    public static int PlayerMaxLevel
    {
      get
      {
        EnsureLoaded();
        var max = 1;
        foreach (var kv in s_playerLevels)
          if (kv.Key > max) max = kv.Key;
        return max;
      }
    }

    // ══════════════════════════════════════════════════════
    //  Event ?随机事件
    // ══════════════════════════════════════════════════════

    /// <summary>?ID 获取事件定义。未找到返回 null?/summary>
    public static EventDef GetEvent(string id)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(id)) return null;
      s_events.TryGetValue(id, out var def);
      return def;
    }

    /// <summary>尝试获取事件定义?/summary>
    public static bool TryGetEvent(string id, out EventDef def)
    {
      def = GetEvent(id);
      return def != null;
    }

    /// <summary>所有事件定义?/summary>
    public static IReadOnlyDictionary<string, EventDef> Events
    {
      get { EnsureLoaded(); return s_events; }
    }

    /// <summary>按类别和世界等级筛选可用事件?/summary>
    public static List<EventDef> GetEventsByCategory(string category, int worldLevel)
    {
      EnsureLoaded();
      var list = new List<EventDef>();
      foreach (var kv in s_events)
      {
        var ev = kv.Value;
        if (ev == null) continue;
        if (!string.IsNullOrEmpty(category) && ev.category != category) continue;
        if (ev.min_world_level > worldLevel) continue;
        list.Add(ev);
      }
      return list;
    }

    // ══════════════════════════════════════════════════════
    //  Shop ?商店
    // ══════════════════════════════════════════════════════

    /// <summary>按商店类?ID 获取商店定义。未找到返回 null?/summary>
    public static ShopDef GetShop(string shopTypeId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(shopTypeId)) return null;
      s_shops.TryGetValue(shopTypeId, out var def);
      return def;
    }

    /// <summary>尝试获取商店定义?/summary>
    public static bool TryGetShop(string shopTypeId, out ShopDef def)
    {
      def = GetShop(shopTypeId);
      return def != null;
    }

    /// <summary>所有商店定义?/summary>
    public static IReadOnlyDictionary<string, ShopDef> Shops
    {
      get { EnsureLoaded(); return s_shops; }
    }

    // ══════════════════════════════════════════════════════
    //  JSON 加载（内部）
    // ══════════════════════════════════════════════════════

    static void LoadCampTypes()
    {
      if (!TryLoadWorldJson("camp_types", json =>
      {
        var root = JsonUtility.FromJson<CampTypeRoot>(json);
        if (root?.camp_types == null) return;
        foreach (var def in root.camp_types)
        {
          if (def != null && !string.IsNullOrEmpty(def.id))
            s_campTypes[def.id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_campTypes.Count} camp types.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] camp_types.json not found in data/world/.");
      }
    }

    static void LoadCampLevels()
    {
      if (!TryLoadWorldJson("camp_levels", json =>
      {
        var root = JsonUtility.FromJson<CampLevelRoot>(json);
        if (root?.camp_levels == null) return;
        foreach (var def in root.camp_levels)
        {
          if (def != null && def.level > 0)
            s_campLevels[def.level] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_campLevels.Count} camp level entries.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] camp_levels.json not found in data/world/.");
      }
    }

    static void LoadWorldLevels()
    {
      if (!TryLoadWorldJson("world_levels", json =>
      {
        var root = JsonUtility.FromJson<WorldLevelRoot>(json);
        if (root?.world_levels == null) return;
        foreach (var def in root.world_levels)
        {
          if (def != null && def.level > 0)
            s_worldLevels[def.level] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_worldLevels.Count} world level entries.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] world_levels.json not found in data/world/.");
      }
    }

    static void LoadPlayerLevels()
    {
      if (!TryLoadWorldJson("player_levels", json =>
      {
        var root = JsonUtility.FromJson<PlayerLevelRoot>(json);
        if (root?.player_levels == null) return;
        foreach (var def in root.player_levels)
        {
          if (def != null && def.level > 0)
            s_playerLevels[def.level] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_playerLevels.Count} player level entries.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] player_levels.json not found in data/world/.");
      }
    }

    static void LoadEvents()
    {
      if (!TryLoadWorldJson("events", json =>
      {
        var root = JsonUtility.FromJson<EventRoot>(json);
        if (root?.events == null) return;
        foreach (var def in root.events)
        {
          if (def != null && !string.IsNullOrEmpty(def.id))
            s_events[def.id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_events.Count} events.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] events.json not found in data/world/.");
      }
    }

    static void LoadShops()
    {
      if (!TryLoadWorldJson("shops", json =>
      {
        var root = JsonUtility.FromJson<ShopRoot>(json);
        if (root?.shops == null) return;
        foreach (var def in root.shops)
        {
          if (def != null && !string.IsNullOrEmpty(def.shop_type_id))
            s_shops[def.shop_type_id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_shops.Count} shop definitions.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] shops.json not found in data/world/.");
      }
    }

    static void LoadInventoryItems()
    {
      if (!TryLoadWorldJson("inventory_items", json =>
      {
        var root = JsonUtility.FromJson<ItemRoot>(json);
        if (root?.items == null) return;
        foreach (var def in root.items)
        {
          if (def != null && !string.IsNullOrEmpty(def.item_id))
            s_items[def.item_id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_items.Count} inventory item definitions.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] inventory_items.json not found in data/world/.");
      }
    }

    static void LoadAttributeDefs()
    {
      if (!TryLoadWorldJson("attribute_defs", json =>
      {
        var root = JsonUtility.FromJson<AttributeRoot>(json);
        if (root?.attributes == null) return;
        foreach (var def in root.attributes)
        {
          if (def != null && !string.IsNullOrEmpty(def.attr_id))
            s_attributeDefs[def.attr_id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_attributeDefs.Count} attribute definitions.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] attribute_defs.json not found in data/world/.");
      }
    }

    static void LoadMonsterDrops()
    {
      if (!TryLoadWorldJson("monster_drops", json =>
      {
        var root = JsonUtility.FromJson<DropRoot>(json);
        if (root?.drop_tables == null) return;
        foreach (var def in root.drop_tables)
        {
          if (def != null && !string.IsNullOrEmpty(def.drop_id))
            s_monsterDrops[def.drop_id] = def;
        }
        Debug.Log($"[WorldDatabase] Loaded {s_monsterDrops.Count} monster drop tables.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] monster_drops.json not found in data/world/.");
      }
    }

    static void LoadWildSpawns()
    {
      if (!TryLoadWorldJson("wild_spawns", json =>
      {
        var root = JsonUtility.FromJson<WildSpawnRoot>(json);
        if (root?.wild_spawns == null) return;
        s_wildSpawns.Clear();
        s_wildSpawns.AddRange(root.wild_spawns);
        Debug.Log($"[WorldDatabase] Loaded {s_wildSpawns.Count} wild spawn tiers.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] wild_spawns.json not found in data/world/.");
      }
    }

    static void LoadShopBuffs()
    {
      if (!TryLoadWorldJson("shop_buffs", json =>
      {
        var root = JsonUtility.FromJson<ShopBuffRoot>(json);
        if (root?.buffs == null) return;
        s_shopBuffs.Clear();
        foreach (var def in root.buffs)
          if (def != null && !string.IsNullOrEmpty(def.buff_id))
            s_shopBuffs[def.buff_id] = def;
        Debug.Log($"[WorldDatabase] Loaded {s_shopBuffs.Count} shop buffs.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] shop_buffs.json not found in data/world/.");
      }
    }

    static void LoadAffixTiers()
    {
      if (!TryLoadWorldJson("ai_affix_tiers", json =>
      {
        var root = JsonUtility.FromJson<AffixTierRoot>(json);
        if (root?.affix_tiers == null) return;
        s_affixTiers.Clear();
        s_affixTiers.AddRange(root.affix_tiers);
        Debug.Log($"[WorldDatabase] Loaded {s_affixTiers.Count} AI affix tiers.");
      }))
      {
        Debug.LogWarning("[WorldDatabase] ai_affix_tiers.json not found in data/world/.");
      }
    }

    /// <summary>尝试?data/world/ ?Resources/Data/World/ 加载 JSON?/summary>
    static bool TryLoadWorldJson(string fileName, Action<string> parser)
    {
      // 优先?1：data/world/ 目录
      var path = Path.Combine(Application.dataPath, "../../data/world", fileName + ".json");
      if (File.Exists(path))
      {
        try
        {
          parser(File.ReadAllText(path));
          return true;
        }
        catch (Exception e)
        {
          Debug.LogError($"[WorldDatabase] Failed to parse {fileName}.json: {e.Message}");
          return false;
        }
      }

      // 优先?2：Resources/Data/World/
      var asset = Resources.Load<TextAsset>($"Data/World/{fileName}");
      if (asset != null)
      {
        try
        {
          parser(asset.text);
          return true;
        }
        catch (Exception e)
        {
          Debug.LogError($"[WorldDatabase] Failed to parse Resources {fileName}.json: {e.Message}");
          return false;
        }
      }

      return false;
    }

    // ══════════════════════════════════════════════════════
    //  JSON Root Wrapper（Unity JsonUtility 需要顶层对象）
    // ══════════════════════════════════════════════════════

    [Serializable] class CampTypeRoot { public CampTypeDef[] camp_types; }
    [Serializable] class CampLevelRoot { public CampLevelDef[] camp_levels; }
    [Serializable] class WorldLevelRoot { public WorldLevelDef[] world_levels; }
    [Serializable] class PlayerLevelRoot { public PlayerLevelDef[] player_levels; }
    [Serializable] class EventRoot { public EventDef[] events; }
    [Serializable] class ShopRoot { public ShopDef[] shops; }
    [Serializable] class ItemRoot { public ItemDef[] items; }
    [Serializable] class AttributeRoot { public AttributeDef[] attributes; }
    [Serializable] class DropRoot { public MonsterDropDef[] drop_tables; }

    // ══════════════════════════════════════════════════════
    //  定义籀"
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 营地类型定义。决定营地刷怪种类、成长行为和奖励?
    /// 配表：data/world/camp_types.json
    /// 参见：docs/design.md §3.3
    /// </summary>
    [Serializable]
    public class CampTypeDef
    {
      /// <summary>营地类型唯一 ID（如 "camp_basic", "camp_elite", "camp_boss_nest"?/summary>
      public string id;

      /// <summary>显示名称</summary>
      public string display_name;

      /// <summary>营地描述（flavor/玩法提示?/summary>
      public string description;

      /// <summary>敌人 archetype ID 列表（引?enemies.json?/summary>
      public string[] enemy_archetype_ids;

      /// <summary>初始营地等级</summary>
      public int base_level;

      /// <summary>营地等级自然成长速率（每秒增加值）</summary>
      public float growth_rate;

      /// <summary>玩家靠近时的额外成长加速倍率</summary>
      public float player_proximity_growth_bonus;

      /// <summary>营地等级上限</summary>
      public int max_level;

      /// <summary>掉落?ID（引?loot_tables.json?/summary>
      public string loot_pool_id;

      /// <summary>最小世界等级要求（低于此等级不生成此类营地?/summary>
      public int min_world_level;

      /// <summary>是否为一次性营地（摧毁后不再刷新）</summary>
      public bool one_shot;

      /// <summary>地图图标资源标识</summary>
      public string map_icon_id;

      /// <summary>休眠状态下预刷怪上限（0或未配置则不休眠刷怪）</summary>
      public int dormant_max_alive;

      /// <summary>从 enemy_archetype_ids 中随机选取的敌人数（每局固定）</summary>
      public int enemy_pool_size;

      /// <summary>自然成长速率乘数（1.0=正常，高=快，低=慢，0=不成长）</summary>
      public float natural_growth_mult;

      /// <summary>摧毁后生成的商店类型ID</summary>
      public string shop_on_destroy;

      /// <summary>商店标签/名称后缀</summary>
      public string shop_tag;

      /// <summary>是否为Boss巢穴（预置Boss+精英，不自然刷怪）</summary>
      public bool is_boss_nest;

      /// <summary>Boss巢穴的Boss enemyId</summary>
      public string boss_nest_boss_id;

      /// <summary>Boss巢穴预置精英数量</summary>
      public int boss_nest_elite_count;
    }

    /// <summary>
    /// 营地等级数值定义。每级独立配置，驱动营地怪物强度?
    /// 配表：data/world/camp_levels.json
    /// </summary>
    [Serializable]
    public class CampLevelDef
    {
      /// <summary>营地等级（≥1?/summary>
      public int level;

      /// <summary>怪物数量倍率</summary>
      public float enemy_count_mult;

      /// <summary>怪物 HP 倍率</summary>
      public float enemy_hp_mult;

      /// <summary>怪物伤害倍率</summary>
      public float enemy_damage_mult;

      /// <summary>怪物移速倍率</summary>
      public float enemy_speed_mult;

      /// <summary>清剿营地奖励?World XP</summary>
      public int xp_reward;

      /// <summary>清剿营地奖励?World 金币</summary>
      public int gold_reward;

      /// <summary>营地怪物出生间隔（秒?/summary>
      public float spawn_interval;

      /// <summary>营地内同时存在的最大怪物?/summary>
      public int max_alive_enemies;

      /// <summary>默认值（配表缺失时回退?/summary>
      public static readonly CampLevelDef Default = new()
      {
        level = 1,
        enemy_count_mult = 1f,
        enemy_hp_mult = 1f,
        enemy_damage_mult = 1f,
        enemy_speed_mult = 1f,
        xp_reward = 50,
        gold_reward = 10,
        spawn_interval = 3f,
        max_alive_enemies = 8
      };
    }

    /// <summary>
    /// 世界等级定义。驱动野外怪物难度、可解锁内容和全局事件?
    /// 配表：data/world/world_levels.json
    /// 参见：docs/design.md §3.4
    /// </summary>
    [Serializable]
    public class WorldLevelDef
    {
      /// <summary>世界等级（≥1?/summary>
      public int level;

      /// <summary>从此等级自然成长到下一级需要的累计时间（秒，参考值）</summary>
      public float time_to_next_level;

      /// <summary>自然成长速率（每秒世界等级增加值）</summary>
      public float growth_rate;

      /// <summary>升到下一级所需?WorldExp 阈值。未配置时由 time_to_next_level × 默认 Danger 推算?/summary>
      public float xp_threshold;

      /// <summary>野外怪物基础等级</summary>
      public int enemy_level_base;

      /// <summary>野外怪物全局属性倍率</summary>
      public float enemy_stat_mult;

      /// <summary>达到此等级时解锁的营地类?ID 列表</summary>
      public string[] unlock_camp_types;

      /// <summary>达到此等级时可触发的事件 ID 列表</summary>
      public string[] unlock_event_ids;

      /// <summary>达到此等级时有概率出现的野外 Boss ID 列表</summary>
      public string[] unlock_boss_ids;

      /// <summary>击杀领先怪物时世界等级下降量（一次）</summary>
      public float kill_leading_enemy_drop;

      /// <summary>营地等级上限加成（相对世界等级）</summary>
      public int camp_max_level_bonus;

      /// <summary>默认值（配表缺失时回退?/summary>
      public static readonly WorldLevelDef Default = new()
      {
        level = 1,
        time_to_next_level = 60f,
        growth_rate = 0.01f,
        xp_threshold = 100f,
        enemy_level_base = 1,
        enemy_stat_mult = 1f,
        unlock_camp_types = Array.Empty<string>(),
        unlock_event_ids = Array.Empty<string>(),
        unlock_boss_ids = Array.Empty<string>(),
        kill_leading_enemy_drop = 0.2f,
        camp_max_level_bonus = 3
      };
    }

    /// <summary>
    /// World 模式玩家等级数值定义。每级独立配?XP 需求和属性加成?
    /// 配表：data/world/player_levels.json
    ///
    /// 仅提供基础属性加成（攻击/生命/防御），不涉及装?技?词条?
    /// Arena 模式的升级三选一系统（ExperienceSystem + LevelUpController）与此并行独立?
    /// </summary>
    [Serializable]
    public class PlayerLevelDef
    {
      /// <summary>玩家等级（≥1?/summary>
      public int level;

      /// <summary>从上一级升到此级所需的累?XP</summary>
      public float xp_required;

      /// <summary>攻击力倍率加成（叠乘）</summary>
      public float attack_mult;

      /// <summary>最大生命值倍率加成（叠乘）</summary>
      public float hp_mult;

      /// <summary>防御/减伤倍率?~1?=无减伤）</summary>
      public float defense_mult;

      /// <summary>暴击率加成（绝对值）</summary>
      public float crit_chance_bonus;

      /// <summary>移速加成（绝对值）</summary>
      public float move_speed_bonus;

      /// <summary>默认值（配表缺失时回退?/summary>
      public static readonly PlayerLevelDef Default = new()
      {
        level = 1,
        xp_required = 100f,
        attack_mult = 1f,
        hp_mult = 1f,
        defense_mult = 0f,
        crit_chance_bonus = 0f,
        move_speed_bonus = 0f
      };
    }

    /// <summary>
    /// 随机事件定义。玩家进入事件结构体时触发?
    /// 配表：data/world/events.json
    /// 参见：docs/design.md §7.2
    /// </summary>
    /// <summary>
    /// 事件定义（节点图格式）。节点间通过 options 或 auto_next 连接。
    /// 配表：data/world/events.json
    /// </summary>
    [Serializable]
    public class EventDef
    {
      public string id;
      public string display_name;
      public string description;
      public string category;
      public int min_world_level;
      public bool is_repeatable;
      public float weight;
      /// <summary>节点列表（按顺序，start 为入口）</summary>
      public EventNodeDef[] nodes;
    }

    /// <summary>事件节点。每节点至多1条condition、1组effects。</summary>
    [Serializable]
    public class EventNodeDef
    {
      public string node_id;
      public string description;
      public EventConditionDef condition;
      public EventEffectDef[] effects;
      /// <summary>玩家选项（有则等待选择，无则检查 auto_next）</summary>
      public EventOptionDef[] options;
      /// <summary>无选项时自动跳转的节点 ID</summary>
      public string auto_next;
    }

    /// <summary>选项定义。text=显示文本，next=跳转节点ID，可选 condition 限制可见。</summary>
    [Serializable]
    public class EventOptionDef
    {
      public string text;
      public string next;
      public EventConditionDef condition;
      /// <summary>是否为暂离选项（选择后保留事件，下次从上级节点继续）</summary>
      public bool is_suspend;
    }

    /// <summary>效果定义。type=效果类型，其他字段按类型选用。</summary>
    [Serializable]
    public class EventEffectDef
    {
      public string type;
      public float value;
      public string value_type;
      public string item_id;
      public string rarity;
      public int count;
      public string attr;
      public string buff_id;
      public float duration;
      public string marker_type;
    }

    /// <summary>条件定义。type=条件类型，op=比较运算符，value=阈值。</summary>
    [Serializable]
    public class EventConditionDef
    {
      public string type;
      public string op;
      public float value;
      public string item_id;
    }

    // ══════════════════════════════════════════════════════
    //  Shop System — 商店品类概率制
    // ══════════════════════════════════════════════════════

    static readonly Dictionary<string, ShopBuffDef> s_shopBuffs = new();

    public static ShopBuffDef GetShopBuff(string buffId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(buffId)) return null;
      s_shopBuffs.TryGetValue(buffId, out var def);
      return def;
    }

    public static IReadOnlyDictionary<string, ShopBuffDef> ShopBuffs
    {
      get { EnsureLoaded(); return s_shopBuffs; }
    }

    /// <summary>
    /// 商店定义。品类概率制——每类商品有独立刷新概率和购买上限。
    /// 配表：data/world/shops.json
    /// </summary>
    [Serializable]
    public class ShopDef
    {
      public string shop_type_id;
      public string display_name;
      public int min_world_level;
      public int total_items;
      public ShopCategoryDef[] categories;
    }

    /// <summary>商品品类定义。</summary>
    [Serializable]
    public class ShopCategoryDef
    {
      public string category;
      public float probability;
      public int max_purchases;
      public int max_count;
    }

    /// <summary>商店 Buff 定义。</summary>
    [Serializable]
    public class ShopBuffDef
    {
      public string buff_id;
      public string display_name;
      public string description;
      public int price;
      public float duration;
      public int min_world_level;
    }

    [Serializable] class ShopBuffRoot { public ShopBuffDef[] buffs; }

    // ══════════════════════════════════════════════════════
    //  Inventory Items ?背包物品
    // ══════════════════════════════════════════════════════

    /// <summary>按 ID 获取物品定义。未找到返回 null。</summary>
    public static ItemDef GetItem(string itemId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(itemId)) return null;
      s_items.TryGetValue(itemId, out var def);
      return def;
    }

    /// <summary>尝试获取物品定义。</summary>
    public static bool TryGetItem(string itemId, out ItemDef def)
    {
      def = GetItem(itemId);
      return def != null;
    }

    /// <summary>所有物品定义。</summary>
    public static IReadOnlyDictionary<string, ItemDef> Items
    {
      get { EnsureLoaded(); return s_items; }
    }

    /// <summary>
    /// 效果参数键值对。JsonUtility 不支持 Dictionary，使用此 [Serializable] 数组作为中间格式。
    /// 运行时通过 ItemDef.GetEffectParams() 转为 Dictionary 使用。
    /// </summary>
    [Serializable]
    public class EffectParamEntry
    {
      public string key;
      public float value;
    }

    /// <summary>
    /// 词条键值对。与 EffectParamEntry 格式相同，语义上区分。
    /// 运行时通过 ItemDef.GetAffixes() 转为 Dictionary 使用。
    /// </summary>
    [Serializable]
    public class AffixEntry
    {
      public string key;
      public float value;
    }

    /// <summary>
    /// 背包物品定义。武器/道具和饰品共用此类，通过 category 区分。
    /// 配表：data/world/inventory_items.json
    /// </summary>
    [Serializable]
    public class ItemDef
    {
      /// <summary>物品唯一 ID</summary>
      public string item_id;

      /// <summary>显示名称</summary>
      public string display_name;

      /// <summary>物品描述</summary>
      public string description;

      /// <summary>
      /// 物品类别：
      ///   "weapon"    = 武器/道具（可堆叠，有 effect_type + effect_params）
      ///   "accessory" = 饰品（不堆叠，有 affixes 词条列表）
      /// </summary>
      public string category;

      /// <summary>
      /// 品质（设计概念，影响 UI 名称颜色和掉落物光芒颜色）：
      ///   "common" / "uncommon" / "rare" / "epic" / "legendary"
      /// </summary>
      public string quality;

      /// <summary>商店售价（金币，0=不可购买）</summary>
      public int shop_price;

      /// <summary>商店单次购买数量</summary>
      public int shop_batch_count;

      /// <summary>效果类型字符串（仅武器/道具有效），如 "area_damage"、"heal"、"chain_lightning"</summary>
      public string effect_type;

      /// <summary>效果参数列表（仅武器/道具有效），存储为键值对数组</summary>
      public EffectParamEntry[] effect_params;

      /// <summary>词条列表（仅饰品有效）</summary>
      public AffixEntry[] affixes;

      /// <summary>将 effect_params 数组转为 Dictionary&lt;string, float&gt;，方便运行时查询。</summary>
      public Dictionary<string, float> GetEffectParams()
      {
        var dict = new Dictionary<string, float>();
        if (effect_params != null)
        {
          foreach (var p in effect_params)
            if (p != null && !string.IsNullOrEmpty(p.key))
              dict[p.key] = p.value;
        }
        return dict;
      }

      /// <summary>将 affixes 数组转为 Dictionary&lt;string, float&gt;，方便运行时查询。</summary>
      public Dictionary<string, float> GetAffixes()
      {
        var dict = new Dictionary<string, float>();
        if (affixes != null)
        {
          foreach (var a in affixes)
            if (a != null && !string.IsNullOrEmpty(a.key))
              dict[a.key] = a.value;
        }
        return dict;
      }

      /// <summary>是否为武器/道具类别。</summary>
      public bool IsWeapon => category == "weapon";

      /// <summary>是否为饰品类别。</summary>
      public bool IsAccessory => category == "accessory";

      /// <summary>解析品质枚举。</summary>
      public ItemQuality ParsedQuality => ParseQuality(quality);
    }

    /// <summary>
    /// 物品品质枚举。设计层面标识物品重要性，代码中主要影响 UI 渲染颜色。
    /// </summary>
    public enum ItemQuality
    {
      Common,
      Uncommon,
      Rare,
      Epic,
      Legendary
    }

    /// <summary>品质字符串 → 枚举解析。</summary>
    public static ItemQuality ParseQuality(string q)
    {
      if (string.IsNullOrEmpty(q)) return ItemQuality.Common;
      switch (q.ToLowerInvariant())
      {
        case "uncommon": return ItemQuality.Uncommon;
        case "rare": return ItemQuality.Rare;
        case "epic": return ItemQuality.Epic;
        case "legendary": return ItemQuality.Legendary;
        default: return ItemQuality.Common;
      }
    }

    /// <summary>根据品质返回 UI 名称颜色。</summary>
    public static Color QualityColor(ItemQuality q)
    {
      switch (q)
      {
        case ItemQuality.Uncommon: return new Color(0.12f, 1f, 0f, 1f);
        case ItemQuality.Rare: return new Color(0f, 0.44f, 1f, 1f);
        case ItemQuality.Epic: return new Color(0.64f, 0.21f, 0.93f, 1f);
        case ItemQuality.Legendary: return new Color(1f, 0.5f, 0f, 1f);
        default: return new Color(0.8f, 0.8f, 0.8f, 1f);
      }
    }

    /// <summary>
    /// 商店商品定义?

    // ══════════════════════════════════════════════════════
    //  Attribute Defs — 属性定义表
    // ══════════════════════════════════════════════════════

    /// <summary>按 ID 获取属性定义。未找到返回 null。</summary>
    public static AttributeDef GetAttributeDef(string attrId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(attrId)) return null;
      s_attributeDefs.TryGetValue(attrId, out var def);
      return def;
    }

    /// <summary>尝试获取属性定义。</summary>
    public static bool TryGetAttributeDef(string attrId, out AttributeDef def)
    {
      def = GetAttributeDef(attrId);
      return def != null;
    }

    /// <summary>所有属性定义。</summary>
    public static IReadOnlyDictionary<string, AttributeDef> AttributeDefs
    {
      get { EnsureLoaded(); return s_attributeDefs; }
    }

    /// <summary>
    /// 属性定义。决定属性的基础值、上下限和显示信息。
    /// 配表：data/world/attribute_defs.json
    ///
    /// 属性表本身仅承担容器功能——定义 base/min/max。
    /// 实际计算和动态叠加由 AttributeManager 管理。
    /// </summary>
    [Serializable]
    public class AttributeDef
    {
      /// <summary>属性唯一 ID（如 "attack"、"move_speed_mult"）</summary>
      public string attr_id;

      /// <summary>显示名称</summary>
      public string display_name;

      /// <summary>属性描述</summary>
      public string description;

      /// <summary>基础值（由 JSON 固定，不可运行时修改）</summary>
      public float base_value;

      /// <summary>属性下限（最终值不会低于此值）</summary>
      public float min;

      /// <summary>属性上限（最终值不会高于此值）</summary>
      public float max;
    }

    // ══════════════════════════════════════════════════════
    //  Monster Drops — 怪物掉落表
    // ══════════════════════════════════════════════════════

    /// <summary>按 drop_id（优先用怪物精确 ID，回退到 loot_table_id）获取掉落表。</summary>
    public static MonsterDropDef GetMonsterDrop(string dropId)
    {
      EnsureLoaded();
      if (string.IsNullOrEmpty(dropId)) return null;
      s_monsterDrops.TryGetValue(dropId, out var def);
      return def;
    }

    /// <summary>尝试获取怪物掉落表。</summary>
    public static bool TryGetMonsterDrop(string dropId, out MonsterDropDef def)
    {
      def = GetMonsterDrop(dropId);
      return def != null;
    }

    /// <summary>所有怪物掉落表。</summary>
    public static IReadOnlyDictionary<string, MonsterDropDef> MonsterDrops
    {
      get { EnsureLoaded(); return s_monsterDrops; }
    }

    /// <summary>
    /// 怪物掉落表定义。每个 table 包含多条 drop 条目。
    /// 配表：data/world/monster_drops.json
    /// </summary>
    [Serializable]
    public class MonsterDropDef
    {
      /// <summary>掉落表 ID（对应 enemies.json 中的 loot_table_id 或怪物精确 ID）</summary>
      public string drop_id;

      /// <summary>掉落条目列表</summary>
      public DropEntryDef[] drops;
    }

    /// <summary>
    /// 单个掉落条目。类型决定如何触发和结算。
    ///
    /// type=exp   : 经验值，自动加到玩家（min/max 定义范围）
    /// type=gold  : 金币，生成拾取物（min/max 定义金额范围）
    /// type=item  : 道具/饰品，按 probability 独立判定，触发后按权重抽取
    /// </summary>
    [Serializable]
    public class DropEntryDef
    {
      /// <summary>条目类型："exp" / "gold" / "item"</summary>
      public string type;

      /// <summary>最小数量（exp=最少经验, gold=最少金币, item=最少抽取个数）</summary>
      public int min;

      /// <summary>最大数量</summary>
      public int max;

      /// <summary>触发概率（仅 item 类型有效，0~1）</summary>
      public float probability;

      /// <summary>物品表（仅 item 类型有效，按权重随机抽取）</summary>
      public DropItemEntry[] items;
    }

    /// <summary>
    /// 掉落物品池中的单个物品条目。
    /// </summary>
    [Serializable]
    public class DropItemEntry
    {
      /// <summary>物品 ID（对应 inventory_items.json）</summary>
      public string item_id;

      /// <summary>每次抽中时的数量</summary>
      public int count;

      /// <summary>随机权重（越高越容易被抽中）</summary>
      public float weight;
    }

    // ══════════════════════════════════════════════════════
    //  AI Affix Tiers — 世界等级驱动的怪物词条池
    // ══════════════════════════════════════════════════════

    /// <summary>按世界等级获取匹配的词条层级定义。</summary>
    public static AffixTierDef GetAffixTierForWorldLevel(int worldLevel)
    {
      EnsureLoaded();
      foreach (var tier in s_affixTiers)
      {
        if (tier != null && worldLevel >= tier.min_world_level && worldLevel <= tier.max_world_level)
          return tier;
      }
      return s_affixTiers.Count > 0 ? s_affixTiers[0] : null;
    }

    /// <summary>所有词条层级定义。</summary>
    public static IReadOnlyList<AffixTierDef> AffixTiers
    {
      get { EnsureLoaded(); return s_affixTiers; }
    }

    /// <summary>
    /// 词条层级定义 — 一个世界等级区间内的可用词条池。
    /// 配表：data/world/ai_affix_tiers.json
    /// </summary>
    [Serializable]
    public class AffixTierDef
    {
      public int min_world_level;
      public int max_world_level;
      public int move_affix_min;
      public int move_affix_max;
      public int attack_affix_min;
      public int attack_affix_max;
      public AffixPoolEntry[] move_affixes;
      public AffixPoolEntry[] attack_affixes;
    }

    /// <summary>
    /// 词条池条目 — 单个词条类型的权重和参数。
    /// p0-p3 对应 EnemyMoveAffix / EnemyAttackAffix 的参数位。
    /// </summary>
    [Serializable]
    public class AffixPoolEntry
    {
      public string type;
      public float weight;
      public float p0, p1, p2, p3;
    }

    [Serializable] class AffixTierRoot { public AffixTierDef[] affix_tiers; }
    [Serializable] class WildSpawnRoot { public WildSpawnDef[] wild_spawns; }

    // ══════════════════════════════════════════════════════
    //  Wild Spawns — 野外怪物自然生成
    // ══════════════════════════════════════════════════════

    /// <summary>按世界等级获取匹配的野外刷怪配置。</summary>
    public static WildSpawnDef GetWildSpawnForWorldLevel(int worldLevel)
    {
      EnsureLoaded();
      foreach (var def in s_wildSpawns)
      {
        if (def != null && worldLevel >= def.min_world_level && worldLevel <= def.max_world_level)
          return def;
      }
      return s_wildSpawns.Count > 0 ? s_wildSpawns[0] : null;
    }

    /// <summary>
    /// 野外刷怪配置定义 — 一个世界等级区间内的刷怪参数。
    /// 配表：data/world/wild_spawns.json
    /// </summary>
    [Serializable]
    public class WildSpawnDef
    {
      public int min_world_level;
      public int max_world_level;
      public float spawn_interval;
      public int batch_count_min;
      public int batch_count_max;
      public float spawn_dist_min;
      public float spawn_dist_max;
      public int max_alive;
      public float despawn_dist;
      public float attr_scale;
      public WildEnemyTypeEntry[] enemy_types;
    }

    /// <summary>
    /// 野外刷怪类型池条目。
    /// </summary>
    [Serializable]
    public class WildEnemyTypeEntry
    {
      public string enemy_id;
      public float weight;
    }

    /// <summary>
  }
}
