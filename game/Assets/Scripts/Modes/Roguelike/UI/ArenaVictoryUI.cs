using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  public sealed class ArenaVictoryUI : MonoBehaviour
  {
    static ArenaVictoryUI s_instance;

    CanvasGroup _group;
    RectTransform _panel;
    Text _statsText;
    Text _rewardText;
    int _shardsEarned;
    bool _showing;

    public static void Show(int shardsEarned)
    {
      EnsureExists().ShowInternal(shardsEarned);
    }

    public static ArenaVictoryUI EnsureExists()
    {
      if (s_instance != null)
        return s_instance;

      var go = new GameObject("_ArenaVictoryUI");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<ArenaVictoryUI>();
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
      canvas.sortingOrder = 970;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 970);

      var root = new GameObject("Root", typeof(RectTransform));
      root.transform.SetParent(transform, false);
      var rootRt = root.GetComponent<RectTransform>();
      rootRt.anchorMin = Vector2.zero;
      rootRt.anchorMax = Vector2.one;
      rootRt.offsetMin = Vector2.zero;
      rootRt.offsetMax = Vector2.zero;

      CreateImage(rootRt, "Overlay", new Color(0.01f, 0.03f, 0.05f, 0.82f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      _panel = CreateImage(rootRt, "VictoryPanel", new Color(0.04f, 0.1f, 0.12f, 0.94f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 540f)).rectTransform;

      var title = CreateText(_panel, "Title", "胜利", 48, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(480f, 70f));
      title.color = new Color(0.95f, 1f, 0.82f, 1f);

      _statsText = CreateText(_panel, "Stats", "", 18, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(480f, 190f));
      _statsText.color = new Color(0.78f, 0.92f, 0.96f, 1f);

      _rewardText = CreateText(_panel, "Reward", "", 22, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(480f, 42f));
      _rewardText.color = new Color(1f, 0.9f, 0.45f, 1f);

      CreateButton(_panel, "ShareCardButton", "生成分享卡片", new Vector2(0f, -216f), () => RunShareCardUI.Show(true));
      CreateButton(_panel, "ContentWallButton", "完整版预览", new Vector2(0f, -288f), () => DemoContentWallUI.ShowAfterVictory(20));
      CreateButton(_panel, "ContinueButton", "再来一局", new Vector2(0f, -72f), RestartRun);
      CreateButton(_panel, "LobbyButton", "返回大厅", new Vector2(0f, -144f), ReturnToLobby);

      _group = root.AddComponent<CanvasGroup>();
      _group.alpha = 0f;
      _group.interactable = false;
      _group.blocksRaycasts = false;
      gameObject.SetActive(false);
    }

    void ShowInternal(int shardsEarned)
    {
      if (_showing)
        return;

      _showing = true;
      _shardsEarned = shardsEarned;
      RefreshStats();
      gameObject.SetActive(true);
      Time.timeScale = 0f;
      StartCoroutine(FadeInRoutine());
    }

    void RefreshStats()
    {
      _statsText.text =
        $"难度：{ArenaDifficultyRuntime.Active?.display_name ?? ArenaDifficultyRuntime.DifficultyId}\n" +
        $"构筑：{ArenaBuildBootstrap.GetDisplayName(ArenaBuildBootstrap.SelectedBuildId)}\n" +
        $"波次：{RunDeathSummary.WaveReached}\n" +
        $"等级：{RunDeathSummary.PlayerLevel}\n" +
        $"击杀：{RunDeathSummary.TotalKills}\n" +
        $"经验：{RunDeathSummary.TotalXp}\n" +
        $"存活：{RunDeathSummary.FormatSurviveTime(RunDeathSummary.SurviveSeconds)}\n\n" +
        RunStoryGenerator.Generate(true).FormatBlock();

      RunTimelineUI.AppendToPanel(_panel, -400f);
      ArenaAchievementUI.AppendToPanel(_panel, -530f);

      _rewardText.text = $"获得元进度碎片 +{_shardsEarned}（总计 {ArenaMetaProgress.TotalShards}）";
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

    void ReturnToLobby()
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

    static void CreateButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
      var image = CreateImage(parent, name, new Color(0.08f, 0.18f, 0.22f, 0.92f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(280f, 52f));
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
