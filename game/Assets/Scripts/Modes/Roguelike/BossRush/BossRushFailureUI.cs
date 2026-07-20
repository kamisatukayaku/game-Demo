using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Modes.Roguelike.BossRush
{
  public sealed class BossRushFailureUI : MonoBehaviour
  {
    static BossRushFailureUI s_instance;

    public static void Show(int defeatedCount, int encounterIndex, float elapsedSeconds)
    {
      if (!Application.isPlaying)
        return;

      EnsureExists();
      s_instance.gameObject.SetActive(true);
      s_instance._body.text =
        $"首领连战失败\n\n" +
        $"到达：第 {encounterIndex} 场\n" +
        $"击破首领：{defeatedCount}\n" +
        $"用时：{RunDeathSummary.FormatSurviveTime(elapsedSeconds)}";
    }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_BossRushFailureUI");
      if (Application.isPlaying)
        DontDestroyOnLoad(go);
      s_instance = go.AddComponent<BossRushFailureUI>();
    }

    Text _body;

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
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;
      gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      gameObject.AddComponent<GraphicRaycaster>();

      var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
      panel.transform.SetParent(transform, false);
      panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
      var rt = panel.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = rt.offsetMax = Vector2.zero;

      _body = BossRushVictoryUI.CreateText(panel.transform, "Body", 22, new Vector2(0.5f, 0.58f), new Vector2(560, 220));
      BossRushVictoryUI.CreateButton(panel.transform, "Restart", "重新挑战", new Vector2(-120f, -120f), () => ArenaRunRestart.ReloadMainScene());
      BossRushVictoryUI.CreateButton(panel.transform, "Menu", "返回主菜单", new Vector2(120f, -120f), () =>
      {
        ArenaRunRestart.ReturnToMainMenu();
      });
    }
  }
}
