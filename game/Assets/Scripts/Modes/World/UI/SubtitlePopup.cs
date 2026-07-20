using System.Collections;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 字幕弹出提示 — 监听 WorldEventBus.SubtitleShown 显示临时文字。
  /// 例如 "一个新的事件出现了！"、"一家新的清剿商店出现了！"
  /// </summary>
  public class SubtitlePopup : MonoBehaviour
  {
    static SubtitlePopup s_instance;

    Text _text;
    CanvasGroup _canvasGroup;
    float _timer;
    const float Duration = 3f;
    const float FadeStart = 2.5f;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_SubtitlePopup");
      DontDestroyOnLoad(go);
      go.AddComponent<SubtitlePopup>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;

      var canvasGo = new GameObject("Canvas", typeof(RectTransform));
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 300;
      canvasGo.AddComponent<GraphicRaycaster>();

      var go = new GameObject("Text", typeof(RectTransform));
      go.transform.SetParent(canvasGo.transform, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0.3f); rt.anchorMax = new Vector2(0.5f, 0.3f);
      rt.sizeDelta = new Vector2(600, 50);

      _text = go.AddComponent<Text>();
      _text.font = UiFontHelper.GetFont();
      _text.fontSize = 24;
      _text.fontStyle = FontStyle.Bold;
      _text.alignment = TextAnchor.MiddleCenter;
      _text.color = new Color(1f, 0.9f, 0.3f, 1f);

      _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
      _canvasGroup.alpha = 0f;

      WorldEventBus.SubtitleShown += Show;
    }

    void OnDestroy()
    {
      WorldEventBus.SubtitleShown -= Show;
      if (s_instance == this) s_instance = null;
    }

    void Show(string message)
    {
      _text.text = message;
      _canvasGroup.alpha = 1f;
      _timer = 0f;
      StopAllCoroutines();
      StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
      while (_timer < Duration)
      {
        _timer += Time.deltaTime;
        if (_timer > FadeStart)
          _canvasGroup.alpha = 1f - (_timer - FadeStart) / (Duration - FadeStart);
        yield return null;
      }
      _canvasGroup.alpha = 0f;
    }
  }
}
