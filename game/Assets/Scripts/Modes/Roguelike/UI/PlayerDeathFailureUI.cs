using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  public sealed class PlayerDeathFailureUI : MonoBehaviour
  {
    const float PanelWidth = 600f;
    const float PanelHeight = 700f;
    const float FooterHeight = 188f;
    const float HeaderHeight = 92f;
    const float ContentWidth = 520f;

    static PlayerDeathFailureUI s_instance;

    CanvasGroup _group;
    RectTransform _panel;
    RectTransform _scrollContent;
    RectTransform _timelineHost;
    Text _statsText;
    Text _storyText;
    Text _damageText;
    ScrollRect _scrollRect;
    bool _showing;

    public static PlayerDeathFailureUI EnsureExists()
    {
      if (s_instance != null)
        return s_instance;

      var go = new GameObject("_PlayerDeathFailureUI");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<PlayerDeathFailureUI>();
      s_instance.Build();
      return s_instance;
    }

    public static void Show()
    {
      EnsureExists().ShowInternal();
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
      canvas.sortingOrder = 960;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 960);

      var root = new GameObject("Root", typeof(RectTransform));
      root.transform.SetParent(transform, false);
      var rootRt = root.GetComponent<RectTransform>();
      rootRt.anchorMin = Vector2.zero;
      rootRt.anchorMax = Vector2.one;
      rootRt.offsetMin = Vector2.zero;
      rootRt.offsetMax = Vector2.zero;

      var overlay = CreateImage(rootRt, "Overlay", new Color(0.01f, 0.016f, 0.024f, 0.78f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      overlay.raycastTarget = true;

      _panel = CreateImage(rootRt, "DeathPanel", new Color(0.035f, 0.07f, 0.09f, 0.94f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight)).rectTransform;

      var title = CreateText(_panel, "Title", "失败", 44, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(ContentWidth, 48f));
      title.color = new Color(0.9f, 0.98f, 1f, 1f);

      var subtitle = CreateText(_panel, "Subtitle", "能量核心已崩解", 18, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(ContentWidth, 24f));
      subtitle.color = new Color(0.52f, 0.76f, 0.86f, 1f);

      BuildScrollArea();
      BuildFooterButtons();

      _group = root.AddComponent<CanvasGroup>();
      _group.alpha = 0f;
      _group.interactable = false;
      _group.blocksRaycasts = false;
      gameObject.SetActive(false);
    }

    void BuildScrollArea()
    {
      var scrollGo = new GameObject("Scroll", typeof(RectTransform));
      scrollGo.transform.SetParent(_panel, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = Vector2.zero;
      scrollRt.anchorMax = Vector2.one;
      scrollRt.offsetMin = new Vector2(24f, FooterHeight);
      scrollRt.offsetMax = new Vector2(-24f, -HeaderHeight);

      var viewportGo = new GameObject("Viewport", typeof(RectTransform));
      viewportGo.transform.SetParent(scrollGo.transform, false);
      var viewportRt = viewportGo.GetComponent<RectTransform>();
      viewportRt.anchorMin = Vector2.zero;
      viewportRt.anchorMax = Vector2.one;
      viewportRt.offsetMin = Vector2.zero;
      viewportRt.offsetMax = Vector2.zero;
      viewportGo.AddComponent<RectMask2D>();

      var contentGo = new GameObject("Content", typeof(RectTransform));
      contentGo.transform.SetParent(viewportGo.transform, false);
      _scrollContent = contentGo.GetComponent<RectTransform>();
      _scrollContent.anchorMin = new Vector2(0f, 1f);
      _scrollContent.anchorMax = new Vector2(1f, 1f);
      _scrollContent.pivot = new Vector2(0.5f, 1f);
      _scrollContent.anchoredPosition = Vector2.zero;
      _scrollContent.sizeDelta = new Vector2(0f, 420f);

      var layout = contentGo.AddComponent<VerticalLayoutGroup>();
      layout.childAlignment = TextAnchor.UpperLeft;
      layout.spacing = 10f;
      layout.padding = new RectOffset(4, 4, 4, 8);
      layout.childControlWidth = true;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;

      contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

      _scrollRect = scrollGo.AddComponent<ScrollRect>();
      _scrollRect.horizontal = false;
      _scrollRect.vertical = true;
      _scrollRect.movementType = ScrollRect.MovementType.Clamped;
      _scrollRect.viewport = viewportRt;
      _scrollRect.content = _scrollContent;

      _statsText = CreateSectionText("StatsSection", "本局数据", 17, 132f);
      _storyText = CreateSectionText("StorySection", "战报摘要", 15, 96f);
      _timelineHost = CreateSectionHost("TimelineSection", "本局时间线", 120f);
      _damageText = CreateSectionText("DamageSection", "最近伤害", 14, 88f);
      _damageText.color = new Color(0.68f, 0.82f, 0.88f, 0.92f);
    }

    Text CreateSectionText(string name, string header, int bodySize, float minHeight)
    {
      var sectionGo = new GameObject(name, typeof(RectTransform));
      sectionGo.transform.SetParent(_scrollContent, false);
      var sectionRt = sectionGo.GetComponent<RectTransform>();
      sectionRt.sizeDelta = new Vector2(ContentWidth, minHeight);

      var layout = sectionGo.AddComponent<LayoutElement>();
      layout.minHeight = minHeight;
      layout.preferredWidth = ContentWidth;

      var innerLayout = sectionGo.AddComponent<VerticalLayoutGroup>();
      innerLayout.spacing = 6f;
      innerLayout.childControlWidth = true;
      innerLayout.childControlHeight = true;
      innerLayout.childForceExpandWidth = true;
      innerLayout.childForceExpandHeight = false;

      CreateSectionHeader(sectionGo.transform, header);

      var bodyGo = new GameObject("Body", typeof(RectTransform));
      bodyGo.transform.SetParent(sectionGo.transform, false);
      var bodyLayout = bodyGo.AddComponent<LayoutElement>();
      bodyLayout.minHeight = minHeight - 28f;
      bodyLayout.flexibleHeight = 1f;

      var label = bodyGo.AddComponent<Text>();
      label.text = "";
      label.fontSize = bodySize;
      label.fontStyle = FontStyle.Normal;
      label.alignment = TextAnchor.UpperLeft;
      label.color = new Color(0.72f, 0.88f, 0.94f, 1f);
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      label.verticalOverflow = VerticalWrapMode.Overflow;
      label.lineSpacing = 1.1f;
      UiFontHelper.StyleText(label, bodySize, FontStyle.Normal);

      var fitter = bodyGo.AddComponent<ContentSizeFitter>();
      fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
      fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      return label;
    }

    RectTransform CreateSectionHost(string name, string header, float minHeight)
    {
      var sectionGo = new GameObject(name, typeof(RectTransform));
      sectionGo.transform.SetParent(_scrollContent, false);

      var layout = sectionGo.AddComponent<LayoutElement>();
      layout.minHeight = minHeight;
      layout.preferredWidth = ContentWidth;

      var innerLayout = sectionGo.AddComponent<VerticalLayoutGroup>();
      innerLayout.spacing = 6f;
      innerLayout.childControlWidth = true;
      innerLayout.childControlHeight = true;
      innerLayout.childForceExpandWidth = true;
      innerLayout.childForceExpandHeight = false;

      CreateSectionHeader(sectionGo.transform, header);

      var hostGo = new GameObject("Host", typeof(RectTransform));
      hostGo.transform.SetParent(sectionGo.transform, false);
      var hostLayout = hostGo.AddComponent<LayoutElement>();
      hostLayout.minHeight = minHeight - 28f;
      hostLayout.flexibleHeight = 1f;
      return hostGo.GetComponent<RectTransform>();
    }

    static void CreateSectionHeader(Transform parent, string text)
    {
      var go = new GameObject("Header", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      go.AddComponent<LayoutElement>().preferredHeight = 20f;

      var label = go.AddComponent<Text>();
      label.text = text;
      label.fontSize = 15;
      label.fontStyle = FontStyle.Bold;
      label.alignment = TextAnchor.MiddleLeft;
      label.color = new Color(0.72f, 0.88f, 0.95f, 1f);
      UiFontHelper.StyleText(label, 15, FontStyle.Bold);
    }

    void BuildFooterButtons()
    {
      CreateButton(_panel, "ShareCardButton", "生成分享卡片", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 142f), () => RunShareCardUI.Show(false));
      CreateButton(_panel, "RestartButton", "重新开始", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), RestartRun);
      CreateButton(_panel, "MainMenuButton", "返回主菜单", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), ReturnToMainMenu);
    }

    void ShowInternal()
    {
      if (_showing)
        return;

      _showing = true;
      RefreshContent();
      gameObject.SetActive(true);
      StartCoroutine(FadeInRoutine());
    }

    void RefreshContent()
    {
      ClearTimelineHost();

      if (_statsText != null)
      {
        _statsText.text =
          $"波次：{RunDeathSummary.WaveReached}\n" +
          $"等级：{RunDeathSummary.PlayerLevel}\n" +
          $"击杀：{RunDeathSummary.TotalKills}\n" +
          $"经验：{RunDeathSummary.TotalXp}\n" +
          $"构筑：{ArenaBuildBootstrap.GetDisplayName(RunDeathSummary.BuildDirection)}\n" +
          $"存活时间：{RunDeathSummary.FormatSurviveTime(RunDeathSummary.SurviveSeconds)}";
      }

      if (_storyText != null)
        _storyText.text = RunStoryGenerator.Generate(false).FormatBlock();

      if (_damageText != null)
        _damageText.text = FormatDamageLog();

      RunTimelineUI.AppendToHost(_timelineHost);

      if (_scrollRect != null)
      {
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 1f;
      }
    }

    void ClearTimelineHost()
    {
      if (_timelineHost == null)
        return;

      for (var i = _timelineHost.childCount - 1; i >= 0; i--)
        Destroy(_timelineHost.GetChild(i).gameObject);
    }

    static string FormatDamageLog()
    {
      var lines = ArenaDamageLog.GetRecentLines(3);
      if (lines.Count == 0)
        return "无记录";

      var text = "";
      foreach (var line in lines)
        text += line + "\n";
      return text.TrimEnd();
    }

    IEnumerator FadeInRoutine()
    {
      var elapsed = 0f;
      var startScale = Vector3.one * 0.92f;
      _panel.localScale = startScale;

      while (elapsed < 0.35f)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / 0.35f);
        var eased = 1f - Mathf.Pow(1f - t, 3f);
        _group.alpha = eased;
        _panel.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
        yield return null;
      }

      _group.alpha = 1f;
      _group.interactable = true;
      _group.blocksRaycasts = true;
    }

    void RestartRun()
    {
      Hide();
      RunShareCardUI.HideIfVisible();
      ArenaRunRestart.ReloadMainScene();
    }

    void ReturnToMainMenu()
    {
      RunShareCardUI.HideIfVisible();
      ArenaRunRestart.ReturnToMainMenu();
    }

    public static void HideIfVisible()
    {
      if (s_instance != null)
        s_instance.Hide();
    }

    void Hide()
    {
      StopAllCoroutines();
      _showing = false;
      ClearTimelineHost();
      if (_group != null)
      {
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
      }

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
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      label.verticalOverflow = VerticalWrapMode.Truncate;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
      var image = CreateImage(parent, name, new Color(0.08f, 0.18f, 0.22f, 0.92f),
        anchorMin, anchorMax, anchoredPosition, new Vector2(280f, 46f));
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
      text.fontSize = 20;
      text.fontStyle = FontStyle.Bold;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = new Color(0.88f, 0.98f, 1f, 1f);
      UiFontHelper.StyleText(text, 20, FontStyle.Bold);
    }
  }
}
