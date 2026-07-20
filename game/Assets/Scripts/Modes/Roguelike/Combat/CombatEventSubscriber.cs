using UnityEngine;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Gameplay.Player;
using Health = global::Game.Shared.Combat.Health.Health;

using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Events;
using Game.Shared.Core;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>
  /// 全局战斗事件订阅器。连?CombatEventBus 的孤岛事件到实际功能?
  /// ?CombatRoot ?Phase 2 创建。单例?
  /// </summary>
  public class CombatEventSubscriber : MonoBehaviour
  {
    [Header("Debug")]
    [SerializeField] bool debugLog;

    int _totalKills;
    EventListenerHandle _enemyKilledHandle;
    EventListenerHandle _playerDeathHandle;
    EventListenerHandle _playerDamagedHandle;

    public int TotalKills => _totalKills;

    static CombatEventSubscriber s_instance;

    public static CombatEventSubscriber Instance => s_instance;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_CombatEventSubscriber");
      DontDestroyOnLoad(go);
      go.AddComponent<CombatEventSubscriber>();
    }

    public static void ResetForNewRun()
    {
      if (s_instance != null)
        s_instance._totalKills = 0;
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
      // ── Subscribe to all CombatEventBus orphan events ──

      // PreDamage: 可用于护?无敌帧等拦截
      CombatEventBus.PreDamage += OnPreDamage;

      // PostDamage: 受伤特效、反伤、统讀"
      CombatEventBus.PostDamage += OnPostDamage;

      // Buff events: Buff 图标提示、音敀"
      CombatEventBus.BuffAppliedRaw += OnBuffApplied;
      CombatEventBus.BuffRemovedRaw += OnBuffRemoved;
      CombatEventBus.BuffExpiredRaw += OnBuffExpired;

      // OnKill hooks via unified GameEventBus
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
      _playerDeathHandle = GameEventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
      _playerDamagedHandle = GameEventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);

      // ── Subscribe to WaveDirector events ──
      WaveDirector.WaveCompleted += OnWaveCompleted;
      WaveDirector.AllWavesCompleted += OnAllWavesCompleted;
    }

    void OnDestroy()
    {
      CombatEventBus.PreDamage -= OnPreDamage;
      CombatEventBus.PostDamage -= OnPostDamage;
      CombatEventBus.BuffAppliedRaw -= OnBuffApplied;
      CombatEventBus.BuffRemovedRaw -= OnBuffRemoved;
      CombatEventBus.BuffExpiredRaw -= OnBuffExpired;
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      if (_playerDeathHandle.Valid)
        GameEventBus.Unsubscribe(_playerDeathHandle);
      if (_playerDamagedHandle.Valid)
        GameEventBus.Unsubscribe(_playerDamagedHandle);
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      WaveDirector.AllWavesCompleted -= OnAllWavesCompleted;

      if (s_instance == this) s_instance = null;
    }

    // ── Event Handlers ──────────────────────────────

    void OnPreDamage(in CombatEventBus.PreDamageArgs args)
    {
      if (debugLog)
        Debug.Log($"[EventBus] PreDamage: {args.Attacker?.name} {args.Target?.name} base={args.Request.Base:F1}");
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      var position = args.Target != null ? args.Target.transform.position : Vector3.zero;
      GameEventBus.Publish(new DamageEvent(
        args.Attacker,
        args.Target,
        position,
        args.Result.FinalDamage,
        args.Request.DamageTypeId,
        args.Request.DamageSourceId));

      if (debugLog)
        Debug.Log($"[EventBus] PostDamage: {args.Attacker?.name} {args.Target?.name} final={args.Result.FinalDamage:F1}");
    }

    void OnBuffApplied(GameObject target, string buffId)
    {
      if (debugLog && target != null)
        Debug.Log($"[EventBus] BuffApplied: {target.name} {buffId}");
    }

    void OnBuffRemoved(GameObject target, string buffId)
    {
      if (debugLog && target != null)
        Debug.Log($"[EventBus] BuffRemoved: {target.name} {buffId}");
    }

    void OnBuffExpired(GameObject target, string buffId)
    {
      if (debugLog && target != null)
        Debug.Log($"[EventBus] BuffExpired: {target.name} {buffId}");
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      _totalKills++;
      var deathPosition = evt.Enemy != null ? evt.Enemy.transform.position : Vector3.zero;
      GameEventBus.Publish(new EnemyDeathEvent(evt.Enemy, deathPosition, evt.EnemyId));

      if (!GameSessionConfig.IsBossRush
          && !string.IsNullOrEmpty(evt.EnemyId)
          && (evt.EnemyId.StartsWith("wild_boss_") || evt.EnemyId.StartsWith("final_boss_"))
          && IsPlayerAttribution(evt.Killer))
      {
        RunTimelineRecorder.Record("Boss", evt.EnemyId);
        ArenaRelicPickUI.ShowOffer();
      }

      var healOnKillPct = RunBuildState.GetHealOnKillPct();
      var healOnKillFlat = RunBuildState.GetHealOnKillFlat();
      if (IsPlayerAttribution(evt.Killer) && (healOnKillPct > 0f || healOnKillFlat > 0f))
      {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
          var playerHealth = player.GetComponent<Health>();
          if (playerHealth != null && !playerHealth.IsDead)
          {
            var healMult = RunBuildState.GetHealingReceivedMult();
            var amount = playerHealth.MaxHp * healOnKillPct;
            if (healOnKillFlat > 0f)
            {
              var flat = healOnKillFlat;
              if (evt.IsBoss)
                flat *= 2f;
              else if (!string.IsNullOrEmpty(evt.EnemyId)
                       && (evt.EnemyId.Contains("elite") || evt.EnemyId.Contains("mini_boss")))
                flat *= 1.5f;
              amount += flat;
            }

            amount *= healMult;
            amount = HealOnKillBudget.RequestHeal(amount);
            if (amount > 0f)
              playerHealth.Heal(amount);
          }
        }
      }

      var victimHealth = evt.Enemy != null ? evt.Enemy.GetComponent<Health>() : null;

      if (debugLog)
      {
        var killerName = evt.Killer != null ? evt.Killer.name : "environment";
        Debug.Log($"[EventBus] EnemyKilled: {killerName} killed {evt.Enemy?.name} ({evt.EnemyId}), total={_totalKills}");
      }
    }

    void OnPlayerDamaged(PlayerDamagedEvent evt)
    {
      if (evt.Player == null)
        return;

      var extra = RunBuildState.GetHitInvulnerabilityAdd();
      if (extra <= 0f)
        return;

      var health = evt.Player.GetComponent<Health>();
      health?.GrantInvulnerability(extra);
    }

    void OnPlayerDeath(PlayerDeathEvent evt)
    {
      MageZonePool.ResetAll();
      if (debugLog)
        Debug.Log($"[EventBus] Player killed by {(evt.Killer != null ? evt.Killer.name : "unknown")}");
    }

    static bool IsPlayerAttribution(GameObject killer) =>
      PlayerCombatAttribution.IsPlayerOrOwned(killer);

    void OnWaveCompleted(int waveNumber)
    {
      if (debugLog)
        Debug.Log($"[WaveDirector] Wave {waveNumber} completed. Total kills: {_totalKills}");
    }

    void OnAllWavesCompleted()
    {
      RunDeathSummary.CaptureAtVictory();
      var shards = ArenaMetaProgress.AwardRun(
        true,
        RunDeathSummary.WaveReached,
        RunDeathSummary.TotalKills,
        RunDeathSummary.PlayerLevel,
        RunDeathSummary.SurviveSeconds);

      if (debugLog)
        Debug.Log($"[WaveDirector] Victory! kills={RunDeathSummary.TotalKills} shards={shards}");

      ArenaVictoryUI.Show(shards);
      ArenaAchievementSystem.EvaluateRun(
        true,
        RunDeathSummary.WaveReached,
        RunDeathSummary.TotalKills,
        RunDeathSummary.PlayerLevel,
        RunDeathSummary.BuildDirection,
        ArenaDifficultyRuntime.DifficultyId);
      DemoContentWallUI.ShowAfterVictory(RunDeathSummary.WaveReached);
    }
  }
}
