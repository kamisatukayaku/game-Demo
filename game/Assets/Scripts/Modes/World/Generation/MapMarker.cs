using System;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 地图标记 ?世界地图上一?POI 的数据表示?
  ///
  /// 仅承载数据，不包含渲染逻辑?
  /// ?WorldMapManager 集中管理，UI 层读取并渲染?
  ///
  /// 显示类型?
  ///   - 营地 (Camp)
  ///   - Boss (WildBoss)
  ///   - 商人 (Merchant / Shop)
  ///   - 事件 (Event)
  ///
  /// 不显示：怪物、地格、掉落物?
  /// </summary>
  [Serializable]
  public class MapMarker
  {
    // ══════════════════════════════════════════════════════
    //  标记类型
    // ══════════════════════════════════════════════════════

    public enum MarkerType
    {
      /// <summary>怪物营地</summary>
      Camp = 0,
      /// <summary>野外 Boss</summary>
      WildBoss = 1,
      /// <summary>商人/商店</summary>
      Merchant = 2,
      /// <summary>随机事件?/summary>
      EventPoint = 3,
      /// <summary>最终Boss巢穴</summary>
      FinalBoss = 4
    }

    /// <summary>
    /// 发现状态：
    ///   Hidden    = 从未被发现（不在玩家视野?
    ///   Discovered = 已被发现（显示在地图上）
    ///   Visited    = 已被访问（标记变?灰掉?
    ///   Destroyed  = 已被摧毁/完成（不可再交互?
    /// </summary>
    public enum DiscoveryState
    {
      Hidden = 0,
      Discovered = 1,
      Visited = 2,
      Destroyed = 3
    }

    // ══════════════════════════════════════════════════════
    //  数据字段
    // ══════════════════════════════════════════════════════

    /// <summary>标记唯一 ID</summary>
    public string MarkerId;

    /// <summary>标记类型</summary>
    public MarkerType Type;

    /// <summary>世界坐标位置</summary>
    public Vector2 WorldPosition;

    /// <summary>显示名称（如 "精英营地"?/summary>
    public string DisplayName;

    /// <summary>子类?ID（如 campTypeId / bossId / eventId?/summary>
    public string SubTypeId;

    /// <summary>发现状?/summary>
    public DiscoveryState State;

    /// <summary>营地当前等级（仅 Camp 类型有效?/summary>
    public int CampLevel;

    /// <summary>营地是否已被摧毁（仅 Camp 类型有效?/summary>
    public bool IsDestroyed;

    /// <summary>地图图标资源标识（供 UI 层使用）</summary>
    public string IconId;

    // ══════════════════════════════════════════════════════
    //  编码信息
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 获取此标记的类型特定编码信息字符串，供 UI tooltip 展示。
    ///
    /// Camp:     "Lv.{level} | 怪物: {type1}, {type2}"
    /// WildBoss: "Boss: {name} | HP: {cur}/{max}"
    /// Merchant:  "商人: {shopName}"
    /// EventPoint:"事件: {eventName}"
    ///
    /// 传入 runtimeData 可获取实时数据（如营地当前等级、Boss 当前 HP）。
    /// 若 runtimeData 为 null，则使用 Marker 自身的静态字段。
    /// </summary>
    /// <param name="runtimeData">可选：WorldRuntimeContext.Camps 中此标记对应的实时营地数据</param>
    /// <param name="bossHp">可选：Boss 当前 HP（由调用方查询 CampController/CampCore 获取）</param>
    /// <param name="bossMaxHp">可选：Boss 最大 HP</param>
    public string GetEncodedInfo(WorldCampData? runtimeData = null, float bossHp = -1f, float bossMaxHp = -1f)
    {
      switch (Type)
      {
        case MarkerType.Camp:
        case MarkerType.FinalBoss:
        {
          int level = runtimeData?.CampLevel ?? CampLevel;
          string[] enemyIds = runtimeData?.EnemyArchetypeIds;
          if (enemyIds == null || enemyIds.Length == 0)
          {
            var typeDef = WorldDatabase.GetCampType(SubTypeId);
            enemyIds = typeDef?.enemy_archetype_ids;
          }
          var enemyStr = enemyIds != null && enemyIds.Length > 0
            ? string.Join(", ", enemyIds)
            : "?";
          var campTypeName = WorldDatabase.GetCampType(SubTypeId)?.display_name ?? SubTypeId ?? "未知营地";
          var lines = $"类型: {campTypeName}\n营地等级: Lv.{level}\n刷新怪物: {enemyStr}";
          // Boss巢穴：追加血量
          if ((Type == MarkerType.WildBoss || Type == MarkerType.FinalBoss) && bossHp >= 0 && bossMaxHp > 0)
            lines += $"\nBoss血量: {bossHp:F0}/{bossMaxHp:F0} ({bossHp / bossMaxHp * 100f:F0}%)";
          return lines;
        }

        case MarkerType.WildBoss:
        {
          var bossName = DisplayName ?? SubTypeId ?? "未知Boss";
          if (bossHp >= 0 && bossMaxHp > 0)
            return $"Boss: {bossName}\nHP: {bossHp:F0}/{bossMaxHp:F0} ({bossHp / bossMaxHp * 100f:F0}%)";
          return $"Boss: {bossName}";
        }

        case MarkerType.Merchant:
        {
          var shopDef = WorldDatabase.GetShop(SubTypeId);
          var shopName = shopDef?.display_name ?? SubTypeId ?? "商人";
          var lines = $"商店类型: {shopName}";
          if (shopDef?.categories != null && shopDef.categories.Length > 0)
          {
            var remainingCats = new System.Collections.Generic.List<string>();
            int totalRemaining = 0;
            foreach (var cat in shopDef.categories)
            {
              if (cat.max_purchases > 0)
              {
                remainingCats.Add($"{cat.category}(剩{cat.max_purchases})");
                totalRemaining += cat.max_purchases;
              }
            }
            if (remainingCats.Count > 0)
              lines += $"\n商品种类: {string.Join(", ", remainingCats)}\n限购总数: {totalRemaining}";
          }
          return lines;
        }

        case MarkerType.EventPoint:
        {
          return "这里似乎发生了什么...";
        }

        default:
          return "?";
      }
    }

    // ══════════════════════════════════════════════════════
    //  工厂方法
    // ══════════════════════════════════════════════════════

    public static MapMarker CreateCamp(Vector2 pos, string campTypeId, string campInstanceId, int level = 1)
    {
      var def = WorldDatabase.GetCampType(campTypeId);
      return new MapMarker
      {
        MarkerId = campInstanceId ?? $"camp_{pos.GetHashCode():X8}",
        Type = MarkerType.Camp,
        WorldPosition = pos,
        DisplayName = def?.display_name ?? campTypeId,
        SubTypeId = campTypeId,
        State = DiscoveryState.Discovered,
        CampLevel = level,
        IsDestroyed = false,
        IconId = def?.map_icon_id ?? "icon_camp_basic"
      };
    }

    public static MapMarker CreateBoss(Vector2 pos, string bossId)
    {
      return new MapMarker
      {
        MarkerId = $"boss_{bossId}_{pos.GetHashCode():X8}",
        Type = MarkerType.WildBoss,
        WorldPosition = pos,
        DisplayName = $"野外Boss [{bossId}]",
        SubTypeId = bossId,
        State = DiscoveryState.Discovered,
        IconId = "icon_boss"
      };
    }

    public static MapMarker CreateMerchant(Vector2 pos, string shopTypeId)
    {
      var def = WorldDatabase.GetShop(shopTypeId);
      return new MapMarker
      {
        MarkerId = $"shop_{shopTypeId}_{pos.GetHashCode():X8}",
        Type = MarkerType.Merchant,
        WorldPosition = pos,
        DisplayName = def?.display_name ?? shopTypeId,
        SubTypeId = shopTypeId,
        State = DiscoveryState.Discovered,
        IconId = "icon_merchant"
      };
    }

    public static MapMarker CreateFinalBoss(Vector2 pos, string bossId)
    {
      return new MapMarker
      {
        MarkerId = $"finalboss_{bossId}_{pos.GetHashCode():X8}",
        Type = MarkerType.FinalBoss,
        WorldPosition = pos,
        DisplayName = $"最终Boss巢穴 [{bossId}]",
        SubTypeId = bossId,
        State = DiscoveryState.Discovered,
        IconId = "icon_final_boss",
        CampLevel = 15
      };
    }

    public static MapMarker CreateEventPoint(Vector2 pos, string eventId)
    {
      var def = WorldDatabase.GetEvent(eventId);
      return new MapMarker
      {
        MarkerId = $"event_{eventId}_{pos.GetHashCode():X8}",
        Type = MarkerType.EventPoint,
        WorldPosition = pos,
        DisplayName = def?.display_name ?? eventId,
        SubTypeId = eventId,
        State = DiscoveryState.Discovered,
        IconId = "icon_event"
      };
    }
  }
}
