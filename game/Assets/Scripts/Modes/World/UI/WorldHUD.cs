using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// World 模式常驻 HUD。
  /// 左上角：玩家等级、XP进度条、金币。
  /// 右上角：世界等级、竖向世界经验进度条。
  /// </summary>
  public class WorldHUD : MonoBehaviour
  {
    static WorldHUD s_instance;

    // ── 左上: 玩家信息 ──
    Canvas _canvas;
    Text _playerLevelText;
    Image _playerXpFillBar;
    Text _playerXpText;
    Text _goldText;

    // ── 右上: 世界等级 ──
    Text _worldLevelText;
    Image _worldXpFillBar;
    Image _worldXpBg;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_WorldHUD");
      DontDestroyOnLoad(go);
      go.AddComponent<WorldHUD>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      BuildUI();
    }

    void OnDestroy() { if (s_instance == this) s_instance = null; }

    void BuildUI()
    {
      var font = UiFontHelper.GetFont();
      var canvasGo = new GameObject("WorldHUDCanvas");
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      _canvas.sortingOrder = 10;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, 280);

      BuildLeftPanel(canvasGo.transform, font);
      BuildRightPanel(canvasGo.transform, font);
    }

    // ══════════════════════════════════════════════════════
    //  左上角: 玩家等级 + XP条 + 金币
    // ══════════════════════════════════════════════════════

    void BuildLeftPanel(Transform root, Font font)
    {
      var panelGo = new GameObject("LeftPanel", typeof(RectTransform));
      panelGo.transform.SetParent(root, false);
      var panelRt = panelGo.GetComponent<RectTransform>();
      panelRt.anchorMin = new Vector2(0, 1);
      panelRt.anchorMax = new Vector2(0, 1);
      panelRt.pivot = new Vector2(0, 1);
      panelRt.anchoredPosition = new Vector2(10, -10);
      panelRt.sizeDelta = new Vector2(180, 88);
      var panelImg = panelGo.AddComponent<Image>();
      panelImg.color = new Color(0.05f, 0.07f, 0.10f, 0.85f);
      panelImg.raycastTarget = false;

      // 玩家等级
      _playerLevelText = MakeLabel(panelGo.transform, "PLv", "PLv.1", 16, FontStyle.Bold,
        TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 0.95f, 1f), font,
        new Vector2(8, -6), new Vector2(164, 22));

      // XP 进度条背景
      var barBgGo = new GameObject("XPBarBg", typeof(RectTransform));
      barBgGo.transform.SetParent(panelGo.transform, false);
      var barBgRt = barBgGo.GetComponent<RectTransform>();
      barBgRt.anchorMin = new Vector2(0, 1);
      barBgRt.anchorMax = new Vector2(0, 1);
      barBgRt.pivot = new Vector2(0, 1);
      barBgRt.anchoredPosition = new Vector2(8, -32);
      barBgRt.sizeDelta = new Vector2(164, 12);
      var barBgImg = barBgGo.AddComponent<Image>();
      barBgImg.color = new Color(0.12f, 0.16f, 0.22f, 1f);
      barBgImg.raycastTarget = false;

      // XP 填充
      var fillGo = new GameObject("XPBarFill", typeof(RectTransform));
      fillGo.transform.SetParent(barBgGo.transform, false);
      var fillRt = fillGo.GetComponent<RectTransform>();
      fillRt.anchorMin = new Vector2(0, 0);
      fillRt.anchorMax = new Vector2(0, 1);
      fillRt.pivot = new Vector2(0, 0.5f);
      fillRt.sizeDelta = new Vector2(0, 0);
      _playerXpFillBar = fillGo.AddComponent<Image>();
      _playerXpFillBar.color = new Color(0.36f, 0.72f, 0.82f, 1f);
      _playerXpFillBar.raycastTarget = false;

      // XP 文字
      _playerXpText = MakeLabel(panelGo.transform, "XPText", "0 / 100 XP", 11, FontStyle.Normal,
        TextAnchor.MiddleCenter, new Color(0.8f, 0.88f, 0.95f, 1f), font,
        new Vector2(8, -32), new Vector2(164, 12));

      // 金币
      _goldText = MakeLabel(panelGo.transform, "Gold", "0 G", 14, FontStyle.Bold,
        TextAnchor.MiddleLeft, new Color(1f, 0.84f, 0.25f, 1f), font,
        new Vector2(8, -48), new Vector2(164, 18));
    }

    // ══════════════════════════════════════════════════════
    //  右上角: 世界等级 + 竖向世界经验进度条
    // ══════════════════════════════════════════════════════

    void BuildRightPanel(Transform root, Font font)
    {
      const float panelWidth = 120f;
      const float panelHeight = 160f;

      var panelGo = new GameObject("RightPanel", typeof(RectTransform));
      panelGo.transform.SetParent(root, false);
      var panelRt = panelGo.GetComponent<RectTransform>();
      panelRt.anchorMin = new Vector2(1, 1);
      panelRt.anchorMax = new Vector2(1, 1);
      panelRt.pivot = new Vector2(1, 1);
      panelRt.anchoredPosition = new Vector2(-10, -10);
      panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
      var panelImg = panelGo.AddComponent<Image>();
      panelImg.color = new Color(0.05f, 0.07f, 0.10f, 0.85f);
      panelImg.raycastTarget = false;

      // 世界等级标题
      _worldLevelText = MakeLabel(panelGo.transform, "WLv", "WLv.1", 18, FontStyle.Bold,
        TextAnchor.MiddleCenter, new Color(0.45f, 0.82f, 1f, 1f), font,
        new Vector2(0, -8), new Vector2(panelWidth - 16, 24));
      _worldLevelText.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1);
      _worldLevelText.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
      _worldLevelText.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);

      // 竖向进度条背景
      float barTop = panelHeight - 40f;
      float barBottom = 16f;
      float barHeight = barTop - barBottom;
      float barWidth = 18f;
      float barCenterX = panelWidth * 0.5f - barWidth * 0.5f;

      var barBgGo = new GameObject("WorldXPBarBg", typeof(RectTransform));
      barBgGo.transform.SetParent(panelGo.transform, false);
      var barBgRt = barBgGo.GetComponent<RectTransform>();
      barBgRt.anchorMin = new Vector2(0, 1);
      barBgRt.anchorMax = new Vector2(0, 1);
      barBgRt.pivot = new Vector2(0, 1);
      barBgRt.anchoredPosition = new Vector2(barCenterX, -barTop + 8f);
      barBgRt.sizeDelta = new Vector2(barWidth, barHeight);
      _worldXpBg = barBgGo.AddComponent<Image>();
      _worldXpBg.color = new Color(0.08f, 0.12f, 0.18f, 1f);
      _worldXpBg.raycastTarget = false;

      // 竖向经验填充（从下往上）
      var fillGo = new GameObject("WorldXPBarFill", typeof(RectTransform));
      fillGo.transform.SetParent(barBgGo.transform, false);
      var fillRt = fillGo.GetComponent<RectTransform>();
      fillRt.anchorMin = new Vector2(0, 0);   // 底部对齐
      fillRt.anchorMax = new Vector2(1, 0);   // 从底部向上
      fillRt.pivot = new Vector2(0.5f, 0);
      fillRt.anchoredPosition = Vector2.zero;
      fillRt.sizeDelta = Vector2.zero;
      _worldXpFillBar = fillGo.AddComponent<Image>();
      _worldXpFillBar.color = new Color(0.82f, 0.36f, 0.18f, 1f); // 橙色 — 区别于蓝色玩家XP
      _worldXpFillBar.raycastTarget = false;

      // "世界" 文字标签
      MakeLabel(panelGo.transform, "WorldLabel", "世界等级", 10, FontStyle.Normal,
        TextAnchor.MiddleCenter, new Color(0.55f, 0.65f, 0.75f, 1f), font,
        new Vector2(0, -panelHeight + 8), new Vector2(panelWidth - 16, 14));
    }

    // ── 辅助 ──

    Text MakeLabel(Transform parent, string name, string text, int size, FontStyle style,
      TextAnchor align, Color color, Font font, Vector2 pos, Vector2 wh)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0, 1);
      rt.anchorMax = new Vector2(0, 1);
      rt.pivot = new Vector2(0, 1);
      rt.anchoredPosition = pos;
      rt.sizeDelta = wh;

      var label = go.AddComponent<Text>();
      label.font = font;
      label.fontSize = size;
      label.fontStyle = style;
      label.alignment = align;
      label.color = color;
      label.text = text;
      label.raycastTarget = false;
      return label;
    }

    // ══════════════════════════════════════════════════════
    //  刷新
    // ══════════════════════════════════════════════════════

    void Update()
    {
      Refresh();
    }

    void Refresh()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      // ── 右上: 世界等级 ──
      _worldLevelText.text = $"WLv.{WorldRuntimeContext.WorldLevel}";

      var wm = WorldManager.Instance;
      if (wm != null)
      {
        var wlSys = wm.GetSystem<WorldLevelSystem>();
        if (wlSys != null)
        {
          float cur = wlSys.WorldExp;
          float max = wlSys.ExpToNextLevel;
          float pct = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
          // 竖向填充：从底部向上
          var fillRt = _worldXpFillBar.GetComponent<RectTransform>();
          fillRt.anchorMax = new Vector2(1, pct);
        }
      }

      // ── 左上: 玩家等级 ──
      _playerLevelText.text = $"PLv.{WorldRuntimeContext.WorldPlayerLevel}";

      if (wm != null)
      {
        var wallet = wm.GoldWallet;
        if (wallet != null)
          _goldText.text = $"{wallet.Balance} G";
      }

      // XP 进度
      var plSys = wm?.GetSystem<PlayerLevelSystem>();
      if (plSys != null)
      {
        float cur = plSys.TotalXp;
        float max = plSys.XpToNextLevel;
        float pct = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
        _playerXpFillBar.GetComponent<RectTransform>().anchorMax = new Vector2(pct, 1);
        _playerXpText.text = $"{cur:F0} / {max:F0} XP";
      }
    }
  }
}
