using System.Collections.Generic;
using System;
using UnityEngine;

using Game.Shared.Combat;
namespace Game.Modes.Roguelike.Loot
{
  /// <summary>
  /// 战利品结算：?loot_tables.json，掉落经验?
  /// </summary>
  public static class LootService
  {
    static readonly Dictionary<string, LootPool> s_pools = new();
    static readonly HashSet<string> s_onceGranted = new();
    static bool s_loaded;

    public static void ResetRunLootState() => s_onceGranted.Clear();

    public struct LootDrop
    {
      public int xp;
    }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;

      s_loaded = true;
      s_pools.Clear();

      if (!TryLoadJson(ParseJson))
        SetupFallback();
    }

    public static List<LootDrop> Roll(string poolId)
    {
      EnsureLoaded();
      var results = new List<LootDrop>();

      if (!s_pools.TryGetValue(poolId, out var pool) || pool.entries == null || pool.entries.Count == 0)
      {
        results.Add(new LootDrop { xp = UnityEngine.Random.Range(5, 12) });
        return results;
      }

      int rolls = Mathf.Max(1, pool.rolls);
      for (int r = 0; r < rolls; r++)
      {
        var entry = PickWeighted(pool.entries);
        if (entry == null)
          continue;

        switch (entry.kind)
        {
          case "xp":
          case "money":
            results.Add(new LootDrop
            {
              xp = UnityEngine.Random.Range(entry.amount_min, entry.amount_max + 1)
            });
            break;
        }
      }

      if (results.Count == 0)
        results.Add(new LootDrop { xp = UnityEngine.Random.Range(5, 12) });

      return results;
    }

    public static void GrantToPlayerOrSpawnPickup(Vector3 position, List<LootDrop> drops)
    {
      if (drops == null)
        return;

      foreach (var drop in drops)
      {
        if (drop.xp > 0)
          SpawnXpPickup(position, drop.xp);
      }
    }

    public static void SpawnXpPickup(Vector3 position, int amount)
    {
      if (amount <= 0)
        return;

      var orbCount = GetXpOrbCount(amount);
      var remaining = amount;
      for (var i = 0; i < orbCount; i++)
      {
        var value = Mathf.Max(1, remaining / (orbCount - i));
        remaining -= value;
        XpPickup.Spawn(position, value);
      }
    }

    static int GetXpOrbCount(int amount)
    {
      if (amount >= 160) return Mathf.Clamp(Mathf.RoundToInt(amount / 18f), 8, 14);
      if (amount >= 80) return Mathf.Clamp(Mathf.RoundToInt(amount / 18f), 5, 9);
      if (amount >= 28) return 3;
      return 1;
    }

    static LootEntry PickWeighted(List<LootEntry> entries)
    {
      float total = 0f;
      foreach (var e in entries)
        total += Mathf.Max(0f, e.weight);

      if (total <= 0f)
        return entries.Count > 0 ? entries[0] : null;

      float roll = UnityEngine.Random.Range(0f, total);
      foreach (var e in entries)
      {
        roll -= e.weight;
        if (roll <= 0f)
          return e;
      }

      return entries[entries.Count - 1];
    }

    static bool TryLoadJson(Action<string> parser)
    {
      var path = System.IO.Path.Combine(Application.dataPath, "../../data/roguelike/loot/loot_tables.json");
      if (System.IO.File.Exists(path))
      {
        parser(System.IO.File.ReadAllText(path));
        return true;
      }

      var textAsset = Resources.Load<TextAsset>("Data/loot/loot_tables");
      if (textAsset != null)
      {
        parser(textAsset.text);
        return true;
      }

      return false;
    }

    static void ParseJson(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<LootTableRoot>(json);
        if (root?.pools == null)
          return;

        foreach (var pool in root.pools)
        {
          if (pool == null || string.IsNullOrEmpty(pool.id))
            continue;

          var runtime = new LootPool
          {
            id = pool.id,
            rolls = pool.rolls > 0 ? pool.rolls : 1,
            entries = new List<LootEntry>()
          };

          if (pool.entries != null)
          {
            foreach (var entry in pool.entries)
            {
              if (entry != null && (entry.kind == "xp" || entry.kind == "money"))
                runtime.entries.Add(entry);
            }
          }

          s_pools[pool.id] = runtime;
        }
      }
      catch (Exception e)
      {
        Debug.LogError($"[LootService] Parse failed: {e.Message}");
      }
    }

    static void SetupFallback()
    {
      s_pools["common_mob"] = new LootPool
      {
        id = "common_mob",
        rolls = 1,
        entries = new List<LootEntry>
        {
          new() { kind = "xp", weight = 100, amount_min = 10, amount_max = 18 }
        }
      };
      s_pools["elite_mob"] = new LootPool
      {
        id = "elite_mob",
        rolls = 1,
        entries = new List<LootEntry>
        {
          new() { kind = "xp", weight = 100, amount_min = 28, amount_max = 45 }
        }
      };
    }

    class LootPool
    {
      public string id;
      public int rolls;
      public List<LootEntry> entries;
    }

    [Serializable]
    class LootTableRoot
    {
      public LootPoolJson[] pools;
    }

    [Serializable]
    class LootPoolJson
    {
      public string id;
      public int rolls;
      public LootEntry[] entries;
    }

    [Serializable]
    public class LootEntry
    {
      public float weight = 1f;
      public string kind;
      public int amount_min;
      public int amount_max;
    }
  }
}
