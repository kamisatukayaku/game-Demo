using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Modes.Roguelike.BossRush
{
  public sealed class BossRushVictoryUI : MonoBehaviour
  {
    static BossRushVictoryUI s_instance;

    public static void ShowVictory(int defeatedCount, float elapsedSeconds)
    {
      EnsureExists();
      s_instance.gameObject.SetActive(true);
      s_instance._body.text =
        $"首领连战胜利！\n\n" +
        $"击破首领：{defeatedCount}\n" +
        $"用时：{RunDeathSummary.FormatSurviveTime(elapsedSeconds)}\n\n" +
        $"Meta 碎片奖励已记录。";

      ArenaMetaProgress.RecordBossRushVictory(elapsedSeconds, defeatedCount);
    }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_BossRushVictoryUI");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      s_instance = go.AddComponent<BossRushVictoryUI>();
    }

    Text _body;
    bool _built;

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      BuildUi();
      gameObject.SetActive(false);
    }

    void BuildUi()
    {
      if (_built)
        return;
      _built = true;

      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;
      gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      gameObject.AddComponent<GraphicRaycaster>();

      var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
      panel.transform.SetParent(transform, false);
      var img = panel.GetComponent<Image>();
      img.color = new Color(0f, 0f, 0f, 0.72f);
      var rt = panel.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = rt.offsetMax = Vector2.zero;

      _body = CreateText(panel.transform, "Body", 22, new Vector2(0.5f, 0.58f), new Vector2(560, 220));
      CreateButton(panel.transform, "Restart", "重新挑战", new Vector2(-120f, -120f), Restart);
      CreateButton(panel.transform, "Menu", "返回主菜单", new Vector2(120f, -120f), ReturnMenu);
    }

    static void Restart()
    {
      ArenaRunRestart.ReloadMainScene();
    }

    static void ReturnMenu()
    {
      ArenaRunRestart.ReturnToMainMenu();
    }

    public static Text CreateText(Transform parent, string name, int size, Vector2 anchor, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = anchor;
      rt.sizeDelta = sizeDelta;
      rt.anchoredPosition = Vector2.zero;
      var text = go.AddComponent<Text>();
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.fontSize = size;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      return text;
    }

    public static void CreateButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.sizeDelta = new Vector2(200f, 46f);
      rt.anchoredPosition = pos;
      go.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.48f, 0.95f);
      go.GetComponent<Button>().onClick.AddListener(onClick);
      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      var text = textGo.AddComponent<Text>();
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.text = label;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      text.fontSize = 20;
      var trt = textGo.GetComponent<RectTransform>();
      trt.anchorMin = Vector2.zero;
      trt.anchorMax = Vector2.one;
      trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
  }
}
