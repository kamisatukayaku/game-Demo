using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
#endif
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>B2: Every 5 waves — 3-pick Corruption UI (strong buff + visible debuff).</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaCorruptionPickUI : MonoBehaviour
  {
    const int SortOrder = 870;

    static ArenaCorruptionPickUI s_instance;

    CanvasGroup _group;
    RectTransform _panel;
    readonly Button[] _buttons = new Button[3];
    readonly Text[] _labels = new Text[3];
    CorruptionDatabase.CorruptionDef[] _offer;
    Text _titleLabel;
    string _timelineTag = "Corruption";
    bool _showing;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_ArenaCorruptionPickUI");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<ArenaCorruptionPickUI>();
    }

    public static bool IsShowing => s_instance != null && s_instance._showing;

    public static void ShowOffer(string title = null, string timelineTag = "Corruption")
    {
      EnsureExists();
      s_instance.ShowInternal(title, timelineTag);
    }

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
    public static bool ValidationAutoPickFirst()
    {
      if (s_instance == null || !s_instance._showing || s_instance._offer == null)
        return false;

      for (var i = 0; i < s_instance._offer.Length; i++)
      {
        if (s_instance._offer[i] == null)
          continue;
        s_instance.Pick(i);
        return true;
      }

      s_instance.Close();
      return false;
    }
#endif

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      BuildUI();
      gameObject.SetActive(false);
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildUI()
    {
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = SortOrder;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      var overlay = CreateImage(transform, "Overlay", new Color(0.08f, 0.02f, 0.04f, 0.82f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      _panel = CreateImage(overlay.transform, "Panel", new Color(0.14f, 0.06f, 0.08f, 0.96f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 420f)).rectTransform;

      _titleLabel = CreateLabel(_panel, "Title", "腐化 — 选择一项强化与代价", 28, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(640f, 40f));

      for (var i = 0; i < 3; i++)
      {
        var x = -220f + i * 220f;
        var btnRt = CreateImage(_panel, $"CorruptionBtn_{i}", new Color(0.22f, 0.1f, 0.12f, 0.95f),
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(200f, 260f)).rectTransform;
        var btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnRt.GetComponent<Image>();
        var captured = i;
        btn.onClick.AddListener(() => Pick(captured));
        _buttons[i] = btn;
        _labels[i] = CreateLabel(btnRt, "Label", "", 15, FontStyle.Normal,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 220f));
        _labels[i].alignment = TextAnchor.UpperCenter;
      }

      _group = overlay.gameObject.AddComponent<CanvasGroup>();
    }

    void ShowInternal(string title, string timelineTag)
    {
      if (_showing)
        return;

      _timelineTag = string.IsNullOrEmpty(timelineTag) ? "Corruption" : timelineTag;
      if (_titleLabel != null)
        _titleLabel.text = string.IsNullOrEmpty(title) ? "腐化 — 选择一项强化与代价" : title;

      _offer = CorruptionRuntime.RollOffer(3);
      for (var i = 0; i < 3; i++)
      {
        var corruption = _offer != null && i < _offer.Length ? _offer[i] : null;
        var visible = corruption != null;
        _buttons[i].gameObject.SetActive(visible);
        if (!visible)
          continue;
        _labels[i].text = $"<b>{corruption.display_name}</b>\n\n{corruption.description}";
      }

      _showing = true;
      Time.timeScale = 0f;
      gameObject.SetActive(true);
      _group.alpha = 1f;
      _group.interactable = true;
      _group.blocksRaycasts = true;
    }

    void Pick(int index)
    {
      if (_offer == null || index < 0 || index >= _offer.Length)
        return;

      CorruptionRuntime.ApplyCorruption(_offer[index]);
      RunTimelineRecorder.Record(_timelineTag, _offer[index].display_name);
      Close();
    }

    void Close()
    {
      _showing = false;
      RestoreTimeScaleAfterBlockingUi();
      gameObject.SetActive(false);
    }

    static void RestoreTimeScaleAfterBlockingUi()
    {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      if (RuntimeValidationSettings.ActiveTimeScale > RuntimeValidationSettings.NormalTimeScale)
      {
        Time.timeScale = RuntimeValidationSettings.ActiveTimeScale;
        return;
      }
#endif
      Time.timeScale = 1f;
    }

    static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = pos;
      if (anchorMin == anchorMax)
        rt.sizeDelta = size;
      else
      {
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
      }

      var img = go.AddComponent<Image>();
      img.color = color;
      return img;
    }

    static Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 rectSize)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = pos;
      rt.sizeDelta = rectSize;
      var label = go.AddComponent<Text>();
      label.text = text;
      label.fontSize = size;
      label.fontStyle = style;
      label.font = UiFontHelper.GetFont();
      label.color = Color.white;
      label.alignment = TextAnchor.MiddleCenter;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      return label;
    }
  }
}
