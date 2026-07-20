using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Enemy.AI;

namespace Game.World
{
  /// <summary>
  /// 世界等级驱动的 AI 词条解析器。
  ///
  /// 职责：
  ///   1. 根据当前世界等级查 ai_affix_tiers.json 获取词条池
  ///   2. 按权重独立随机选取移动词条（min~max 个）和攻击词条（min~max 个）
  ///   3. 生成 EnemyAffixSet 供 EnemySpawner 注入到怪物
  ///
  /// 使用方式：
  ///   var affixSet = WorldAffixResolver.Resolve(worldLevel);
  ///   spawner.SpawnEnemy(enemyId, pos, affixSet);
  ///
  /// 设计要点：
  ///   - 跨类型词条自由组合（move + attack 独立选取）
  ///   - 同类型内按权重独立随机（可重复选取相同类型但参数相同，目前避免重复）
  ///   - 词条类型字符串通过 switch 解析为枚举值
  /// </summary>
  public static class WorldAffixResolver
  {
    /// <summary>根据世界等级生成随机词条集合。</summary>
    public static EnemyAffixSet Resolve(int worldLevel)
    {
      WorldDatabase.EnsureLoaded();
      var tier = WorldDatabase.GetAffixTierForWorldLevel(worldLevel);
      if (tier == null)
        return EnemyAffixSet.CreateDefault(EnemyAttackKind.Melee);

      var set = new EnemyAffixSet();

      // 移动词条：按权重随机选取 min~max 个（不重复）
      if (tier.move_affixes != null && tier.move_affixes.Length > 0)
      {
        var count = UnityEngine.Random.Range(tier.move_affix_min,
          Mathf.Min(tier.move_affix_max, tier.move_affixes.Length) + 1);
        count = Mathf.Clamp(count, 0, tier.move_affixes.Length);
        PickWeighted(tier.move_affixes, count, entry =>
        {
          var type = ParseMoveAffixType(entry.type);
          if (type == null) return;
          set.MovementAffixes.Add(new EnemyMoveAffix(
            type.Value, 1f, entry.p0, entry.p1, entry.p2, entry.p3));
        });
      }

      // 攻击词条：按权重随机选取 min~max 个（不重复）
      if (tier.attack_affixes != null && tier.attack_affixes.Length > 0)
      {
        var count = UnityEngine.Random.Range(tier.attack_affix_min,
          Mathf.Min(tier.attack_affix_max, tier.attack_affixes.Length) + 1);
        count = Mathf.Clamp(count, 0, tier.attack_affixes.Length);
        PickWeighted(tier.attack_affixes, count, entry =>
        {
          var type = ParseAttackAffixType(entry.type);
          if (type == null) return;
          set.AttackAffixes.Add(new EnemyAttackAffix(
            type.Value, entry.p0, entry.p1, entry.p2, entry.p3));
        });
      }

      return set;
    }

    // ══════════════════════════════════════════════════════
    //  加权不放回抽取
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 从词条池中按权重随机抽取 count 个不同的条目。
    /// 抽取后从池中移除，保证不重复选取同一条目。
    /// </summary>
    static void PickWeighted(WorldDatabase.AffixPoolEntry[] pool, int count,
      Action<WorldDatabase.AffixPoolEntry> onPicked)
    {
      if (pool == null || pool.Length == 0 || count <= 0) return;

      // 构建可抽取的索引列表
      var available = new List<int>(pool.Length);
      for (int i = 0; i < pool.Length; i++)
        if (pool[i] != null && pool[i].weight > 0f)
          available.Add(i);

      var picked = Mathf.Min(count, available.Count);

      for (int p = 0; p < picked; p++)
      {
        // 计算总权重
        float totalWeight = 0f;
        for (int i = 0; i < available.Count; i++)
          totalWeight += pool[available[i]].weight;

        if (totalWeight <= 0f) break;

        // 加权随机
        var roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        int pickedIdx = -1;

        for (int i = 0; i < available.Count; i++)
        {
          cumulative += pool[available[i]].weight;
          if (roll <= cumulative)
          {
            pickedIdx = i;
            break;
          }
        }

        if (pickedIdx < 0) pickedIdx = available.Count - 1;

        // 回调并移除
        onPicked(pool[available[pickedIdx]]);
        available.RemoveAt(pickedIdx);
      }
    }

    // ══════════════════════════════════════════════════════
    //  类型字符串解析
    // ══════════════════════════════════════════════════════

    static MoveAffixType? ParseMoveAffixType(string s)
    {
      switch (s)
      {
        case "MeleeBasic": return MoveAffixType.MeleeBasic;
        case "DodgeProjectile": return MoveAffixType.DodgeProjectile;
        case "MeleeSpread": return MoveAffixType.MeleeSpread;
        case "MeleeCooperative": return MoveAffixType.MeleeCooperative;
        case "RangedBasic": return MoveAffixType.RangedBasic;
        case "RangedDodge": return MoveAffixType.RangedDodge;
        case "RangedCooperative": return MoveAffixType.RangedCooperative;
        default: return null;
      }
    }

    static AttackAffixType? ParseAttackAffixType(string s)
    {
      switch (s)
      {
        case "Burst": return AttackAffixType.Burst;
        case "Prediction": return AttackAffixType.Prediction;
        case "Suppression": return AttackAffixType.Suppression;
        case "Shotgun": return AttackAffixType.Shotgun;
        case "Turn": return AttackAffixType.Turn;
        case "Sweep": return AttackAffixType.Sweep;
        case "MultiCharge": return AttackAffixType.MultiCharge;
        default: return null;
      }
    }

    static class Mathf
    {
      public static int Min(int a, int b) => a < b ? a : b;
      public static int Clamp(int v, int min, int max)
      {
        if (v < min) return min;
        if (v > max) return max;
        return v;
      }
    }
  }
}
