using System.Collections;
using System.Collections.Generic;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
#endif
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Modes.Roguelike.Tutorial;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>升级时弹出三选一（混合池随机）；选牌期间战斗时停。</summary>
  public class LevelUpController : MonoBehaviour
  {
    static LevelUpController s_instance;
    static AudioClip s_confirmClip;

    readonly Queue<int> _queuedTargetLevels = new();

    LevelUpChoiceDatabase.LevelUpOffer _pending;
    bool _waiting;
    int _fromLevel;
    int _toLevel;

    public static bool IsWaiting => s_instance != null && s_instance._waiting;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_LevelUpController");
      go.AddComponent<LevelUpController>();
    }

    public static void ResetForNewRun()
    {
      if (s_instance == null)
        return;

      if (s_instance._waiting)
        s_instance.EndWaiting();

      s_instance._queuedTargetLevels.Clear();
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
    }

    void OnEnable()
    {
      ExperienceSystem.LevelUp += OnLevelUp;
    }

    void OnDisable()
    {
      ExperienceSystem.LevelUp -= OnLevelUp;
      if (_waiting)
        EndWaiting();
      _queuedTargetLevels.Clear();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void OnLevelUp(int fromLevel, int toLevel)
    {
      for (var lv = fromLevel + 1; lv <= toLevel; lv++)
        _queuedTargetLevels.Enqueue(lv);

      TryShowNextLevelUp();
    }

    void TryShowNextLevelUp()
    {
      if (_waiting || _queuedTargetLevels.Count == 0)
        return;

      LevelUpChoiceDatabase.EnsureLoaded();
      _toLevel = _queuedTargetLevels.Dequeue();
      _fromLevel = _toLevel - 1;
      _pending = RunBuildState.GetPendingOffer();
      _waiting = _pending.HasAny;

      if (_waiting)
        RuntimeValidationTelemetry.RecordLevelUpPause();

      if (!_waiting)
      {
        Debug.Log($"[LevelUp] Lv.{_toLevel} — no eligible upgrades in pool.");
        TryShowNextLevelUp();
        return;
      }

      CombatTimePause.PushPause();
      GameEventBus.Publish(new TutorialUiBlockingEvent(true, "level_up"));
      LevelUpCeremonyUI.Show(_fromLevel, _toLevel, _pending, Pick);
    }

    void Update()
    {
      if (!_waiting)
        return;

      if (!LevelUpCeremonyUI.IsAcceptingInput)
        return;

      if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        LevelUpCeremonyUI.Select(0);
      else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        LevelUpCeremonyUI.Select(1);
      else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        LevelUpCeremonyUI.Select(2);
    }

    void Pick(int index)
    {
      if (!_waiting || _pending.choices == null || index < 0 || index >= _pending.choices.Length)
        return;

      var choice = _pending.choices[index];
      if (choice == null)
        return;

      StartCoroutine(PickRoutine(choice));
    }

    IEnumerator PickRoutine(LevelUpChoiceDatabase.UpgradeDef choice)
    {
      LevelUpCeremonyUI.LockInput();
      PlayConfirmSfx();

      yield return new WaitForSecondsRealtime(0.15f);

      var applied = RunBuildState.ApplyChoice(choice);
      if (applied)
      {
        if (UpgradeOfferGroupPolicy.IsPlayerUpgrade(choice))
          UpgradeOfferPityTracker.OnPlayerUpgradePicked();

        var player = GameObject.FindWithTag("Player");
        var position = player != null ? player.transform.position : Vector3.zero;
        GameEventBus.Publish(new UpgradeAppliedEvent(player, position, choice.id));
      }

      Debug.Log($"[LevelUp] Lv.{_toLevel} chose [{LevelUpChoiceDatabase.GetRouteDisplayName(LevelUpChoiceDatabase.ResolveRoute(choice))}] {choice.display_name}");
      EndWaiting();
      TryShowNextLevelUp();
    }

    static void PlayConfirmSfx()
    {
      if (s_confirmClip == null)
      {
        const int sampleRate = 44100;
        var count = Mathf.CeilToInt(sampleRate * 0.08f);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
          var t = i / (float)sampleRate;
          samples[i] = Mathf.Sin(t * 660f * Mathf.PI * 2f) * Mathf.Exp(-t * 30f) * 0.4f;
        }
        s_confirmClip = AudioClip.Create("LevelUpConfirm", count, 1, sampleRate, false);
        s_confirmClip.SetData(samples, 0);
      }

      if (Camera.main != null)
      {
        var src = Camera.main.GetComponent<AudioSource>();
        if (src == null)
          src = Camera.main.gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(s_confirmClip, 0.55f);
      }
    }

    void EndWaiting()
    {
      _waiting = false;
      GameEventBus.Publish(new TutorialUiBlockingEvent(false, "level_up"));
      CombatTimePause.PopPause();
    }

#if DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS
    static float s_validationWaitingSince = -1f;

    /// <summary>Validation-only: pick first legal upgrade without UI ceremony delay.</summary>
    public static bool ValidationAutoPickFirst()
    {
      if (s_instance == null || !s_instance._waiting || s_instance._pending.choices == null)
      {
        s_validationWaitingSince = -1f;
        return false;
      }

      if (s_validationWaitingSince < 0f)
        s_validationWaitingSince = Time.unscaledTime;
      else if (Time.unscaledTime - s_validationWaitingSince > 5f)
      {
        LevelUpCeremonyUI.ValidationDismiss();
        s_instance.EndWaiting();
        s_instance.TryShowNextLevelUp();
        s_validationWaitingSince = -1f;
        return false;
      }

      LevelUpChoiceDatabase.UpgradeDef choice = null;
      foreach (var candidate in s_instance._pending.choices)
      {
        if (candidate == null)
          continue;
        if (UpgradeEligibilityRules.IsBlockedByPickHistory(candidate, RunBuildState.PickStacks))
          continue;
        choice = candidate;
        break;
      }

      choice = PreferUnifiedValidationChoice(choice, s_instance._pending.choices) ?? choice;

      if (choice == null)
      {
        LevelUpCeremonyUI.ValidationDismiss();
        s_instance.EndWaiting();
        s_instance.TryShowNextLevelUp();
        s_validationWaitingSince = -1f;
        return false;
      }

      LevelUpCeremonyUI.ValidationDismiss();
      var applied = RunBuildState.ApplyChoice(choice);
      if (applied && UpgradeOfferGroupPolicy.IsPlayerUpgrade(choice))
        UpgradeOfferPityTracker.OnPlayerUpgradePicked();

      s_instance.EndWaiting();
      s_instance.TryShowNextLevelUp();
      RuntimeValidationTelemetry.RecordLevelUpResume();
      s_validationWaitingSince = -1f;
      return applied;
    }

    static LevelUpChoiceDatabase.UpgradeDef PreferUnifiedValidationChoice(
      LevelUpChoiceDatabase.UpgradeDef current,
      LevelUpChoiceDatabase.UpgradeDef[] choices)
    {
      if (!ArenaBuildBootstrap.IsUnifiedBuild || choices == null || choices.Length == 0)
        return null;

      string missingTag = null;
      if (!RunBuildState.HasTag("projectile"))
        missingTag = "projectile";
      else if (!RunBuildState.HasTag("orbit"))
        missingTag = "orbit";

      if (string.IsNullOrEmpty(missingTag))
        return null;

      foreach (var candidate in choices)
      {
        if (candidate == null || candidate.id == current?.id)
          continue;
        if (UpgradeEligibilityRules.IsBlockedByPickHistory(candidate, RunBuildState.PickStacks))
          continue;
        if (!UpgradeGrantsTag(candidate, missingTag))
          continue;
        return candidate;
      }

      return null;
    }

    static bool UpgradeGrantsTag(LevelUpChoiceDatabase.UpgradeDef def, string tag)
    {
      if (def?.tags == null || string.IsNullOrEmpty(tag))
        return false;

      foreach (var candidateTag in def.tags)
      {
        if (candidateTag == tag)
          return true;
      }

      return false;
    }
#endif
  }
}
