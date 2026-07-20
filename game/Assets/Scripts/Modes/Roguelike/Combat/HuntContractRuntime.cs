using UnityEngine;



using Game.Modes.Roguelike.UI;

using Game.Shared.Gameplay;

using Game.Shared.Gameplay.Events;



namespace Game.Modes.Roguelike.Combat

{

  /// <summary>A12: 每局随机 1 波 — 90s 内击杀 30 敌，成功奖励 Relic 三选一。</summary>

  [DisallowMultipleComponent]

  public sealed class HuntContractRuntime : MonoBehaviour

  {

    public const int KillTarget = 30;

    public const float TimeLimitSeconds = 90f;



    static HuntContractRuntime s_instance;

    static int s_contractWave = -1;

    static bool s_contractConsumed;



    int _kills;

    float _timeRemaining;

    bool _tracking;

    bool _resolved;

    bool _rewardGranted;



    EventListenerHandle _enemyKilledHandle;



    public static bool IsContractWave(int wave) =>

      !s_contractConsumed && wave > 0 && wave == s_contractWave;



    public static bool IsTracking =>

      s_instance != null && s_instance._tracking && !s_instance._resolved;



    public static int CurrentKills => s_instance != null ? s_instance._kills : 0;



    public static float TimeRemaining => s_instance != null ? s_instance._timeRemaining : 0f;



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_HuntContractRuntime");

      if (!Application.isPlaying)

        go.hideFlags = HideFlags.HideAndDontSave;

      go.AddComponent<HuntContractRuntime>();

    }



    public static void BeginRun(int totalWaves)
    {
      s_contractConsumed = false;
      s_contractWave = PickContractWave(Mathf.Max(6, totalWaves));
      if (s_instance != null)
      {
        s_instance._kills = 0;
        s_instance._timeRemaining = 0f;
        s_instance._tracking = false;
        s_instance._resolved = false;
        s_instance._rewardGranted = false;
      }
    }

#if UNITY_EDITOR

    public static void EditorDestroyForTests()

    {

      foreach (var runtime in Object.FindObjectsOfType<HuntContractRuntime>(true))

      {

        if (runtime != null)

          DestroyImmediate(runtime.gameObject);

      }

      s_instance = null;

      s_contractWave = -1;

      s_contractConsumed = false;

    }

#endif



    static int PickContractWave(int totalWaves)

    {

      var maxWave = Mathf.Min(18, totalWaves - 1);

      const int attempts = 24;

      for (var i = 0; i < attempts; i++)

      {

        var wave = Random.Range(6, maxWave + 1);

        if (wave == 7 || wave == 9 || wave == 17 || wave == 19)

          continue;

        return wave;

      }



      return 12;

    }



    void Awake()

    {

      if (s_instance != null)

      {

        Destroy(gameObject);

        return;

      }



      s_instance = this;

      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);

      WaveDirector.PhaseChanged += OnPhaseChanged;

      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);

    }



    void OnDestroy()

    {

      WaveDirector.PhaseChanged -= OnPhaseChanged;

      if (_enemyKilledHandle.Valid)

        GameEventBus.Unsubscribe(_enemyKilledHandle);

      if (s_instance == this)

        s_instance = null;

    }



    void Update()

    {

      if (!_tracking || _resolved)

        return;



      _timeRemaining -= Time.deltaTime;

      if (_timeRemaining <= 0f)

        ResolveFailure();

    }



    void OnPhaseChanged(WaveDirector.Phase phase, int wave)

    {

      if (phase == WaveDirector.Phase.WaveCountdown && IsContractWave(wave))

      {

        _kills = 0;

        _timeRemaining = TimeLimitSeconds;

        _tracking = true;

        _resolved = false;

        _rewardGranted = false;

        ArenaMomentUI.ShowBanner("猎杀契约 — 90 秒内击杀 30 敌", new Color(1f, 0.78f, 0.28f, 1f));

        return;

      }



      if (phase != WaveDirector.Phase.WaveActive && phase != WaveDirector.Phase.WaveCountdown)

        _tracking = false;



      if (phase == WaveDirector.Phase.BuildPhase && wave > s_contractWave && !_rewardGranted)

        s_contractConsumed = true;

    }



    void OnEnemyKilled(EnemyKilledEvent evt)

    {

      if (!_tracking || _resolved || !PlayerCombatAttribution.IsPlayerOrOwned(evt.Killer))

        return;



      _kills++;

      if (_kills < KillTarget)

        return;



      ResolveSuccess();

    }



    void ResolveSuccess()

    {

      if (_resolved)

        return;



      _resolved = true;

      _tracking = false;

      _rewardGranted = true;

      s_contractConsumed = true;

      ArenaMomentUI.ShowBanner("契约完成 — 选择 Relic 奖励", new Color(0.55f, 1f, 0.72f, 1f));

      RunTimelineRecorder.Record("猎杀契约", "完成");

      ArenaRelicPickUI.ShowOffer("猎杀契约 — Relic 三选一", "猎杀契约");

    }



    void ResolveFailure()

    {

      if (_resolved)

        return;



      _resolved = true;

      _tracking = false;

      s_contractConsumed = true;

      ArenaMomentUI.ShowBanner("契约失败", new Color(1f, 0.42f, 0.35f, 1f));

      RunTimelineRecorder.Record("猎杀契约", "失败");

    }

  }

}


