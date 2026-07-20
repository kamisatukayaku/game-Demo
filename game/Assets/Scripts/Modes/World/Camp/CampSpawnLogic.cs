using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Database;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.World
{
  /// <summary>
  /// 营地刷怪逻辑。根据营地状态和等级生成怪物?
  ///
  /// 复用现有 EnemySpawner 创建怪物实体，不做怪物系统重写?
  /// 所有刷怪参数来?WorldDatabase JSON 配表?
  ///
  /// 使用方式?
  ///   var spawnLogic = new CampSpawnLogic(enemySpawner, campTypeDef, levelLogic);
  ///   每帧调用 spawnLogic.Tick(deltaTime, campCenter, campState);
  ///
  /// 状态对应：
  ///   Dormant  ?不刷怀"
  ///   Alert    ?慢速刷怪（间隔 × 1.5?
  ///   Active   ?全速刷怪（配表间隔?
  ///   Destroyed ?停止刷怀"
  /// </summary>
  public class CampSpawnLogic
  {
    // ══════════════════════════════════════════════════════
    //  配置
    // ══════════════════════════════════════════════════════

    readonly EnemySpawner _enemySpawner;
    readonly WorldDatabase.CampTypeDef _typeDef;
    readonly CampLevelLogic _levelLogic;

    /// <summary>从 enemy_archetype_ids 中随机选取的敌人池（每局固定）</summary>
    string[] _selectedEnemyPool;

    /// <summary>Boss巢穴是否已预生成初始敌人</summary>
    bool _bossNestPreSpawned;

    /// <summary>营地实例 ID</summary>
    string _campInstanceId;

    /// <summary>营地中心世界坐标</summary>
    Vector2 _campCenter;

    /// <summary>刷怪半径（怪物在营地周围此半径内生成）</summary>
    public float SpawnRadius { get; set; } = 6f;

    // ══════════════════════════════════════════════════════
    //  运行时状态"
    // ══════════════════════════════════════════════════════

    float _spawnTimer;
    readonly List<GameObject> _activeEnemies = new();

    /// <summary>当前距离激活等级（由 DistanceActivationSystem 设置）</summary>
    DistanceActivationLevel _currentDistanceLevel = DistanceActivationLevel.Level2_Near;

    /// <summary>上一次距离等级，用于检测 Level0→Level1+ 过渡</summary>
    DistanceActivationLevel _previousDistanceLevel = DistanceActivationLevel.Level2_Near;

    /// <summary>
    /// 存储的敌人快照：玩家远离营地（Level0_Far）时，保存活跃敌人的类型+血量比例。
    /// 玩家靠近时从此列表恢复生成，属性变化（营地等级/世界等级提升）是允许的。
    /// </summary>
    readonly List<CampEnemyRecord> _storedEnemyRecords = new();

    /// <summary>
    /// 休眠预制怪池：Dormant状态下预刷怪的敌人类型列表。
    /// 进入Alert/Active时优先从此列表生成实体。
    /// </summary>
    readonly List<string> _dormantPool = new();

    /// <summary>由 CampController 调用，同步距离等级</summary>
    public void SetDistanceLevel(DistanceActivationLevel level)
    {
      _previousDistanceLevel = _currentDistanceLevel;
      _currentDistanceLevel = level;
      ApplyDistanceLevelToActiveEnemies();
    }

    /// <summary>对已生成的怪物应用距离优化，含持久化存储/恢复逻辑</summary>
    void ApplyDistanceLevelToActiveEnemies()
    {
      // ── Level0_Far：存储敌人数据后销毁 ──
      if (_currentDistanceLevel == DistanceActivationLevel.Level0_Far)
      {
        StoreActiveEnemies();
        // 统一销毁所有已存储的活跃敌人对象
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
          var enemy = _activeEnemies[i];
          if (enemy != null) Object.Destroy(enemy);
          _activeEnemies.RemoveAt(i);
        }
        return;
      }

      // ── 从 Level0 恢复到 Level1+：重新生成存储的敌人 ──
      if (_previousDistanceLevel == DistanceActivationLevel.Level0_Far &&
          _currentDistanceLevel >= DistanceActivationLevel.Level1_Mid)
      {
        RespawnFromStoredRecords();
      }

      // ── Level1_Mid / Level2_Near：正常的组件启用/禁用 ──
      for (int i = _activeEnemies.Count - 1; i >= 0; i--)
      {
        var enemy = _activeEnemies[i];
        if (enemy == null) { _activeEnemies.RemoveAt(i); continue; }

        var isBoss = enemy.name.Contains("Boss", System.StringComparison.OrdinalIgnoreCase) ||
                     enemy.name.Contains("boss", System.StringComparison.OrdinalIgnoreCase);

        DistanceActivationSystem.ApplyToEnemy(enemy, _currentDistanceLevel, isBoss);
      }
    }

    /// <summary>当前存活的营地怪物?/summary>
    public int AliveCount
    {
      get
      {
        CleanupDeadEnemies();
        return _activeEnemies.Count;
      }
    }

    // ══════════════════════════════════════════════════════
    //  构速"
    // ══════════════════════════════════════════════════════

    /// <param name="enemySpawner">现有 EnemySpawner 实例（FindObjectOfType 获取或手动注入）</param>
    /// <param name="typeDef">营地类型定义（来?WorldDatabase.GetCampType?/param>
    /// <param name="levelLogic">营地等级逻辑实例</param>
    public CampSpawnLogic(
      EnemySpawner enemySpawner,
      WorldDatabase.CampTypeDef typeDef,
      CampLevelLogic levelLogic)
    {
      _enemySpawner = enemySpawner;
      _typeDef = typeDef;
      _levelLogic = levelLogic;

      // 从 enemy_archetype_ids 中随机选取 enemy_pool_size 个敌人（每局固定）
      _selectedEnemyPool = SelectEnemyPool(typeDef);
    }

    /// <summary>从营地类型定义中随机选取敌人池（每局固定）。</summary>
    static string[] SelectEnemyPool(WorldDatabase.CampTypeDef typeDef)
    {
      if (typeDef?.enemy_archetype_ids == null || typeDef.enemy_archetype_ids.Length == 0)
        return new string[0];

      var poolSize = typeDef.enemy_pool_size > 0
        ? typeDef.enemy_pool_size
        : Mathf.Min(3, typeDef.enemy_archetype_ids.Length);

      poolSize = Mathf.Min(poolSize, typeDef.enemy_archetype_ids.Length);

      var all = new List<string>(typeDef.enemy_archetype_ids);
      var pool = new string[poolSize];
      for (int i = 0; i < poolSize; i++)
      {
        var idx = Random.Range(0, all.Count);
        pool[i] = all[idx];
        all.RemoveAt(idx);
      }
      return pool;
    }

    /// <summary>设置营地身份（营地实例ID），供怪物生成时标记来源。</summary>
    public void SetCampIdentity(string campInstanceId)
    {
      _campInstanceId = campInstanceId;
    }

    // ══════════════════════════════════════════════════════
    //  Tick
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 更新刷怪逻辑?
    /// </summary>
    /// <param name="deltaTime">帧间隔（秒）</param>
    /// <param name="campCenter">营地中心世界坐标</param>
    /// <param name="state">当前营地状?/param>
    public void Tick(float deltaTime, Vector2 campCenter, CampController.CampState state)
    {
      _campCenter = campCenter;

      if (state == CampController.CampState.Destroyed)
        return;

      // ── Boss巢穴：不自然刷怪，但首次进入 Active 时预生成 Boss + 精英 ──
      if (_typeDef.is_boss_nest)
      {
        if (!_bossNestPreSpawned && state != CampController.CampState.Dormant)
        {
          PreSpawnBossNest();
          _bossNestPreSpawned = true;
        }
        CleanupDeadEnemies();
        return;
      }

      // ── Dormant：预刷怪到列表（不生成GameObject）──
      if (state == CampController.CampState.Dormant)
      {
        TickDormantSpawn(deltaTime);
        return;
      }

      // Level0 距离 ?不刷怪，仅清琀"
      if (_currentDistanceLevel == DistanceActivationLevel.Level0_Far)
      {
        CleanupDeadEnemies();
        ApplyDistanceLevelToActiveEnemies();
        return;
      }

      CleanupDeadEnemies();

      // 优先从预制怪池恢复（从Dormant进入Alert时的残留）
      SpawnFromDormantPool(maxPerTick: 2);

      var maxAlive = _levelLogic.GetMaxAliveEnemies();
      if (_activeEnemies.Count >= maxAlive)
        return;

      var baseInterval = _levelLogic.GetSpawnInterval();
      var interval = state == CampController.CampState.Alert
        ? baseInterval * 1.5f
        : baseInterval;

      _spawnTimer += deltaTime;
      if (_spawnTimer < interval)
        return;

      _spawnTimer -= interval;

      var countMult = _levelLogic.GetEnemyCountMult();
      var spawnCount = Mathf.Max(1, Mathf.RoundToInt(countMult));
      var slotsAvailable = maxAlive - _activeEnemies.Count;
      spawnCount = Mathf.Min(spawnCount, slotsAvailable);

      for (int i = 0; i < spawnCount; i++)
        SpawnOne();
    }

    /// <summary>Dormant状态：以2.5x间隔将怪物类型加入预制池。</summary>
    void TickDormantSpawn(float deltaTime)
    {
      var dormantCap = _typeDef?.dormant_max_alive ?? 0;
      if (dormantCap <= 0) return;
      if (_dormantPool.Count >= dormantCap) return;

      var baseInterval = _levelLogic.GetSpawnInterval();
      var interval = baseInterval * 2.5f;

      _spawnTimer += deltaTime;
      if (_spawnTimer < interval) return;
      _spawnTimer -= interval;

      // 随机选怪并加入预制池（使用营地专属敌人池）
      if (_selectedEnemyPool != null && _selectedEnemyPool.Length > 0)
      {
        var idx = Random.Range(0, _selectedEnemyPool.Length);
        _dormantPool.Add(_selectedEnemyPool[idx]);
      }
    }

    /// <summary>将预制池中的怪物逐步生成实体（避免一帧内大量生成）。</summary>
    void SpawnFromDormantPool(int maxPerTick)
    {
      int spawned = 0;
      while (_dormantPool.Count > 0 && spawned < maxPerTick)
      {
        var enemyId = _dormantPool[0];
        _dormantPool.RemoveAt(0);
        SpawnOneById(enemyId, -1f);
        spawned++;
      }
    }

    // ══════════════════════════════════════════════════════
    //  怪物生成
    // ══════════════════════════════════════════════════════

    void SpawnOne()
    {
      if (_enemySpawner == null)
      {
        Debug.LogWarning("[CampSpawnLogic] EnemySpawner is null, cannot spawn.");
        return;
      }

      if (_selectedEnemyPool == null || _selectedEnemyPool.Length == 0)
        return;

      var idx = Random.Range(0, _selectedEnemyPool.Length);
      var enemyId = _selectedEnemyPool[idx];

      SpawnOneById(enemyId, -1f);
    }

    /// <summary>
    /// 按敌人 ID 生成一只怪物。若 hpRatio >= 0，则按比例设置当前HP（恢复用）。
    /// hpRatio = -1 表示满血，0~1 表示血量比例。
    /// </summary>
    void SpawnOneById(string enemyId, float hpRatio = -1f)
    {
      if (_enemySpawner == null || string.IsNullOrEmpty(enemyId)) return;

      var spawnPos = _campCenter + Random.insideUnitCircle.normalized *
        Random.Range(SpawnRadius * 0.5f, SpawnRadius);

      var affixSet = WorldAffixResolver.Resolve(WorldRuntimeContext.WorldLevel);
      var enemy = _enemySpawner.SpawnEnemy(enemyId, spawnPos, affixSet);

      if (enemy == null)
      {
        Debug.LogWarning($"[CampSpawnLogic] Failed to spawn '{enemyId}' at {spawnPos}");
        return;
      }

      var controller = enemy.GetComponent<EnemyCore>();
      if (controller != null)
      {
        controller.DisableAiStyle = true; // World 模式由词条驱动移动
        float regenRate = _levelLogic.GetRegenRate();
        controller.SetCampOrigin(_campInstanceId ?? "unknown", _campCenter, regenRate);
      }

      ApplyCampScaling(enemy, enemyId);

      // 按血量比例恢复（比例由存储快照提供，允许MaxHp因等级变化而不同）
      if (hpRatio >= 0f)
      {
        var health = enemy.GetComponent<Health>();
        if (health != null && hpRatio < 1f)
        {
          var targetHp = health.CurrentHp * hpRatio;
          if (targetHp < 1f) targetHp = 1f;
          var currentHpField = typeof(Health).GetField("_currentHp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
          currentHpField?.SetValue(health, targetHp);
        }
      }

      enemy.name = $"[Camp]_{enemyId}_{_activeEnemies.Count}";
      _activeEnemies.Add(enemy);
    }

    // ══════════════════════════════════════════════════════
    //  Boss巢穴预生成
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Boss巢穴预生成：一次性生成野外Boss + N个精英怪。
    /// Boss不加Boss标签（以T3敌人形式出现），精英从营地敌人池中选取。
    /// </summary>
    public void PreSpawnBossNest()
    {
      if (_enemySpawner == null || string.IsNullOrEmpty(_typeDef.boss_nest_boss_id))
      {
        Debug.LogWarning("[CampSpawnLogic] Boss nest missing boss_nest_boss_id in camp type config.");
        return;
      }

      int eliteCount = _typeDef.boss_nest_elite_count > 0 ? _typeDef.boss_nest_elite_count : 3;

      // 1. 生成 Boss（使用基础生成方式，不通过 SpawnEnemy(boss) 避免挂载BossCore标签）
      var bossId = _typeDef.boss_nest_boss_id;
      var bossPos = _campCenter;
      var affixSet = WorldAffixResolver.Resolve(WorldRuntimeContext.WorldLevel);
      var boss = _enemySpawner.SpawnEnemy(bossId, bossPos, affixSet);

      if (boss != null)
      {
        var bossCore = boss.GetComponent<BossCore>();
        if (bossCore == null)
        {
          // Boss巢穴的boss也挂载BossCore（用于技能系统），但通过 CampSpawnLogic 标记为非Boss类别
          var controller = boss.GetComponent<EnemyCore>();
          if (controller != null)
          {
            controller.DisableAiStyle = true;
            float regenRate = _levelLogic.GetRegenRate();
            controller.SetCampOrigin(_campInstanceId ?? "unknown", _campCenter, regenRate);
          }
          ApplyCampScaling(boss, bossId);
        }
        boss.name = $"[Camp]_{bossId}_boss";
        _activeEnemies.Add(boss);
      }

      // 2. 生成精英怪
      for (int i = 0; i < eliteCount; i++)
      {
        if (_selectedEnemyPool == null || _selectedEnemyPool.Length == 0) break;
        var eliteId = _selectedEnemyPool[Random.Range(0, _selectedEnemyPool.Length)];
        var elitePos = _campCenter + Random.insideUnitCircle.normalized *
          Random.Range(SpawnRadius * 0.3f, SpawnRadius * 1.2f);
        var elite = _enemySpawner.SpawnEnemy(eliteId, elitePos, affixSet);

        if (elite != null)
        {
          var controller = elite.GetComponent<EnemyCore>();
          if (controller != null)
          {
            controller.DisableAiStyle = true;
            float regenRate = _levelLogic.GetRegenRate();
            controller.SetCampOrigin(_campInstanceId ?? "unknown", _campCenter, regenRate);
          }
          ApplyCampScaling(elite, eliteId);
          elite.name = $"[Camp]_{eliteId}_{i}";
          _activeEnemies.Add(elite);
        }
      }

      Debug.Log($"[CampSpawnLogic] Boss nest pre-spawned: {bossId} + {eliteCount} elites.");
    }

    /// <summary>
    /// 存储所有活跃敌人的类型和血量比例到 _storedEnemyRecords。
    /// 在切换到 Level0_Far 时调用。
    /// 存储比例而非绝对HP，使恢复时可应用当前营地等级的属性缩放。
    /// </summary>
    void StoreActiveEnemies()
    {
      _storedEnemyRecords.Clear();
      foreach (var enemy in _activeEnemies)
      {
        if (enemy == null) continue;

        var enemyId = ExtractEnemyIdFromName(enemy.name);
        if (string.IsNullOrEmpty(enemyId)) continue;

        var health = enemy.GetComponent<Health>();
        var ratio = health != null && health.MaxHp > 0f
          ? UnityEngine.Mathf.Clamp01(health.CurrentHp / health.MaxHp)
          : 1f;

        _storedEnemyRecords.Add(new CampEnemyRecord(enemyId, ratio));
      }
    }

    /// <summary>
    /// 从 _storedEnemyRecords 重新生成所有存储的敌人并恢复血量比例。
    /// 在从 Level0_Far 恢复到 Level1+ 时调用。
    /// 属性变化（营地等级/世界等级提升）是允许的——重新生成时使用当前属性。
    /// </summary>
    void RespawnFromStoredRecords()
    {
      if (_storedEnemyRecords.Count == 0) return;

      int respawned = 0;
      foreach (var record in _storedEnemyRecords)
      {
        SpawnOneById(record.EnemyId, record.HpRatio);
        respawned++;
      }

      _storedEnemyRecords.Clear();
      _spawnTimer = 0f;

      if (respawned > 0)
        Debug.Log($"[CampSpawnLogic] Respawned {respawned} enemies from stored records.");
    }

    /// <summary>从 GameObject 名称中提取 enemyId（格式："[Camp]_{enemyId}_{index}"）</summary>
    static string ExtractEnemyIdFromName(string name)
    {
      if (string.IsNullOrEmpty(name)) return null;
      // 格式: [Camp]_mob_hex_01_0
      var tagEnd = name.IndexOf("]_");
      if (tagEnd < 0) return null;
      var afterTag = name.Substring(tagEnd + 2); // "_mob_hex_01_0"
      var lastUnderscore = afterTag.LastIndexOf('_');
      if (lastUnderscore < 0) return afterTag;
      return afterTag.Substring(0, lastUnderscore);
    }

    /// <summary>
    /// 对已生成的营地怪物应用营地等级数值缩放?
    /// 完全复用 EnemySpawner.ApplyWaveScaling 的模式：
    ///   1. Health.Configure(scaledHp)
    ///   2. EnemySphereController.ApplyScaledStats(scaledSpeed, scaledDamage, ...)
    ///
    /// 所有缩放倍率来自 WorldDatabase.CampLevelDef?
    /// </summary>
    void ApplyCampScaling(GameObject enemy, string enemyId)
    {
      if (enemy == null) return;

      var def = _enemySpawner.GetEnemyDef(enemyId);
      if (def == null) return;

      var hpMult = _levelLogic.GetEnemyHpMult();
      var dmgMult = _levelLogic.GetEnemyDamageMult();
      var spdMult = _levelLogic.GetEnemySpeedMult();

      // HP 缩放 ??ApplyWaveScaling 完全一臀"
      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        var scaledHp = def.base_hp * hpMult;
        if (scaledHp < 1f) scaledHp = 1f;
        health.Configure(scaledHp);
      }

      // 伤害/速度缩放 ??ApplyWaveScaling 完全一臀"
      var controller = enemy.GetComponent<EnemyCore>();
      if (controller != null)
      {
        var baseSpeed = def.move_speed > 0f ? def.move_speed : 2.5f;
        var baseDamage = def.base_damage > 0f ? def.base_damage : 6f;

        var ai = EnemyAiProfileDatabase.Get(def.ai_profile);
        var aggro = ai?.aggro_range_base ?? 12f;

        var isRanged = def.attack_mode == "ranged" ||
                       def.attack_mode == "barrage" ||
                       def.attack_mode == "laser";
        var atkRange = isRanged
          ? (ai?.attack_range_base ?? 6.5f)
          : (ai?.attack_range_base ?? 1.1f);

        // 营地刷怪不应用波次 EarlyWaveAggression 修正，直接用配表原始值"
        var windup = ai?.windup_base ?? 0.3f;
        var cooldown = ai?.attack_cooldown_base ?? 1.5f;

        // 应用营地等级缩放后的数值"
        controller.ApplyScaledStats(
          baseSpeed * spdMult,
          baseDamage * dmgMult,
          aggro,
          atkRange,
          windup,
          cooldown,
          spdMult  // dashSpeedMult 与普通速度一臀"
        );
      }
    }

    // ══════════════════════════════════════════════════════
    //  清理
    // ══════════════════════════════════════════════════════

    void CleanupDeadEnemies()
    {
      for (int i = _activeEnemies.Count - 1; i >= 0; i--)
      {
        var enemy = _activeEnemies[i];
        if (enemy == null)
        {
          _activeEnemies.RemoveAt(i);
          continue;
        }

        var h = enemy.GetComponent<Health>();
        if (h != null && h.IsDead)
          _activeEnemies.RemoveAt(i);
      }
    }

    /// <summary>
    /// 营地等级或世界等级提升时：刷新所有活跃怪物的属性。
    /// 更新MaxHp（保留CurrentHp比例）、伤害、速度——不更新AI词条。
    /// </summary>
    public void RefreshActiveEnemyStats()
    {
      for (int i = _activeEnemies.Count - 1; i >= 0; i--)
      {
        var enemy = _activeEnemies[i];
        if (enemy == null) { _activeEnemies.RemoveAt(i); continue; }

        var enemyId = ExtractEnemyIdFromName(enemy.name);
        if (string.IsNullOrEmpty(enemyId)) continue;

        var health = enemy.GetComponent<Health>();
        var hpBefore = health != null ? health.CurrentHp : 0f;
        var maxHpBefore = health != null ? health.MaxHp : 0f;
        var ratio = maxHpBefore > 0f ? hpBefore / maxHpBefore : 1f;

        // 重新应用缩放（更新MaxHp）
        ApplyCampScaling(enemy, enemyId);

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
    }

    /// <summary>营地摧毁时：清理所有已生成的营地怪物和存储记录</summary>
    public void DespawnAll()
    {
      foreach (var e in _activeEnemies)
        if (e != null) Object.Destroy(e);
      _activeEnemies.Clear();
      _storedEnemyRecords.Clear();
      _dormantPool.Clear();
      _spawnTimer = 0f;
    }

    /// <summary>
    /// 营地被摧毁时：将所有营地的怪物转为野外状态（不销毁），清空追踪列表。
    /// 怪物失去营地加成后变为野外来源，使用野外游荡逻辑。
    /// </summary>
    public void ReleaseAllToWild()
    {
      foreach (var e in _activeEnemies)
      {
        if (e == null) continue;
        var controller = e.GetComponent<EnemyCore>();
        if (controller != null)
          controller.MarkCampDestroyed();
      }
      _activeEnemies.Clear();
      _storedEnemyRecords.Clear();
      _dormantPool.Clear();
      _spawnTimer = 0f;
    }

    /// <summary>
    /// 生成营地核心敌人。核心为 structure 类型（不可移动/攻击），
    /// 其死亡通过 CombatEventBus 触发营地摧毁。
    /// </summary>
    public GameObject SpawnCore(Vector2 position, float hpMult, float damageMult, float speedMult)
    {
      if (_enemySpawner == null) return null;
      var core = _enemySpawner.SpawnEnemy("camp_core", position);
      if (core == null) return null;

      var ec = core.GetComponent<EnemyCore>();
      if (ec != null)
      {
        ec.EnemyType = $"camp_core:{_campInstanceId}";
        ec.ApplyScaledStats(speedMult, damageMult, 0f, 0f, 0f, 0f);
        // 营地核心为结构物，不需要标记为营地来源
        ec.SetWildOrigin();
      }

      var health = core.GetComponent<Health>();
      if (health != null)
      {
        var def = EnemyDatabase.Get("camp_core");
        float baseHp = def != null ? def.base_hp : 200f;
        health.Configure(UnityEngine.Mathf.Max(1f, baseHp * hpMult));
      }

      core.name = $"[CampCore]_{_campInstanceId}";
      return core;
    }

    /// <summary>获取当前活跃（非核心）怪物数量。</summary>
    public int GetAliveEnemyCount()
    {
      CleanupDeadEnemies();
      return _activeEnemies.Count;
    }

    /// <summary>获取营地最大敌人数。</summary>
    public int GetMaxAliveEnemies() => _levelLogic.GetMaxAliveEnemies();

    public void Dispose()
    {
      DespawnAll();
      _dormantPool.Clear();
    }

    // ── 辅助 ──────────────────────────────────────────

    static class Mathf
    {
      public static int Max(int a, int b) => a > b ? a : b;
      public static int Min(int a, int b) => a < b ? a : b;
      public static int RoundToInt(float v) => (int)(v + 0.5f);
    }
  }
}
