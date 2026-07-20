using System;
using System.Collections;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  public enum BossRushPhase
  {
    NotStarted,
    PreparingRun,
    EncounterIntro,
    PreFightCountdown,
    BossActive,
    BossDefeated,
    RewardSelection,
    Recovery,
    NextEncounter,
    FinalVictory,
    PlayerDefeated,
    ConfigError
  }

  [DisallowMultipleComponent]
  public sealed class BossRushDirector : MonoBehaviour
  {
    public static BossRushDirector Instance { get; private set; }

    public BossRushPhase Phase { get; private set; } = BossRushPhase.NotStarted;
    public int CurrentEncounterIndex { get; private set; }
    public int DefeatedBossCount { get; private set; }
    public float RunElapsedSeconds { get; private set; }
    public string LastError { get; private set; }

    EnemySpawner _spawner;
    GameObject _activeBoss;
    BossRushEncounterDef _activeEncounter;
    Coroutine _flowRoutine;
    EventListenerHandle _bossKilledHandle;
    EventListenerHandle _playerDeathHandle;
    float _countdownRemaining;
    float _encounterStartedAt;

    public static event Action<BossRushPhase, int> PhaseChanged;

    public static void BeginRun()
    {
      if (!GameSessionConfig.IsBossRush)
        return;

      if (Instance != null)
      {
        Instance.ResetRunState();
        return;
      }

      var go = new GameObject("_BossRushDirector");
      go.AddComponent<BossRushDirector>();
    }

    public static void ResetForNewRun()
    {
      if (Instance != null)
        Instance.ResetRunState();
    }

    void Awake()
    {
      if (Instance != null)
      {
        Destroy(gameObject);
        return;
      }

      Instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
      UnsubscribeEvents();
      if (Instance == this)
        Instance = null;
    }

    void Start()
    {
      ResetRunState();
    }

    void Update()
    {
      if (Phase == BossRushPhase.BossActive || Phase == BossRushPhase.PreFightCountdown)
        RunElapsedSeconds += Time.deltaTime;

      if (Phase == BossRushPhase.BossActive)
        BossRushEncounterRuntime.RefreshLivingMinionCount(_activeBoss);
    }

    public void ResetRunState()
    {
      StopAllCoroutines();
      UnsubscribeEvents();
      BossRushDatabase.EnsureLoaded();
      if (!BossRushDatabase.IsLoaded)
      {
        SetPhase(BossRushPhase.ConfigError);
        LastError = BossRushDatabase.LoadError ?? "Boss Rush config missing.";
        BossRushHUD.ShowConfigError(LastError);
        return;
      }

      CurrentEncounterIndex = 1;
      DefeatedBossCount = 0;
      RunElapsedSeconds = 0f;
      _activeBoss = null;
      _activeEncounter = null;
      BossRushEncounterRuntime.Clear();
      BossWaveContext.ExternalLivingMinionCounter = null;
      HealOnKillBudget.Reset();
      UpgradeOfferPityTracker.ResetForNewRun();
      SubscribeEvents();
      _flowRoutine = StartCoroutine(RunFlow());
    }

    void SubscribeEvents()
    {
      UnsubscribeEvents();
      _bossKilledHandle = GameEventBus.Subscribe<BossKilledEvent>(OnBossKilled);
      _playerDeathHandle = GameEventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
    }

    void UnsubscribeEvents()
    {
      if (_bossKilledHandle.Valid)
        GameEventBus.Unsubscribe(_bossKilledHandle);
      if (_playerDeathHandle.Valid)
        GameEventBus.Unsubscribe(_playerDeathHandle);
      _bossKilledHandle = default;
      _playerDeathHandle = default;
    }

    IEnumerator RunFlow()
    {
      SetPhase(BossRushPhase.PreparingRun);
      _spawner = BossRushCombatService.EnsureSpawner();
      yield return null;

      var openingRewards = Mathf.Max(0, BossRushDatabase.Settings.opening_reward_count);
      if (openingRewards > 0)
      {
        SetPhase(BossRushPhase.RewardSelection);
        yield return RunOpeningRewards(openingRewards);
      }

      while (CurrentEncounterIndex <= BossRushDatabase.Encounters.Count)
      {
        _activeEncounter = BossRushDatabase.GetEncounter(CurrentEncounterIndex);
        if (_activeEncounter == null || string.IsNullOrEmpty(_activeEncounter.boss_id))
        {
          LastError = $"Encounter {_activeEncounter?.index ?? CurrentEncounterIndex} has invalid boss_id.";
          SetPhase(BossRushPhase.ConfigError);
          BossRushHUD.ShowConfigError(LastError);
          yield break;
        }

        SetPhase(BossRushPhase.EncounterIntro);
        BossRushEncounterRuntime.BeginEncounter(_activeEncounter, CurrentEncounterIndex);
        if (_activeEncounter.target_duration_seconds <= 0f)
          Debug.LogWarning($"[BossRush] Encounter {_activeEncounter.index} missing target_duration_seconds; telemetry only.");
        BossRushHUD.ShowEncounterIntro(_activeEncounter, CurrentEncounterIndex, BossRushDatabase.Encounters.Count);
        yield return new WaitForSecondsRealtime(1.2f);

        SetPhase(BossRushPhase.PreFightCountdown);
        var countdown = BossRushDatabase.Settings.pre_fight_countdown;
        while (countdown > 0f)
        {
          BossRushHUD.ShowCountdown(Mathf.CeilToInt(countdown));
          yield return new WaitForSecondsRealtime(1f);
          countdown -= 1f;
        }

        if (!TrySpawnCurrentBoss())
        {
          yield return new WaitForSecondsRealtime(0.5f);
          if (!TrySpawnCurrentBoss())
          {
            LastError = $"Failed to spawn boss '{_activeEncounter.boss_id}'.";
            SetPhase(BossRushPhase.ConfigError);
            BossRushHUD.ShowConfigError(LastError);
            yield break;
          }
        }

        SetPhase(BossRushPhase.BossActive);
        _encounterStartedAt = RunElapsedSeconds;
        BossWaveContext.ExternalLivingMinionCounter = () => BossRushEncounterRuntime.LivingMinionCount;
        BossRushHUD.ShowBossActive(_activeEncounter, CurrentEncounterIndex, BossRushDatabase.Encounters.Count);

        while (Phase == BossRushPhase.BossActive)
          yield return null;

        if (Phase == BossRushPhase.PlayerDefeated || Phase == BossRushPhase.ConfigError)
          yield break;

        if (Phase != BossRushPhase.BossDefeated)
          yield break;

        DefeatedBossCount++;
        SetPhase(BossRushPhase.RewardSelection);
        yield return RunRewardPhase(_activeEncounter);

        SetPhase(BossRushPhase.Recovery);
        BossRushCombatService.CleanupBattlefield(BossRushDatabase.Settings);
        var heal = _activeEncounter.heal_percent > 0f
          ? _activeEncounter.heal_percent
          : BossRushDatabase.Settings.base_heal_percent;
        BossRushCombatService.ApplyRecovery(heal, BossRushDatabase.Settings.minimum_heal_percent);
        yield return new WaitForSecondsRealtime(BossRushDatabase.Settings.post_fight_delay);

        if (_activeEncounter.IsFinalBoss)
        {
          SetPhase(BossRushPhase.FinalVictory);
          BossRushVictoryUI.ShowVictory(DefeatedBossCount, RunElapsedSeconds);
          yield break;
        }

        SetPhase(BossRushPhase.NextEncounter);
        CurrentEncounterIndex++;
        yield return null;
      }

      SetPhase(BossRushPhase.FinalVictory);
      BossRushVictoryUI.ShowVictory(DefeatedBossCount, RunElapsedSeconds);
    }

    IEnumerator RunOpeningRewards(int rewardCount)
    {
      CombatTimePause.PushPause();
      try
      {
        for (var i = 0; i < rewardCount; i++)
        {
          var offer = RunBuildState.GetPendingOffer();
          if (offer.choices == null || offer.choices.Length == 0)
            yield break;

          var picked = false;
          var pickIndex = -1;
          LevelUpCeremonyUI.Show(0, 1, offer, index =>
          {
            pickIndex = index;
            picked = true;
          });

          while (!picked)
            yield return null;

          if (pickIndex >= 0 && pickIndex < offer.choices.Length)
            RunBuildState.ApplyChoice(offer.choices[pickIndex]);
        }
      }
      finally
      {
        CombatTimePause.PopPause();
      }
    }

    IEnumerator RunRewardPhase(BossRushEncounterDef encounter)
    {
      var rewardCount = Mathf.Max(0, encounter.reward_count);
      if (rewardCount == 0)
        yield break;

      CombatTimePause.PushPause();
      try
      {
        for (var i = 0; i < rewardCount; i++)
        {
          var offer = RunBuildState.GetPendingOffer();
          if (offer.choices == null || offer.choices.Length == 0)
          {
            Debug.LogWarning("[BossRushDirector] Empty reward offer; skipping remaining rewards.");
            yield break;
          }

          var picked = false;
          var pickIndex = -1;
          LevelUpCeremonyUI.Show(
            DefeatedBossCount,
            DefeatedBossCount + 1,
            offer,
            index =>
            {
              pickIndex = index;
              picked = true;
            });

          while (!picked)
            yield return null;

          if (pickIndex >= 0 && pickIndex < offer.choices.Length)
            RunBuildState.ApplyChoice(offer.choices[pickIndex]);
        }
      }
      finally
      {
        CombatTimePause.PopPause();
      }
    }

    bool TrySpawnCurrentBoss()
    {
      if (_spawner == null || _activeEncounter == null)
        return false;

      var pos = BossRushCombatService.PickSpawnPosition(0f);
      _activeBoss = BossRushCombatService.SpawnBoss(_spawner, _activeEncounter, CurrentEncounterIndex, pos);
      return _activeBoss != null;
    }

    void OnBossKilled(BossKilledEvent evt)
    {
      if (Phase != BossRushPhase.BossActive || _activeEncounter == null)
        return;

      if (evt.Boss == null || evt.Boss != _activeBoss)
        return;

      if (!string.Equals(evt.BossId, _activeEncounter.boss_id, StringComparison.Ordinal))
        return;

      var core = _activeBoss.GetComponent<Shared.Enemy.AI.BossCore>();
      if (core != null)
        core.enabled = false;

      SetPhase(BossRushPhase.BossDefeated);
      BossRushHUD.ShowBossDefeated(_activeEncounter.display_name);
      _activeBoss = null;
    }

    void OnPlayerDeath(PlayerDeathEvent evt)
    {
      if (Phase is BossRushPhase.PlayerDefeated or BossRushPhase.FinalVictory or BossRushPhase.ConfigError)
        return;

      StopAllCoroutines();
      CombatTimePause.ForceResume();
      BossRushCombatService.CleanupBattlefield(BossRushDatabase.Settings);
      if (_activeBoss != null)
      {
        BossRushCombatService.DestroyActiveBoss(_activeBoss);
        _activeBoss = null;
      }

      SetPhase(BossRushPhase.PlayerDefeated);
      BossRushFailureUI.Show(DefeatedBossCount, CurrentEncounterIndex, RunElapsedSeconds);
    }

    void SetPhase(BossRushPhase phase)
    {
      Phase = phase;
      PhaseChanged?.Invoke(phase, CurrentEncounterIndex);
    }

#if UNITY_EDITOR
    public static void EditorDestroyInstance()
    {
      if (Instance == null)
        return;

      var go = Instance.gameObject;
      Instance = null;
      if (go != null)
        DestroyImmediate(go);
    }

    public void EditorForceDefeatBoss()
    {
      if (_activeBoss == null)
        return;

      var health = _activeBoss.GetComponent<Shared.Combat.Health.Health>();
      if (health != null && !health.IsDead)
        health.TakeDamage(health.MaxHp * 10f);
    }

    public void EditorAdvanceEncounter() => CurrentEncounterIndex++;

    public BossRushPhase EditorGetPhase() => Phase;

    public void EditorBindActiveBoss(GameObject boss, BossRushEncounterDef encounter)
    {
      _activeBoss = boss;
      _activeEncounter = encounter;
    }

    public void EditorPrepareForTests()
    {
      StopAllCoroutines();
      BossRushDatabase.EnsureLoaded();
      CurrentEncounterIndex = 1;
      DefeatedBossCount = 0;
      RunElapsedSeconds = 0f;
      _activeBoss = null;
      _activeEncounter = null;
      SubscribeEvents();
      SetPhase(BossRushPhase.NotStarted);
    }

    public void EditorForcePhase(BossRushPhase phase) => SetPhase(phase);

    public void EditorOpenRewardPhase()
    {
      if (_activeEncounter == null)
        return;

      StopAllCoroutines();
      SetPhase(BossRushPhase.RewardSelection);
      _flowRoutine = StartCoroutine(RunRewardPhase(_activeEncounter));
    }
#endif
  }
}
