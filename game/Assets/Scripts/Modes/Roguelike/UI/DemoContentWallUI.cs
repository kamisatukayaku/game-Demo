using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>C4: Grayed-out full-version content wall after Demo W15 victory.</summary>
  public sealed class DemoContentWallUI : MonoBehaviour
  {
    static DemoContentWallUI s_instance;

    CanvasGroup _group;
    RectTransform _panel;
    bool _showing;

    static readonly (string title, string note)[] LockedItems =
    {
      ("无尽模式 - 50波", "完整版"),
      ("多Boss轮换", "完整版"),
      ("Hard+规则包 x6", "完整版"),
      ("Build变体 x8", "完整版"),
      ("Arena Layout x12", "完整版"),
      ("Meta天赋树", "完整版"),
      ("联机 Co-op", "完整版"),
      ("Season 排行榜", "完整版")
    };

    public static void ShowAfterVictory(int waveReached)
    {
      if (waveReached < 15)
        return;
      EnsureExists().ShowInternal();
    }

    public static DemoContentWallUI EnsureExists()
    {
      if (s_instance != null)
        return s_instance;

      var go = new GameObject("_DemoContentWallUI");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<DemoContentWallUI>();
      s_instance.Build();
      return s_instance;
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Build()
    {
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 980;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 980);

      var root = new GameObject("Root", typeof(RectTransform));
      root.transform.SetParent(transform, false);
      var rootRt = root.GetComponent<RectTransform>();
      rootRt.anchorMin = Vector2.zero;
      rootRt.anchorMax = Vector2.one;
      rootRt.offsetMin = Vector2.zero;
      rootRt.offsetMax = Vector2.zero;

      CreateImage(rootRt, "Overlay", new Color(0.01f, 0.02f, 0.04f, 0.75f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      _panel = CreateImage(rootRt, "WallPanel", new Color(0.05f, 0.08f, 0.12f, 0.96f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 520f)).rectTransform;

      var title = CreateText(_panel, "Title", "Demo之外 - 完整版内容", 28, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(540f, 40f));
      title.color = new Color(0.88f, 0.92f, 0.96f, 1f);

      var subtitle = CreateText(_panel, "Subtitle", "你已通关 Demo W15，以下内容为完整版解锁项预览。", 15, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(540f, 28f));
      subtitle.color = new Color(0.55f, 0.68f, 0.78f, 1f);

      var y = -110f;
      foreach (var item in LockedItems)
      {
        var row = CreateImage(_panel, $"Row_{item.title}", new Color(0.12f, 0.14f, 0.18f, 0.55f),
          new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(540f, 36f));
        var label = CreateText(row.transform, "Label", item.title, 16, FontStyle.Normal,
          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(360f, 30f));
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.45f, 0.48f, 0.52f, 0.85f);

        var tag = CreateText(row.transform, "Tag", item.note, 14, FontStyle.Bold,
          new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(90f, 26f));
        tag.alignment = TextAnchor.MiddleRight;
        tag.color = new Color(0.38f, 0.42f, 0.48f, 0.75f);

        y -= 44f;
      }

      CreateButton(_panel, "CloseButton", "缁х画", new Vector2(0f, 36f), Hide);

      _group = root.AddComponent<CanvasGroup>();
      _group.alpha = 0f;
      _group.interactable = false;
      _group.blocksRaycasts = false;
      gameObject.SetActive(false);
    }

    void ShowInternal()
    {
      if (_showing)
        return;

      _showing = true;
      gameObject.SetActive(true);
      _group.alpha = 1f;
      _group.interactable = true;
      _group.blocksRaycasts = true;
    }

    void Hide()
    {
      _showing = false;
      gameObject.SetActive(false);
    }

    static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      if (anchorMin.x != anchorMax.x || anchorMin.y != anchorMax.y)
        rt.sizeDelta = size;
      else
        rt.sizeDelta = size;
      var image = go.AddComponent<Image>();
      image.color = color;
      return image;
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 rectSize)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = rectSize;
      var label = go.AddComponent<Text>();
      label.text = text;
      label.fontSize = size;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = Color.white;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
      var image = CreateImage(parent, name, new Color(0.1f, 0.2f, 0.26f, 0.92f),
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(200f, 46f));
      var button = image.gameObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.onClick.AddListener(action);

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(image.transform, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = Vector2.zero;
      textRt.offsetMax = Vector2.zero;
      var text = textGo.AddComponent<Text>();
      text.text = label;
      text.fontSize = 18;
      text.fontStyle = FontStyle.Bold;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = new Color(0.85f, 0.96f, 1f, 1f);
      UiFontHelper.StyleText(text, 18, FontStyle.Bold);
    }
  }
}

