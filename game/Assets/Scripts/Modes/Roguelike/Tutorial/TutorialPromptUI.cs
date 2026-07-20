using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Single-line Chinese tutorial prompt with fade in/out.</summary>
  public sealed class TutorialPromptUI : MonoBehaviour
  {
    const int SortOrder = 940;
    const int LevelUpHintSortOrder = 1245;

    static TutorialPromptUI s_instance;

    CanvasGroup _bottomGroup;
    Text _bottomBody;
    GameObject _bottomPanel;

    CanvasGroup _levelUpGroup;
    Text _levelUpBody;
    GameObject _levelUpPanel;

    Coroutine _bottomRoutine;
    Coroutine _levelUpRoutine;
    float _bottomPauseUntil;

    public static TutorialPromptUI Instance => s_instance;

#if UNITY_EDITOR
    public static void EditorDestroyForTests()
    {
      foreach (var ui in Object.FindObjectsOfType<TutorialPromptUI>(true))
      {
        if (ui != null)
          DestroyImmediate(ui.gameObject);
      }
      s_instance = null;
    }
#endif

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_TutorialPromptUI");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      else
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<TutorialPromptUI>();
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
      BuildBottomPanel();
      BuildLevelUpHintPanel();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildBottomPanel()
    {
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = SortOrder;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      _bottomPanel = CreatePanel(transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f),
        new Vector2(760f, 120f), new Color(0.03f, 0.08f, 0.12f, 0.88f), out _bottomGroup, out _bottomBody, 18);
      _bottomPanel.SetActive(false);
    }

    void BuildLevelUpHintPanel()
    {
      var go = new GameObject("LevelUpHintCanvas", typeof(RectTransform));
      go.transform.SetParent(transform, false);
      var canvas = go.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = LevelUpHintSortOrder;
      var scaler = go.AddComponent<CanvasScaler>();
      go.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, LevelUpHintSortOrder);

      _levelUpPanel = CreatePanel(go.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f),
        new Vector2(760f, 56f), new Color(0.03f, 0.08f, 0.12f, 0.72f), out _levelUpGroup, out _levelUpBody, 16);
      _levelUpPanel.SetActive(false);
    }

    static GameObject CreatePanel(
      Transform parent,
      Vector2 anchorMin,
      Vector2 anchorMax,
      Vector2 anchoredPos,
      Vector2 size,
      Color bgColor,
      out CanvasGroup group,
      out Text body,
      int fontSize)
    {
      var panel = new GameObject("Panel", typeof(RectTransform));
      panel.transform.SetParent(parent, false);
      var rt = panel.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0.5f, 0f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = size;

      var bg = panel.AddComponent<Image>();
      bg.color = bgColor;
      bg.raycastTarget = false;

      var textGo = new GameObject("Body", typeof(RectTransform));
      textGo.transform.SetParent(panel.transform, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = new Vector2(12f, 8f);
      textRt.offsetMax = new Vector2(-12f, -8f);

      body = textGo.AddComponent<Text>();
      body.alignment = TextAnchor.MiddleCenter;
      body.color = new Color(0.82f, 0.96f, 1f, 1f);
      body.raycastTarget = false;
      UiFontHelper.StyleText(body, fontSize);

      group = panel.AddComponent<CanvasGroup>();
      group.alpha = 0f;
      group.blocksRaycasts = false;
      group.interactable = false;
      return panel;
    }

    public void ShowBottom(string message, float duration, int priority)
    {
      if (string.IsNullOrEmpty(message))
        return;
      if (_bottomRoutine != null)
        StopCoroutine(_bottomRoutine);
      _bottomPauseUntil = 0f;
      _bottomRoutine = StartCoroutine(ShowBottomRoutine(message, duration));
    }

    public void ShowLevelUpHint(string message, float duration)
    {
      if (string.IsNullOrEmpty(message))
        return;
      if (_levelUpRoutine != null)
        StopCoroutine(_levelUpRoutine);
      _levelUpRoutine = StartCoroutine(ShowLevelUpRoutine(message, duration));
    }

    public void HideLevelUpHint()
    {
      if (_levelUpRoutine != null)
      {
        StopCoroutine(_levelUpRoutine);
        _levelUpRoutine = null;
      }
      if (_levelUpPanel != null)
        _levelUpPanel.SetActive(false);
      if (_levelUpGroup != null)
        _levelUpGroup.alpha = 0f;
    }

    public void PauseBottomTimer(float seconds)
    {
      if (seconds <= 0f)
        _bottomPauseUntil = 0f;
      else
        _bottomPauseUntil = Mathf.Max(_bottomPauseUntil, Time.unscaledTime + seconds);
    }

    public void HideBottomImmediate()
    {
      if (_bottomRoutine != null)
      {
        StopCoroutine(_bottomRoutine);
        _bottomRoutine = null;
      }
      if (_bottomPanel != null)
        _bottomPanel.SetActive(false);
      if (_bottomGroup != null)
        _bottomGroup.alpha = 0f;
    }

    public IEnumerator WaitUntilBottomHidden()
    {
      while (_bottomRoutine != null)
        yield return null;
    }

    public IEnumerator WaitUntilLevelUpHintHidden()
    {
      while (_levelUpRoutine != null)
        yield return null;
    }

    IEnumerator ShowBottomRoutine(string message, float duration)
    {
      yield return ShowRoutine(_bottomPanel, _bottomGroup, _bottomBody, message, duration, respectBottomPause: true);
      _bottomRoutine = null;
    }

    IEnumerator ShowLevelUpRoutine(string message, float duration)
    {
      yield return ShowRoutine(_levelUpPanel, _levelUpGroup, _levelUpBody, message, duration, respectBottomPause: false);
      _levelUpRoutine = null;
    }

    IEnumerator ShowRoutine(
      GameObject panel,
      CanvasGroup group,
      Text body,
      string message,
      float duration,
      bool respectBottomPause)
    {
      body.text = message;
      panel.SetActive(true);

      var elapsed = 0f;
      while (elapsed < 0.25f)
      {
        elapsed += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(elapsed / 0.25f);
        yield return null;
      }

      group.alpha = 1f;
      var hold = 0f;
      while (hold < duration)
      {
        if (respectBottomPause && Time.unscaledTime < _bottomPauseUntil)
        {
          yield return null;
          continue;
        }
        hold += Time.unscaledDeltaTime;
        yield return null;
      }

      elapsed = 0f;
      while (elapsed < 0.25f)
      {
        elapsed += Time.unscaledDeltaTime;
        group.alpha = 1f - Mathf.Clamp01(elapsed / 0.25f);
        yield return null;
      }

      group.alpha = 0f;
      panel.SetActive(false);
    }
  }
}
