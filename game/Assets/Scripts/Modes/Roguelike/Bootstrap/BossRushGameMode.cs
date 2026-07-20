using System.Collections.Generic;
using Game.Modes.Roguelike.Gameplay;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Runtime;
using Game.Shared.UI;
using UnityEngine;

namespace Game.Modes.Roguelike
{
  public sealed class BossRushGameMode : GameModeDescriptor
  {
    const string DefaultDifficulty = "normal";
    string _selectedDifficulty = DefaultDifficulty;
    StartGameUIShared _host;
    RectTransform _parent;
    RectTransform _root;

    public override string ModeId => "boss_rush";
    public override string DisplayName => "首领连战";
    public override string Description => "连续挑战七名首领，在战间快速完成构筑。";
    public override Color ThemeColor => new(1f, 0.55f, 0.22f, 1f);

    public override void BuildModeUI(Transform parent, StartGameUIShared host)
    {
      _host = host;
      _parent = parent as RectTransform ?? parent.GetComponent<RectTransform>();
      RebuildEntryUi();
      _host?.SetVisualStage(MenuVisualStage.ArenaEntry);
    }

    public override void TeardownModeUI()
    {
      if (_root != null)
        Object.Destroy(_root.gameObject);
      _root = null;
      _parent = null;
      _host = null;
    }

    public override void OnStart()
    {
      var theme = ArenaBuildBootstrap.GetThemeForBuild(ArenaBuildBootstrap.Unified);
      GameSessionConfig.Configure(
        theme,
        new HashSet<string>(),
        _selectedDifficulty,
        GameSessionConfig.GameMode.BossRush,
        ArenaBuildBootstrap.Unified);
      ArenaRunRestart.PrepareForNewRun();
      CombatRoot.ResetMainSceneInitialization();
      CombatSceneBootstrapLocator.Register(RoguelikeCombatSceneBootstrap.Instance);
      GameSceneTransitionCurtain.LoadScene("MainScene");
    }

    void RebuildEntryUi()
    {
      if (_root != null)
        Object.Destroy(_root.gameObject);

      _root = StartGameUIShared.CreatePanel(
        _parent,
        "BossRushEntryRoot",
        Color.clear,
        Vector2.zero,
        Vector2.one,
        Vector2.zero,
        Vector2.zero);
      _root.offsetMin = Vector2.zero;
      _root.offsetMax = Vector2.zero;

      _host.CreateLabel(_root, "Title", "首领连战", 40, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(720f, 58f));
      _host.CreateLabel(_root, "Subtitle", "连续挑战七名首领，在战间快速完成构筑。", 18, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(720f, 34f));

      BuildDifficultyRow(new Vector2(0f, 32f));

      var best = ArenaMetaProgress.BestBossRushSeconds;
      var meta = _host.CreateLabel(_root, "MetaLabel",
        best > 0f ? $"最佳通关：{RunDeathSummary.FormatSurviveTime(best)}" : "尚无通关记录",
        16, FontStyle.Normal,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(720f, 30f));
      meta.color = new Color(0.92f, 0.72f, 0.55f, 1f);

      _host.CreateButton(_root, "BackButton", "返回",
        new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(122f, 46f), new Vector2(190f, 46f),
        StartGameUIShared.NormalBg, () => _host.NavigateBackToModeSelect());

      _host.CreateButton(_root, "StartButton", "开始连战",
        new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 46f), new Vector2(230f, 52f),
        StartGameUIShared.Accent, StartWithGameplayTransition);
    }

    void BuildDifficultyRow(Vector2 anchor)
    {
      _host.CreateLabel(_root, "DifficultyLabel", "难度", 18, FontStyle.Bold,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300f, anchor.y), new Vector2(80f, 30f));
      CreateOptionButton("EasyBtn", "简单", new Vector2(-120f, anchor.y), "easy");
      CreateOptionButton("NormalBtn", "普通", new Vector2(40f, anchor.y), "normal");
      CreateOptionButton("HardBtn", "困难", new Vector2(200f, anchor.y), "hard");
    }

    void CreateOptionButton(string name, string label, Vector2 pos, string difficultyId)
    {
      _host.CreateButton(_root, name, label,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(120f, 42f),
        _selectedDifficulty == difficultyId ? StartGameUIShared.Accent : StartGameUIShared.NormalBg,
        () => { _selectedDifficulty = difficultyId; RebuildEntryUi(); });
    }

    void StartWithGameplayTransition()
    {
      if (_host != null)
        _host.PlayGameplayLoadingTransition(OnStart);
      else
        OnStart();
    }
  }

  static class BossRushModeRegistration
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StartGameUIShared.RegisteredModes.Add(new BossRushGameMode());
  }
}
