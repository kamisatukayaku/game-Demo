using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Game.Shared.Core;

namespace Game.Shared.UI
{
  /// <summary>
  /// DontDestroyOnLoad 全屏暗幕，避免 StartGame → MainScene 同步切场景时的白屏闪一下。
  /// </summary>
  public sealed class GameSceneTransitionCurtain : MonoBehaviour
  {
    const float FadeInSeconds = 0.12f;
    const float FadeOutSeconds = 0.35f;

    static GameSceneTransitionCurtain s_instance;
    static bool s_loading;

    Canvas _canvas;
    Image _fade;
    Coroutine _loadRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneLoaded() => SceneManager.sceneLoaded += OnSceneLoaded;

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      if (scene.name == "MainScene")
        s_instance?.ForceHide();
    }

    public static void LoadScene(string sceneName, Action beforeLoad = null)
    {
      EnsureExists();
      if (s_loading)
        s_instance.ForceHide();

      s_instance.BeginLoad(sceneName, beforeLoad);
    }

    public static void DismissIfVisible() => s_instance?.ForceHide();

    static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var host = CombatRoot.Instance != null ? CombatRoot.Instance.gameObject : new GameObject("_GameSceneTransitionCurtain");
      if (CombatRoot.Instance == null)
        DontDestroyOnLoad(host);

      s_instance = host.GetComponent<GameSceneTransitionCurtain>();
      if (s_instance == null)
        s_instance = host.AddComponent<GameSceneTransitionCurtain>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(this);
        return;
      }

      s_instance = this;
      BuildUi();
      _canvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildUi()
    {
      var canvasGo = new GameObject("SceneTransitionCanvas", typeof(RectTransform));
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      _canvas.sortingOrder = 5000;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 5000);
      canvasGo.AddComponent<GraphicRaycaster>();

      var fadeGo = new GameObject("Fade", typeof(RectTransform));
      fadeGo.transform.SetParent(canvasGo.transform, false);
      var rt = fadeGo.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      _fade = fadeGo.AddComponent<Image>();
      _fade.sprite = UiSolidSprite.White;
      _fade.raycastTarget = true;
      _fade.color = new Color(0.01f, 0.03f, 0.05f, 0f);
    }

    void BeginLoad(string sceneName, Action beforeLoad)
    {
      if (_loadRoutine != null)
        StopCoroutine(_loadRoutine);

      _loadRoutine = StartCoroutine(LoadRoutine(sceneName, beforeLoad));
    }

    IEnumerator LoadRoutine(string sceneName, Action beforeLoad)
    {
      s_loading = true;
      _canvas.gameObject.SetActive(true);
      _fade.raycastTarget = true;

      var fadeIn = 0f;
      while (fadeIn < FadeInSeconds)
      {
        fadeIn += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(fadeIn / FadeInSeconds);
        _fade.color = new Color(0.01f, 0.03f, 0.05f, Mathf.Lerp(0f, 1f, t));
        yield return null;
      }

      _fade.color = new Color(0.01f, 0.03f, 0.05f, 1f);
      beforeLoad?.Invoke();

      var op = SceneManager.LoadSceneAsync(sceneName);
      if (op == null)
      {
        SceneManager.LoadScene(sceneName);
      }
      else
      {
        while (!op.isDone)
          yield return null;
      }

      yield return null;
      yield return null;

      var fadeOut = 0f;
      while (fadeOut < FadeOutSeconds)
      {
        fadeOut += Time.unscaledDeltaTime;
        var t = 1f - Mathf.Clamp01(fadeOut / FadeOutSeconds);
        _fade.color = new Color(0.01f, 0.03f, 0.05f, t);
        yield return null;
      }

      _fade.color = new Color(0.01f, 0.03f, 0.05f, 0f);
      _fade.raycastTarget = false;
      _canvas.gameObject.SetActive(false);
      s_loading = false;
      _loadRoutine = null;
    }

    public void ForceHide()
    {
      if (_loadRoutine != null)
      {
        StopCoroutine(_loadRoutine);
        _loadRoutine = null;
      }

      s_loading = false;
      if (_fade != null)
        _fade.color = new Color(0.01f, 0.03f, 0.05f, 0f);
      if (_canvas != null)
        _canvas.gameObject.SetActive(false);
    }
  }
}
