using System.Text;

using UnityEngine;

using UnityEngine.UI;



using Game.Modes.Roguelike.Progression;

using Game.Shared.Core;

using Game.Shared.UI;



namespace Game.Modes.Roguelike.UI

{

  /// <summary>B11: Share card panel with build, wave, keywords, brightest moment, copy button.</summary>

  public sealed class RunShareCardUI : MonoBehaviour

  {

    const int SortOrder = 980;



    static RunShareCardUI s_instance;



    CanvasGroup _group;

    RectTransform _panel;

    Text _cardText;

    Text _statusText;



    public static void Show(bool victory)
    {
      EnsureExists().ShowInternal(victory);
    }

    public static void HideIfVisible()
    {
      if (s_instance == null || !s_instance.gameObject.activeInHierarchy)
        return;

      s_instance.Hide();
    }

    static RunShareCardUI EnsureExists()

    {

      if (s_instance != null)

        return s_instance;



      var go = new GameObject("_RunShareCardUI");

      DontDestroyOnLoad(go);

      s_instance = go.AddComponent<RunShareCardUI>();

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

      canvas.sortingOrder = SortOrder;

      var scaler = gameObject.AddComponent<CanvasScaler>();

      gameObject.AddComponent<GraphicRaycaster>();

      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);



      var root = new GameObject("Root", typeof(RectTransform));

      root.transform.SetParent(transform, false);

      var rootRt = root.GetComponent<RectTransform>();

      rootRt.anchorMin = Vector2.zero;

      rootRt.anchorMax = Vector2.one;

      rootRt.offsetMin = Vector2.zero;

      rootRt.offsetMax = Vector2.zero;



      var overlay = CreateImage(rootRt, "Overlay", new Color(0.01f, 0.02f, 0.04f, 0.72f),

        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      overlay.raycastTarget = true;

      var overlayButton = overlay.gameObject.AddComponent<Button>();

      overlayButton.onClick.AddListener(Hide);



      _panel = CreateImage(rootRt, "ShareCardPanel", new Color(0.05f, 0.1f, 0.14f, 0.96f),

        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 420f)).rectTransform;



      var title = CreateText(_panel, "Title", "分享卡片", 32, FontStyle.Bold,

        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(420f, 44f));

      title.color = new Color(0.95f, 1f, 0.88f, 1f);



      _cardText = CreateText(_panel, "CardBody", "", 16, FontStyle.Normal,

        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(420f, 240f));

      _cardText.alignment = TextAnchor.UpperLeft;

      _cardText.color = new Color(0.82f, 0.94f, 0.98f, 1f);

      _cardText.lineSpacing = 1.15f;

      _cardText.horizontalOverflow = HorizontalWrapMode.Wrap;



      _statusText = CreateText(_panel, "Status", "", 14, FontStyle.Italic,

        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(420f, 24f));

      _statusText.color = new Color(0.55f, 0.95f, 0.75f, 0.95f);



      CreateButton(_panel, "CopyButton", "复制到剪贴板", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), CopyToClipboard);

      CreateButton(_panel, "CloseButton", "关闭", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), Hide);



      _group = root.AddComponent<CanvasGroup>();

      _group.alpha = 0f;

      _group.interactable = false;

      _group.blocksRaycasts = false;

      gameObject.SetActive(false);

    }



    void ShowInternal(bool victory)

    {

      _cardText.text = BuildCardText(victory);

      _statusText.text = "";

      gameObject.SetActive(true);

      _group.alpha = 1f;

      _group.interactable = true;

      _group.blocksRaycasts = true;

    }



    void Hide()

    {

      _group.alpha = 0f;

      _group.interactable = false;

      _group.blocksRaycasts = false;

      gameObject.SetActive(false);

    }



    void CopyToClipboard()

    {

      GUIUtility.systemCopyBuffer = _cardText.text;

      _statusText.text = "已复制到剪贴板";

    }



    public static string BuildCardText(bool victory)

    {

      var build = ArenaBuildBootstrap.GetDisplayName(RunDeathSummary.BuildDirection);

      var wave = RunDeathSummary.WaveReached;

      var title = RunStoryGenerator.GenerateRunTitle(victory);

      var keywords = RunStoryGenerator.GenerateKeywords(victory);

      var brightest = RunTimelineRecorder.GetBrightestMoment();



      var sb = new StringBuilder();

      sb.AppendLine("【环形竞技场】");

      sb.AppendLine($"「{title}」");

      sb.AppendLine($"构筑：{build}  ·  第 {wave} 波");

      sb.AppendLine($"关键词：{keywords[0]} / {keywords[1]} / {keywords[2]}");



      if (brightest.HasValue)

      {

        var moment = brightest.Value;

        sb.AppendLine($"最亮瞬间：{moment.Category} — {moment.Detail}");

      }

      else

        sb.AppendLine("最亮瞬间：本局无标记性决定瞬间");



      sb.AppendLine(victory ? "结果：胜利" : "结果：失败");

      return sb.ToString().TrimEnd();

    }



    static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)

    {

      var go = new GameObject(name, typeof(RectTransform));

      go.transform.SetParent(parent, false);

      var rt = go.GetComponent<RectTransform>();

      rt.anchorMin = anchorMin;

      rt.anchorMax = anchorMax;

      rt.anchoredPosition = anchoredPosition;

      if (anchorMin == anchorMax)

        rt.sizeDelta = size;

      else

      {

        rt.offsetMin = Vector2.zero;

        rt.offsetMax = Vector2.zero;

      }



      var image = go.AddComponent<Image>();

      image.sprite = UiSolidSprite.White;

      image.color = color;

      return image;

    }



    Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 rectSize)

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



    static void CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)

    {

      var image = CreateImage(parent, name, new Color(0.08f, 0.18f, 0.22f, 0.92f),

        anchorMin, anchorMax, anchoredPosition, new Vector2(260f, 44f));

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

      text.color = new Color(0.88f, 0.98f, 1f, 1f);

      UiFontHelper.StyleText(text, 18, FontStyle.Bold);

    }

  }

}


