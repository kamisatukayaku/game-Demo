using System.Collections.Generic;
using System;
using UnityEngine;

namespace Game.World
{
  [Serializable]
  public class PlacementEntry
  {
    public Vector2 Position;
    public string Category;
    public string TypeId;
  }

  [Serializable]
  public class WorldGenTypeEntry
  {
    public string TypeId;
    public int Count = 1;
    public float MinSameTypeDistance = 50f;
    public float MinSameCategoryDistance = 30f;
    public float MinAnyDistance = 20f;
  }

  [Serializable]
  public class WorldGenCategoryConfig
  {
    public List<WorldGenTypeEntry> Entries = new List<WorldGenTypeEntry>();
  }

  [Serializable]
  public class WorldGenConfig
  {
    public float BoundsRadius = 512f;
    public string DifficultyId = "normal";
    public WorldGenCategoryConfig CampConfig = new WorldGenCategoryConfig();
    public WorldGenCategoryConfig BossConfig = new WorldGenCategoryConfig();
    public WorldGenCategoryConfig MerchantConfig = new WorldGenCategoryConfig();
    public WorldGenCategoryConfig EventConfig = new WorldGenCategoryConfig();
    public int MaxAttemptsPerPlacement = 200;
    public bool DebugLog;

    public static WorldGenConfig Default => new WorldGenConfig
    {
      BoundsRadius = 512f,
      DifficultyId = "normal",
      MaxAttemptsPerPlacement = 200,
      CampConfig = new WorldGenCategoryConfig
      {
        Entries = new List<WorldGenTypeEntry>
        {
          new WorldGenTypeEntry { TypeId = "camp_temporary", Count = 6, MinSameTypeDistance = 50f, MinSameCategoryDistance = 35f, MinAnyDistance = 25f },
          new WorldGenTypeEntry { TypeId = "camp_normal",    Count = 4, MinSameTypeDistance = 70f, MinSameCategoryDistance = 45f, MinAnyDistance = 30f },
          new WorldGenTypeEntry { TypeId = "camp_elite",     Count = 2, MinSameTypeDistance = 100f, MinSameCategoryDistance = 60f, MinAnyDistance = 40f }
        }
      },
      BossConfig = new WorldGenCategoryConfig
      {
        Entries = new List<WorldGenTypeEntry>
        {
          new WorldGenTypeEntry { TypeId = "wild_boss_star_hive",     Count = 1, MinSameTypeDistance = 150f, MinSameCategoryDistance = 120f, MinAnyDistance = 60f },
          new WorldGenTypeEntry { TypeId = "wild_boss_hex_king",     Count = 1, MinSameTypeDistance = 150f, MinSameCategoryDistance = 120f, MinAnyDistance = 60f },
          new WorldGenTypeEntry { TypeId = "wild_boss_pent_colossus",Count = 1, MinSameTypeDistance = 150f, MinSameCategoryDistance = 120f, MinAnyDistance = 60f }
        }
      },
      MerchantConfig = new WorldGenCategoryConfig
      {
        Entries = new List<WorldGenTypeEntry>
        {
          new WorldGenTypeEntry { TypeId = "world_shop", Count = 2, MinSameTypeDistance = 150f, MinSameCategoryDistance = 100f, MinAnyDistance = 40f }
        }
      },
      EventConfig = new WorldGenCategoryConfig
      {
        Entries = new List<WorldGenTypeEntry>
        {
          new WorldGenTypeEntry { TypeId = "event_point", Count = 8, MinSameTypeDistance = 60f, MinSameCategoryDistance = 40f, MinAnyDistance = 25f }
        }
      }
    };
  }

    // ══════════════════════════════════════════════════════
    //  输出结构
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 世界生成结果。含地图数据、POI 列表等。
    /// 生成完成后赋值给 WorldRuntimeContext.MapData。
    /// </summary>
    public class WorldGenResult
    {
    public List<PlacementEntry> Placements = new List<PlacementEntry>();
    public string DifficultyId;
    public Vector2 PlayerSpawnPoint;
    }

  public class WorldGenerator
  {
    public WorldGenResult Generate(WorldGenConfig config)
    {
      return GenerateInternal(config, null, 0f, 0f);
    }

    public List<PlacementEntry> AppendGenerate(WorldGenConfig config, List<PlacementEntry> existing)
    {
      return PlaceAllCategories(config, null, 0f, 0f, existing ?? new List<PlacementEntry>());
    }

    public WorldGenResult GenerateNear(WorldGenConfig config, Vector2 center, float nearMin, float nearMax)
    {
      return GenerateInternal(config, center, nearMin, nearMax);
    }

    public List<PlacementEntry> AppendGenerateNear(
      WorldGenConfig config, Vector2 center, float nearMin, float nearMax, List<PlacementEntry> existing)
    {
      return PlaceAllCategories(config, center, nearMin, nearMax, existing ?? new List<PlacementEntry>());
    }

    WorldGenResult GenerateInternal(
      WorldGenConfig config, Vector2? center, float nearMin, float nearMax)
    {
      if (config == null)
      {
        Debug.LogError("[WorldGenerator] Config is null.");
        return new WorldGenResult { DifficultyId = "normal" };
      }

      var placements = new List<PlacementEntry>();
      PlaceAllCategories(config, center, nearMin, nearMax, placements);
      var spawnPoint = ComputePlayerSpawn(config, placements, center);

      if (config.DebugLog)
        Debug.Log($"[WorldGenerator] Generation complete. mode=center:{(center.HasValue ? "near" : "random")} " +
                  $"placements={placements.Count} spawn=({spawnPoint.x:F0},{spawnPoint.y:F0})");

      return new WorldGenResult
      {
        Placements = placements,
        DifficultyId = config.DifficultyId,
        PlayerSpawnPoint = spawnPoint
      };
    }

    List<PlacementEntry> PlaceAllCategories(
      WorldGenConfig config, Vector2? center, float nearMin, float nearMax, List<PlacementEntry> placements)
    {
      PlaceCategory(config.CampConfig, "Camp", config, center, nearMin, nearMax, placements);
      PlaceCategory(config.BossConfig, "Boss", config, center, nearMin, nearMax, placements);
      PlaceCategory(config.MerchantConfig, "Merchant", config, center, nearMin, nearMax, placements);
      PlaceCategory(config.EventConfig, "Event", config, center, nearMin, nearMax, placements);
      return placements;
    }

    void PlaceCategory(WorldGenCategoryConfig catConfig, string categoryName, WorldGenConfig rootConfig,
      Vector2? center, float nearMin, float nearMax, List<PlacementEntry> placements)
    {
      if (catConfig == null || catConfig.Entries == null || catConfig.Entries.Count == 0) return;
      int skipped = 0;
      foreach (var entry in catConfig.Entries)
      {
        if (entry == null || string.IsNullOrEmpty(entry.TypeId) || entry.Count <= 0) continue;
        int placed = 0;
        for (int i = 0; i < entry.Count; i++)
        {
          if (TryPlaceOne(entry, categoryName, rootConfig, center, nearMin, nearMax, placements))
            placed++;
          else skipped++;
        }
        if (rootConfig.DebugLog && placed < entry.Count)
          Debug.LogWarning($"[WorldGenerator] {categoryName}/{entry.TypeId}: placed {placed}/{entry.Count}, skipped {entry.Count - placed}");
      }
      if (rootConfig.DebugLog && skipped > 0)
        Debug.LogWarning($"[WorldGenerator] {categoryName}: total skipped {skipped} placements.");
    }

    bool TryPlaceOne(WorldGenTypeEntry entry, string category, WorldGenConfig config,
      Vector2? center, float nearMin, float nearMax, List<PlacementEntry> placements)
    {
      int maxAttempts = config?.MaxAttemptsPerPlacement ?? 200;
      for (int attempt = 0; attempt < maxAttempts; attempt++)
      {
        Vector2 pos = GenerateRandomPosition(center, nearMin, nearMax, config.BoundsRadius);
        if (ValidatePlacement(pos, category, entry.TypeId,
              entry.MinSameTypeDistance, entry.MinSameCategoryDistance, entry.MinAnyDistance, placements))
        {
          placements.Add(new PlacementEntry { Position = pos, Category = category, TypeId = entry.TypeId });
          return true;
        }
      }
      return false;
    }

    static Vector2 GenerateRandomPosition(Vector2? center, float nearMin, float nearMax, float boundsRadius)
    {
      if (center.HasValue)
      {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distance = UnityEngine.Random.Range(nearMin, nearMax);
        return center.Value + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
      }
      else
      {
        return new Vector2(
          UnityEngine.Random.Range(-boundsRadius, boundsRadius),
          UnityEngine.Random.Range(-boundsRadius, boundsRadius));
      }
    }

    static bool ValidatePlacement(Vector2 pos, string targetCategory, string targetTypeId,
      float minSameTypeDist, float minSameCategoryDist, float minAnyDist, List<PlacementEntry> existing)
    {
      float anySqr = minAnyDist * minAnyDist;
      float sameCatSqr = minSameCategoryDist * minSameCategoryDist;
      float sameTypeSqr = minSameTypeDist * minSameTypeDist;
      foreach (var entry in existing)
      {
        float sqrDist = (pos - entry.Position).sqrMagnitude;
        if (sqrDist < anySqr) return false;
        if (entry.Category == targetCategory)
        {
          if (sqrDist < sameCatSqr) return false;
          if (entry.TypeId == targetTypeId && sqrDist < sameTypeSqr) return false;
        }
      }
      return true;
    }

    static Vector2 ComputePlayerSpawn(WorldGenConfig config, List<PlacementEntry> placements, Vector2? center)
    {
      const float safeDistance = 40f;
      const int maxSpawnAttempts = 50;
      if (center.HasValue)
      {
        float nearMax = config.BoundsRadius * 0.3f;
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
          float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
          float dist = UnityEngine.Random.Range(nearMax * 0.2f, nearMax);
          var candidate = center.Value + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
          if (ValidatePlacement(candidate, "_spawn_", "_spawn_", safeDistance, safeDistance, safeDistance, placements))
            return candidate;
        }
        return center.Value;
      }
      else
      {
        float halfR = config.BoundsRadius * 0.5f;
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
          float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
          float dist = UnityEngine.Random.Range(halfR * 0.8f, halfR);
          var candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
          bool safe = true;
          float safeSqr = safeDistance * safeDistance;
          foreach (var entry in placements)
          {
            if ((candidate - entry.Position).sqrMagnitude < safeSqr) { safe = false; break; }
          }
          if (safe) return candidate;
        }
        return Vector2.zero;
      }
    }
  }
}
