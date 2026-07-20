using System.Collections;
using UnityEngine.UI;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Modes.Roguelike.Progression;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>升级特效：全屏白?+ 居中 LEVEL UP 文字?/summary>
  public class LevelUpVfxController : MonoBehaviour
  {
    static LevelUpVfxController s_instance;

    Canvas _canvas;
    Image _flashImage;
    Text _levelUpText;
    RectTransform _textRt;
    Coroutine _playRoutine;
    EventListenerHandle _levelUpHandle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_LevelUpVfx");
      DontDestroyOnLoad(go);
      go.AddComponent<LevelUpVfxController>();
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
      BuildUI();
    }

    void OnEnable() => _levelUpHandle = GameEventBus.Subscribe<LevelUpEvent>(OnLevelUp);
    void OnDisable() => GameEventBus.Unsubscribe(_levelUpHandle);

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("LevelUpVfxCanvas");
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 1200);
      canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

      var flashGo = new GameObject("Flash", typeof(RectTransform));
      flashGo.transform.SetParent(canvasGo.transform, false);
      var flashRt = flashGo.GetComponent<RectTransform>();
      flashRt.anchorMin = Vector2.zero;
      flashRt.anchorMax = Vector2.one;
      flashRt.offsetMin = Vector2.zero;
      flashRt.offsetMax = Vector2.zero;
      _flashImage = flashGo.AddComponent<Image>();
      _flashImage.color = new Color(1f, 1f, 1f, 0f);
      _flashImage.raycastTarget = false;

      var textGo = new GameObject("LevelUpText", typeof(RectTransform));
      textGo.transform.SetParent(canvasGo.transform, false);
      _textRt = textGo.GetComponent<RectTransform>();
      _textRt.anchorMin = _textRt.anchorMax = new Vector2(0.5f, 0.5f);
      _textRt.sizeDelta = new Vector2(640f, 120f);
      _textRt.anchoredPosition = Vector2.zero;

      _levelUpText = textGo.AddComponent<Text>();
      _levelUpText.font = UiFontHelper.GetFont();
      _levelUpText.fontSize = 56;
      _levelUpText.fontStyle = FontStyle.Bold;
      _levelUpText.alignment = TextAnchor.MiddleCenter;
      _levelUpText.color = new Color(1f, 1f, 1f, 0f);
      _levelUpText.text = "LEVEL UP";
      _levelUpText.raycastTarget = false;

      var outline = textGo.AddComponent<Outline>();
      outline.effectColor = new Color(0.15f, 0.55f, 0.85f, 0.95f);
      outline.effectDistance = new Vector2(2f, -2f);
    }

    void OnLevelUp(LevelUpEvent evt)
    {
      if (_playRoutine != null)
        StopCoroutine(_playRoutine);

      _playRoutine = StartCoroutine(PlayRoutine(evt.OldLevel, evt.NewLevel));
    }

    IEnumerator PlayRoutine(int fromLevel, int toLevel)
    {
      _levelUpText.text = toLevel > fromLevel ? $"LEVEL UP\nLv.{toLevel}" : "LEVEL UP";
      _levelUpText.color = new Color(1f, 1f, 1f, 0f);
      _textRt.localScale = Vector3.one * 0.55f;
      _flashImage.color = new Color(1f, 1f, 1f, 0f);

      const float flashPeak = 0.92f;
      var elapsed = 0f;

      while (elapsed < 0.55f)
      {
        elapsed += Time.unscaledDeltaTime;

        if (elapsed < 0.1f)
        {
          var t = elapsed / 0.1f;
          _flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, flashPeak, t));
        }
        else if (elapsed < 0.16f)
        {
          _flashImage.color = new Color(1f, 1f, 1f, flashPeak);
        }
        else
        {
          var t = (elapsed - 0.16f) / 0.39f;
          _flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(flashPeak, 0f, t));
        }

        if (elapsed < 0.2f)
        {
          var t = elapsed / 0.2f;
          _textRt.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.1f, Mathf.SmoothStep(0f, 1f, t));
          _levelUpText.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t));
        }
        else if (elapsed < 0.45f)
        {
          _textRt.localScale = Vector3.one;
          _levelUpText.color = Color.white;
        }
        else
        {
          var t = (elapsed - 0.45f) / 0.1f;
          _levelUpText.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
        }

        yield return null;
      }

      _flashImage.color = new Color(1f, 1f, 1f, 0f);
      _levelUpText.color = new Color(1f, 1f, 1f, 0f);
      _playRoutine = null;
    }
  }
}
