using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Enemy.AI;
using Game.Shared.Core;
using Game.Shared.Enemy.Death;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
#endif
namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// 波次导演。管理 ring_waves.json 定义的波次 + 建造间隔。
  /// 阶段：BuildPhase（建造间隔）?WaveActive（怪物生成中）?BuildPhase ?...
  /// </summary>
  public class WaveDirector : MonoBehaviour
  {
    [Header("Wave Config")]
    [SerializeField] int totalWaves = 15;
    [SerializeField] float buildPhaseDuration = 5f;
    [SerializeField] float preFirstWaveDelay = 5f;
    [SerializeField] float spawnInterval = 0.07f;
    [SerializeField] Vector2 spawnPoint = new Vector2(-10f, 0f);
    [SerializeField] Vector2 basePoint = new Vector2(0f, 0f);
    [SerializeField] bool spawnAroundPlayer = true;
    [SerializeField] float spawnRadius = 36f;
    [SerializeField] float spawnRadiusJitter = 10f;

    [Header("Wave Scaling")]
    [SerializeField] int minEnemiesPerWave = 28;
    [SerializeField] int maxEnemiesPerWaveBase = 24;
    [SerializeField] float growthPerWave = 1.14f;
    [SerializeField] int maxCapEnemies = 350;

    [Header("Enemy Prefabs")]
    [SerializeField] GameObject defaultEnemyPrefab;

    [Header("Wave Data (JSON fallback)")]
    [SerializeField] WaveDefinition[] waveDefinitions;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    [Header("Temporary")]
    [Tooltip("开启后不刷怪，建造阶段结束后重新计时（便于测?商人")]
    [SerializeField] bool disableEnemySpawning = false;

    // State
    public enum Phase { NotStarted, PreGame, BuildPhase, WaveCountdown, WaveActive, AllWavesComplete }
    const float WaveCountdownDuration = 3f;

    Phase _phase = Phase.NotStarted;
    int _currentWave = 0;
    float _phaseTimer;
    int _enemiesRemainingInWave;
    int _totalEnemiesSpawned;
    readonly HashSet<GameObject> _trackedWaveEnemies = new();
    float _spawnTimer;
    bool _eliteSpawnedThisWave;
    int _elitesSpawnedThisWave;
    int _eliteQuotaThisWave = 1;
    int _supportQuotaThisWave;
    int _supportsSpawnedThisWave;
    bool _supportDoubledThisWave;
    bool _bossSpawnedThisWave;
    bool _bossPrepActive;
    float _buildPhaseReduction;
    static readonly int[] ShrinkMilestoneWaves = { 5, 10, 15 };
    const float BossPrepDuration = 15f;
    const float MiniBossPrepDuration = 12f;
    const float BossWaveSpawnScale = 0.72f;
    const float MiniBossWaveSpawnScale = 0.85f;
    WaveComposition _activeComposition;
    int _compositionCursor;
    BossWaveProfile[] _bossWaves;
    string _currentModifierId = "standard";
    float _waveActiveElapsed;
    int _spawnGroupRemaining;
    float _spawnGroupCooldown;
    float _spawnUnitDelay;
    float[] _activeSectorAngles;
    int _sectorSlot;
    int _hordePending;
    float _hordeSpawnDelay;
    bool _waveCompletionLocked;

#if UNITY_EDITOR
    int _editorSpawnFailRemaining;
#endif

    WaveScalingCurves _scalingCurves = WaveScalingCalculator.DefaultCurves;

    EnemySpawner _spawner;

    // Events
    public static event System.Action<Phase, int> PhaseChanged;  // phase, waveNumber
    public static event System.Action<int> WaveCompleted;         // waveNumber
    public static event System.Action AllWavesCompleted;
    public static event System.Action<float> BuildTimerUpdated;   // remaining seconds

    public Phase CurrentPhase => _phase;
    public int CurrentWave => _currentWave;
    public float BuildPhaseRemaining => _phase == Phase.BuildPhase ? _phaseTimer : 0f;
    public float WaveCountdownRemaining => _phase == Phase.WaveCountdown ? Mathf.Max(0f, _phaseTimer) : 0f;
    public int WaveCountdownDisplay => _phase == Phase.WaveCountdown
      ? Mathf.Clamp(Mathf.CeilToInt(_phaseTimer), 1, 3)
      : 0;

    public int EnemiesRemaining => _enemiesRemainingInWave;
    public int TotalWaves => totalWaves;
    public int WaveSpawnQuota => GetWaveEnemyCount(_currentWave);
    public int WaveEnemiesSpawned => _totalEnemiesSpawned;
    public bool IsWaveSpawnQuotaMet =>
      _phase == Phase.WaveActive && _totalEnemiesSpawned >= WaveSpawnQuota;
    public string CurrentModifierId => _currentModifierId;
    public WaveModifierDatabase.WaveModifierEntry CurrentModifier =>
      WaveModifierDatabase.Get(_currentModifierId);
    public bool IsBossPrepActive => _bossPrepActive;
    public float BossPrepRemaining => _bossPrepActive && _phase == Phase.BuildPhase ? _phaseTimer : 0f;

    public void ApplyBuildPhaseReduction(float seconds) => _buildPhaseReduction += seconds;

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
    public static void ValidationMinimizeBuildPhases()
    {
      if (Instance != null)
        Instance.ApplyBuildPhaseReduction(999f);
    }
#endif

    static bool IsUpcomingBossWave(int wave, BossWaveProfile[] bosses)
    {
      if (bosses == null)
        return false;
      foreach (var boss in bosses)
        if (boss != null && boss.wave == wave)
          return true;
      return false;
    }
    public static WaveDirector Instance { get; private set; }

    /// <summary>CombatRoot 统一调用：首局创建，重开时重置波次状态。</summary>
    public static void BeginRun()
    {
      if (GameSessionConfig.IsBossRush)
        return;

      if (Instance != null)
      {
        Instance.ResetRunState();
        return;
      }

      var go = new GameObject("_WaveDirector");
      go.AddComponent<WaveDirector>();
    }

    public static void PrepareArenaRestart()
    {
      if (Instance == null)
      {
        ClearActiveEnemies();
        return;
      }

      Instance.PrepareForArenaSceneReload();
    }

    /// <summary>Stop wave spawning when entering Boss Rush or other alternate arena modes.</summary>
    public static void ShutdownForAlternateMode()
    {
      var directors = UnityEngine.Object.FindObjectsByType<WaveDirector>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None);
      foreach (var director in directors)
      {
        if (director == null)
          continue;

        director.StopAllCoroutines();
        UnityEngine.Object.Destroy(director.gameObject);
      }

      Instance = null;
      EnemySpawner.SpawningEnabled = true;
    }

    public static void EnsureExists() => BeginRun();

    void Awake()
    {
      if (GameSessionConfig.IsBossRush)
      {
        Destroy(gameObject);
        return;
      }

      if (Instance != null) { Destroy(gameObject); return; }
      Instance = this;
      DontDestroyOnLoad(gameObject);
      LoadWaveDefinitions();
      MonsterEcosystemDatabase.EnsureLoaded();
      WaveModifierDatabase.EnsureLoaded();
      BindRunConfig();
    }

    void Start()
    {
      DisableScenePlacedEnemies();
      SyncSpawnerGate();
      EnsureEnemySpawner();
      BeginFirstWave();
    }

    /// <summary>死亡/重开过渡：清场并冻结波次，等 MainScene 加载后由 ResetRunState 重启。</summary>
    void PrepareForArenaSceneReload()
    {
      enabled = false;
      StopAllCoroutines();
      ResetWaveFields();
      _spawner = null;
      ClearActiveEnemies();
      DisableScenePlacedEnemies();
      EnemySpawner.SpawningEnabled = true;
    }

    void EnsureEnemySpawner()
    {
      if (_spawner != null)
        return;

      _spawner = FindAnyObjectByType<EnemySpawner>();
      if (_spawner != null)
        return;

      var go = new GameObject("_EnemySpawner");
      _spawner = go.AddComponent<EnemySpawner>();
    }

    void BindRunConfig()
    {
      ArenaDifficultyRuntime.BindSession();
      totalWaves = ArenaDifficultyRuntime.TotalWaves;
      ClampTotalWavesToDefinitions();
      buildPhaseDuration = ArenaDifficultyRuntime.BuildPhaseSeconds;
      preFirstWaveDelay = 0f;
      HuntContractRuntime.BeginRun(totalWaves);
      ArenaRareEnemySpawner.BeginRun(totalWaves);
      ArenaXpZoneController.BeginRun();
      ArenaMidWaveEventDirector.BeginRun();
      ArenaCorruptionDirector.BeginRun();
    }

    void ClampTotalWavesToDefinitions()
    {
      if (waveDefinitions != null && waveDefinitions.Length > 0 && totalWaves > waveDefinitions.Length)
        totalWaves = waveDefinitions.Length;
    }

    void ResetRunState()
    {
      enabled = true;
      StopAllCoroutines();
      ResetWaveFields();
      BindRunConfig();
      ClearActiveEnemies();
      DisableScenePlacedEnemies();
      SyncSpawnerGate();
      EnsureEnemySpawner();
      BeginFirstWave();
    }

    void ResetWaveFields()
    {
      _phase = Phase.NotStarted;
      _currentWave = 0;
      _phaseTimer = 0f;
      _enemiesRemainingInWave = 0;
      _totalEnemiesSpawned = 0;
      _trackedWaveEnemies.Clear();
      _spawnTimer = 0f;
      _eliteSpawnedThisWave = false;
      _elitesSpawnedThisWave = 0;
      _eliteQuotaThisWave = 1;
      _supportQuotaThisWave = 0;
      _supportsSpawnedThisWave = 0;
      _supportDoubledThisWave = false;
      _bossSpawnedThisWave = false;
      _bossPrepActive = false;
      _waveCompletionLocked = false;
      _buildPhaseReduction = 0f;
      _compositionCursor = 0;
      _currentModifierId = "standard";
      _activeComposition = default;
      CircleArenaController.SetBossPrepHighlight(false);
    }

    void BeginFirstWave()
    {
      if (preFirstWaveDelay <= 0f)
        StartWave(1);
      else
      {
        _phase = Phase.PreGame;
        _phaseTimer = preFirstWaveDelay;
      }
    }

    static void ClearActiveEnemies()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      var snapshot = new List<EnemyCore>(registry.AllEnemies);
      foreach (var enemy in snapshot)
      {
        if (enemy != null)
          Destroy(enemy.gameObject);
      }
    }

    /// <summary>场景?Enemies 下的样板怪仅作参考，运行时全部由波次刷出?/summary>
    static void DisableScenePlacedEnemies()
    {
      var root = GameObject.Find("Enemies");
      if (root == null)
        return;

      foreach (Transform child in root.transform)
        child.gameObject.SetActive(false);
    }

    void Update()
    {
      if (GameSessionConfig.IsBossRush)
        return;

      switch (_phase)
      {
        case Phase.PreGame:
          _phaseTimer -= Time.deltaTime;
          if (_phaseTimer <= 0f)
            StartBuildPhase(1);
          break;

        case Phase.BuildPhase:
          _phaseTimer -= Time.deltaTime;
          BuildTimerUpdated?.Invoke(_phaseTimer);
          if (_phaseTimer <= 0f)
          {
            if (disableEnemySpawning)
            {
              if (debugLog)
                Debug.Log($"[WaveDirector] Enemy spawning disabled ?repeating build phase {_currentWave}/{totalWaves}");
              StartBuildPhase(_currentWave);
            }
            else
              StartWave(_currentWave);
          }
          break;

        case Phase.WaveCountdown:
          _phaseTimer -= Time.deltaTime;
          if (_phaseTimer <= 0f)
            ActivateWaveActive();
          break;

        case Phase.WaveActive:
          _waveActiveElapsed += Time.deltaTime;
          UpdateWaveSpawning();
          RefreshTrackedEnemyCount();
          if (IsBossWave(_currentWave) && !_bossSpawnedThisWave)
            SpawnBossForCurrentWave();
          if (_enemiesRemainingInWave <= 0 && IsWaveSpawnQuotaMet
              && (!IsBossWave(_currentWave) || _bossSpawnedThisWave))
            CompleteWave(_currentWave);
          break;
      }
    }

    void SyncSpawnerGate() => EnemySpawner.SpawningEnabled = !disableEnemySpawning;

    void StartBuildPhase(int waveNumber)
    {
      SyncSpawnerGate();
      _phase = Phase.BuildPhase;
      _currentWave = waveNumber;
      _bossPrepActive = IsUpcomingBossWave(waveNumber, _bossWaves);
      if (_bossPrepActive)
      {
        var profile = GetBossWave(waveNumber);
        _phaseTimer = profile != null && profile.is_mini_boss
          ? MiniBossPrepDuration
          : BossPrepDuration;
      }
      else
        _phaseTimer = Mathf.Max(1f, buildPhaseDuration - _buildPhaseReduction);

      if (_bossPrepActive)
        CircleArenaController.SetBossPrepHighlight(true);

      if (debugLog)
        Debug.Log($"[WaveDirector] Build Phase {waveNumber}/{totalWaves} | {_phaseTimer}s bossPrep={_bossPrepActive}");

      PhaseChanged?.Invoke(Phase.BuildPhase, waveNumber);
    }

    void StartWave(int waveNumber)
    {
      PrepareWave(waveNumber);
      _phase = Phase.WaveCountdown;
      _phaseTimer = WaveCountdownDuration;

      if (debugLog)
        Debug.Log($"[WaveDirector] Wave {waveNumber}/{totalWaves} countdown | {_enemiesRemainingInWave} enemies");

      PhaseChanged?.Invoke(Phase.WaveCountdown, waveNumber);
    }

    void ActivateWaveActive()
    {
      _phase = Phase.WaveActive;
      _bossPrepActive = false;
      CircleArenaController.SetBossPrepHighlight(false);
      Game.Modes.Roguelike.Gameplay.Player.PlayerDashController.SetDashBlocked(_currentModifierId == "no_dash_wave");

      if (debugLog)
        Debug.Log($"[WaveDirector] Wave {_currentWave}/{totalWaves} active");

      PhaseChanged?.Invoke(Phase.WaveActive, _currentWave);
      GameEventBus.Publish(new WaveStartedEvent(_currentWave));
      SpawnBossForCurrentWave();
    }

    void PrepareWave(int waveNumber)
    {
      _waveCompletionLocked = false;
      _currentWave = waveNumber;
      _totalEnemiesSpawned = 0;
      _waveActiveElapsed = 0f;
      _trackedWaveEnemies.Clear();
      _enemiesRemainingInWave = 0;
      _spawnTimer = 0f;
      _eliteSpawnedThisWave = false;
      _elitesSpawnedThisWave = 0;
      _supportDoubledThisWave = false;
      _supportsSpawnedThisWave = 0;
      _supportQuotaThisWave = waveNumber >= 8
        ? Mathf.Clamp(1 + (waveNumber - 8) / 5, 1, 2)
        : 0;
      _bossSpawnedThisWave = false;
      var modifier = WaveModifierDatabase.PickForWave(waveNumber, totalWaves);
      _currentModifierId = modifier != null ? modifier.id : "standard";
      _eliteQuotaThisWave = _currentModifierId == "double_elite" ? 2 : 1;
      Game.Modes.Roguelike.Gameplay.Player.PlayerDashController.SetDashBlocked(false);
      // Survival mode uses the explicit time-stage pools so ranged and interceptor
      // ratios cannot be replaced by a randomly selected composition.
      _activeComposition = null;
      _compositionCursor = _activeComposition?.enemy_ids != null && _activeComposition.enemy_ids.Length > 0
        ? Random.Range(0, _activeComposition.enemy_ids.Length)
        : 0;
      ResetSpawnRhythmState();
      ArenaSpawnPlanner.ClearRecentSpawns();
    }

    void ResetSpawnRhythmState()
    {
      _spawnGroupRemaining = 0;
      _spawnGroupCooldown = 0f;
      _spawnUnitDelay = 0f;
      _spawnTimer = 0f;
      _activeSectorAngles = null;
      _sectorSlot = 0;
      _hordePending = 0;
      _hordeSpawnDelay = 0f;
    }

    void UpdateWaveSpawning()
    {
      if (_spawnGroupCooldown > 0f)
        _spawnGroupCooldown -= Time.deltaTime;

      if (_spawnUnitDelay > 0f)
        _spawnUnitDelay -= Time.deltaTime;

      if (_hordeSpawnDelay > 0f)
        _hordeSpawnDelay -= Time.deltaTime;

      var waveQuota = GetWaveEnemyCount(_currentWave);
      var quotaMet = _totalEnemiesSpawned >= waveQuota;
      if (quotaMet)
      {
        _hordePending = 0;
        _hordeSpawnDelay = 0f;
      }
      else if (_totalEnemiesSpawned < waveQuota
          && _spawnUnitDelay <= 0f
          && _spawnGroupCooldown <= 0f)
      {
        if (_spawnGroupRemaining <= 0)
          BeginSpawnGroup();

        if (_spawnGroupRemaining > 0)
        {
          _spawnTimer -= Time.deltaTime;
          if (_spawnTimer <= 0f)
          {
            if (TrySpawnEnemyForCurrentWave())
            {
              _spawnGroupRemaining--;
              _spawnUnitDelay = Random.Range(0f, ArenaSpawnSettings.UnitSpawnJitter);
              _spawnTimer = GetEffectiveSpawnInterval();
              if (_spawnGroupRemaining <= 0)
                _spawnGroupCooldown = ArenaSpawnSettings.GroupInterval;
            }
            else
              _spawnTimer = GetEffectiveSpawnInterval() * 0.35f;
          }
        }
      }

      if (!quotaMet)
        ProcessHordeSpawns();
    }

    float GetWaveSpawnProgress()
    {
      var waveQuota = Mathf.Max(1, GetWaveEnemyCount(_currentWave));
      return Mathf.Clamp01(_totalEnemiesSpawned / (float)waveQuota);
    }

    void BeginSpawnGroup()
    {
      var waveQuota = Mathf.Max(1, GetWaveEnemyCount(_currentWave));
      var progress = Mathf.Clamp01(_totalEnemiesSpawned / (float)waveQuota);
      _activeSectorAngles = ArenaSpawnPlanner.PickAttackSectors(_currentWave, progress);
      _sectorSlot = 0;
      _spawnGroupRemaining = ArenaSpawnSettings.GetGroupSize(_currentWave);
    }

    void ProcessHordeSpawns()
    {
      if (_hordePending <= 0 || _hordeSpawnDelay > 0f || disableEnemySpawning)
        return;

      if (_totalEnemiesSpawned >= GetWaveEnemyCount(_currentWave))
      {
        _hordePending = 0;
        return;
      }

      if (TrySpawnEnemyForCurrentWave())
      {
        _hordePending--;
        _hordeSpawnDelay = Random.Range(0.12f, ArenaSpawnSettings.UnitSpawnJitter + 0.18f);
      }
      else
        _hordeSpawnDelay = 0.08f;
    }

    float GetEffectiveSpawnInterval()
    {
      var interval = spawnInterval;
      if (_currentModifierId == "frenzy")
        interval *= WaveModifierRuntime.FrenzySpawnIntervalMultiplier;
      interval *= ArenaNarrativeEventDirector.SpawnIntervalMult;
      if (_currentModifierId == "support_x2")
        interval *= 0.82f;
      return interval;
    }

    void CompleteWave(int waveNumber)
    {
      if (_phase != Phase.WaveActive || waveNumber != _currentWave || _waveCompletionLocked)
        return;

      _waveCompletionLocked = true;
      var allComplete = waveNumber >= totalWaves;
      WaveCompleted?.Invoke(waveNumber);
      GameEventBus.Publish(new WaveFinishedEvent(waveNumber, allComplete));

      if (debugLog)
        Debug.Log($"[WaveDirector] Wave {waveNumber}/{totalWaves} complete!");

      if (waveNumber >= totalWaves)
      {
        _phase = Phase.AllWavesComplete;
        PhaseChanged?.Invoke(Phase.AllWavesComplete, waveNumber);
        AllWavesCompleted?.Invoke();
        if (debugLog) Debug.Log("[WaveDirector] All waves complete! Victory!");
      }
      else
      {
        StartBuildPhase(waveNumber + 1);
      }
    }

    bool TrySpawnEnemyForCurrentWave()
    {
      if (disableEnemySpawning || _spawner == null)
        return false;

#if UNITY_EDITOR
      if (_editorSpawnFailRemaining > 0)
      {
        _editorSpawnFailRemaining--;
        return false;
      }
#endif

      if (_totalEnemiesSpawned >= GetWaveEnemyCount(_currentWave))
        return false;

      var def = GetWaveDef(_currentWave);
      if (def == null)
        return false;

      var enemyId = PickEcosystemEnemy(def);
      var profile = MonsterEcosystemDatabase.Get(enemyId);
      var role = profile?.role ?? "chaser";
      var spawnPos = PickSpawnPosition(enemyId, role);
      var enemyDef = _spawner.GetEnemyDef(enemyId);

      var scaling = WaveScalingCalculator.Compute(_currentWave, def, _scalingCurves);
      scaling.hpMult *= ArenaDifficultyRuntime.EnemyHpMult;
      scaling.damageMult *= ArenaDifficultyRuntime.EnemyDamageMult;

      var enemy = _spawner.SpawnWaveEnemy(enemyId, spawnPos, scaling);

      if (enemy != null)
      {
        ConfigureSpawnedEnemy(enemy, enemyId, scaling, enemyDef, SelectEliteAffix(enemyId));
        _totalEnemiesSpawned++;
        TrackSupportSpawn(enemyId);
        if (_currentModifierId == "support_x2" && !_supportDoubledThisWave)
        {
          if (profile != null && profile.role == "supporter"
              && _supportsSpawnedThisWave < _supportQuotaThisWave
              && _totalEnemiesSpawned < GetWaveEnemyCount(_currentWave))
          {
            _supportDoubledThisWave = true;
            var clonePos = PickSpawnPosition(enemyId, profile.role);
            var clone = _spawner.SpawnWaveEnemy(enemyId, clonePos, scaling);
            if (clone != null)
            {
              ConfigureSpawnedEnemy(clone, enemyId, scaling, enemyDef, null);
              _totalEnemiesSpawned++;
              TrackSupportSpawn(enemyId);
            }
          }
        }

        return true;
      }

      return false;
    }

    /// <summary>Queue horde spawns that consume the same wave quota as regular spawns.</summary>
    public void SpawnHordeReinforcement(int count)
    {
      if (_phase != Phase.WaveActive || disableEnemySpawning || count <= 0)
        return;

      if (_totalEnemiesSpawned >= GetWaveEnemyCount(_currentWave))
        return;

      _hordePending = Mathf.Min(
        _hordePending + count,
        ArenaSpawnSettings.MaxHordePending);
    }

    public GameObject SpawnEcologyEnemy(string enemyId, Vector2 position, WaveSpawnScaling scaling, int generation)
    {
      if (_spawner == null || string.IsNullOrEmpty(enemyId) || _phase != Phase.WaveActive)
        return null;

      var enemyDef = _spawner.GetEnemyDef(enemyId);
      var enemy = _spawner.SpawnWaveEnemy(enemyId, ApplySpawnClamp(position), scaling);
      if (enemy == null)
        return null;

      ConfigureSpawnedEnemy(enemy, enemyId, scaling, enemyDef, null, generation);
      return enemy;
    }

    void ConfigureSpawnedEnemy(
      GameObject enemy,
      string enemyId,
      WaveSpawnScaling scaling,
      EnemySpawner.EnemyDef enemyDef,
      EliteAffixProfile eliteAffix,
      int generation = 0)
    {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      var isBoss = !string.IsNullOrEmpty(enemyId)
                   && (enemyId.StartsWith("wild_boss_", StringComparison.Ordinal)
                       || enemyId.StartsWith("mini_boss_", StringComparison.Ordinal)
                       || enemyId.StartsWith("final_boss_", StringComparison.Ordinal));
      RuntimeValidationTelemetry.RecordEnemySpawn(isBoss);
      CombatChainTelemetry.RecordSpawn();
#endif
      TrackWaveEnemy(enemy);

      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        health.Died += () =>
        {
          UntrackWaveEnemy(enemy);
          if (enemyId != null && (enemyId.StartsWith("final_boss_", StringComparison.Ordinal)
                                  || enemyId.StartsWith("wild_boss_", StringComparison.Ordinal)
                                  || enemyId.StartsWith("mini_boss_", StringComparison.Ordinal)))
            ArenaQuadrantBlocker.Clear();
        };
      }

      if (enemyId.StartsWith("wild_boss_", StringComparison.Ordinal)
          || enemyId.StartsWith("final_boss_", StringComparison.Ordinal)
          || enemyId.StartsWith("mini_boss_", StringComparison.Ordinal))
      {
        BossWaveContext.Ensure(enemy, enemyId, scaling.waveNumber);
        if (enemy.GetComponent<BossDamageMitigation>() == null)
          enemy.AddComponent<BossDamageMitigation>();
      }

      var deathHandler = enemy.GetComponent<EnemyDeathHandler>();
      var resolved = enemyDef ?? _spawner.GetEnemyDef(enemyId);
      if (deathHandler != null && resolved != null)
        deathHandler.LootTableId = resolved.loot_table_id ?? "common_mob";

      var profile = MonsterEcosystemDatabase.Get(enemyId);
      if (profile != null)
        enemy.AddComponent<MonsterEcosystemAgent>().Configure(profile, this, scaling, eliteAffix, generation);

      if (eliteAffix != null)
        EliteHuntMarker.Attach(enemy);

      ArenaTutorialController.NotifyEnemySpawned(enemyId);
      EnemySpawnCeremony.Play(enemy, enemyId);
      ApplyBossIntroGrace(enemy, enemyId, scaling.waveNumber);
    }

    static void ApplyBossIntroGrace(GameObject enemy, string enemyId, int waveNumber)
    {
      if (enemy == null || string.IsNullOrEmpty(enemyId))
        return;

      var boss = enemy.GetComponent<BossCore>();
      if (boss == null)
        return;

      BossBalanceDatabase.EnsureLoaded();
      var ctx = enemy.GetComponent<BossWaveContext>();
      var grace = ctx != null && ctx.IntroGraceOverride > 0.01f
        ? ctx.IntroGraceOverride
        : enemyId.StartsWith("final_boss_", StringComparison.Ordinal)
          ? BossBalanceDatabase.Defaults.intro_grace_final_sec
          : enemyId.StartsWith("wild_boss_", StringComparison.Ordinal)
            ? BossBalanceDatabase.Defaults.intro_grace_wild_sec
            : BossBalanceDatabase.Defaults.intro_grace_mini_sec;

      boss.SetSkillIntroGrace(grace);
    }

    void TrackWaveEnemy(GameObject enemy)
    {
      if (enemy == null || !_trackedWaveEnemies.Add(enemy))
        return;

      _enemiesRemainingInWave = _trackedWaveEnemies.Count;
    }

    void UntrackWaveEnemy(GameObject enemy)
    {
      if (enemy != null)
        _trackedWaveEnemies.Remove(enemy);
      else
        _trackedWaveEnemies.RemoveWhere(candidate => candidate == null);

      _enemiesRemainingInWave = _trackedWaveEnemies.Count;
    }

    void RefreshTrackedEnemyCount()
    {
      _trackedWaveEnemies.RemoveWhere(enemy => enemy == null || !enemy.activeInHierarchy);
      _enemiesRemainingInWave = _trackedWaveEnemies.Count;
    }

    void SpawnBossForCurrentWave()
    {
      if (_bossSpawnedThisWave)
        return;

      if (_spawner == null || _bossWaves == null || _bossWaves.Length == 0)
      {
        if (IsBossWave(_currentWave))
        {
          Debug.LogWarning(
            $"[WaveDirector] Boss wave {_currentWave} cannot spawn (spawner/config missing). Degrading wave without boss.");
          _bossSpawnedThisWave = true;
        }

        return;
      }

      var bossProfile = GetBossWave(_currentWave);
      if (bossProfile == null || string.IsNullOrEmpty(bossProfile.enemy_id))
        return;

      var dualHardBoss = ShouldSpawnDualHardBoss(_currentWave, bossProfile);
      var bossSpawnRadius = PositiveOrDefault(bossProfile.spawn_radius, spawnRadius * 0.82f);
      if (dualHardBoss)
      {
        var quadrant = Random.Range(0, 4);
        var spawned = 0;
        if (TrySpawnBossAtQuadrant(bossProfile, bossSpawnRadius, quadrant))
          spawned++;
        if (TrySpawnBossAtQuadrant(bossProfile, bossSpawnRadius, (quadrant + 2) % 4))
          spawned++;
        else if (TrySpawnBossAtCenter(bossProfile, BuildBossScaling(bossProfile)))
          spawned++;
        if (spawned == 0 && !TrySpawnBossAtCenter(bossProfile, BuildBossScaling(bossProfile)))
        {
          Debug.LogWarning(
            $"[WaveDirector] Failed to spawn boss for wave {_currentWave}: {bossProfile.enemy_id}. Degrading wave without boss.");
          _bossSpawnedThisWave = true;
          return;
        }
      }
      else if (!TrySpawnBossAtQuadrant(bossProfile, bossSpawnRadius, Random.Range(0, 4)))
      {
        if (!TrySpawnBossAtCenter(bossProfile, BuildBossScaling(bossProfile)))
        {
          Debug.LogWarning(
            $"[WaveDirector] Failed to spawn boss for wave {_currentWave}: {bossProfile.enemy_id}. Degrading wave without boss.");
          _bossSpawnedThisWave = true;
          return;
        }
      }

      _bossSpawnedThisWave = true;

      if (debugLog)
      {
        var label = dualHardBoss ? "dual hard bosses" : "boss";
        Debug.Log($"[WaveDirector] Boss wave {_currentWave}: {bossProfile.enemy_id} ({label})");
      }
    }

    static bool ShouldSpawnDualHardBoss(int wave, BossWaveProfile profile) =>
      wave == 15
      && profile != null
      && string.Equals(ArenaDifficultyRuntime.DifficultyId, "hard", System.StringComparison.OrdinalIgnoreCase);

    bool TrySpawnBossAtQuadrant(BossWaveProfile bossProfile, float radius, int quadrant)
    {
      var bossDef = _spawner.GetEnemyDef(bossProfile.enemy_id);
      if (bossDef == null)
      {
        Debug.LogWarning($"[WaveDirector] Boss '{bossProfile.enemy_id}' has no enemy definition.");
        return false;
      }

      var bossScaling = BuildBossScaling(bossProfile);
      var spawnPos = PickBossSpawnPositionInQuadrant(radius, quadrant);
      var boss = _spawner.SpawnWaveEnemy(bossProfile.enemy_id, spawnPos, bossScaling);
      if (boss == null)
        return false;

      ConfigureSpawnedEnemy(boss, bossProfile.enemy_id, bossScaling, bossDef, null);
      var deathHandler = boss.GetComponent<EnemyDeathHandler>();
      if (deathHandler != null && !string.IsNullOrEmpty(bossProfile.loot_table_id))
        deathHandler.LootTableId = bossProfile.loot_table_id;
      return true;
    }

    bool TrySpawnBossAtCenter(BossWaveProfile bossProfile, WaveSpawnScaling bossScaling)
    {
      var bossDef = _spawner.GetEnemyDef(bossProfile.enemy_id);
      if (bossDef == null)
        return false;

      var center = GetPlayerCenter();
      var spawnPos = CircleArenaController.IsActive
        ? CircleArenaController.ClampPosition(center, 1.2f)
        : center;
      var boss = _spawner.SpawnWaveEnemy(bossProfile.enemy_id, spawnPos, bossScaling);
      if (boss == null)
        return false;

      ConfigureSpawnedEnemy(boss, bossProfile.enemy_id, bossScaling, bossDef, null);
      var deathHandler = boss.GetComponent<EnemyDeathHandler>();
      if (deathHandler != null && !string.IsNullOrEmpty(bossProfile.loot_table_id))
        deathHandler.LootTableId = bossProfile.loot_table_id;
      return true;
    }

    WaveSpawnScaling BuildBossScaling(BossWaveProfile bossProfile)
    {
      var waveDef = GetWaveDef(_currentWave);
      var waveScaling = WaveScalingCalculator.Compute(_currentWave, waveDef, _scalingCurves);
      var balance = BossBalanceDatabase.GetWaveOverride(bossProfile.enemy_id, _currentWave);
      return new WaveSpawnScaling
      {
        waveNumber = _currentWave,
        hpMult = waveScaling.hpMult
                 * PositiveOrDefault(bossProfile.hp_mult, 1f)
                 * (balance?.hp_mult_bonus ?? 1f)
                 * ArenaDifficultyRuntime.EnemyHpMult,
        damageMult = waveScaling.damageMult
                     * PositiveOrDefault(bossProfile.damage_mult, 1f)
                     * (balance?.damage_mult_bonus ?? 1f)
                     * ArenaDifficultyRuntime.EnemyDamageMult,
        speedMult = PositiveOrDefault(bossProfile.speed_mult, 1f),
        dashSpeedMult = PositiveOrDefault(bossProfile.dash_speed_mult, 1f)
      };
    }

    public bool IsBossWave(int waveNumber) => GetBossWave(waveNumber) != null;

    public bool IsMiniBossWave(int waveNumber)
    {
      var profile = GetBossWave(waveNumber);
      return profile != null && profile.is_mini_boss;
    }

    BossWaveProfile GetBossWave(int wave)
    {
      if (_bossWaves == null || _bossWaves.Length == 0)
        return null;

      foreach (var bossWave in _bossWaves)
        if (bossWave != null && bossWave.wave == wave)
          return bossWave;
      return null;
    }

    static float PositiveOrDefault(float value, float fallback) => value > 0f ? value : fallback;

    Vector2 PickBossSpawnPositionInQuadrant(float radius, int quadrant)
    {
      var center = GetPlayerCenter();
      var baseAngle = quadrant * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
      var angle = baseAngle + Random.Range(-0.28f, 0.28f);

      var minDist = 7f;
      var maxDist = 14f;
      if (CircleArenaController.IsActive && Camera.main != null)
      {
        var viewportEdge = ArenaSpawnPlanner.GetViewportEdgeRadius(Camera.main);
        minDist = Mathf.Max(7f, viewportEdge * 0.92f);
        maxDist = Mathf.Min(
          viewportEdge * ArenaSpawnSettings.SpawnBandOuterFactor,
          ArenaSpawnSettings.MaxEngagementDistance);
      }

      if (radius > 0f && radius <= 18f)
        maxDist = Mathf.Min(maxDist, radius);

      var distance = Random.Range(minDist, maxDist);
      var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

      if (CircleArenaController.IsActive)
        return CircleArenaController.ClampPosition(pos, 1.2f);

      return ApplySpawnClamp(pos);
    }

    Vector2 PickBossSpawnPosition(float radius)
    {
      return PickBossSpawnPositionInQuadrant(radius, Random.Range(0, 4));
    }

    string PickEcosystemEnemy(WaveDefinition fallback)
    {
      var ids = _activeComposition?.enemy_ids;
      if (ids != null && ids.Length > 0)
      {
        for (var attempt = 0; attempt < ids.Length; attempt++)
        {
          var enemyId = ids[_compositionCursor % ids.Length];
          _compositionCursor++;
          if (!IsSupportQuotaReached(enemyId))
            return enemyId;
        }

        return fallback.PickRandomEnemy();
      }
      return fallback.PickRandomEnemy();
    }

    bool IsSupportQuotaReached(string enemyId)
    {
      var profile = MonsterEcosystemDatabase.Get(enemyId);
      if (profile == null || profile.role != "supporter")
        return false;

      if (_supportQuotaThisWave <= 0)
        return true;

      return _supportsSpawnedThisWave >= _supportQuotaThisWave;
    }

    void TrackSupportSpawn(string enemyId)
    {
      var profile = MonsterEcosystemDatabase.Get(enemyId);
      if (profile != null && profile.role == "supporter")
        _supportsSpawnedThisWave++;
    }

    WaveComposition PickComposition(int wave)
    {
      var all = MonsterEcosystemDatabase.Root?.compositions;
      if (all == null || all.Length == 0)
        return null;

      var eligible = new List<WaveComposition>();
      var totalWeight = 0;
      foreach (var composition in all)
      {
        if (composition == null || composition.min_wave > wave
            || (composition.max_wave > 0 && composition.max_wave < wave)
            || composition.enemy_ids == null || composition.enemy_ids.Length == 0)
          continue;
        eligible.Add(composition);
        totalWeight += Mathf.Max(1, composition.weight);
      }
      if (eligible.Count == 0)
        return null;

      var roll = Random.Range(0, totalWeight);
      foreach (var composition in eligible)
      {
        roll -= Mathf.Max(1, composition.weight);
        if (roll < 0)
          return composition;
      }
      return eligible[eligible.Count - 1];
    }

    EliteAffixProfile SelectEliteAffix(string enemyId)
    {
      if (_currentWave <= 0 || _elitesSpawnedThisWave >= _eliteQuotaThisWave)
        return null;

      var allowStandardEliteWave = _currentWave % 3 == 0 || _currentModifierId == "double_elite" || _currentModifierId == "elite_hunt";
      if (!allowStandardEliteWave)
        return null;

      var profile = MonsterEcosystemDatabase.Get(enemyId);
      var affixes = MonsterEcosystemDatabase.Root?.elite_affixes;
      if (profile == null || !profile.elite_eligible || affixes == null || affixes.Length == 0)
        return null;

      _elitesSpawnedThisWave++;
      _eliteSpawnedThisWave = _elitesSpawnedThisWave >= _eliteQuotaThisWave;
      return affixes[Random.Range(0, affixes.Length)];
    }

    int GetWaveEnemyCount(int wave)
    {
      var def = GetWaveDef(wave);
      var baseCount = Mathf.FloorToInt(maxEnemiesPerWaveBase * Mathf.Pow(growthPerWave, wave - 1));
      var bonus = def != null ? def.countBonus : 0;
      var count = Mathf.Max(minEnemiesPerWave, baseCount + bonus);
      count += Mathf.FloorToInt(wave * 1.6f);
      count = Mathf.Min(count, maxCapEnemies);
      if (GetBossWave(wave) != null)
      {
        var bossProfile = GetBossWave(wave);
        var scale = bossProfile.is_mini_boss ? MiniBossWaveSpawnScale : BossWaveSpawnScale;
        count = Mathf.Max(minEnemiesPerWave / 2, Mathf.RoundToInt(count * scale));
      }
      return ArenaDifficultyRuntime.ScaleEnemyCount(count, GetBossWave(wave) != null);
    }

    WaveDefinition GetWaveDef(int wave)
    {
      int idx = wave - 1;
      if (waveDefinitions != null && idx < waveDefinitions.Length)
        return waveDefinitions[idx];
      return null;
    }

    void LoadWaveDefinitions()
    {
      var candidates = new[]
      {
        System.IO.Path.Combine(Application.dataPath, "../../data/roguelike/enemies/ring_waves.json"),
        System.IO.Path.Combine(Application.dataPath, "../../data/combat/waves.json"),
        System.IO.Path.Combine(Application.dataPath, "../../data/waves.json")
      };

      foreach (var path in candidates)
      {
        if (!System.IO.File.Exists(path))
          continue;

        var json = System.IO.File.ReadAllText(path);
        ParseWavesJson(json);
        if (debugLog)
          Debug.Log($"[WaveDirector] Loaded waves from {path}");
        return;
      }

      var resource = Resources.Load<TextAsset>("Data/roguelike/enemies/ring_waves")
                     ?? Resources.Load<TextAsset>("Data/ring_waves");
      if (resource != null)
      {
        ParseWavesJson(resource.text);
        if (debugLog)
          Debug.Log("[WaveDirector] Loaded waves from Resources ring_waves");
        return;
      }

      Debug.LogWarning("[WaveDirector] waves.json not found. Using built-in defaults.");
      waveDefinitions = CreateDefaultWaveDefs();
    }

    void ParseWavesJson(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<WavesRoot>(json);

        if (root?.wave_config != null)
          ApplyWaveConfig(root.wave_config);
        _bossWaves = root?.boss_waves;

        if (root?.waves == null || root.waves.Length == 0)
        {
          waveDefinitions = CreateDefaultWaveDefs();
          return;
        }

        var defs = new List<WaveDefinition>();
        foreach (var wd in root.waves)
        {
          var enemies = new List<WaveEnemyEntry>();
          if (wd.enemy_pool != null)
          {
            foreach (var ep in wd.enemy_pool)
              enemies.Add(new WaveEnemyEntry { enemyId = ep.enemy_id, weight = ep.weight });
          }

          defs.Add(new WaveDefinition
          {
            waveNumber = wd.wave,
            description = wd.description ?? $"Wave {wd.wave}",
            enemyPool = enemies.ToArray(),
            hpMult = wd.scaling?.hp_mult ?? 1f,
            speedMult = wd.scaling?.speed_mult ?? 1f,
            damageMult = wd.scaling?.damage_mult ?? 1f,
            dashSpeedMult = wd.scaling?.dash_speed_mult ?? 1f,
            countBonus = wd.scaling?.count_bonus ?? 0
          });
        }
        waveDefinitions = defs.ToArray();
        totalWaves = waveDefinitions.Length;
        if (debugLog) Debug.Log($"[WaveDirector] Loaded {waveDefinitions.Length} wave definitions.");
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[WaveDirector] Failed to parse waves.json: {e.Message}");
        waveDefinitions = CreateDefaultWaveDefs();
      }
    }

    WaveDefinition[] CreateDefaultWaveDefs()
    {
      return new WaveDefinition[]
      {
        MakeDef(1, 1f, 1f, 1f, 0, new[] { ("mob_square_01", 55), ("mob_hex_03", 45) }),
        MakeDef(2, 1.03f, 1.01f, 1.03f, 2, new[] { ("mob_square_01", 45), ("mob_hex_03", 40), ("mob_pent_01", 15) }),
        MakeDef(3, 1.06f, 1.02f, 1.06f, 4, new[] { ("mob_square_01", 35), ("mob_hex_03", 25), ("mob_hex_01", 25), ("mob_square_02", 15) }),
        MakeDef(4, 1.09f, 1.03f, 1.09f, 6, new[] { ("mob_square_01", 30), ("mob_hex_03", 25), ("mob_hex_01", 25), ("mob_square_02", 20) }),
        MakeDef(5, 1.12f, 1.04f, 1.12f, 8, new[] { ("mob_hex_01", 60), ("mob_square_02", 40) }),
        MakeDef(6, 1.15f, 1.05f, 1.15f, 10, new[] { ("mob_hex_01", 50), ("mob_square_02", 50) }),
        MakeDef(7, 1.18f, 1.06f, 1.18f, 12, new[] { ("mob_square_01", 35), ("mob_hex_03", 25), ("mob_star4_01", 30), ("mob_star5_01", 10) }),
        MakeDef(8, 1.22f, 1.07f, 1.22f, 14, new[] { ("mob_square_01", 30), ("mob_hex_03", 25), ("mob_pent_01", 10), ("mob_star4_01", 25), ("mob_star5_01", 10) }),
        MakeDef(9, 1.26f, 1.08f, 1.26f, 16, new[] { ("mob_tri_05", 100) }),
        MakeDef(10, 1.3f, 1.09f, 1.3f, 18, new[] { ("mob_square_01", 30), ("mob_hex_03", 20), ("mob_star4_01", 25), ("mob_star5_01", 12), ("mob_star4_02", 8), ("mob_tri_05", 5) }),
        MakeDef(11, 1.34f, 1.1f, 1.34f, 20, new[] { ("mob_square_01", 25), ("mob_hex_03", 20), ("mob_pent_01", 10), ("mob_star4_01", 22), ("mob_star5_01", 12), ("mob_star4_02", 7), ("mob_tri_05", 4) }),
        MakeDef(12, 1.38f, 1.11f, 1.38f, 22, new[] { ("mob_square_01", 20), ("mob_hex_03", 15), ("mob_hex_01", 15), ("mob_square_02", 10), ("mob_star4_01", 18), ("mob_star5_01", 10), ("mob_star4_02", 7), ("mob_tri_05", 5) }),
        MakeDef(13, 1.42f, 1.12f, 1.42f, 24, new[] { ("mob_square_01", 18), ("mob_hex_03", 14), ("mob_pent_01", 8), ("mob_hex_01", 14), ("mob_square_02", 10), ("mob_star4_01", 15), ("mob_star5_01", 9), ("mob_star4_02", 6), ("mob_star8_01", 2), ("mob_tri_05", 4) }),
        MakeDef(14, 1.48f, 1.13f, 1.48f, 26, new[] { ("mob_square_01", 16), ("mob_hex_03", 14), ("mob_pent_01", 10), ("mob_hex_01", 14), ("mob_square_02", 10), ("mob_star4_01", 14), ("mob_star5_01", 9), ("mob_star4_02", 7), ("mob_star8_01", 2), ("mob_tri_05", 4) }),
        MakeDef(15, 1.55f, 1.15f, 1.55f, 30, new[] { ("mob_square_01", 15), ("mob_hex_03", 13), ("mob_pent_01", 12), ("mob_hex_01", 13), ("mob_square_02", 12), ("mob_star4_01", 12), ("mob_star5_01", 9), ("mob_star4_02", 7), ("mob_star8_01", 3), ("mob_tri_05", 4) }),
      };
    }

    void ApplyWaveConfig(WaveConfigJson config)
    {
      if (config.build_phase_duration > 0f)
        buildPhaseDuration = config.build_phase_duration;
      if (config.pre_first_wave_delay >= 0f)
        preFirstWaveDelay = config.pre_first_wave_delay;
      if (config.min_enemies_per_wave > 0)
        minEnemiesPerWave = config.min_enemies_per_wave;
      if (config.max_enemies_per_wave_base > 0)
        maxEnemiesPerWaveBase = config.max_enemies_per_wave_base;
      if (config.enemies_per_wave_growth > 0f)
        growthPerWave = config.enemies_per_wave_growth;
      if (config.spawn_interval > 0f)
        spawnInterval = config.spawn_interval;
      if (config.spawn_point != null)
        spawnPoint = config.spawn_point.ToVector2();
      if (config.base_point != null)
        basePoint = config.base_point.ToVector2();
      if (!string.IsNullOrEmpty(config.spawn_mode))
        spawnAroundPlayer = config.spawn_mode != "fixed";
      if (config.spawn_radius > 0f)
        spawnRadius = config.spawn_radius;
      if (config.spawn_radius_jitter >= 0f)
        spawnRadiusJitter = config.spawn_radius_jitter;
      if (config.scaling_curves != null)
        _scalingCurves = config.scaling_curves;

      if (spawnAroundPlayer && config.spawn_radius <= 0f && config.spawn_point != null && config.base_point != null)
      {
        var legacyDist = Vector2.Distance(config.spawn_point.ToVector2(), config.base_point.ToVector2());
        if (legacyDist > 0.5f)
          spawnRadius = legacyDist;
      }
    }

    /// <summary>外部注入的刷怪位置修正（如竞技场边界限制）?/summary>
    public static System.Func<Vector2, Vector2> SpawnPositionClamper;

    /// <summary>刷怪环中心：优先玩家当前位置，找不到玩家时?base_point?/summary>
    Vector2 GetPlayerCenter()
    {
      var player = GameObject.FindGameObjectWithTag("Player");
      if (player == null)
        player = GameObject.Find("Player");

      if (player != null)
        return player.transform.position;

      return basePoint;
    }

    const int SpawnPickAttempts = 48;

    Vector2 PickSpawnPosition(string enemyId = null, string role = "chaser")
    {
      if (!spawnAroundPlayer)
        return PickSpawnAvoidingTowers(spawnPoint, 0f);

      if (ArenaLayoutLocator.Layout.IsActive)
      {
        if (_activeSectorAngles == null || _activeSectorAngles.Length == 0)
        {
          var progress = GetWaveSpawnProgress();
          _activeSectorAngles = ArenaSpawnPlanner.PickAttackSectors(_currentWave, progress);
        }

        var center = GetPlayerCenter();
        var pos = ArenaSpawnPlanner.PickPosition(center, role, _currentWave, _activeSectorAngles, _sectorSlot);
        _sectorSlot = (_sectorSlot + 1) % Mathf.Max(1, _activeSectorAngles.Length);
        return pos;
      }

      var center2 = GetPlayerCenter();
      for (int attempt = 0; attempt < SpawnPickAttempts; attempt++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var radius = spawnRadius + Random.Range(-spawnRadiusJitter, spawnRadiusJitter);
        radius = Mathf.Max(4f, radius);
        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        var pos = center2 + offset;
        if (!IsSpawnPositionBlocked(pos))
          return ApplySpawnClamp(pos);
      }

      // 环上多次失败：略扩大半径再试
      for (int attempt = 0; attempt < SpawnPickAttempts; attempt++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var radius = spawnRadius + spawnRadiusJitter + 2f + attempt * 0.35f;
        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        var pos = center2 + offset;
        if (!IsSpawnPositionBlocked(pos))
          return ApplySpawnClamp(pos);
      }

      return ApplySpawnClamp(center2 + Vector2.right * Mathf.Max(4f, spawnRadius));
    }

    static Vector2 ApplySpawnClamp(Vector2 pos)
    {
      return SpawnPositionClamper != null ? SpawnPositionClamper(pos) : pos;
    }

    Vector2 PickSpawnAvoidingTowers(Vector2 candidate, float extraRadius)
    {
      if (!IsSpawnPositionBlocked(candidate))
        return candidate;

      var center = GetPlayerCenter();
      for (int attempt = 0; attempt < SpawnPickAttempts; attempt++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var radius = Mathf.Max(4f, spawnRadius + extraRadius + attempt * 0.25f);
        var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        if (!IsSpawnPositionBlocked(pos))
          return pos;
      }

      return candidate;
    }

    static bool IsSpawnPositionBlocked(Vector2 worldPos) => false;

    WaveDefinition MakeDef(int wave, float hp, float spd, float dmg, int bonus, (string id, int w)[] enemies)
    {
      var pool = new WaveEnemyEntry[enemies.Length];
      for (int i = 0; i < enemies.Length; i++)
        pool[i] = new WaveEnemyEntry { enemyId = enemies[i].id, weight = enemies[i].w };
      return new WaveDefinition
      {
        waveNumber = wave,
        description = $"Wave {wave}",
        enemyPool = pool,
        hpMult = hp,
        speedMult = spd,
        damageMult = dmg,
        countBonus = bonus
      };
    }

    // ── Data Structures ──────────────────────────

    [System.Serializable]
    public class WaveDefinition
    {
      public int waveNumber;
      public string description;
      public WaveEnemyEntry[] enemyPool;
      public float hpMult = 1f;
      public float speedMult = 1f;
      public float damageMult = 1f;
      public float dashSpeedMult = 1f;
      public int countBonus;

      public string PickRandomEnemy()
      {
        if (enemyPool == null || enemyPool.Length == 0) return "mob_hex_01";

        int totalWeight = 0;
        foreach (var e in enemyPool) totalWeight += e.weight;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var e in enemyPool)
        {
          cumulative += e.weight;
          if (roll < cumulative) return e.enemyId;
        }
        return enemyPool[enemyPool.Length - 1].enemyId;
      }
    }

    [System.Serializable]
    public class WaveEnemyEntry
    {
      public string enemyId;
      public int weight;
    }

    // JSON parsing
    [System.Serializable]
    class WavesRoot
    {
      public WaveConfigJson wave_config;
      public WaveJson[] waves;
      public BossWaveProfile[] boss_waves;
    }

    [System.Serializable]
    class WaveConfigJson
    {
      public float build_phase_duration;
      public float pre_first_wave_delay;
      public int min_enemies_per_wave;
      public int max_enemies_per_wave_base;
      public float enemies_per_wave_growth;
      public float spawn_interval;
      public string spawn_mode;
      public Vec2Json spawn_point;
      public Vec2Json base_point;
      public float spawn_radius;
      public float spawn_radius_jitter;
      public WaveScalingCurves scaling_curves;
    }

    [System.Serializable]
    class Vec2Json
    {
      public float x;
      public float y;

      public Vector2 ToVector2() => new(x, y);
    }

    [System.Serializable]
    class WaveJson
    {
      public int wave;
      public string description;
      public EnemyPoolEntry[] enemy_pool;
      public WaveScaling scaling;
    }

    [System.Serializable]
    class EnemyPoolEntry { public string enemy_id; public int weight; }

    [System.Serializable]
    class WaveScaling
    {
      public float hp_mult;
      public float speed_mult;
      public float damage_mult;
      public float dash_speed_mult;
      public int count_bonus;
    }

    [System.Serializable]
    class BossWaveProfile
    {
      public int wave;
      public string enemy_id;
      public float spawn_radius;
      public float hp_mult = 1f;
      public float damage_mult = 1f;
      public float speed_mult = 1f;
      public float dash_speed_mult = 1f;
      public bool is_mini_boss;
      public string loot_table_id = "wild_boss";
    }

#if UNITY_EDITOR
    public bool BossSpawnedThisWave => _bossSpawnedThisWave;
    public int EditorSpawnFailRemaining
    {
      get => _editorSpawnFailRemaining;
      set => _editorSpawnFailRemaining = Mathf.Max(0, value);
    }

    public static void EditorDestroyInstance()
    {
      if (Instance == null)
        return;
      var go = Instance.gameObject;
      Instance = null;
      if (go != null)
        DestroyImmediate(go);
    }

    public void EditorInitializeForTests(EnemySpawner spawner, bool manualSpawning = true)
    {
      Instance = this;
      _spawner = spawner;
      disableEnemySpawning = manualSpawning;
      LoadWaveDefinitions();
      MonsterEcosystemDatabase.EnsureLoaded();
      WaveModifierDatabase.EnsureLoaded();
      totalWaves = 15;
      buildPhaseDuration = 3f;
      preFirstWaveDelay = 0f;
      ResetWaveFields();
    }

    public void EditorRefreshTrackingForTests() => RefreshTrackedEnemyCount();

    public void EditorRetryBossSpawnForTests() => SpawnBossForCurrentWave();

    public void EditorMarkBossSpawnedForTests() => _bossSpawnedThisWave = true;

    public void EditorSetManualSpawning(bool enabled) => disableEnemySpawning = enabled;

    public void EditorForceWaveActive(int wave)
    {
      PrepareWave(wave);
      ActivateWaveActive();
    }

    public GameObject EditorTrackMockEnemy(bool countTowardQuota = true)
    {
      var go = new GameObject("MockWaveEnemy");
      TrackWaveEnemy(go);
      if (countTowardQuota)
        _totalEnemiesSpawned++;
      return go;
    }

    public void EditorUntrackWithoutDeath(GameObject enemy)
    {
      UntrackWaveEnemy(enemy);
      if (enemy != null)
        DestroyImmediate(enemy);
    }

    public void EditorKillTracked(GameObject enemy)
    {
      if (enemy == null)
        return;
      UntrackWaveEnemy(enemy);
      DestroyImmediate(enemy);
    }

    public void EditorTickWaveActive()
    {
      if (_phase != Phase.WaveActive)
        return;

      RefreshTrackedEnemyCount();
      if (IsBossWave(_currentWave) && !_bossSpawnedThisWave)
        SpawnBossForCurrentWave();

      if (_enemiesRemainingInWave <= 0 && IsWaveSpawnQuotaMet
          && (!IsBossWave(_currentWave) || _bossSpawnedThisWave))
        CompleteWave(_currentWave);
    }

    public void EditorFillSpawnQuotaWithMocks()
    {
      var quota = GetWaveEnemyCount(_currentWave);
      while (_totalEnemiesSpawned < quota)
        EditorTrackMockEnemy(true);
    }

    public void EditorProcessHordeOnce()
    {
      _hordeSpawnDelay = 0f;
      ProcessHordeSpawns();
    }

    public bool EditorTrySpawnOnceForTests() => TrySpawnEnemyForCurrentWave();

    public void EditorSimulateBossSpawnFailureForTests()
    {
      if (_bossSpawnedThisWave)
        return;

      var spawner = _spawner;
      _spawner = null;
      SpawnBossForCurrentWave();
      _spawner = spawner;
    }

    public void EditorPrepareWaveActiveWithoutBossSpawn(int wave)
    {
      PrepareWave(wave);
      _phase = Phase.WaveActive;
      _bossSpawnedThisWave = false;
    }

    public void EditorSimulateRunRestartAfterDeath()
    {
      enabled = false;
      ResetRunState();
    }

    public int EditorHordePending => _hordePending;
    public int EditorSpawnGroupRemaining => _spawnGroupRemaining;

    public void EditorBeginSpawnGroupForTests()
    {
      BeginSpawnGroup();
      _spawnTimer = 0f;
      _spawnGroupCooldown = 0f;
      _spawnUnitDelay = 0f;
    }

    public void EditorTickSpawnRhythmOnce()
    {
      if (_spawnGroupRemaining <= 0 || _spawnTimer > 0f)
        return;

      if (TrySpawnEnemyForCurrentWave())
      {
        _spawnGroupRemaining--;
        _spawnUnitDelay = Random.Range(0f, ArenaSpawnSettings.UnitSpawnJitter);
        _spawnTimer = GetEffectiveSpawnInterval();
        if (_spawnGroupRemaining <= 0)
          _spawnGroupCooldown = ArenaSpawnSettings.GroupInterval;
      }
      else
        _spawnTimer = GetEffectiveSpawnInterval() * 0.35f;
    }
#endif
  }
}
