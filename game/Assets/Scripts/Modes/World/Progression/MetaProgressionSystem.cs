using System.Collections.Generic;
using System.IO;
using System;

using UnityEngine;
using Game.Shared.Stats;

namespace Game.World
{
  /// <summary>
  /// 局外成长系??跨局永久进度树?
  ///
  /// 流程?
  ///   1. 单局结束 ?CalculateBattleScore(runStats) ?BattleScore ?BattleExp
  ///   2. BattleExp 永久保存（PlayerPrefs JSON?
  ///   3. 消?BattleExp 解锁/升级成长节点
  ///   4. 已解锁节点效果在每局开始时自动应用
  ///
  /// 成长树类型：
  ///   - attribute : 直接增添属性（HP/ATK/DEF/移速等?
  ///   - world     : 影响世界生成（额外营?难度修正?
  ///   - event     : 增加新的事件汀"
  ///   - loot      : 解锁新的装备/加成掉落
  ///
  /// 配表：data/world/meta_progression.json
  /// </summary>
  public class MetaProgressionSystem : IWorldSystem
  {
    // ══════════════════════════════════════════════════════
    //  PlayerPrefs Key
    // ══════════════════════════════════════════════════════

    const string PrefsKeyExp = "World_Meta_BattleExp";
    const string PrefsKeyNodes = "World_Meta_UnlockedNodes";

    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    /// <summary>是否输出调试日志</summary>
    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  公开状态（只读?
    // ══════════════════════════════════════════════════════

    /// <summary>累计战斗经验（跨局永久?/summary>
    public float TotalBattleExp { get; private set; }

    /// <summary>本局战斗分数</summary>
    public float CurrentRunScore { get; private set; }

    /// <summary>本局获得的战斗经?/summary>
    public float CurrentRunExp { get; private set; }

    /// <summary>已解?升级的节点（nodeId ?当前等级?/summary>
    public IReadOnlyDictionary<string, int> UnlockedNodes => _unlockedNodes;

    /// <summary>所有节点定?/summary>
    public IReadOnlyDictionary<string, MetaNodeDef> AllNodes => _nodeDefs;

    // ══════════════════════════════════════════════════════
    //  内部状态"
    // ══════════════════════════════════════════════════════

    readonly Dictionary<string, MetaNodeDef> _nodeDefs = new();
    readonly Dictionary<string, int> _unlockedNodes = new();
    RunStats _currentRunStats = new();
    bool _initialized;
    bool _loaded;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      LoadNodeDefinitions();
      LoadPersistentData();
      _initialized = true;

      if (DebugLog)
        Debug.Log($"[MetaProgression] Initialized. TotalExp={TotalBattleExp:F0} " +
                  $"unlocked={_unlockedNodes.Count}/{_nodeDefs.Count} nodes");
    }

    public void Tick(float deltaTime)
    {
      // 完全事件驱动
    }

    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      SavePersistentData();
      _initialized = false;

      if (DebugLog)
        Debug.Log("[MetaProgression] Shut down. Data saved.");
    }

    // ══════════════════════════════════════════════════════
    //  RunStats ?单局统计（外部写入）
    // ══════════════════════════════════════════════════════

    /// <summary>记录本局统计（由各系统在单局中实时写入）</summary>
    public RunStats CurrentStats => _currentRunStats;

    /// <summary>单局开??重置统计</summary>
    public void ResetRunStats()
    {
      _currentRunStats = new RunStats();
      CurrentRunScore = 0f;
      CurrentRunExp = 0f;

      if (DebugLog)
        Debug.Log("[MetaProgression] Run stats reset.");
    }

    // ══════════════════════════════════════════════════════
    //  战斗结算（单局结束时调用）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 单局结束 ?计算 BattleScore ?转化 BattleExp ?永久保存?
    ///
    /// BattleScore 公式（配表可调）?
    ///   kills          × 1
    ///   campsDestroyed × 50
    ///   bossesKilled   × 200
    ///   eventsDone     × 80
    ///   worldLevel     × 30
    ///   victoryBonus   × 500（胜利额外）
    /// </summary>
    public float FinalizeRun(bool victory)
    {
      var s = _currentRunStats;

      float score = 0f;
      score += s.Kills * 1f;
      score += s.CampsDestroyed * 50f;
      score += s.BossesKilled * 200f;
      score += s.EventsDone * 80f;
      score += s.MaxWorldLevelReached * 30f;
      score += s.MaxPlayerLevelReached * 10f;
      score += s.GoldCollected * 0.1f;

      if (victory)
        score += 500f;

      CurrentRunScore = score;

      // BattleExp = BattleScore?:1 转化，可配置倍率?
      var battleExp = score;
      CurrentRunExp = battleExp;

      TotalBattleExp += battleExp;
      SavePersistentData();

      if (DebugLog)
        Debug.Log($"[MetaProgression] Run finalized. victory={victory} " +
                  $"score={score:F0} ?exp={battleExp:F0} totalExp={TotalBattleExp:F0}");

      return battleExp;
    }

    // ══════════════════════════════════════════════════════
    //  节点解锁
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 节点展示状态（供 UI 使用）。
    /// </summary>
    public enum NodeDisplayState
    {
      /// <summary>前置未满足，灰色不可点击</summary>
      Locked,
      /// <summary>前置已满足，可解锁</summary>
      Available,
      /// <summary>已解锁</summary>
      Unlocked
    }

    /// <summary>检查前提节点是否全部已解锁（至少 1 级）</summary>
    public bool ArePrerequisitesMet(string nodeId)
    {
      if (!_nodeDefs.TryGetValue(nodeId, out var def)) return false;
      if (def.prerequisites == null || def.prerequisites.Length == 0) return true;
      foreach (var prereq in def.prerequisites)
      {
        if (GetNodeLevel(prereq) < 1) return false;
      }
      return true;
    }

    /// <summary>获取节点的展示状态（Locked / Available / Unlocked）</summary>
    public NodeDisplayState GetNodeDisplayState(string nodeId)
    {
      if (GetNodeLevel(nodeId) >= 1) return NodeDisplayState.Unlocked;
      return ArePrerequisitesMet(nodeId) ? NodeDisplayState.Available : NodeDisplayState.Locked;
    }

    /// <summary>检查节点是否可以解?升级</summary>
    public bool CanUnlock(string nodeId)
    {
      if (!_nodeDefs.TryGetValue(nodeId, out var def)) return false;

      // 检查当前等级是否已达上陀"
      var currentLevel = GetNodeLevel(nodeId);
      if (currentLevel >= def.max_level) return false;

      // 检?BattleExp 是否足够
      var cost = GetUnlockCost(def, currentLevel);
      if (TotalBattleExp < cost) return false;

      // 检查前置节点是否已解锁
      if (def.prerequisites != null)
      {
        foreach (var prereq in def.prerequisites)
        {
          if (GetNodeLevel(prereq) < 1) return false;
        }
      }

      return true;
    }

    /// <summary>解锁/升级一个节点。消?BattleExp?/summary>
    /// <returns>是否成功</returns>
    public bool UnlockNode(string nodeId)
    {
      if (!CanUnlock(nodeId)) return false;

      var def = _nodeDefs[nodeId];
      var currentLevel = GetNodeLevel(nodeId);
      var cost = GetUnlockCost(def, currentLevel);

      // 消费
      TotalBattleExp -= cost;

      // 升级
      _unlockedNodes[nodeId] = currentLevel + 1;

      // 应用效果（attribute 类型直接写入 RunBuildState?
      if (def.tree_type == "attribute" && def.effects != null)
      {
        foreach (var effect in def.effects)
        {
          if (effect != null && !string.IsNullOrEmpty(effect.effect_type))
            ApplyAttributeEffect(effect.effect_type, effect.effect_value, effect.effect_param);
        }
      }

      SavePersistentData();

      if (DebugLog)
        Debug.Log($"[MetaProgression] Unlocked '{nodeId}' LV.{currentLevel + 1} cost={cost:F0} " +
                  $"remainingExp={TotalBattleExp:F0}");

      return true;
    }

    /// <summary>获取节点的当前解锁等级（0=未解锁）</summary>
    public int GetNodeLevel(string nodeId)
    {
      _unlockedNodes.TryGetValue(nodeId, out var level);
      return level;
    }

    /// <summary>解锁成本（随等级递增?/summary>
    public float GetUnlockCost(string nodeId)
    {
      if (!_nodeDefs.TryGetValue(nodeId, out var def)) return float.MaxValue;
      return GetUnlockCost(def, GetNodeLevel(nodeId));
    }

    static float GetUnlockCost(MetaNodeDef def, int currentLevel)
    {
      // 基础成本 × 等级倍率（每升一级增?50%?
      var mult = 1f + currentLevel * 0.5f;
      return def.cost * mult;
    }

    // ══════════════════════════════════════════════════════
    //  效果应用（每局开始时由外部调用）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 应用所有已解锁节点的属性加成到 RunBuildState?
    /// 在每局开始时调用（如 GameSessionConfig ?CombatRoot 初始化阶段）?
    ///
    /// 仅应?tree_type=attribute 的节点（直接属性加成）?
    /// world/event/loot 类型由对应系统自行读取解锁状态?
    /// </summary>
    public void ApplyAttributeBonuses()
    {
      foreach (var kv in _unlockedNodes)
      {
        var nodeId = kv.Key;
        var level = kv.Value;
        if (level <= 0) continue;

        if (!_nodeDefs.TryGetValue(nodeId, out var def)) continue;
        if (def.tree_type != "attribute") continue;
        if (def.effects == null) continue;

        foreach (var effect in def.effects)
        {
          if (effect == null || string.IsNullOrEmpty(effect.effect_type)) continue;

          var totalValue = effect.effect_value * level;
          ApplyAttributeEffect(effect.effect_type, totalValue, effect.effect_param);
        }
      }

      if (DebugLog)
        Debug.Log($"[MetaProgression] Applied {_unlockedNodes.Count} unlocked node bonuses.");
    }

    /// <summary>
    /// 检查指定世界节点是否已解锁（供 WorldSpawnSystem / WorldGenerator 查询?
    /// </summary>
    public bool IsWorldNodeUnlocked(string nodeId)
    {
      return GetNodeLevel(nodeId) >= 1;
    }

    /// <summary>
    /// 获取已解锁的 loot 类型节点列表（供 LootService 扩展掉落池）
    /// </summary>
    public List<MetaNodeDef> GetUnlockedLootNodes()
    {
      var result = new List<MetaNodeDef>();
      foreach (var kv in _unlockedNodes)
      {
        if (kv.Value < 1) continue;
        if (_nodeDefs.TryGetValue(kv.Key, out var def) && def.tree_type == "loot")
          result.Add(def);
      }
      return result;
    }

    /// <summary>
    /// 获取已解锁的事件节点列表（供 EventManager 扩展事件池）
    /// </summary>
    public List<MetaNodeDef> GetUnlockedEventNodes()
    {
      var result = new List<MetaNodeDef>();
      foreach (var kv in _unlockedNodes)
      {
        if (kv.Value < 1) continue;
        if (_nodeDefs.TryGetValue(kv.Key, out var def) && def.tree_type == "event")
          result.Add(def);
      }
      return result;
    }

    // ══════════════════════════════════════════════════════
    //  属性效果实率"
    // ══════════════════════════════════════════════════════

    static void ApplyAttributeEffect(string effectType, float value, string param)
    {
      var writer = BuildStatWriterLocator.Writer;
      switch (effectType)
      {
        case "stat_mod":
          if (!string.IsNullOrEmpty(param))
            writer.AddStat(param, value);
          break;

        case "all_damage_mult":
          writer.AddStat("all_damage_mult", value);
          break;

        case "max_hp_mult":
          writer.AddStat("max_hp_mult", value);
          break;

        case "move_speed_mult":
          writer.AddStat("move_speed_mult", value);
          break;

        case "exp_gain_mult":
          writer.AddStat("exp_gain_mult", value);
          break;

        case "crit_chance":
          writer.AddStat("crit_chance", value);
          break;

        case "damage_reduction":
          writer.AddStat("damage_reduction", value);
          break;

        case "lifesteal":
          writer.AddStat("lifesteal", value);
          break;

        default:
          Debug.LogWarning($"[MetaProgression] Unknown attribute effect_type '{effectType}'.");
          break;
      }
    }

    // ══════════════════════════════════════════════════════
    //  持久匀"
    // ══════════════════════════════════════════════════════

    void LoadPersistentData()
    {
      if (_loaded) return;
      _loaded = true;

      TotalBattleExp = PlayerPrefs.GetFloat(PrefsKeyExp, 0f);

      var json = PlayerPrefs.GetString(PrefsKeyNodes, "{}");
      try
      {
        var wrapper = JsonUtility.FromJson<NodeUnlockWrapper>(json);
        _unlockedNodes.Clear();
        if (wrapper?.entries != null)
        {
          foreach (var entry in wrapper.entries)
            if (!string.IsNullOrEmpty(entry.id) && entry.level > 0)
              _unlockedNodes[entry.id] = entry.level;
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[MetaProgression] Failed to load unlock data: {e.Message}");
      }

      if (DebugLog)
        Debug.Log($"[MetaProgression] Loaded persistent data. Exp={TotalBattleExp:F0} unlocked={_unlockedNodes.Count}");
    }

    public void SavePersistentData()
    {
      PlayerPrefs.SetFloat(PrefsKeyExp, TotalBattleExp);

      var entries = new List<NodeUnlockEntry>();
      foreach (var kv in _unlockedNodes)
        entries.Add(new NodeUnlockEntry { id = kv.Key, level = kv.Value });

      var wrapper = new NodeUnlockWrapper { entries = entries.ToArray() };
      var json = JsonUtility.ToJson(wrapper, false);
      PlayerPrefs.SetString(PrefsKeyNodes, json);
      PlayerPrefs.Save();
    }

    // ══════════════════════════════════════════════════════
    //  JSON 加载
    // ══════════════════════════════════════════════════════

    void LoadNodeDefinitions()
    {
      if (_nodeDefs.Count > 0) return;

      if (!TryLoadMetaJson(ParseNodes))
        SetupFallbackNodes();
    }

    static bool TryLoadMetaJson(Action<string> parser)
    {
      var path = Path.Combine(Application.dataPath, "../../data/world/meta_progression.json");
      if (File.Exists(path))
      {
        parser(File.ReadAllText(path));
        return true;
      }

      var asset = Resources.Load<TextAsset>("Data/World/meta_progression");
      if (asset != null)
      {
        parser(asset.text);
        return true;
      }

      return false;
    }

    void ParseNodes(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<MetaProgressionRoot>(json);
        _nodeDefs.Clear();
        if (root?.nodes == null) return;

        foreach (var node in root.nodes)
        {
          if (node != null && !string.IsNullOrEmpty(node.id))
            _nodeDefs[node.id] = node;
        }

        Debug.Log($"[MetaProgression] Loaded {_nodeDefs.Count} meta nodes.");
      }
      catch (Exception e)
      {
        Debug.LogError($"[MetaProgression] Parse failed: {e.Message}");
        SetupFallbackNodes();
      }
    }

    void SetupFallbackNodes()
    {
      Debug.LogWarning("[MetaProgression] meta_progression.json not found. Using fallback nodes.");
      _nodeDefs.Clear();

      // 属性类
      AddFallback("attr_max_hp_1", "生命强化 I", "attribute", 1, 100, null, "max_hp_mult", 0.05f);
      AddFallback("attr_max_hp_2", "生命强化 II", "attribute", 2, 200, new[] { "attr_max_hp_1" }, "max_hp_mult", 0.08f);
      AddFallback("attr_attack_1", "攻击强化 I", "attribute", 1, 100, null, "all_damage_mult", 0.05f);
      AddFallback("attr_attack_2", "攻击强化 II", "attribute", 2, 200, new[] { "attr_attack_1" }, "all_damage_mult", 0.08f);
      AddFallback("attr_move_1", "移速强匀", "attribute", 1, 150, null, "move_speed_mult", 0.08f);

      // 世界籀"
      AddFallback("world_extra_camp", "额外营地", "world", 1, 300, null, "unlock_camp_type", 1f, "camp_elite");
      AddFallback("world_less_danger", "低危世界", "world", 2, 500, new[] { "world_extra_camp" }, "world_danger_reduce", 0.1f);

      // 事件籀"
      AddFallback("event_new_trial", "新试炀", "event", 1, 200, null, "unlock_event", 1f, "event_challenge_trial");
      AddFallback("event_gold_rush", "淘金烀", "event", 2, 350, new[] { "event_new_trial" }, "unlock_event", 1f, "event_gold_rush");

      // 掉落籀"
      AddFallback("loot_better_equip", "稀有装夀", "loot", 1, 250, null, "unlock_equipment", 1f, "equipment_rare");
      AddFallback("loot_more_gold", "金币加成", "loot", 1, 200, null, "gold_drop_mult", 0.2f);

      Debug.Log($"[MetaProgression] Fallback: {_nodeDefs.Count} nodes created.");
    }

    void AddFallback(string id, string name, string type, int tier, float cost, string[] prereqs, string effectType, float value, string param = "")
    {
      _nodeDefs[id] = new MetaNodeDef
      {
        id = id,
        display_name = name,
        tree_type = type,
        tier = tier,
        cost = cost,
        prerequisites = prereqs ?? Array.Empty<string>(),
        max_level = 1,
        effects = new[]
        {
          new MetaNodeEffect
          {
            effect_type = effectType,
            effect_value = value,
            effect_param = param
          }
        }
      };
    }

    // ══════════════════════════════════════════════════════
    //  测试/调试
    // ══════════════════════════════════════════════════════

    public void Debug_AddExp(float amount)
    {
      TotalBattleExp += amount;
      SavePersistentData();
      Debug.Log($"[MetaProgression] Debug: +{amount:F0} Exp ?total={TotalBattleExp:F0}");
    }

    public void Debug_ResetAll()
    {
      TotalBattleExp = 0f;
      _unlockedNodes.Clear();
      SavePersistentData();
      Debug.Log("[MetaProgression] Debug: All progress reset.");
    }

    // ══════════════════════════════════════════════════════
    //  数据结构
    // ══════════════════════════════════════════════════════

    [Serializable]
    class MetaProgressionRoot { public MetaNodeDef[] nodes; }

    [Serializable]
    class NodeUnlockWrapper { public NodeUnlockEntry[] entries; }

    [Serializable]
    class NodeUnlockEntry { public string id; public int level; }

    // ══════════════════════════════════════════════════════
    //  公开定义籀"
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 局外成长树节点定义?
    /// 配表：data/world/meta_progression.json
    /// </summary>
    [Serializable]
    public class MetaNodeDef
    {
      /// <summary>节点唯一 ID</summary>
      public string id;

      /// <summary>显示名称</summary>
      public string display_name;

      /// <summary>描述文本</summary>
      public string description;

      /// <summary>
      /// 成长树类型：
      ///   "attribute" = 直接属性加戀"
      ///   "world"     = 影响世界生成
      ///   "event"     = 新增事件
      ///   "loot"      = 解锁装备/掉落
      /// </summary>
      public string tree_type;

      /// <summary>节点层级（越深越强）</summary>
      public int tier;

      /// <summary>解锁成本（BattleExp?/summary>
      public float cost;

      /// <summary>前置节点 ID（须全部解锁才能解锁此节点）</summary>
      public string[] prerequisites;

      /// <summary>最大等级（1=一次性解锁，?=可多次升级）</summary>
      public int max_level;

      /// <summary>解锁后的效果列表</summary>
      public MetaNodeEffect[] effects;
    }

    /// <summary>
    /// 节点效果定义。完全配置驱动?
    ///
    /// effect_type 支持（可扩展）：
    ///   stat_mod  ?通用属性修改（effect_param=statKey?
    ///   all_damage_mult / max_hp_mult / move_speed_mult 等独立属怀"
    ///   unlock_camp_type / unlock_event / unlock_equipment ?解锁内容
    ///   world_danger_reduce / gold_drop_mult ?世界/经济修正
    /// </summary>
    [Serializable]
    public class MetaNodeEffect
    {
      public string effect_type;
      public float effect_value;
      public string effect_param;
    }
  }

  // ══════════════════════════════════════════════════════
  //  RunStats ?单局统计（供战斗结算用）
  // ══════════════════════════════════════════════════════

  /// <summary>
  /// 单局运行时统计。由各系统在单局中实时写入?
  /// 单局结束时由 MetaProgressionSystem.FinalizeRun() 读取并计?BattleScore?
  /// </summary>
  [Serializable]
  public class RunStats
  {
    /// <summary>击杀怪物总数</summary>
    public int Kills;

    /// <summary>摧毁营地?/summary>
    public int CampsDestroyed;

    /// <summary>击杀 Boss ?/summary>
    public int BossesKilled;

    /// <summary>完成事件?/summary>
    public int EventsDone;

    /// <summary>达到的最高世界等?/summary>
    public int MaxWorldLevelReached;

    /// <summary>达到的最高玩家等?/summary>
    public int MaxPlayerLevelReached;

    /// <summary>收集的金币总数</summary>
    public int GoldCollected;

    /// <summary>记录一次击杀</summary>
    public void RecordKill() => Kills++;

    /// <summary>记录营地摧毁</summary>
    public void RecordCampDestroyed() => CampsDestroyed++;

    /// <summary>记录 Boss 击杀</summary>
    public void RecordBossKill() => BossesKilled++;

    /// <summary>记录事件完成</summary>
    public void RecordEventDone() => EventsDone++;

    /// <summary>更新世界等级记录</summary>
    public void TrackWorldLevel(int level)
    {
      if (level > MaxWorldLevelReached) MaxWorldLevelReached = level;
    }

    /// <summary>更新玩家等级记录</summary>
    public void TrackPlayerLevel(int level)
    {
      if (level > MaxPlayerLevelReached) MaxPlayerLevelReached = level;
    }

    /// <summary>记录金币</summary>
    public void RecordGold(int amount) => GoldCollected += amount;
  }
}
