using UnityEngine;



using Game.Modes.Roguelike.Combat;

using Game.Modes.Roguelike.Progression;

using Game.Shared.Gameplay.Events;

using Health = global::Game.Shared.Combat.Health.Health;



namespace Game.Modes.Roguelike.UI

{

  /// <summary>B10: Records decision moments from combat events (W8+ gate).</summary>

  [DisallowMultipleComponent]

  public sealed class RunDecisionMomentListener : MonoBehaviour

  {

    const int WaveGate = 8;

    const float BossClutchHpThreshold = 0.01f;



    static RunDecisionMomentListener s_instance;



    EventListenerHandle _bossKilledHandle;

    EventListenerHandle _levelUpHandle;



    public static void EnsureExists()

    {

      if (s_instance != null)

        return;



      var go = new GameObject("_RunDecisionMomentListener");

      DontDestroyOnLoad(go);

      go.AddComponent<RunDecisionMomentListener>();

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

    }



    void OnDestroy()

    {

      if (s_instance == this)

        s_instance = null;

    }



    void OnEnable()

    {

      _bossKilledHandle = GameEventBus.Subscribe<BossKilledEvent>(OnBossKilled);

      _levelUpHandle = GameEventBus.Subscribe<LevelUpEvent>(OnLevelUp);

    }



    void OnDisable()

    {

      if (_bossKilledHandle.Valid)

        GameEventBus.Unsubscribe(_bossKilledHandle);

      if (_levelUpHandle.Valid)

        GameEventBus.Unsubscribe(_levelUpHandle);

    }



    void OnBossKilled(BossKilledEvent evt)

    {

      if (!IsWaveGateMet())

        return;



      var killer = evt.Killer;

      if (killer == null)

        return;



      var health = killer.GetComponent<Health>();

      if (health == null || health.HpPercent > BossClutchHpThreshold)

        return;



      var pct = Mathf.RoundToInt(health.HpPercent * 100f);

      RunTimelineRecorder.TryRecordOnce(

        RunTimelineRecorder.MomentKind.BossClutchKill,

        "决定瞬间",

        $"Boss 反杀（HP {pct}%）· {evt.BossId}");

    }



    void OnLevelUp(LevelUpEvent evt)

    {

      if (!IsWaveGateMet())

        return;



      var delta = evt.NewLevel - evt.OldLevel;

      if (delta < 2)

        return;



      RunTimelineRecorder.TryRecordOnce(

        RunTimelineRecorder.MomentKind.LevelUpStreak,

        "决定瞬间",

        $"连升 {delta} 级 · Lv.{evt.OldLevel}→{evt.NewLevel}");

    }



    static bool IsWaveGateMet()

    {

      var director = WaveDirector.Instance;

      return director != null && director.CurrentWave >= WaveGate;

    }

  }

}


