using System.Collections.Generic;
using UnityEngine;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Buff;

namespace Game.World
{
  /// <summary>
  /// 事件节点图执行引擎。替代旧 EventExecutor。
  ///
  /// 每个事件由多个节点组成节点图，节点间通过 options(玩家选择)或 auto_next(自动推进)连接。
  /// 每节点至多1条 condition + 1组 effects。
  ///
  /// 条件类型：hp_percent, hp_absolute, gold, player_level, world_level, total_items, item_count
  /// 效果类型：modify_hp, modify_xp, modify_gold, add_item, add_accessory, add_attribute, apply_buff, reveal_markers, modify_world_level
  /// </summary>
  public class EventNodeExecutor
  {
    readonly GoldWallet _wallet;
    readonly PlayerLevelSystem _playerLevel;
    readonly WorldLevelSystem _worldLevel;
    public bool DebugLog { get; set; }

    public EventNodeExecutor(GoldWallet wallet, PlayerLevelSystem playerLevel, WorldLevelSystem worldLevel)
    {
      _wallet = wallet; _playerLevel = playerLevel; _worldLevel = worldLevel;
    }

    // ══════════════════════════════════════════════════════
    //  核心 API
    // ══════════════════════════════════════════════════════

    /// <summary>从 start 节点开始，返回第一批选项（或 null 表示结束）。</summary>
    public EventContext Start(WorldDatabase.EventDef evt)
    {
      return LoadNode(evt, "start");
    }

    /// <summary>玩家选择了某选项后，加载下一个节点。</summary>
    public EventContext Advance(WorldDatabase.EventDef evt, string currentNodeId, string chosenNext)
    {
      return LoadNode(evt, chosenNext);
    }

    /// <summary>从指定节点重新加载（用于暂离恢复，不重复执行效果）。</summary>
    public EventContext RestartFromNode(WorldDatabase.EventDef evt, string nodeId)
    {
      return LoadNode(evt, nodeId, skipEffects: true);
    }

    /// <summary>执行当前节点的所有效果（不推进）。</summary>
    public void ExecuteEffects(WorldDatabase.EventNodeDef node)
    {
      if (node?.effects == null) return;
      foreach (var eff in node.effects)
      {
        if (eff == null || string.IsNullOrEmpty(eff.type)) continue;
        ApplyEffect(eff);
      }
    }

    EventContext LoadNode(WorldDatabase.EventDef evt, string nodeId, bool skipEffects = false)
    {
      var node = FindNode(evt, nodeId);
      if (node == null) return null;

      var ctx = new EventContext { Node = node, HasOptions = false };

      // 检查条件
      if (node.condition != null && !EvaluateCondition(node.condition))
      {
        ctx.ConditionFailed = true;
        return ctx;
      }

      // 过滤满足条件的 options
      if (node.options != null && node.options.Length > 0)
      {
        var valid = new List<WorldDatabase.EventOptionDef>();
        foreach (var opt in node.options)
        {
          if (opt == null) continue;
          if (opt.condition == null || EvaluateCondition(opt.condition))
            valid.Add(opt);
        }
        if (valid.Count > 0)
        {
          ctx.Options = valid.ToArray();
          ctx.HasOptions = true;
          return ctx;
        }
      }

      // 有 auto_next → 自动推进（暂离恢复时不执行效果）
      if (!string.IsNullOrEmpty(node.auto_next))
      {
        if (!skipEffects) ExecuteEffects(node);
        return LoadNode(evt, node.auto_next, skipEffects);
      }

      // 终点节点：执行效果（暂离恢复时不重复执行）
      if (!skipEffects) ExecuteEffects(node);
      ctx.IsEnd = true;
      return ctx;
    }

    // ══════════════════════════════════════════════════════
    //  条件判定
    // ══════════════════════════════════════════════════════

    bool EvaluateCondition(WorldDatabase.EventConditionDef cond)
    {
      if (cond == null || string.IsNullOrEmpty(cond.type)) return true;
      float current = GetConditionValue(cond.type, cond.item_id);
      return CompareValue(current, cond.op, cond.value);
    }

    float GetConditionValue(string type, string itemId)
    {
      var player = GetPlayer();
      switch (type)
      {
        case "hp_percent":
          if (player == null) return 0;
          var hp = player.GetComponent<Health>();
          return hp != null && hp.MaxHp > 0 ? hp.CurrentHp / hp.MaxHp * 100f : 0;
        case "hp_absolute":
          var hp2 = player?.GetComponent<Health>();
          return hp2?.CurrentHp ?? 0;
        case "gold":
          return _wallet?.Balance ?? 0;
        case "player_level":
          return WorldRuntimeContext.WorldPlayerLevel;
        case "world_level":
          return WorldRuntimeContext.WorldLevel;
        case "total_items":
          var inv = WorldManager.Instance?.Inventory;
          return inv != null ? inv.WeaponSlotCount + inv.AccessoryCount : 0;
        case "item_count":
          if (string.IsNullOrEmpty(itemId)) return 0;
          return WorldManager.Instance?.Inventory?.GetItemCount(itemId) ?? 0;
        default: return 0;
      }
    }

    static bool CompareValue(float current, string op, float target)
    {
      return op switch
      {
        "gte" => current >= target,
        "gt"  => current > target,
        "lte" => current <= target,
        "lt"  => current < target,
        "eq"  => Mathf.Abs(current - target) < 0.01f,
        _     => true
      };
    }

    // ══════════════════════════════════════════════════════
    //  效果执行
    // ══════════════════════════════════════════════════════

    void ApplyEffect(WorldDatabase.EventEffectDef eff)
    {
      switch (eff.type)
      {
        case "modify_hp":     ApplyModifyHp(eff); break;
        case "modify_xp":     ApplyModifyXp(eff); break;
        case "modify_gold":   ApplyModifyGold(eff); break;
        case "add_item":      ApplyAddItem(eff, false); break;
        case "add_accessory": ApplyAddItem(eff, true); break;
        case "add_attribute": ApplyAddAttribute(eff); break;
        case "apply_buff":    ApplyBuff(eff); break;
        case "reveal_markers": ApplyRevealMarkers(eff); break;
        case "modify_world_level": ApplyModifyWorldLevel(eff); break;
        default:
          if (DebugLog) Debug.LogWarning($"[EventNodeExecutor] Unknown effect '{eff.type}'.");
          break;
      }
    }

    void ApplyModifyHp(WorldDatabase.EventEffectDef eff)
    {
      var player = GetPlayer();
      var health = player?.GetComponent<Health>();
      if (health == null) return;

      float delta;
      if (eff.value_type == "percent")
        delta = health.MaxHp * eff.value / 100f;
      else
        delta = eff.value;

      if (delta > 0) health.Heal(delta);
      else if (delta < 0)
      {
        var dmg = new DamageRequest { Base = -delta, DamageTypeId = "true", Attacker = null };
        DamagePipeline.Apply(dmg, health);
      }
    }

    void ApplyModifyXp(WorldDatabase.EventEffectDef eff)
    {
      _playerLevel?.AddXp(eff.value);
    }

    void ApplyModifyGold(WorldDatabase.EventEffectDef eff)
    {
      int amount = Mathf.RoundToInt(eff.value);
      if (amount > 0) _wallet?.AddGold(amount);
      else if (amount < 0) _wallet?.SpendGold(-amount);
    }

    void ApplyAddItem(WorldDatabase.EventEffectDef eff, bool isAccessory)
    {
      var inv = WorldManager.Instance?.Inventory;
      if (inv == null) return;

      int count = eff.count > 0 ? eff.count : 1;
      if (!string.IsNullOrEmpty(eff.item_id))
      {
        inv.AddItem(eff.item_id, count);
      }
      else if (!string.IsNullOrEmpty(eff.rarity))
      {
        // 按稀有度随机选取
        var candidates = new List<WorldDatabase.ItemDef>();
        foreach (var kv in WorldDatabase.Items)
        {
          var def = kv.Value;
          if ((isAccessory ? def.IsAccessory : def.IsWeapon) && def.quality == eff.rarity)
            candidates.Add(def);
        }
        if (candidates.Count == 0)
        {
          // 回退：尝试更低的稀有度
          string[] fallbackOrder = { "rare", "uncommon", "common" };
          foreach (var fb in fallbackOrder)
          {
            candidates.Clear();
            foreach (var kv in WorldDatabase.Items)
            {
              var def = kv.Value;
              if ((isAccessory ? def.IsAccessory : def.IsWeapon) && def.quality == fb)
                candidates.Add(def);
            }
            if (candidates.Count > 0) break;
          }
        }
        if (candidates.Count > 0)
        {
          var pick = candidates[Random.Range(0, candidates.Count)];
          inv.AddItem(pick.item_id, count);
          if (DebugLog) Debug.Log($"[EventNodeExecutor] Random {eff.rarity} item: {pick.item_id} x{count}");
        }
      }
    }

    void ApplyAddAttribute(WorldDatabase.EventEffectDef eff)
    {
      if (string.IsNullOrEmpty(eff.attr)) return;
      var attrMgr = WorldManager.Instance?.Attributes;
      if (attrMgr == null) return;

      // 直接用 meta_progression 来源写入属性
      attrMgr.ApplyModifiers("event", new List<AttributeManager.ModifierEntry>
      {
        new AttributeManager.ModifierEntry(eff.attr, AttributeManager.ModifierOp.Add, eff.value)
      });
    }

    void ApplyBuff(WorldDatabase.EventEffectDef eff)
    {
      if (string.IsNullOrEmpty(eff.buff_id)) return;
      var player = GetPlayer();
      var container = player?.GetComponent<BuffContainer>();
      if (container == null) return;

      container.ApplyBuff(eff.buff_id, new BuffContainer.BuffApplyContext
      {
        sourceEntity = player,
        durationOverride = eff.duration > 0 ? eff.duration : 30f
      });
    }

    void ApplyRevealMarkers(WorldDatabase.EventEffectDef eff)
    {
      // 广播揭示事件供地图系统处理
      WorldEventBus.FireRandomEventTriggered("reveal_markers", 0);
    }

    void ApplyModifyWorldLevel(WorldDatabase.EventEffectDef eff)
    {
      _worldLevel?.Debug_AddExp(eff.value * 50f);
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    WorldDatabase.EventNodeDef FindNode(WorldDatabase.EventDef evt, string nodeId)
    {
      if (evt?.nodes == null || string.IsNullOrEmpty(nodeId)) return null;
      foreach (var n in evt.nodes)
        if (n != null && n.node_id == nodeId) return n;
      return null;
    }

    static GameObject GetPlayer()
    {
      var go = GameObject.FindWithTag("Player");
      if (go == null) go = GameObject.Find("Player");
      return go;
    }

    static class Mathf
    {
      public static float Abs(float v) => v < 0f ? -v : v;
      public static int RoundToInt(float v) => (int)(v + 0.5f);
    }
  }

  /// <summary>事件上下文：当前节点信息和可用选项。</summary>
  public class EventContext
  {
    public WorldDatabase.EventNodeDef Node;
    public WorldDatabase.EventOptionDef[] Options;
    public bool HasOptions;
    public bool IsEnd;
    public bool ConditionFailed;
  }
}
