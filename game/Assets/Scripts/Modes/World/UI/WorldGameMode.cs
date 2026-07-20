using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Game.Shared.Core;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Runtime;
using Game.Shared.UI;
using Game.UI;

namespace Game.World
{
  /// <summary>
  /// World 旷野探索模式。
  /// 启动流程：选择模式 → "开始探索" → 随机饰品3选1 → 进入游戏
  /// 新增 "总结经验" 按钮打开局外天赋树。
  /// </summary>
  public class WorldGameMode : GameModeDescriptor
  {
    public override string ModeId => "explore";
    public override string DisplayName => "旷野探索";
    public override string Description => "在广阔地图中自由探索，发现未知区域";
    public override Color ThemeColor => new(0.22f, 0.48f, 0.34f, 1f);

    RectTransform _root;
    StartGameUIShared _host;
    string _selectedAccessoryId;

    public override void BuildModeUI(Transform parent, StartGameUIShared host)
    {
      _root = parent as RectTransform ?? parent.GetComponent<RectTransform>();
      _host = host;
      _selectedAccessoryId = null;

      BuildStartPanel();
    }

    // ══════════════════════════════════════════════════════
    //  开始面板: 开始探索 / 总结经验 / 返回
    // ══════════════════════════════════════════════════════

    void BuildStartPanel()
    {
      // 清除旧UI
      for (int i = _root.childCount - 1; i >= 0; i--)
        Object.Destroy(_root.GetChild(i).gameObject);

      var panel = StartGameUIShared.CreatePanel(_root, "WorldStartPanel", StartGameUIShared.PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));

      _host.CreateLabel(panel, "Title", "旷野探索", 32, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(460f, 44f));

      _host.CreateLabel(panel, "Desc", "在广阔地图中自由探索，摧毁营地、挑战Boss、收集强力饰品。\n每次探索都是新的旅程。",
        15, FontStyle.Normal,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(440f, 70f))
        .color = new Color(0.75f, 0.85f, 0.9f, 1f);

      // "开始探索" — 跳转到饰品3选1
      _host.CreateButton(panel, "StartExploreButton", "开始探索",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(280f, 52f),
        StartGameUIShared.Accent, () => BuildAccessoryPicker());

      // "总结经验" — 打开局外天赋树
      _host.CreateButton(panel, "MetaProgressionButton", "总结经验",
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(280f, 46f),
        new Color(0.6f, 0.5f, 0.2f, 1f), OpenMetaProgression);

      // "返回"
      _host.CreateButton(panel, "BackButton", "返回",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(180f, 44f),
        StartGameUIShared.NormalBg, () => _host.NavigateBackToModeSelect());
    }

    void OpenMetaProgression()
    {
      var sys = new MetaProgressionSystem();
      sys.Initialize();
      MetaProgressionUI.Open(sys);
    }

    // ══════════════════════════════════════════════════════
    //  饰品3选1面板
    // ══════════════════════════════════════════════════════

    void BuildAccessoryPicker()
    {
      // 清除旧UI
      for (int i = _root.childCount - 1; i >= 0; i--)
        Object.Destroy(_root.GetChild(i).gameObject);

      WorldDatabase.EnsureLoaded();

      // 随机抽取3个精致（refined）饰品
      var refinedItems = new List<WorldDatabase.ItemDef>();
      foreach (var kv in WorldDatabase.Items)
      {
        var def = kv.Value;
        if (def != null && def.IsAccessory && def.quality == "refined")
          refinedItems.Add(def);
      }

      // 打乱并取前3个（若不足则用intact补）
      Shuffle(refinedItems);
      var picks = refinedItems.Take(3).ToList();
      if (picks.Count < 3)
      {
        var intactItems = new List<WorldDatabase.ItemDef>();
        foreach (var kv in WorldDatabase.Items)
        {
          var def = kv.Value;
          if (def != null && def.IsAccessory && def.quality == "intact" && !picks.Contains(def))
            intactItems.Add(def);
        }
        Shuffle(intactItems);
        while (picks.Count < 3 && intactItems.Count > 0)
        {
          picks.Add(intactItems[0]);
          intactItems.RemoveAt(0);
        }
      }

      var panel = StartGameUIShared.CreatePanel(_root, "AccessoryPickerPanel", StartGameUIShared.PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 460f));

      _host.CreateLabel(panel, "Title", "选择初始饰品", 28, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(520f, 40f));

      _host.CreateLabel(panel, "Hint", "选择一件精致的饰品作为你的初始装备",
        14, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(500f, 22f))
        .color = new Color(0.65f, 0.75f, 0.85f, 1f);

      // 三列卡片
      float cardWidth = 160f;
      float cardHeight = 280f;
      float spacing = 20f;
      float totalWidth = cardWidth * 3 + spacing * 2;
      float startX = -totalWidth / 2f + cardWidth / 2f;
      float cardY = -60f;

      for (int i = 0; i < picks.Count; i++)
      {
        var item = picks[i];
        var idx = i;
        float x = startX + i * (cardWidth + spacing);
        BuildAccessoryCard(panel, item, idx, x, cardY, cardWidth, cardHeight);
      }

      // 返回按钮
      _host.CreateButton(panel, "BackFromPicker", "返回",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(180f, 44f),
        StartGameUIShared.NormalBg, BuildStartPanel);
    }

    void BuildAccessoryCard(RectTransform parent, WorldDatabase.ItemDef item, int index,
      float x, float y, float width, float height)
    {
      var borderColor = index == 0 ? new Color(1f, 0.84f, 0.25f, 0.6f)   // 金
                      : index == 1 ? new Color(0.7f, 0.7f, 0.75f, 0.6f)   // 银
                      : new Color(0.75f, 0.45f, 0.2f, 0.6f);              // 铜

      var card = StartGameUIShared.CreatePanel(parent, $"Card_{item.item_id}", borderColor,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        new Vector2(x, y), new Vector2(width, height));

      // 内层背景
      var inner = StartGameUIShared.CreatePanel(card, "Inner", new Color(0.08f, 0.1f, 0.14f, 1f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        Vector2.zero, new Vector2(width - 8, height - 8));

      // 品质标签
      string qualityLabel = item.quality switch
      {
        "refined" => "★精致",
        "intact" => "完好",
        _ => item.quality
      };
      var qLabel = _host.CreateLabel(inner, "Quality", qualityLabel, 12, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(width - 20, 20f));
      qLabel.color = item.quality == "refined"
        ? new Color(1f, 0.84f, 0.25f, 1f)
        : new Color(0.7f, 0.85f, 0.95f, 1f);
      qLabel.alignment = TextAnchor.MiddleCenter;

      // 名称
      var nameLabel = _host.CreateLabel(inner, "Name", item.display_name, 16, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(width - 20, 24f));
      nameLabel.alignment = TextAnchor.MiddleCenter;
      nameLabel.color = new Color(0.9f, 0.92f, 0.95f, 1f);

      // 描述
      var descLabel = _host.CreateLabel(inner, "Desc", item.description ?? "", 11, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(width - 20, 48f));
      descLabel.alignment = TextAnchor.MiddleCenter;
      descLabel.color = new Color(0.6f, 0.7f, 0.8f, 1f);
      descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

      // 属性词条
      string affixStr = "";
      if (item.affixes != null && item.affixes.Length > 0)
      {
        foreach (var affix in item.affixes)
        {
          if (affix == null || string.IsNullOrEmpty(affix.key)) continue;
          var attrDef = WorldDatabase.GetAttributeDef(affix.key);
          var attrName = attrDef?.display_name ?? affix.key;
          var valStr = affix.value >= 0 ? $"+{affix.value}" : $"{affix.value}";
          affixStr += $"{attrName} {valStr}\n";
        }
      }

      var affixLabel = _host.CreateLabel(inner, "Affixes", affixStr.TrimEnd('\n'), 11, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
        new Vector2(0f, -108f), new Vector2(width - 20, 80f));
      affixLabel.alignment = TextAnchor.UpperCenter;
      affixLabel.color = new Color(0.5f, 0.9f, 0.55f, 1f);
      affixLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

      // 选择按钮
      var btnRt = StartGameUIShared.CreatePanel(card, "SelectBtn", StartGameUIShared.Accent,
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
        new Vector2(0f, 12f), new Vector2(width - 40, 40f));
      var btn = btnRt.gameObject.AddComponent<Button>();
      btn.targetGraphic = btnRt.GetComponent<Image>();

      var btnTextGo = new GameObject("BtnText", typeof(RectTransform));
      btnTextGo.transform.SetParent(btnRt, false);
      var btnTextRt = btnTextGo.GetComponent<RectTransform>();
      btnTextRt.anchorMin = Vector2.zero; btnTextRt.anchorMax = Vector2.one;
      btnTextRt.sizeDelta = Vector2.zero;
      var btnText = btnTextGo.AddComponent<Text>();
      btnText.font = UiFontHelper.GetFont();
      btnText.fontSize = 16;
      btnText.fontStyle = FontStyle.Bold;
      btnText.alignment = TextAnchor.MiddleCenter;
      btnText.color = Color.white;
      btnText.text = "选择";
      btnText.raycastTarget = false;

      btn.onClick.AddListener(() =>
      {
        _selectedAccessoryId = item.item_id;
        OnStart();
      });
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    static void Shuffle<T>(List<T> list)
    {
      int n = list.Count;
      while (n > 1)
      {
        n--;
        int k = Random.Range(0, n + 1);
        (list[k], list[n]) = (list[n], list[k]);
      }
    }

    public override void TeardownModeUI()
    {
      if (_root != null)
      {
        Object.Destroy(_root.gameObject);
        _root = null;
      }
    }

    public override void OnStart()
    {
      // 存储选中的初始饰品ID，供WorldManager在初始化背包时添加
      if (!string.IsNullOrEmpty(_selectedAccessoryId))
      {
        GameSessionConfig.SetWorldStartingAccessory(_selectedAccessoryId);
      }

      var mode = GameSessionConfig.GameMode.Explore;
      GameSessionConfig.Configure("ranged", new HashSet<string>(), "normal", mode);
      CombatSceneBootstrapLocator.Register(WorldCombatSceneBootstrap.Instance);
      SceneManager.LoadScene("MainScene");
    }
  }

  /// <summary>在程序集加载时自动注册 World 模式</summary>
  static class WorldModeRegistration
  {
    // Temporary release gate: keep the Explore implementation intact while
    // removing its card from the public mode-selection screen.
    static readonly bool ShowInModeSelect = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
      if (!ShowInModeSelect)
        return;

      StartGameUIShared.RegisteredModes.Add(new WorldGameMode());
    }
  }
}
