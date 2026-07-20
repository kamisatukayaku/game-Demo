using UnityEngine;
using UnityEngine.UI;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Sandbox-only tutorial debug panel (not shown in production HUD).</summary>
  public sealed class RoguelikeTutorialSandboxUI : MonoBehaviour
  {
    static RoguelikeTutorialSandboxUI s_instance;

    GameObject _panel;
    Text _status;

    public static void EnsureExistsIfSandbox()
    {
      var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
      if (!scene.Contains("Sandbox") && !scene.Contains("ArchetypeSandbox"))
        return;
      if (s_instance != null)
        return;
      var go = new GameObject("_RoguelikeTutorialSandboxUI");
      DontDestroyOnLoad(go);
      go.AddComponent<RoguelikeTutorialSandboxUI>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      RoguelikeTutorialState.SandboxBypassPersistence = true;
      RoguelikeTutorialDirector.EnsureExists();
      TutorialEventListener.EnsureExists();
      GroundZoneProximityTracker.EnsureExists();
      GroundZoneInfoPresenter.EnsureExists();
      BuildUi();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildUi()
    {
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 710;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 710);

      _panel = new GameObject("TutorialDebugPanel", typeof(RectTransform));
      _panel.transform.SetParent(transform, false);
      var rt = _panel.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(1f, 1f);
      rt.anchorMax = new Vector2(1f, 1f);
      rt.pivot = new Vector2(1f, 1f);
      rt.anchoredPosition = new Vector2(-12f, -12f);
      rt.sizeDelta = new Vector2(280f, 320f);

      var bg = _panel.AddComponent<Image>();
      bg.color = new Color(0.05f, 0.08f, 0.12f, 0.94f);

      CreateButton("重置教程", new Vector2(0f, 0f), () =>
      {
        RoguelikeTutorialDirector.ResetAllTutorialState();
        RefreshStatus();
      });
      CreateButton("移动提示", new Vector2(0f, -36f), () => RoguelikeTutorialDirector.Instance?.DebugTriggerStep("move"));
      CreateButton("冲刺提示", new Vector2(0f, -72f), () => RoguelikeTutorialDirector.Instance?.DebugTriggerStep("dash"));
      CreateButton("XP区说明", new Vector2(0f, -108f), () =>
      {
        var center = new Vector2(0f, 0f);
        RoguelikeTutorialDirector.Instance?.DebugSimulateZone("xp_boost_zone", center, 4.2f);
        GroundZoneProximityTracker.EnsureExists();
      });
      CreateButton("危险区说明", new Vector2(0f, -144f), () =>
        RoguelikeTutorialDirector.Instance?.DebugSimulateZone("meteor_strike_zone", Vector2.zero, 3f));
      CreateButton("模拟进入区域", new Vector2(0f, -180f), () =>
      {
        GroundZoneProximityTracker.EnsureExists();
        var tracker = FindObjectOfType<GroundZoneProximityTracker>();
        tracker?.DebugInjectZone("xp_boost_zone", Vector2.zero, 6f, 12f);
      });
      CreateButton("查看队列", new Vector2(0f, -216f), RefreshStatus);

      var statusGo = new GameObject("Status", typeof(RectTransform));
      statusGo.transform.SetParent(_panel.transform, false);
      var statusRt = statusGo.GetComponent<RectTransform>();
      statusRt.anchorMin = new Vector2(0f, 0f);
      statusRt.anchorMax = new Vector2(1f, 0f);
      statusRt.pivot = new Vector2(0.5f, 0f);
      statusRt.anchoredPosition = new Vector2(0f, 8f);
      statusRt.sizeDelta = new Vector2(-16f, 72f);
      _status = statusGo.AddComponent<Text>();
      _status.alignment = TextAnchor.UpperLeft;
      _status.fontSize = 11;
      _status.color = new Color(0.72f, 0.88f, 0.96f, 1f);
      UiFontHelper.StyleText(_status, 11);
      RefreshStatus();
    }

    void CreateButton(string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(label, typeof(RectTransform));
      go.transform.SetParent(_panel.transform, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0f, 1f);
      rt.anchorMax = new Vector2(1f, 1f);
      rt.pivot = new Vector2(0.5f, 1f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = new Vector2(-16f, 28f);
      var img = go.AddComponent<Image>();
      img.color = new Color(0.12f, 0.2f, 0.28f, 1f);
      var btn = go.AddComponent<Button>();
      btn.onClick.AddListener(onClick);
      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = Vector2.zero;
      textRt.offsetMax = Vector2.zero;
      var text = textGo.AddComponent<Text>();
      text.text = label;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      UiFontHelper.StyleText(text, 13);
    }

    void RefreshStatus()
    {
      if (_status == null || RoguelikeTutorialDirector.Instance == null)
        return;
      _status.text = "队列:\n" + RoguelikeTutorialDirector.Instance.DebugQueueSnapshot()
                     + "\n已完成:\n" + RoguelikeTutorialDirector.Instance.DebugCompletedSteps();
    }
  }
}
