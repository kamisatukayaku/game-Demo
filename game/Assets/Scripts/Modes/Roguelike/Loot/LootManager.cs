using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Gameplay.Bridges;
namespace Game.Modes.Roguelike.Loot
{
  /// <summary>
  /// 战利品管理器。读?loot_tables.json，执行加权随机抽取?
  /// </summary>
  public class LootManager : MonoBehaviour
  {
    [SerializeField] bool debugLog;

    static LootManager s_instance;

    Dictionary<string, (int min, int max)> _xpPools = new();
    EventListenerHandle _enemyKilledHandle;

    public static bool Exists => s_instance != null;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_LootManager");
      go.AddComponent<LootManager>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      LoadLootTables();
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDestroy()
    {
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);

      if (s_instance == this) s_instance = null;
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (!IsPlayerAttribution(evt.Killer))
        return;

      var drops = LootService.Roll(evt.LootTableId);
      if (drops == null || drops.Count == 0)
        return;

      if (ArenaLayoutLocator.Layout.IsActive)
        GrantArenaLoot(evt.Position, drops);
      else
        LootService.GrantToPlayerOrSpawnPickup(evt.Position, drops);

      if (debugLog)
        Debug.Log($"[LootManager] Loot from '{evt.EnemyId}' pool '{evt.LootTableId}' x{drops.Count}");
    }

    static void GrantArenaLoot(Vector3 position, List<LootService.LootDrop> drops)
    {
      foreach (var drop in drops)
      {
        if (drop.xp > 0)
          LootService.SpawnXpPickup(position, ArenaDifficultyRuntime.ScaleXp(drop.xp));
      }
    }

    static bool IsPlayerAttribution(GameObject killer) =>
      PlayerCombatAttribution.IsPlayerOrOwned(killer);

    void LoadLootTables()
    {
      var textAsset = Resources.Load<TextAsset>("Data/loot/loot_tables");
      if (textAsset == null)
      {
#if UNITY_EDITOR
        var path = System.IO.Path.Combine(Application.dataPath, "../../data/roguelike/loot/loot_tables.json");
        if (System.IO.File.Exists(path))
        {
          ParseJson(System.IO.File.ReadAllText(path));
          return;
        }
#endif
        Debug.LogWarning("[LootManager] loot_tables.json not found. Using default XP values.");
        SetupFallback();
        return;
      }

      ParseJson(textAsset.text);
    }

    void ParseJson(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<LootTableRoot>(json);
        if (root?.pools != null)
        {
          foreach (var pool in root.pools)
          {
            if (pool.entries != null)
            {
              foreach (var entry in pool.entries)
              {
                if (entry.kind == "xp" || entry.kind == "money")
                {
                  _xpPools[pool.id] = (entry.amount_min, entry.amount_max);
                  break;
                }
              }
            }
          }
          if (debugLog) Debug.Log($"[LootManager] Loaded {_xpPools.Count} loot pools.");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[LootManager] Failed to parse loot_tables.json: {e.Message}");
        SetupFallback();
      }
    }

    void SetupFallback()
    {
      _xpPools["common_mob"] = (8, 15);
      _xpPools["mutant_mob"] = (12, 22);
      _xpPools["polluted_mob"] = (10, 20);
      _xpPools["elite_mob"] = (18, 35);
      _xpPools["lane_mob"] = (5, 12);
      Debug.Log("[LootManager] Using fallback XP values.");
    }

    public static int RollXp(string poolId)
    {
      Game.Modes.Roguelike.Loot.LootService.EnsureLoaded();
      var drops = Game.Modes.Roguelike.Loot.LootService.Roll(poolId);
      int xp = 0;
      foreach (var d in drops)
        xp += d.xp;

      if (xp > 0)
        return xp;

      if (s_instance != null && s_instance._xpPools.TryGetValue(poolId, out var range))
        return Random.Range(range.min, range.max + 1);

      return Random.Range(5, 12);
    }
  }

  [System.Serializable]
  class LootTableRoot
  {
    public LootPool[] pools;
  }

  [System.Serializable]
  class LootPool
  {
    public string id;
    public LootEntry[] entries;
  }

  [System.Serializable]
  class LootEntry
  {
    public string kind;
    public int amount_min;
    public int amount_max;
  }
}
