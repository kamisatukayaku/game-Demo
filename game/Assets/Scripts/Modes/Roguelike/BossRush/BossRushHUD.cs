using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Modes.Roguelike.BossRush
{
  public sealed class BossRushHUD : MonoBehaviour
  {
    static BossRushHUD s_instance;
    Canvas _canvas;
    Text _header;
    Text _bossName;
    Text _centerBanner;
    Text _tip;
    Text _timer;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_BossRushHUD");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      s_instance = go.AddComponent<BossRushHUD>();
    }

    public static void ShowEncounterIntro(BossRushEncounterDef encounter, int index, int total)
    {
      EnsureExists();
      s_instance.SetHeader(index, total);
      s_instance._bossName.text = encounter?.display_name ?? "未知首领";
      s_instance._centerBanner.text = encounter?.display_name ?? "";
      s_instance._centerBanner.gameObject.SetActive(true);
      s_instance._tip.text = string.IsNullOrEmpty(encounter?.tip) ? string.Empty : encounter.tip;
      s_instance._tip.gameObject.SetActive(!string.IsNullOrEmpty(encounter?.tip));
    }

    public static void ShowCountdown(int seconds)
    {
      EnsureExists();
      s_instance._centerBanner.text = seconds.ToString();
      s_instance._centerBanner.gameObject.SetActive(true);
    }

    public static void ShowBossActive(BossRushEncounterDef encounter, int index, int total)
    {
      EnsureExists();
      s_instance.SetHeader(index, total);
      s_instance._bossName.text = encounter?.display_name ?? string.Empty;
      s_instance._centerBanner.gameObject.SetActive(false);
      s_instance._tip.gameObject.SetActive(false);
    }

    public static void ShowBossDefeated(string displayName)
    {
      if (!Application.isPlaying)
        return;

      EnsureExists();
      s_instance._centerBanner.text = "首领已击破";
      s_instance._centerBanner.gameObject.SetActive(true);
    }

    public static void ShowConfigError(string message)
    {
      EnsureExists();
      s_instance._centerBanner.text = message ?? "配置错误";
      s_instance._centerBanner.gameObject.SetActive(true);
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      BuildUi();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      if (_timer == null || BossRushDirector.Instance == null)
        return;

      _timer.text = $"用时 {RunDeathSummary.FormatSurviveTime(BossRushDirector.Instance.RunElapsedSeconds)}";
    }

    void BuildUi()
    {
      _canvas = gameObject.AddComponent<Canvas>();
      _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      _canvas.sortingOrder = 900;
      gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      gameObject.AddComponent<GraphicRaycaster>();

      _header = CreateText("Header", 22, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(640, 36));
      _bossName = CreateText("BossName", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(640, 40));
      _centerBanner = CreateText("CenterBanner", 48, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 80));
      _tip = CreateText("Tip", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0, 96), new Vector2(720, 32));
      _timer = CreateText("Timer", 16, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-16, -24), new Vector2(180, 28));
      _centerBanner.gameObject.SetActive(false);
      _tip.gameObject.SetActive(false);
    }

    void SetHeader(int index, int total) => _header.text = $"首领连战 {index} / {total}";

    Text CreateText(string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 pos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(_canvas.transform, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = anchorMin;
      rt.pivot = anchorMin;
      rt.anchoredPosition = pos;
      rt.sizeDelta = sizeDelta;
      var text = go.AddComponent<Text>();
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.fontSize = size;
      text.alignment = anchor;
      text.color = Color.white;
      text.horizontalOverflow = HorizontalWrapMode.Overflow;
      return text;
    }
  }
}
