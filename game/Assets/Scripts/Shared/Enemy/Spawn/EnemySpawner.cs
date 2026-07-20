using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Combat;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Death;
using Game.Shared.Enemy.Visual;
using Game.Shared.Enemy.Collision;
using Game.Shared.Runtime.Physics;
using Game.Shared.Vfx;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay;
namespace Game.Shared.Enemy.Spawn
{
  /// <summary>
  /// 怪物生成器。读?enemies.json + ai_profiles.json，按波次缩放实例化?
  /// </summary>
  public class EnemySpawner : MonoBehaviour
  {
    [Header("Prefab")]
    [SerializeField] GameObject defaultEnemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] Vector2 spawnPoint = new Vector2(-10f, 0f);
    [SerializeField] float spawnSpread = 1.5f;

    [Header("Defaults (missing def)")]
    [SerializeField] float defaultHp = 30f;
    [SerializeField] float defaultSpeed = 2.5f;
    [SerializeField] float defaultAggroRange = 12f;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    /// <summary>?WaveDirector.disableEnemySpawning 同步；为 true 时拒绝生成?/summary>
    public static bool SpawningEnabled { get; set; } = true;

    /// <summary>
    /// Boss enemyId → BossCore 子类 Type 映射。
    /// 生成 Boss 时自动 AddComponent，使其技能系统生效。
    /// </summary>
    static readonly Dictionary<string, Type> BossTypeMap = new()
    {
      ["wild_boss_hex_king"]        = typeof(WildBossHexKing),
      ["wild_boss_pent_colossus"]   = typeof(WildBossPentColossus),
      ["wild_boss_star_hive"]       = typeof(WildBossStarHive),
      ["final_boss_prism_nexus"]    = typeof(FinalBossPrismNexus),
      ["mini_boss_hex_sentinel"]    = typeof(MiniBossHexSentinel),
      ["mini_boss_star_chorus"]     = typeof(MiniBossStarChorus),
      ["mini_boss_square_jailer"]   = typeof(MiniBossSquareJailer),
    };

    Dictionary<string, EnemyDef> _enemyDefs = new();
    Dictionary<string, float> _attackProjectileSpeeds = new();
    Dictionary<string, float> _attackProjectileScales = new();
    Dictionary<string, string> _attackProjectileHoming = new();
    Dictionary<string, float> _attackProjectileTurnRates = new();
    Dictionary<string, float> _attackLockLossAngles = new();

    public void SetSpawnPoint(Vector2 point) => spawnPoint = point;

    void Awake()
    {
      LoadEnemyDefinitions();
      EnemyAiProfileDatabase.EnsureLoaded();
      LoadAttackProjectileSpeeds();
    }

    void LoadEnemyDefinitions()
    {
      if (TryLoadJson("enemies", ParseEnemiesJson))
        return;

      Debug.LogWarning("[EnemySpawner] enemies.json not found.");
    }

    bool TryLoadJson(string name, System.Action<string> parser)
    {
      var candidates = new[]
      {
        System.IO.Path.Combine(Application.dataPath, $"../../data/{name}.json"),
        System.IO.Path.Combine(Application.dataPath, $"../../data/combat/{name}.json")
      };

      foreach (var path in candidates)
      {
        if (!System.IO.File.Exists(path))
          continue;

        parser(System.IO.File.ReadAllText(path));
        return true;
      }

      var textAsset = Resources.Load<TextAsset>($"Data/{name}");
      if (textAsset != null)
      {
        parser(textAsset.text);
        return true;
      }

      return false;
    }

    void ParseEnemiesJson(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<EnemiesRoot>(json);
        if (root?.definitions == null)
          return;

        _enemyDefs.Clear();
        foreach (var def in root.definitions)
          _enemyDefs[def.id] = def;

        if (debugLog)
          Debug.Log($"[EnemySpawner] Loaded {_enemyDefs.Count} enemy definitions.");
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[EnemySpawner] Failed to parse enemies.json: {e.Message}");
      }
    }

    void LoadAttackProjectileSpeeds()
    {
      if (!TryLoadJson("attacks", ParseAttackProjectileSpeedsJson))
        return;

      if (_attackProjectileSpeeds.Count == 0 && debugLog)
        Debug.LogWarning("[EnemySpawner] No monster projectile speeds in attacks.json.");
    }

    void ParseAttackProjectileSpeedsJson(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<AttacksRoot>(json);
        if (root?.definitions == null)
          return;

        _attackProjectileSpeeds.Clear();
        _attackProjectileScales.Clear();
        _attackProjectileHoming.Clear();
        _attackProjectileTurnRates.Clear();
        _attackLockLossAngles.Clear();
        foreach (var attack in root.definitions)
        {
          if (attack?.delivery_params == null)
            continue;

          if (attack.damage_source != "monster" && (attack.id == null || !attack.id.StartsWith("enemy_")))
            continue;

          if (attack.delivery_params.projectile_speed > 0f)
            _attackProjectileSpeeds[attack.id] = attack.delivery_params.projectile_speed;

          if (attack.delivery_params.projectile_scale > 0f)
            _attackProjectileScales[attack.id] = attack.delivery_params.projectile_scale;

          if (!string.IsNullOrEmpty(attack.delivery_params.projectile_homing))
            _attackProjectileHoming[attack.id] = attack.delivery_params.projectile_homing;

          if (attack.delivery_params.projectile_turn_rate_deg > 0f)
            _attackProjectileTurnRates[attack.id] = attack.delivery_params.projectile_turn_rate_deg;

          if (attack.delivery_params.lock_loss_angle_deg > 0f)
            _attackLockLossAngles[attack.id] = attack.delivery_params.lock_loss_angle_deg;
        }

        if (debugLog)
          Debug.Log($"[EnemySpawner] Loaded {_attackProjectileSpeeds.Count} monster projectile speeds.");
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[EnemySpawner] Failed to parse attacks.json: {e.Message}");
      }
    }

    float GetProjectileSpeed(string attackProfileId)
    {
      if (string.IsNullOrEmpty(attackProfileId))
        return -1f;

      return _attackProjectileSpeeds.TryGetValue(attackProfileId, out var speed) ? speed : -1f;
    }

    float GetProjectileScale(string attackProfileId)
    {
      if (string.IsNullOrEmpty(attackProfileId))
        return -1f;

      return _attackProjectileScales.TryGetValue(attackProfileId, out var scale) ? scale : -1f;
    }

    string GetProjectileHoming(string attackProfileId)
    {
      if (string.IsNullOrEmpty(attackProfileId))
        return null;

      return _attackProjectileHoming.TryGetValue(attackProfileId, out var homing) ? homing : null;
    }

    float GetProjectileTurnRateFromAttack(string attackProfileId)
    {
      if (string.IsNullOrEmpty(attackProfileId))
        return -1f;

      return _attackProjectileTurnRates.TryGetValue(attackProfileId, out var rate) ? rate : -1f;
    }

    float GetLockLossAngle(string attackProfileId)
    {
      if (string.IsNullOrEmpty(attackProfileId))
        return -1f;

      return _attackLockLossAngles.TryGetValue(attackProfileId, out var angle) ? angle : -1f;
    }

    public EnemyDef GetEnemyDef(string enemyId)
    {
      _enemyDefs.TryGetValue(enemyId, out var def);
      return def;
    }

    /// <summary>波次生成：应用 enemies.json 基准属性 × 波次缩放。</summary>
    /// <param name="affixSet">词条集合，null 则自动生成默认词条</param>
    public GameObject SpawnWaveEnemy(string enemyId, Vector2 position, WaveSpawnScaling scaling,
      EnemyAffixSet affixSet = null)
    {
      if (!SpawningEnabled)
        return null;

      var def = GetEnemyDef(enemyId);
      var enemy = SpawnEnemy(enemyId, position, def, affixSet);
      ApplyWaveScaling(enemy, def, scaling);
      return enemy;
    }

    /// <summary>在指定位置生成怪物（无波次缩放）。</summary>
    /// <param name="affixSet">词条集合，null 则自动生成默认词条</param>
    public GameObject SpawnEnemy(string enemyId, Vector2 position,
      EnemyAffixSet affixSet = null)
    {
      return SpawnEnemy(enemyId, position, GetEnemyDef(enemyId), affixSet, respectSpawnGate: true);
    }

    /// <summary>Boss 子部件/召唤物生成，不受波次 SpawningEnabled 门控影响。</summary>
    public GameObject SpawnEnemyForBossSystems(string enemyId, Vector2 position,
      EnemyAffixSet affixSet = null)
    {
      return SpawnEnemy(enemyId, position, GetEnemyDef(enemyId), affixSet, respectSpawnGate: false);
    }

    const int SpawnJitterAttempts = 16;

    GameObject SpawnEnemy(string enemyId, Vector2 position, EnemyDef def,
      EnemyAffixSet affixSet = null, bool respectSpawnGate = true)
    {
      if (respectSpawnGate && !SpawningEnabled)
        return null;

      var spawnPos = ResolveSpawnPosition(position, spawnSpread);

      GameObject enemy;
      if (defaultEnemyPrefab != null)
      {
        enemy = Instantiate(defaultEnemyPrefab, spawnPos, Quaternion.identity);
        enemy.name = $"Enemy_{enemyId}";
      }
      else
      {
        enemy = CreateDefaultEnemy(enemyId, spawnPos);
      }

      if (enemy != null && !enemy.activeSelf)
        enemy.SetActive(true);

      SetupEnemyComponents(enemy, enemyId, def, WaveSpawnScaling.Identity(), affixSet);
      SetupBossIfNeeded(enemy, enemyId);
      return enemy;
    }

    /// <summary>若 enemyId 为已知 Boss，自动挂载 BossCore 子类 + 加载 Boss 精灵</summary>
    static void SetupBossIfNeeded(GameObject enemy, string enemyId)
    {
      if (enemy == null || !BossTypeMap.TryGetValue(enemyId, out var bossType))
        return;

      var visualScale = CombatPlaceholderVisual.ResolveScale(enemyId, 0f)
        * CombatPlaceholderVisual.MinionDisplayScaleMultiplier;
      if (!CombatSpriteVisual.ApplyBoss(enemy, enemyId, visualScale))
        Debug.LogWarning($"[EnemySpawner] Boss visual failed for '{enemyId}'.");

      if (enemy.GetComponent(bossType) == null)
        enemy.AddComponent(bossType);
    }

    Vector3 ResolveSpawnPosition(Vector2 position, float jitterRadius)
    {
      if (ArenaLayoutLocator.Layout.IsActive)
        return new Vector3(position.x, position.y, 0f);

      for (int attempt = 0; attempt < SpawnJitterAttempts; attempt++)
      {
        var offset = attempt == 0
          ? Vector2.zero
          : UnityEngine.Random.insideUnitCircle * jitterRadius;
        var candidate = new Vector2(position.x + offset.x, position.y + offset.y);
        if (!IsSpawnPositionBlocked(candidate))
          return new Vector3(candidate.x, candidate.y, 0f);
      }

      return new Vector3(position.x, position.y, 0f);
    }

    static bool IsSpawnPositionBlocked(Vector2 worldPos) => false;

    void ApplyWaveScaling(GameObject enemy, EnemyDef def, WaveSpawnScaling scaling)
    {
      if (enemy == null || scaling.hpMult <= 0f)
        return;

      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        var baseHp = def != null ? def.base_hp : defaultHp;
        health.Configure(Mathf.Max(1f, baseHp * scaling.hpMult));
      }

      var controller = enemy.GetComponent<EnemyCore>();
      if (controller == null || def == null)
        return;

      var ai = GetAiProfile(def.ai_profile);
      var baseSpeed = def.move_speed > 0f ? def.move_speed : defaultSpeed;
      var baseDamage = def.base_damage > 0f ? def.base_damage : 6f;
      GetAiAttackTiming(ai, scaling.waveNumber, def.attack_mode == "charge", out var windup, out var cooldown);

      controller.ApplyScaledStats(
        baseSpeed * scaling.speedMult,
        baseDamage * scaling.damageMult,
        ai?.aggro_range_base ?? defaultAggroRange,
        def.attack_mode == "ranged" || def.attack_mode == "barrage" || def.attack_mode == "laser"
          ? ai?.attack_range_base ?? 6.5f
          : ai?.attack_range_base ?? 1.1f,
        windup,
        cooldown,
        scaling.dashSpeedMult);

      if (debugLog)
      {
        Debug.Log(
          $"[EnemySpawner] Wave {scaling.waveNumber} {def.id}: HP×{scaling.hpMult:F2} DMG×{scaling.damageMult:F2} SPD×{scaling.speedMult:F2} DASH×{scaling.dashSpeedMult:F2} {health?.MaxHp:F0}HP {baseDamage * scaling.damageMult:F1}dmg");
      }
    }

    static EnemyAiProfileDatabase.AiProfile GetAiProfile(string profileId) =>
      EnemyAiProfileDatabase.Get(profileId);

    static void GetAiAttackTiming(
      EnemyAiProfileDatabase.AiProfile ai,
      int waveNumber,
      bool chargeAttack,
      out float windup,
      out float cooldown)
    {
      EarlyWaveAggression.GetMultipliers(waveNumber, out var windupMult, out var cooldownMult);
      if (chargeAttack)
        cooldownMult = Mathf.Max(cooldownMult, EarlyWaveAggression.GetChargeCooldownMultiplier(waveNumber));
      windup = (ai?.windup_base ?? 0.2f) * windupMult;
      cooldown = (ai?.attack_cooldown_base ?? 1f) * cooldownMult;
    }

    GameObject CreateDefaultEnemy(string id, Vector3 pos)
    {
      var go = new GameObject($"Enemy_{id}");
      go.transform.position = pos;
      go.transform.localScale = Vector3.one;
      return go;
    }

    void SetupEnemyComponents(GameObject enemy, string enemyId, EnemyDef def, WaveSpawnScaling scaling,
      EnemyAffixSet affixSet = null)
    {
      var health = enemy.GetComponent<Health>();
      if (health == null) health = enemy.AddComponent<Health>();

      // ── 主协调器 ──
      var controller = enemy.GetComponent<EnemyCore>();
      if (controller == null) controller = enemy.AddComponent<EnemyCore>();

      // 检测是否为结构类敌人（不可移动/攻击）
      bool isStructure = def?.tags != null && System.Array.IndexOf(def.tags, "structure") >= 0;

      if (def != null)
      {
        var ai = GetAiProfile(def.ai_profile);
        var mode = def.attack_mode ?? "melee";
        var usesRangedRange = mode == "ranged" || mode == "barrage" || mode == "laser";

        health.Configure(Mathf.Max(1f, def.base_hp * scaling.hpMult));
        GetAiAttackTiming(ai, scaling.waveNumber, def.attack_mode == "charge", out var windup, out var cooldown);

        // ── 子组件：按 tags 决定是否挂载 ──
        if (!isStructure)
        {
          if (enemy.GetComponent<EnemyMovement>() == null)
            enemy.AddComponent<EnemyMovement>();
          if (enemy.GetComponent<EnemyAttack>() == null)
            enemy.AddComponent<EnemyAttack>();
        }

        controller.ConfigureFromDef(
          enemyId,
          def.move_speed * scaling.speedMult,
          def.base_damage * scaling.damageMult,
          usesRangedRange,
          ai?.aggro_range_base ?? defaultAggroRange,
          ai?.attack_range_base ?? (usesRangedRange ? 6.5f : 1.1f),
          windup,
          cooldown,
          def.projectile_turn_rate_deg,
          def.move_mode == "lane_follow",
          usesRangedRange ? GetProjectileSpeed(def.attack_profile_id) : -1f,
          usesRangedRange ? GetProjectileScale(def.attack_profile_id) : -1f,
          def.attack_profile_id,
          usesRangedRange ? GetProjectileHoming(def.attack_profile_id) : null,
          usesRangedRange ? GetProjectileTurnRateFromAttack(def.attack_profile_id) : -1f,
          usesRangedRange ? GetLockLossAngle(def.attack_profile_id) : -1f,
          mode);

        controller.ConfigureAi(ai, def.move_mode);

        // 应用词条：传入 null 则自动生成默认词条
        controller.SetAffixSet(
          affixSet ?? EnemyAffixSet.CreateDefault(controller.AttackKind));

        controller.enableChargeParticle = (def.base_hp >= 30f) && (mode == "charge");
        ApplyLaneAdvanceDirection(controller, enemy.transform.position);

        ApplyEnemyVisual(enemy, def, enemyId);
        EnemyMotionVisual.Ensure(enemy);
        EnemyDebuffVisual.Ensure(enemy);
      }
      else
      {
        health.Configure(defaultHp);
        ApplyEnemyVisual(enemy, null, enemyId);
      }

      var deathHandler = enemy.GetComponent<EnemyDeathHandler>();
      if (deathHandler == null) deathHandler = enemy.AddComponent<EnemyDeathHandler>();
      if (def != null) deathHandler.LootTableId = def.loot_table_id ?? "common_mob";

      var metadata = enemy.GetComponent<EnemySpawnMetadata>();
      if (metadata == null) metadata = enemy.AddComponent<EnemySpawnMetadata>();
      if (def != null)
      {
        metadata.Configure(def);
        ApplyPassiveBuffs(enemy, def.passive_buffs);
      }

      if (enemy.GetComponent<DamageDisplay>() == null)
        enemy.AddComponent<DamageDisplay>();
      Game.Shared.Combat.CombatFeedbackManager.ShowHealthBar(health);

      if (enemy.GetComponent<HitFlash>() == null)
        enemy.AddComponent<HitFlash>();

      var visualScale = CombatPlaceholderVisual.ResolveScale(def?.id ?? enemyId, def?.visual_scale ?? 0f)
        * CombatPlaceholderVisual.MinionDisplayScaleMultiplier;
      EntityPhysicsBody.EnsureEnemy(enemy, CombatPlaceholderVisual.CollisionRadiusFromVisualScale(visualScale));
      EnemyHitbox.Ensure(enemy, def?.id ?? enemyId, visualScale);
      DamageReceiver.Ensure(enemy);
    }

    static void ApplyEnemyVisual(GameObject enemy, EnemyDef def, string enemyId)
    {
      var id = def?.id ?? enemyId ?? "mob_hex_01";
      if (BossTypeMap.ContainsKey(id))
        return;

      var scale = CombatPlaceholderVisual.ResolveScale(id, def?.visual_scale ?? 0f)
        * CombatPlaceholderVisual.MinionDisplayScaleMultiplier;
      if (!CombatSpriteVisual.ApplyMinion(enemy, id, scale))
        CombatPlaceholderVisual.ApplySphere(enemy, scale, GetEnemyColor(id));
    }

    static void ApplyPassiveBuffs(GameObject enemy, string[] buffIds)
    {
      if (buffIds == null || buffIds.Length == 0)
        return;

      var bc = enemy.GetComponent<BuffContainer>();
      if (bc == null)
        bc = enemy.AddComponent<BuffContainer>();

      var ctx = new BuffContainer.BuffApplyContext
      {
        sourceEntity = enemy,
        sourceKind = "passive",
        stacks = 1
      };

      foreach (var buffId in buffIds)
      {
        if (!string.IsNullOrEmpty(buffId))
          bc.ApplyBuff(buffId, ctx);
      }
    }

    static void ApplyLaneAdvanceDirection(EnemyCore controller, Vector3 spawnPos)
    {
      if (controller == null)
        return;

      var playerGo = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
      if (playerGo == null)
      {
        controller.SetLaneAdvanceDirection(Vector2.right);
        return;
      }

      var offset = GameplayPlane.ToPlanar(playerGo.transform.position) - GameplayPlane.ToPlanar(spawnPos);
      controller.SetLaneAdvanceDirection(offset);
    }

    static Color GetEnemyColor(string id)
    {
      // Placeholder: unified mob palette until polygon visuals load enemy_visuals.json
      if (id == "mob_pent_01" || id == "mob_hex_03" || id == "mob_tri_05")
        return new Color(0.79f, 0.29f, 0.24f); // mob_red_dark #C94A3D
      return new Color(0.91f, 0.36f, 0.30f); // mob_red #E85D4C
    }

    [System.Serializable]
    public class EnemyDef
    {
      public string id;
      public string display_name;
      public string move_mode;
      public string attack_mode;
      public string ai_profile;
      public float base_hp;
      public float base_damage;
      public float move_speed;
      public float visual_scale;
      public float projectile_turn_rate_deg;
      public string[] tags;
      public string attack_profile_id;
      public string loot_table_id;
      public string[] passive_buffs;
      public string[] on_hit_buffs;
      public string[] on_death;
    }

    [System.Serializable]
    class EnemiesRoot
    {
      public EnemyDef[] definitions;
    }

    [System.Serializable]
    class AttacksRoot
    {
      public AttackDef[] definitions;
    }

    [System.Serializable]
    class AttackDef
    {
      public string id;
      public string damage_source;
      public AttackDeliveryParams delivery_params;
    }

    [System.Serializable]
    class AttackDeliveryParams
    {
      public float projectile_speed;
      public float projectile_scale;
      public string projectile_homing;
      public float projectile_turn_rate_deg;
      public float lock_loss_angle_deg;
    }
  }
}
