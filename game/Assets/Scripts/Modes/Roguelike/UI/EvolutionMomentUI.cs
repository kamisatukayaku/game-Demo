using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Presentation.Audio;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>S7: 外置武器进化 Tier 3/5 全屏 EVOLUTION 微演出。</summary>
  [DisallowMultipleComponent]
  public sealed class EvolutionMomentUI : MonoBehaviour
  {
    const int SortOrder = 335;

    static EvolutionMomentUI s_instance;
    static AudioClip s_evolutionClip;

    CanvasGroup _group;
    Image _flash;
    Text _label;
    RectTransform _labelRt;
    AudioSource _audio;
    Coroutine _routine;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_EvolutionMomentUI");
      go.AddComponent<EvolutionMomentUI>();
    }

    public static void PlayIfCapstone(string upgradeId)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      var choice = LevelUpChoiceDatabase.FindById(upgradeId);
      if (choice == null || (choice.tier != 3 && choice.tier != 5))
        return;

      EnsureExists();
      s_instance.Play(choice.tier);
      if (choice.tier == 3)
        RunTimelineRecorder.Record("首进化", choice.display_name);
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
      _audio = gameObject.AddComponent<AudioSource>();
      _audio.playOnAwake = false;
      _audio.spatialBlend = 0f;
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Play(int tier)
    {
      if (_routine != null)
        StopCoroutine(_routine);

      _routine = StartCoroutine(PlayRoutine(tier));
    }

    IEnumerator PlayRoutine(int tier)
    {
      ArenaBgmController.NotifyEvolutionMoment(1.15f);

      var accent = tier >= 5
        ? new Color(1f, 0.82f, 0.22f, 1f)
        : new Color(0.45f, 0.88f, 1f, 1f);
      _label.text = tier >= 5 ? "终局进化" : "进化";
      _label.color = accent;
      _group.alpha = 0f;
      _flash.color = WithAlpha(accent, 0f);
      _labelRt.localScale = Vector3.one * 0.55f;

      _audio.pitch = tier >= 5 ? 1.08f : 0.96f;
      _audio.PlayOneShot(EnsureClip(), 0.78f);

      const float duration = 1.05f;
      var elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);

        if (elapsed < 0.12f)
        {
          var flashT = elapsed / 0.12f;
          _flash.color = WithAlpha(accent, Mathf.Lerp(0f, tier >= 5 ? 0.72f : 0.58f, flashT));
        }
        else if (elapsed < 0.2f)
        {
          _flash.color = WithAlpha(accent, tier >= 5 ? 0.72f : 0.58f);
        }
        else
        {
          var flashT = (elapsed - 0.2f) / 0.85f;
          _flash.color = WithAlpha(accent, Mathf.Lerp(tier >= 5 ? 0.72f : 0.58f, 0f, flashT));
        }

        if (elapsed < 0.22f)
        {
          var scaleT = elapsed / 0.22f;
          _labelRt.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.12f, Mathf.SmoothStep(0f, 1f, scaleT));
          _group.alpha = scaleT;
        }
        else if (elapsed < 0.75f)
        {
          _labelRt.localScale = Vector3.one;
          _group.alpha = 1f;
        }
        else
        {
          var fadeT = (elapsed - 0.75f) / 0.3f;
          _group.alpha = 1f - fadeT;
        }

        yield return null;
      }

      _group.alpha = 0f;
      _flash.color = WithAlpha(accent, 0f);
      _labelRt.localScale = Vector3.one;
      _routine = null;
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("EvolutionMomentCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      canvasGo.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      var flashRt = CreateStretch(canvasGo.transform, "Flash");
      _flash = flashRt.gameObject.AddComponent<Image>();
      _flash.sprite = UiSolidSprite.White;
      _flash.raycastTarget = false;
      _flash.color = new Color(1f, 1f, 1f, 0f);

      var labelRt = CreateStretch(canvasGo.transform, "LabelRoot");
      _labelRt = labelRt;
      _group = labelRt.gameObject.AddComponent<CanvasGroup>();
      _group.alpha = 0f;
      _label = labelRt.gameObject.AddComponent<Text>();
      _label.alignment = TextAnchor.MiddleCenter;
      _label.fontStyle = FontStyle.Bold;
      _label.raycastTarget = false;
      UiFontHelper.StyleText(_label, 64, FontStyle.Bold);
    }

    static RectTransform CreateStretch(Transform parent, string name)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      return rt;
    }

    static Color WithAlpha(Color c, float a)
    {
      c.a = a;
      return c;
    }

    static AudioClip EnsureClip()
    {
      if (s_evolutionClip != null)
        return s_evolutionClip;

      const int sampleRate = 44100;
      const float duration = 0.35f;
      var count = Mathf.CeilToInt(sampleRate * duration);
      var samples = new float[count];
      for (var i = 0; i < count; i++)
      {
        var t = i / (float)sampleRate;
        var env = Mathf.Exp(-t * 6f);
        samples[i] = (Mathf.Sin(t * 880f * Mathf.PI * 2f) * 0.45f + Mathf.Sin(t * 1320f * Mathf.PI * 2f) * 0.25f) * env;
      }

      s_evolutionClip = AudioClip.Create("EvolutionSfx", count, 1, sampleRate, false);
      s_evolutionClip.SetData(samples, 0);
      return s_evolutionClip;
    }
  }
}
