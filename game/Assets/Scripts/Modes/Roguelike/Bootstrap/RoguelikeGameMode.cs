using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Modes.Roguelike.Gameplay;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Core;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Runtime;
using Game.Shared.UI;

namespace Game.Modes.Roguelike
{
  public sealed class RoguelikeGameMode : GameModeDescriptor
  {
    const string DefaultDifficulty = "normal";

    public override string ModeId => "arena";
    public override string DisplayName => "环形竞技场";
    public override string Description => "持续清理怪群、升级并完成十五波挑战。";
    public override Color ThemeColor => new(0.3f, 0.9f, 1f, 1f);

    StartGameUIShared _host;
    RectTransform _parent;
    RectTransform _root;
    string _selectedDifficulty = DefaultDifficulty;

    public override void BuildModeUI(Transform parent, StartGameUIShared host)
    {
      _host = host;
      _parent = parent as RectTransform ?? parent.GetComponent<RectTransform>();
      RebuildEntryUi();
      _host?.SetVisualStage(MenuVisualStage.ArenaEntry);
    }

    void RebuildEntryUi()
    {
      if (_root != null)
        Object.Destroy(_root.gameObject);

      _root = StartGameUIShared.CreatePanel(
        _parent,
        "ArenaEntryRoot",
        Color.clear,
        Vector2.zero,
        Vector2.one,
        Vector2.zero,
        Vector2.zero);
      _root.offsetMin = Vector2.zero;
      _root.offsetMax = Vector2.zero;
      var lobbyRaycastRoot = _root.gameObject.AddComponent<CanvasGroup>();
      BuildCodexDetailUI.SetLobbyRaycastRoot(lobbyRaycastRoot);
      BuildArenaEntry(_root);
    }

    public override void TeardownModeUI()
    {
      BuildCodexDetailUI.HideIfOpen();
      BuildCodexDetailUI.SetLobbyRaycastRoot(null);
      if (_root != null)
      {
        Object.Destroy(_root.gameObject);
        _root = null;
      }
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
        GameSessionConfig.GameMode.Arena,
        ArenaBuildBootstrap.Unified);
      ArenaRunRestart.PrepareForNewRun();
      CombatRoot.ResetMainSceneInitialization();
      CombatSceneBootstrapLocator.Register(RoguelikeCombatSceneBootstrap.Instance);
      GameSceneTransitionCurtain.LoadScene("MainScene");
    }

    void BuildArenaEntry(RectTransform panel)
    {
      _host.CreateLabel(
        panel,
        "Title",
        "环形竞技场",
        40,
        FontStyle.Bold,
        new Vector2(0.5f, 1f),
        new Vector2(0.5f, 1f),
        new Vector2(0f, -82f),
        new Vector2(720f, 58f));

      _host.CreateLabel(
        panel,
        "Subtitle",
        "持续清理怪群、升级并完成十五波挑战。",
        18,
        FontStyle.Normal,
        new Vector2(0.5f, 1f),
        new Vector2(0.5f, 1f),
        new Vector2(0f, -126f),
        new Vector2(720f, 34f));

      BuildDifficultyRow(panel, new Vector2(0f, 32f));

      var meta = _host.CreateLabel(
        panel,
        "MetaLabel",
        $"元进度碎片：{ArenaMetaProgress.TotalShards}  |  最佳波次：{ArenaMetaProgress.BestWave}",
        16,
        FontStyle.Normal,
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(0f, -78f),
        new Vector2(720f, 30f));
      meta.color = new Color(0.62f, 0.82f, 0.92f, 1f);

      _host.CreateButton(
        panel,
        "CodexButton",
        "图鉴",
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(300f, -78f),
        new Vector2(72f, 36f),
        StartGameUIShared.NormalBg,
        () => BuildCodexDetailUI.Open(_parent));

      _host.CreateButton(
        panel,
        "BackButton",
        "返回",
        new Vector2(0f, 0f),
        new Vector2(0f, 0f),
        new Vector2(122f, 46f),
        new Vector2(190f, 46f),
        StartGameUIShared.NormalBg,
        () => _host.NavigateBackToModeSelect());

      _host.CreateButton(
        panel,
        "StartButton",
        "开始生存",
        new Vector2(1f, 0f),
        new Vector2(1f, 0f),
        new Vector2(-150f, 46f),
        new Vector2(230f, 52f),
        StartGameUIShared.Accent,
        StartWithGameplayTransition);
    }

    void BuildDifficultyRow(RectTransform panel, Vector2 anchor)
    {
      _host.CreateLabel(
        panel,
        "DifficultyLabel",
        "难度",
        18,
        FontStyle.Bold,
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(-300f, anchor.y),
        new Vector2(80f, 30f));

      CreateOptionButton(panel, "EasyBtn", "简单", new Vector2(-120f, anchor.y), "easy");
      CreateOptionButton(panel, "NormalBtn", "普通", new Vector2(40f, anchor.y), "normal");
      CreateOptionButton(panel, "HardBtn", "困难", new Vector2(200f, anchor.y), "hard");
    }

    void CreateOptionButton(RectTransform parent, string name, string label, Vector2 pos, string difficultyId)
    {
      _host.CreateButton(
        parent,
        name,
        label,
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        pos,
        new Vector2(120f, 42f),
        _selectedDifficulty == difficultyId ? StartGameUIShared.Accent : StartGameUIShared.NormalBg,
        () =>
        {
          _selectedDifficulty = difficultyId;
          RebuildEntryUi();
        });
    }

    void StartWithGameplayTransition()
    {
      if (_host != null)
        _host.PlayGameplayLoadingTransition(OnStart);
      else
        OnStart();
    }
  }

  static class RoguelikeModeRegistration
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
      StartGameUIShared.RegisteredModes.Add(new RoguelikeGameMode());
    }
  }
}
