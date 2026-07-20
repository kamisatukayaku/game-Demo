using UnityEngine;



using Game.Modes.Roguelike.Progression;

using Game.Shared.Enemy.AI;

using Game.Shared.Enemy.Spawn;

using Game.Shared.Gameplay;

using Game.Shared.Gameplay.Bridges;



namespace Game.Modes.Roguelike.Combat

{

  /// <summary>A13: 每局 0–1 只金色高 HP 游荡怪，击杀给进化加速或大额 XP。</summary>

  [DisallowMultipleComponent]

  public sealed class ArenaRareEnemySpawner : MonoBehaviour

  {

    public const string RareEnemyId = "arena_rare_golden";

    const string BaseEnemyId = "mob_pent_01";

    const float SpawnChance = 0.65f;

    const float HpMultiplier = 11f;

    const float SpeedMultiplier = 0.62f;

    const float WanderAggroRange = 1.6f;



    static ArenaRareEnemySpawner s_instance;



    bool _willSpawn;

    int _spawnWave = -1;

    bool _spawned;



    public static bool WillSpawnThisRun => s_instance != null && s_instance._willSpawn;

    public static int SpawnWave => s_instance != null ? s_instance._spawnWave : -1;



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_ArenaRareEnemySpawner");

      go.AddComponent<ArenaRareEnemySpawner>();

    }



    public static void BeginRun(int totalWaves)

    {

      EnsureExists();

      if (s_instance == null)

        return;

      s_instance.ResetRun(totalWaves);

    }



    void ResetRun(int totalWaves)

    {

      _willSpawn = Random.value <= SpawnChance;

      _spawnWave = _willSpawn

        ? Random.Range(4, Mathf.Min(15, Mathf.Max(5, totalWaves - 2)) + 1)

        : -1;

      _spawned = false;

    }



    void Awake()

    {

      if (s_instance != null)

      {

        Destroy(gameObject);

        return;

      }



      s_instance = this;

      DontDestroyOnLoad(gameObject);

      WaveDirector.PhaseChanged += OnPhaseChanged;

    }



    void OnDestroy()

    {

      WaveDirector.PhaseChanged -= OnPhaseChanged;

      if (s_instance == this)

        s_instance = null;

    }



    void OnPhaseChanged(WaveDirector.Phase phase, int wave)

    {

      if (!_willSpawn || _spawned || wave != _spawnWave)

        return;



      if (phase == WaveDirector.Phase.BuildPhase || phase == WaveDirector.Phase.WaveActive)

        TrySpawn(wave);

    }



    void TrySpawn(int wave)

    {

      if (_spawned)

        return;



      var spawner = FindAnyObjectByType<EnemySpawner>();

      if (spawner == null)

        return;



      var scaling = WaveScalingCalculator.Compute(wave, null, WaveScalingCalculator.DefaultCurves);

      scaling.hpMult *= HpMultiplier * ArenaDifficultyRuntime.EnemyHpMult;

      scaling.damageMult *= 0.35f * ArenaDifficultyRuntime.EnemyDamageMult;

      scaling.speedMult *= SpeedMultiplier;



      var enemy = spawner.SpawnWaveEnemy(BaseEnemyId, PickSpawnPosition(), scaling);

      if (enemy == null)

        return;



      _spawned = true;

      enemy.name = RareEnemyId;

      ConfigureRareEnemy(enemy);

    }



    static void ConfigureRareEnemy(GameObject enemy)

    {

      var core = enemy.GetComponent<EnemyCore>();

      core?.ApplyScaledStats(1.35f, 3f, WanderAggroRange, 1.1f, 0.35f, 1.6f, 0.75f);

      core?.SetWildOrigin();



      var deathHandler = enemy.GetComponent<Game.Shared.Enemy.Death.EnemyDeathHandler>();

      if (deathHandler != null)

        deathHandler.LootTableId = string.Empty;



      if (enemy.GetComponent<RareGoldenMarker>() == null)

        enemy.AddComponent<RareGoldenMarker>();

    }



    static Vector2 PickSpawnPosition()

    {

      var player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");

      var center = player != null ? (Vector2)player.transform.position : Vector2.zero;



      if (ArenaLayoutLocator.Layout.IsActive)

      {

        var angle = Random.Range(0f, Mathf.PI * 2f);

        var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 12f;

        return ArenaLayoutLocator.Layout.GetSpawnPointOnCircle(pos);

      }



      var offset = Random.insideUnitCircle.normalized * Random.Range(10f, 14f);

      return center + offset;

    }

  }

}


