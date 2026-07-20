using System.Text;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// World 模式结算界面 — 单局结束时显示。
  ///
  /// 监听 WorldEventBus.RunEnded 自动弹出。
  /// 显示结局类型、统计数据、分数、BattleExp 和返回菜单按钮。
  ///
  /// 结局类型影响显示效果：
  ///   - Failure   → 暗红色主题，"探索失败"
  ///   - Clear1    → 蓝色主题，"营地平定"
  ///   - Clear2    → 金色主题，"霸主陨落"
  /// </summary>
  public class WorldResultsUI : MonoBehaviour
  {
    static WorldResultsUI s_instance;

    static readonly Color BgDim = new(0f, 0f, 0f, 0.7f);
    static readonly Color PanelBg = new(0.06f, 0.08f, 0.12f, 0.97f);
    static readonly Color FailureAccent = new(0.9f, 0.2f, 0.2f, 1f);
    static readonly Color Clear1Accent = new(0.3f, 0.6f, 1f, 1f);
    static readonly Color Clear2Accent = new(1f, 0.75f, 0.2f, 1f);
    static readonly Color StatLabelColor = new(0.65f, 0.7f, 0.78f, 1f);
    static readonly Color StatValueColor = new(0.9f, 0.93f, 0.95f, 1f);

    Font _font;
    GameObject _panel;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_WorldResultsUI");
      DontDestroyOnLoad(go);
      go.AddComponent<WorldResultsUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      _font = UiFontHelper.GetFont();
      BuildCanvas();
      _panel.SetActive(false);

      WorldEventBus.RunEnded += OnRunEnded;
    }

    void OnDestroy()
    {
      WorldEventBus.RunEnded -= OnRunEnded;
      if (s_instance == this) s_instance = null;
    }

    void OnRunEnded(WorldEndType endType)
    {
      // 延迟一帧弹出，避免在事件链中修改 UI 导致死循环
      StartCoroutine(ShowDelayed(endType));
    }

    System.Collections.IEnumerator ShowDelayed(WorldEndType endType)
    {
      yield return null;
      Show(endType);
    }

    void Show(WorldEndType endType)
    {
      _panel.SetActive(true);
      Refresh(endType);
    }

    void Refresh(WorldEndType endType)
    {
      var accentColor = endType switch
      {
        WorldEndType.Clear1 => Clear1Accent,
        WorldEndType.Clear2 => Clear2Accent,
        _ => FailureAccent
      };

      var (title, subtitle) = endType switch
      {
        WorldEndType.Failure => ("探索失败", "你在旷野中倒下了……"),
        WorldEndType.Clear1 => ("营地平定", "所有营地已被摧毁，但英雄也力竭而亡。"),
        WorldEndType.Clear2 => ("霸主陨落", "荒野中的霸主尽数伏诛，世界重归宁静。"),
        _ => ("结算", "")
      };

      // 清空旧内容
      var content = _panel.transform;
      for (int i = content.childCount - 1; i >= 0; i--)
      {
        if (content.GetChild(i).name != "BgPanel") // 保留背景面板
          Destroy(content.GetChild(i).gameObject);
      }

      // ── 标题 ──
      var titleLabel = CreateLabel(content, "Title", title, 32, FontStyle.Bold,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(400, 44));
      titleLabel.color = accentColor;
      titleLabel.alignment = TextAnchor.MiddleCenter;

      // ── 副标题 ──
      var subLabel = CreateLabel(content, "Subtitle", subtitle, 14, FontStyle.Normal,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(420, 24));
      subLabel.color = StatLabelColor;
      subLabel.alignment = TextAnchor.MiddleCenter;

      // ── 分隔线 ──
      var sepRt = CreatePanel(content, "Separator", accentColor,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(360, 2));

      // ── 统计数据 ──
      float y = -150f;
      AddStatLine(content, "摧毁营地", WorldRuntimeContext.NonBossCampsDestroyed.ToString(), ref y);
      AddStatLine(content, "击杀Boss", $"{WorldRuntimeContext.BossesKilled}/{WorldRuntimeContext.TotalBosses}", ref y);
      AddStatLine(content, "达到世界等级", WorldRuntimeContext.WorldLevel.ToString(), ref y);
      AddStatLine(content, "达到玩家等级", WorldRuntimeContext.WorldPlayerLevel.ToString(), ref y);
      AddStatLine(content, "收集金币", WorldRuntimeContext.WorldGold.ToString(), ref y);

      // ── 分隔线 ──
      y -= 14;
      var sep2Rt = CreatePanel(content, "Separator2", accentColor * 0.5f,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(300, 1));

      // ── 分数 ──
      y -= 30;
      AddStatLine(content, "战斗分数", WorldRuntimeContext.RunScore.ToString("F0"), ref y);
      AddStatLine(content, "获得战斗经验", WorldRuntimeContext.RunBattleExp.ToString("F0"), ref y);

      // ── 结局倍率 ──
      y -= 10;
      var multStr = endType switch
      {
        WorldEndType.Clear1 => "x1.0 倍率 (通关奖励 +500)",
        WorldEndType.Clear2 => "x1.0 倍率 (通关奖励 +500)",
        _ => "x0.5 倍率 (无通关奖励)"
      };
      var multLabel = CreateLabel(content, "Mult", multStr, 11, FontStyle.Italic,
        new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(360, 20));
      multLabel.color = StatLabelColor;
      multLabel.alignment = TextAnchor.MiddleCenter;

      // ── 返回菜单按钮 ──
      y -= 46;
      var btnGo = new GameObject("ReturnBtn", typeof(RectTransform));
      btnGo.transform.SetParent(content, false);
      var btnRt = btnGo.GetComponent<RectTransform>();
      btnRt.anchorMin = new Vector2(0.5f, 1);
      btnRt.anchorMax = new Vector2(0.5f, 1);
      btnRt.pivot = new Vector2(0.5f, 1);
      btnRt.anchoredPosition = new Vector2(0, y);
      btnRt.sizeDelta = new Vector2(220, 40);

      var btnImg = btnGo.AddComponent<Image>();
      btnImg.color = accentColor;

      var btnBtn = btnGo.AddComponent<Button>();
      btnBtn.onClick.AddListener(ReturnToMenu);

      var btnLabel = CreateLabel(btnRt, "Label", "返回主菜单", 16, FontStyle.Bold,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      btnLabel.color = Color.white;
    }

    void AddStatLine(Transform parent, string label, string value, ref float y)
    {
      // 标签（左）
      var labelCtrl = CreateLabel(parent, $"StatL_{label}", label, 15, FontStyle.Normal,
        new Vector2(0.35f, 1), new Vector2(0.35f, 1), new Vector2(0, y), new Vector2(180, 24));
      labelCtrl.alignment = TextAnchor.MiddleRight;
      labelCtrl.color = StatLabelColor;

      // 数值（右）
      var valueCtrl = CreateLabel(parent, $"StatV_{label}", value, 15, FontStyle.Bold,
        new Vector2(0.55f, 1), new Vector2(0.55f, 1), new Vector2(10, y), new Vector2(120, 24));
      valueCtrl.alignment = TextAnchor.MiddleLeft;
      valueCtrl.color = StatValueColor;

      y -= 28;
    }

    void ReturnToMenu()
    {
      if (WorldManager.Instance != null)
        WorldManager.Instance.Shutdown();

      SceneManager.LoadScene("MainScene");
    }

    // ══════════════════════════════════════════════════════
    //  Canvas 构建
    // ══════════════════════════════════════════════════════

    void BuildCanvas()
    {
      UiBootstrap.EnsureEventSystem();
      var canvasGo = new GameObject("ResultsCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = 200;
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 500);
      canvasGo.AddComponent<GraphicRaycaster>();

      // 背景遮罩
      var dimGo = new GameObject("DimBg", typeof(RectTransform));
      dimGo.transform.SetParent(canvasGo.transform, false);
      var dimRt = dimGo.GetComponent<RectTransform>();
      dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
      dimRt.sizeDelta = Vector2.zero;
      var dimImg = dimGo.AddComponent<Image>();
      dimImg.color = BgDim;
      dimImg.raycastTarget = true;

      // 主面板
      _panel = CreatePanel(canvasGo.transform, "ResultsPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 500)).gameObject;

      // 内边距面板（用于内容）
      var innerGo = new GameObject("BgPanel", typeof(RectTransform));
      innerGo.transform.SetParent(_panel.transform, false);
      var innerRt = innerGo.GetComponent<RectTransform>();
      innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
      innerRt.sizeDelta = Vector2.zero;
    }

    // ══════════════════════════════════════════════════════
    //  UI 工具方法
    // ══════════════════════════════════════════════════════

    RectTransform CreatePanel(Transform parent, string name, Color bg, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.pivot = aMin == aMax ? aMin : new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos; rt.sizeDelta = size;
      if (bg.a > 0.001f) { var img = go.AddComponent<Image>(); img.color = bg; img.raycastTarget = bg.a > 0.01f; }
      return rt;
    }

    Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 wh)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = aMin; rt.anchorMax = aMax;
      rt.pivot = new Vector2(aMin.x, aMin.y == aMax.y ? 0.5f : 1f);
      rt.anchoredPosition = pos; rt.sizeDelta = wh;
      var label = go.AddComponent<Text>();
      label.font = _font; label.fontSize = size; label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
      label.text = text; label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      return label;
    }
  }
}
