#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using Game.Modes.Roguelike.Combat;

using Game.Shared.Combat.Events;

using Game.Shared.Diagnostics;

using Game.Shared.Gameplay.Events;

using UnityEngine;



namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation

{

  /// <summary>Hooks combat events into <see cref="CombatChainTelemetry"/>.</summary>

  [DisallowMultipleComponent]

  public sealed class CombatChainProbe : MonoBehaviour

  {

    static CombatChainProbe s_instance;



    EventListenerHandle _enemyKilledHandle;

    WaveDirector.Phase _lastPhase = WaveDirector.Phase.NotStarted;

    bool _hooksRegistered;



    public static void EnsureExists()

    {

      if (s_instance != null)

      {

        s_instance.RegisterHooks();

        return;

      }



      var go = new GameObject("_CombatChainProbe");

      DontDestroyOnLoad(go);

      s_instance = go.AddComponent<CombatChainProbe>();

    }



    void Awake()

    {

      if (s_instance != null && s_instance != this)

      {

        Destroy(gameObject);

        return;

      }



      s_instance = this;

      DontDestroyOnLoad(gameObject);

      RegisterHooks();

    }



    void RegisterHooks()

    {

      if (_hooksRegistered)

        return;



      CombatValidationHooks.OnEnemyRegistered = () => CombatChainTelemetry.RecordRegistryRegister();

      CombatValidationHooks.OnTargetAcquired = () => CombatChainTelemetry.RecordTargetAcquire();

      CombatValidationHooks.OnAttackAttempt = () => CombatChainTelemetry.RecordAttackAttempt();

      CombatValidationHooks.OnAttackBlocked = reason => CombatChainTelemetry.RecordAttackBlocked(reason);

      CombatValidationHooks.OnPlayerProjectileSpawned = () => CombatChainTelemetry.RecordProjectileSpawn();

      _hooksRegistered = true;

    }



    void OnEnable()

    {

      CombatEventBus.PostDamage += OnPostDamage;

      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);

    }



    void OnDisable()

    {

      CombatEventBus.PostDamage -= OnPostDamage;

      if (_enemyKilledHandle.Valid)

        GameEventBus.Unsubscribe(_enemyKilledHandle);

    }



    void OnDestroy()

    {

      if (s_instance == this)

      {

        s_instance = null;

        CombatValidationHooks.Reset();

      }

    }



    void Update()

    {

      var director = WaveDirector.Instance;

      if (director == null)

        return;



      var phase = director.CurrentPhase;

      if (phase == WaveDirector.Phase.WaveActive && _lastPhase != WaveDirector.Phase.WaveActive)

        CombatChainTelemetry.MarkWaveActiveStart();



      _lastPhase = phase;

    }



    static void OnPostDamage(in CombatEventBus.PostDamageArgs args)

    {

      if (args.Result.FinalDamage <= 0f)

        return;



      var player = GameObject.FindWithTag("Player");

      if (player == null || args.Attacker != player)

        return;



      var target = args.Target;

      if (target == null || target.CompareTag("Player"))

        return;



      CombatChainTelemetry.RecordPlayerDamage();

    }



    static void OnEnemyKilled(EnemyKilledEvent evt) => CombatChainTelemetry.RecordKill();



    public static void NotifyXpPickupSpawn() => CombatChainTelemetry.RecordXpPickupSpawn();



    public static void NotifyXpPickupCollect() => CombatChainTelemetry.RecordXpPickupCollect();

  }

}

#endif

