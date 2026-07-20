using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Schedules tutorial prompts, tracks completion, and resolves priority conflicts.</summary>
  public sealed class RoguelikeTutorialDirector : MonoBehaviour
  {
    public enum PromptPriority
    {
      Hazard = 0,
      GroundZoneFirst = 1,
      BasicOperation = 2,
      BuildUpgrade = 3,
      Status = 4
    }

    struct PendingPrompt
    {
      public string StepId;
      public string Message;
      public PromptPriority Priority;
      public float Duration;
      public bool UseLevelUpAnchor;
    }

    static RoguelikeTutorialDirector s_instance;

    readonly Queue<PendingPrompt> _queue = new();
    readonly HashSet<string> _queuedStepIds = new();
    readonly List<string> _queueDebug = new();

    bool _uiBlocked;
    bool _showing;
    Coroutine _showRoutine;
    PendingPrompt _active;
    float _arenaEnteredAt = -1f;
    bool _moveComplete;
    bool _dashStepEligible;

    public static RoguelikeTutorialDirector Instance => s_instance;
    public static bool SandboxDebugEnabled { get; private set; }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_RoguelikeTutorialDirector");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      else
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<RoguelikeTutorialDirector>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
      TutorialStepDatabase.EnsureLoaded();
      GroundZoneDefinitionDatabase.EnsureLoaded();
      TutorialPromptUI.EnsureExists();
      DetectSandbox();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    static void DetectSandbox()
    {
      var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
      SandboxDebugEnabled = scene.Contains("Sandbox") || scene.Contains("ArchetypeSandbox");
      if (SandboxDebugEnabled)
        RoguelikeTutorialState.SandboxBypassPersistence = true;
    }

    public static void ResetForNewRun()
    {
      if (s_instance != null)
        s_instance.ResetRuntimeState();
    }

#if UNITY_EDITOR
    public static void EditorDestroyForTests()
    {
      foreach (var director in Object.FindObjectsOfType<RoguelikeTutorialDirector>(true))
      {
        if (director != null)
          DestroyImmediate(director.gameObject);
      }
      s_instance = null;
    }
#endif

    public static void ResetAllTutorialState()
    {
      TutorialStepDatabase.EnsureLoaded();
      GroundZoneDefinitionDatabase.EnsureLoaded();
      RoguelikeTutorialState.ResetAllKnown(TutorialStepDatabase.AllStepIds, GroundZoneDefinitionDatabase.AllZoneIds);
      if (s_instance != null)
        s_instance.ResetRuntimeState();
    }

    void ResetRuntimeState()
    {
      _queue.Clear();
      _queuedStepIds.Clear();
      _queueDebug.Clear();
      _showing = false;
      if (_showRoutine != null)
      {
        StopCoroutine(_showRoutine);
        _showRoutine = null;
      }
      _uiBlocked = false;
      _moveComplete = false;
      _dashStepEligible = false;
      _arenaEnteredAt = -1f;
      TutorialPromptUI.Instance?.HideBottomImmediate();
      TutorialPromptUI.Instance?.HideLevelUpHint();
    }

    public void OnArenaEntered()
    {
      _arenaEnteredAt = Time.unscaledTime;
      TryOfferStep("move", PromptPriority.BasicOperation, 3.5f);
    }

    public void OnPlayerMoved()
    {
      if (_moveComplete)
        return;
      _moveComplete = true;
      CompleteStep("move");
      _dashStepEligible = true;
      TryOfferStep("dash", PromptPriority.BasicOperation, 3.5f);
    }

    public void OnPlayerDashed()
    {
      if (!_dashStepEligible)
        return;
      CompleteStep("dash");
    }

    public void OnAutoAttackHit() => CompleteStep("auto_attack");

    public void OnXpPickupCollected() => CompleteStep("xp_pickup");

    public void OnLevelUpUiOpened()
    {
      TryOfferStep("level_up", PromptPriority.BuildUpgrade, 4f, true);
    }

    public void OnLevelUpUiClosed()
    {
      CompleteStep("level_up");
      TutorialPromptUI.Instance?.HideLevelUpHint();
    }

    public void OnDetachedWeaponAcquired()
    {
      TryOfferStep("detached_weapon", PromptPriority.BasicOperation, 3.5f);
    }

    public void OnDetachedWeaponImpact() => CompleteStep("detached_weapon");

    public void OnEnemyInAttackRange() =>
      TryOfferStep("auto_attack", PromptPriority.BasicOperation, 3.5f);

    public void OnFirstXpOrbSpawned() =>
      TryOfferStep("xp_pickup", PromptPriority.BasicOperation, 3.5f);

    public void NotifyEnemySpawned(string enemyId)
    {
      if (string.IsNullOrEmpty(enemyId))
        return;

      if (enemyId.Contains("support"))
        TryOfferStep("enemy_support", PromptPriority.Status, 2.8f);
      else if (enemyId.Contains("disruptor"))
        TryOfferStep("enemy_disruptor", PromptPriority.Status, 2.8f);
    }

    public void OnUiBlockingChanged(bool blocking)
    {
      _uiBlocked = blocking;
      if (blocking)
      {
        TutorialPromptUI.Instance?.PauseBottomTimer(999f);
        return;
      }

      TutorialPromptUI.Instance?.PauseBottomTimer(0f);
      TryShowNext();
    }

    public void OnGroundZoneSpawned(string zoneId, Vector2 center, float radius)
    {
      var def = GroundZoneDefinitionDatabase.Get(zoneId);
      if (!def.showOnFirstEncounter)
        return;
      if (!RoguelikeTutorialState.ShouldSkipPersistence && RoguelikeTutorialState.IsZoneIntroComplete(zoneId))
        return;

      GroundZoneInfoPresenter.Instance?.ShowFirstEncounter(zoneId, center, radius);
      if (!RoguelikeTutorialState.ShouldSkipPersistence)
        RoguelikeTutorialState.MarkZoneIntroComplete(zoneId);
    }

    public void OnGroundZoneProximity(string zoneId)
    {
      var def = GroundZoneDefinitionDatabase.Get(zoneId);
      if (RoguelikeTutorialState.ShouldSkipPersistence)
        return;
      if (RoguelikeTutorialState.IsZoneProximityComplete(zoneId))
        return;

      var priority = GroundZoneDefinitionDatabase.GetPromptPriority(def) == 0
        ? PromptPriority.Hazard
        : PromptPriority.GroundZoneFirst;
      Enqueue(new PendingPrompt
      {
        StepId = "zone_prox:" + zoneId,
        Message = def.proximityHint,
        Priority = priority,
        Duration = 2.5f
      });
      RoguelikeTutorialState.MarkZoneProximityComplete(zoneId);
    }

    public void OnGroundZoneEntered(string zoneId)
    {
      var def = GroundZoneDefinitionDatabase.Get(zoneId);
      var priority = GroundZoneDefinitionDatabase.GetPromptPriority(def) == 0
        ? PromptPriority.Hazard
        : PromptPriority.Status;

      InterruptBottomPrompt();

      Enqueue(new PendingPrompt
      {
        StepId = "zone_enter:" + zoneId + ":" + Time.frameCount,
        Message = def.enteredHint,
        Priority = priority,
        Duration = 2f
      });
    }

    void InterruptBottomPrompt()
    {
      _showing = false;
      if (_showRoutine != null)
      {
        StopCoroutine(_showRoutine);
        _showRoutine = null;
      }
      TutorialPromptUI.Instance?.HideBottomImmediate();
    }

    public void CompleteStep(string stepId)
    {
      if (string.IsNullOrEmpty(stepId))
        return;

      if (!RoguelikeTutorialState.ShouldSkipPersistence && RoguelikeTutorialState.IsStepComplete(stepId))
        return;

      if (!RoguelikeTutorialState.ShouldSkipPersistence)
        RoguelikeTutorialState.MarkStepComplete(stepId);

      _queuedStepIds.Remove(stepId);
      if (_active.StepId == stepId)
      {
        _showing = false;
        if (_showRoutine != null)
        {
          StopCoroutine(_showRoutine);
          _showRoutine = null;
        }
        TutorialPromptUI.Instance?.HideBottomImmediate();
        TutorialPromptUI.Instance?.HideLevelUpHint();
      }

      TryShowNext();
    }

    public void TryOfferStep(string stepId, PromptPriority priority, float duration, bool levelUpAnchor = false)
    {
      if (string.IsNullOrEmpty(stepId))
        return;
      if (!RoguelikeTutorialState.ShouldSkipPersistence && RoguelikeTutorialState.IsStepComplete(stepId))
        return;
      if (_queuedStepIds.Contains(stepId))
        return;

      var message = TutorialStepDatabase.GetMessage(stepId, string.Empty);
      if (string.IsNullOrEmpty(message))
        return;

      Enqueue(new PendingPrompt
      {
        StepId = stepId,
        Message = message,
        Priority = priority,
        Duration = duration,
        UseLevelUpAnchor = levelUpAnchor
      });
      _queuedStepIds.Add(stepId);
    }

    void Enqueue(PendingPrompt prompt)
    {
      if (string.IsNullOrEmpty(prompt.Message))
        return;

      _queue.Enqueue(prompt);
      _queueDebug.Add(prompt.StepId + "@" + prompt.Priority);
      TryShowNext();
    }

    void TryShowNext()
    {
      if (_showing || _uiBlocked || TutorialPromptUI.Instance == null)
        return;

      if (LevelUpController.IsWaiting)
        return;

      PendingPrompt? best = null;
      var temp = new List<PendingPrompt>();
      while (_queue.Count > 0)
        temp.Add(_queue.Dequeue());

      for (var i = 0; i < temp.Count; i++)
      {
        var item = temp[i];
        if (!best.HasValue || item.Priority < best.Value.Priority)
          best = item;
      }

      for (var i = 0; i < temp.Count; i++)
      {
        var item = temp[i];
        if (best.HasValue && item.StepId == best.Value.StepId)
          continue;
        _queue.Enqueue(item);
      }

      if (!best.HasValue)
        return;

      _active = best.Value;
      _showing = true;
      _showRoutine = StartCoroutine(ShowActiveRoutine(_active));
    }

    IEnumerator ShowActiveRoutine(PendingPrompt prompt)
    {
      if (TutorialPromptUI.Instance == null)
      {
        _showing = false;
        yield break;
      }

      if (prompt.UseLevelUpAnchor)
      {
        TutorialPromptUI.Instance.ShowLevelUpHint(prompt.Message, prompt.Duration);
        yield return TutorialPromptUI.Instance.WaitUntilLevelUpHintHidden();
      }
      else
      {
        TutorialPromptUI.Instance.ShowBottom(prompt.Message, prompt.Duration, (int)prompt.Priority);
        yield return TutorialPromptUI.Instance.WaitUntilBottomHidden();
      }

      _showing = false;
      _showRoutine = null;
      TryShowNext();
    }

    public void DebugTriggerStep(string stepId)
    {
      RoguelikeTutorialState.SandboxBypassPersistence = true;
      _queuedStepIds.Remove(stepId);
      TryOfferStep(stepId, PromptPriority.BasicOperation, 3.5f, stepId == "level_up");
    }

    public void DebugSimulateZone(string zoneId, Vector2 center, float radius)
    {
      GroundZoneInfoPresenter.EnsureExists();
      OnGroundZoneSpawned(zoneId, center, radius);
    }

    public string DebugQueueSnapshot()
    {
      var sb = new StringBuilder();
      sb.Append("active=").Append(_active.StepId).Append("; queue=");
      foreach (var entry in _queueDebug)
        sb.Append(entry).Append('|');
      return sb.ToString();
    }

    public string DebugCompletedSteps()
    {
      var sb = new StringBuilder();
      foreach (var id in TutorialStepDatabase.AllStepIds)
      {
        if (RoguelikeTutorialState.IsStepComplete(id))
          sb.Append(id).Append(',');
      }
      return sb.ToString();
    }
  }
}
