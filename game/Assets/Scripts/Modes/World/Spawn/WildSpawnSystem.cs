using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Database;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.World
{
  /// <summary>
  /// 野外怪物自然生成系统。
  ///
  /// 在玩家周围的环形区域（视野外、删除距离内）周期性生成野外怪物。
  /// 刷怪参数（间隔/批次/密度/类型/属性）由世界等级通过 JSON 配表驱动。
  ///
  /// 设计要点：
  ///   - 生成距离：spawn_dist_min（> 视野范围16u）~ spawn_dist_max（< despawn距离90u）
  ///   - 属性缩放：取 worldLevel-1 的营地等级数值 × attr_scale
  ///   - AI 词条：与世界等级挂钩，与营地怪物逻辑一致
  ///   - 删除：超出 despawn_dist 直接 Destroy（不像营地怪物存列表）
  ///   - 生成速度远低于营地（间隔 6-15 秒 vs 营地 1-4 秒）
  ///
  /// 实现 IWorldSystem，由 WorldManager 管理生命周期。
  /// </summary>
  public class WildSpawnSystem : IWorldSystem, System.IDisposable
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    public bool DebugLog { get; set; }

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    readonly List<GameObject> _activeMonsters = new();
    EnemySpawner _enemySpawner;
    float _spawnTimer;
    float _cleanupTimer;
    int _lastWorldLevel = -1;
    const float CleanupInterval = 2f;
    Transform _playerTransform;
    bool _initialized;
    bool _paused;

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      WorldDatabase.EnsureLoaded();
      _enemySpawner = Object.FindObjectOfType<EnemySpawner>();

      if (_enemySpawner == null)
        Debug.LogWarning("[WildSpawnSystem] No EnemySpawner found in scene. Wild spawning disabled.");

      _initialized = true;

      if (DebugLog)
        Debug.Log("[WildSpawnSystem] Initialized.");
    }

    public void Tick(float deltaTime)
    {
      if (!_initialized || _paused) return;
      if (_enemySpawner == null) return;
      if (!WorldRuntimeContext.IsWorldModeActive || WorldRuntimeContext.IsRunEnded) return;

      // 查找玩家
      if (_playerTransform == null)
        _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
      if (_playerTransform == null) return;

      var worldLevel = WorldRuntimeContext.WorldLevel;

      // ── 世界等级变化 → 刷新野外怪物属性 ──
      if (_lastWorldLevel > 0 && worldLevel != _lastWorldLevel)
      {
        RefreshWildMonsterStats(worldLevel);
      }
      _lastWorldLevel = worldLevel;

      var config = WorldDatabase.GetWildSpawnForWorldLevel(worldLevel);
      if (config == null) return;

      // ── 刷怪计时器 ──
      _spawnTimer += deltaTime;
      if (_spawnTimer >= config.spawn_interval)
      {
        _spawnTimer -= config.spawn_interval;
        SpawnBatch(config, worldLevel);
      }

      // ── 定期清理超距怪物 ──
      _cleanupTimer += deltaTime;
      if (_cleanupTimer >= CleanupInterval)
      {
        _cleanupTimer -= CleanupInterval;
        CleanupFarMonsters(config.despawn_dist);
      }
    }

    public void OnPause() => _paused = true;
    public void OnResume() => _paused = false;

    public void Shutdown()
    {
      CleanupAllMonsters();
      _initialized = false;

      if (DebugLog)
        Debug.Log("[WildSpawnSystem] Shut down. All wild monsters destroyed.");
    }

    public void Dispose() => Shutdown();

    // ══════════════════════════════════════════════════════
    //  批量生成
    // ══════════════════════════════════════════════════════

    void SpawnBatch(WorldDatabase.WildSpawnDef config, int worldLevel)
    {
      if (config.enemy_types == null || config.enemy_types.Length == 0) return;

      // 检查存活上限
      CleanupNullRefs();
      if (_activeMonsters.Count >= config.max_alive) return;

      var batchCount = Random.Range(config.batch_count_min, config.batch_count_max + 1);
      var slotsLeft = config.max_alive - _activeMonsters.Count;
      batchCount = Mathf.Min(batchCount, slotsLeft);

      // 计算总权重
      float totalWeight = 0f;
      foreach (var et in config.enemy_types)
        if (et != null && !string.IsNullOrEmpty(et.enemy_id))
          totalWeight += et.weight;
      if (totalWeight <= 0f) return;

      for (int i = 0; i < batchCount; i++)
      {
        var enemyId = RollWeightedEnemy(config.enemy_types, totalWeight);
        if (string.IsNullOrEmpty(enemyId)) continue;

        SpawnOne(enemyId, config, worldLevel);
      }
    }

    void SpawnOne(string enemyId, WorldDatabase.WildSpawnDef config, int worldLevel)
    {
      var playerPos = (Vector2)_playerTransform.position;
      var angle = Random.Range(0f, UnityEngine.Mathf.PI * 2f);
      var dist = Random.Range(config.spawn_dist_min, config.spawn_dist_max);
      var spawnPos = playerPos + new Vector2(UnityEngine.Mathf.Cos(angle), UnityEngine.Mathf.Sin(angle)) * dist;

      // 生成 AI 词条（与世界等级挂钩，与营地一致）
      var affixSet = WorldAffixResolver.Resolve(worldLevel);

      var enemy = _enemySpawner.SpawnEnemy(enemyId, spawnPos, affixSet);
      if (enemy == null) return;

      // T3野外Boss作为小怪生成：去除BossCore组件（不加Boss标签，不在小地图标注）
      // 仅保留EnemyCore的普通AI行为，使用词条系统驱动
      if (enemyId.StartsWith("wild_boss_") || enemyId.StartsWith("final_boss_"))
      {
        var bossCore = enemy.GetComponent<global::Game.Shared.Enemy.AI.BossCore>();
        if (bossCore != null)
        {
          UnityEngine.Object.Destroy(bossCore);
        }
      }

      // 标记为野外来源
      var controller = enemy.GetComponent<EnemyCore>();
      if (controller != null)
      {
        controller.DisableAiStyle = true; // World 模式由词条驱动移动
        controller.SetWildOrigin();
      }

      // 应用属性缩放（≈ 营地等级 worldLevel-1 的数值 × attr_scale）
      ApplyWildScaling(enemy, enemyId, worldLevel, config.attr_scale);

      enemy.name = $"[Wild]_{enemyId}_{_activeMonsters.Count}";
      _activeMonsters.Add(enemy);

      if (DebugLog)
        Debug.Log($"[WildSpawnSystem] Spawned '{enemyId}' at {spawnPos} (WLv.{worldLevel})");
    }

    // ══════════════════════════════════════════════════════
    //  属性缩放
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 对野外怪物应用属性缩放：
    ///   使用 worldLevel-1（不低于1）的营地等级数值 × attr_scale
    ///   再叠加世界等级的 enemy_stat_mult
    /// </summary>
    void ApplyWildScaling(GameObject enemy, string enemyId, int worldLevel, float attrScale)
    {
      if (enemy == null) return;

      var enemyDef = EnemyDatabase.Get(enemyId);
      if (enemyDef == null) return;

      // 取 worldLevel-1 的营地等级数值（不低于 Lv.1）
      var campLevel = Mathf.Max(1, worldLevel - 1);
      var campLevelDef = WorldDatabase.GetCampLevel(campLevel);

      var worldLevelDef = WorldDatabase.GetWorldLevel(worldLevel);
      var worldStatMult = worldLevelDef?.enemy_stat_mult ?? 1f;

      // HP 缩放
      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        var scaledHp = enemyDef.base_hp * campLevelDef.enemy_hp_mult * attrScale * worldStatMult;
        if (scaledHp < 1f) scaledHp = 1f;
        health.Configure(scaledHp);
      }

      // 伤害/速度/索敌 缩放
      var controller = enemy.GetComponent<EnemyCore>();
      if (controller != null)
      {
        var baseSpeed = enemyDef.move_speed > 0f ? enemyDef.move_speed : 2.5f;
        var baseDamage = enemyDef.base_damage > 0f ? enemyDef.base_damage : 6f;

        var ai = EnemyAiProfileDatabase.Get(enemyDef.ai_profile);
        var aggro = ai?.aggro_range_base ?? 12f;

        var isRanged = enemyDef.attack_mode == "ranged" ||
                       enemyDef.attack_mode == "barrage" ||
                       enemyDef.attack_mode == "laser";
        var atkRange = isRanged
          ? (ai?.attack_range_base ?? 6.5f)
          : (ai?.attack_range_base ?? 1.1f);

        var windup = ai?.windup_base ?? 0.3f;
        var cooldown = ai?.attack_cooldown_base ?? 1.5f;

        controller.ApplyScaledStats(
          baseSpeed * campLevelDef.enemy_speed_mult * attrScale,
          baseDamage * campLevelDef.enemy_damage_mult * attrScale * worldStatMult,
          aggro,
          atkRange,
          windup,
          cooldown,
          campLevelDef.enemy_speed_mult * attrScale
        );
      }
    }

    // ══════════════════════════════════════════════════════
    //  清理
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 世界等级提升时：刷新所有野外怪物的属性。
    /// 更新MaxHp（保留CurrentHp比例）、伤害、速度——不更新AI词条。
    /// </summary>
    void RefreshWildMonsterStats(int worldLevel)
    {
      CleanupNullRefs();
      foreach (var m in _activeMonsters)
      {
        if (m == null) continue;
        var enemyId = ExtractWildEnemyId(m.name);
        if (string.IsNullOrEmpty(enemyId)) continue;

        var health = m.GetComponent<Health>();
        var hpBefore = health != null ? health.CurrentHp : 0f;
        var maxHpBefore = health != null ? health.MaxHp : 0f;
        var ratio = maxHpBefore > 0f ? hpBefore / maxHpBefore : 1f;

        var config = WorldDatabase.GetWildSpawnForWorldLevel(worldLevel);
        ApplyWildScaling(m, enemyId, worldLevel, config?.attr_scale ?? 1f);

        // 恢复血量比例
        if (health != null && ratio < 1f)
        {
          var currentHpField = typeof(Health).GetField("_currentHp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
          var targetHp = health.MaxHp * ratio;
          if (targetHp < 1f) targetHp = 1f;
          currentHpField?.SetValue(health, targetHp);
        }
      }

      if (DebugLog)
        Debug.Log($"[WildSpawnSystem] Refreshed stats for {_activeMonsters.Count} wild monsters (WLv.{worldLevel}).");
    }

    /// <summary>从名称中提取 enemyId（格式："[Wild]_{enemyId}_{index}"）。</summary>
    static string ExtractWildEnemyId(string name)
    {
      if (string.IsNullOrEmpty(name)) return null;
      var tagEnd = name.IndexOf("]_");
      if (tagEnd < 0) return null;
      var afterTag = name.Substring(tagEnd + 2);
      var lastUnderscore = afterTag.LastIndexOf('_');
      if (lastUnderscore < 0) return afterTag;
      return afterTag.Substring(0, lastUnderscore);
    }

    /// <summary>销毁超出删除距离的野外怪物。</summary>
    void CleanupFarMonsters(float despawnDist)
    {
      if (_playerTransform == null) return;
      var playerPos = (Vector2)_playerTransform.position;

      for (int i = _activeMonsters.Count - 1; i >= 0; i--)
      {
        var m = _activeMonsters[i];
        if (m == null)
        {
          _activeMonsters.RemoveAt(i);
          continue;
        }

        var dist = Vector2.Distance(playerPos, m.transform.position);
        if (dist > despawnDist)
        {
          if (DebugLog)
            Debug.Log($"[WildSpawnSystem] Despawned '{m.name}' at dist={dist:F0}");
          Object.Destroy(m);
          _activeMonsters.RemoveAt(i);
        }
      }
    }

    void CleanupNullRefs()
    {
      for (int i = _activeMonsters.Count - 1; i >= 0; i--)
        if (_activeMonsters[i] == null)
          _activeMonsters.RemoveAt(i);
    }

    void CleanupAllMonsters()
    {
      foreach (var m in _activeMonsters)
        if (m != null) Object.Destroy(m);
      _activeMonsters.Clear();
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    string RollWeightedEnemy(WorldDatabase.WildEnemyTypeEntry[] pool, float totalWeight)
    {
      var roll = Random.Range(0f, totalWeight);
      float cumulative = 0f;
      foreach (var et in pool)
      {
        if (et == null || string.IsNullOrEmpty(et.enemy_id)) continue;
        cumulative += et.weight;
        if (roll <= cumulative) return et.enemy_id;
      }
      return pool[pool.Length - 1]?.enemy_id;
    }

    static class Mathf
    {
      public static int Min(int a, int b) => a < b ? a : b;
      public static int Max(int a, int b) => a > b ? a : b;
      public static float Abs(float v) => v < 0f ? -v : v;
    }
  }
}
