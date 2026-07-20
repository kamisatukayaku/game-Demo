using UnityEngine;

using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.UI;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>C8: Records build story beats at key levels and boosts capstone pool weights toward W15.</summary>
  [DisallowMultipleComponent]
  public sealed class BuildMovieArcDirector : MonoBehaviour
  {
    static BuildMovieArcDirector s_instance;

    BuildMovieArcDatabase.ArcDef _arc;
    readonly System.Collections.Generic.HashSet<int> _playedLevels = new();
    bool _capstoneWavePlayed;
    EventListenerHandle _levelUpHandle;
    float _capstoneWeightBoost = 1f;

    public static float CapstoneWeightBoost => s_instance != null ? s_instance._capstoneWeightBoost : 1f;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_BuildMovieArcDirector");
      go.AddComponent<BuildMovieArcDirector>();
    }

    public static void BeginRun()
    {
      EnsureExists();
      s_instance.ResetRun();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildMovieArcDatabase.EnsureLoaded();
      WaveDirector.WaveCompleted += OnWaveCompleted;
    }

    void OnDestroy()
    {
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      if (_levelUpHandle.Valid)
        GameEventBus.Unsubscribe(_levelUpHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void ResetRun()
    {
      _playedLevels.Clear();
      _capstoneWavePlayed = false;
      _capstoneWeightBoost = 1f;
      _arc = BuildMovieArcDatabase.GetForBuild(ArenaBuildBootstrap.SelectedBuildId);

      if (_levelUpHandle.Valid)
        GameEventBus.Unsubscribe(_levelUpHandle);
      _levelUpHandle = GameEventBus.Subscribe<LevelUpEvent>(OnLevelUp);
    }

    void OnLevelUp(LevelUpEvent evt)
    {
      if (_arc?.beats == null)
        return;

      foreach (var beat in _arc.beats)
      {
        if (beat == null || beat.level != evt.NewLevel || _playedLevels.Contains(beat.level))
          continue;

        _playedLevels.Add(beat.level);
        RunTimelineRecorder.Record("Arc", beat.title);
        ArenaMomentUI.ShowBanner($"{beat.title}\n<size=18>{beat.subtitle}</size>", new Color(0.55f, 0.88f, 1f, 1f));
        if (beat.level >= 12 && _arc != null)
          ArenaMetaProgress.UnlockEvolution(_arc.build_id + "_capstone");
      }
    }

    void OnWaveCompleted(int wave)
    {
      _capstoneWeightBoost = BuildMovieArcDatabase.CapstoneWeightMult(wave);

      if (_capstoneWavePlayed || _arc == null || wave < _arc.capstone_wave)
        return;

      _capstoneWavePlayed = true;
      RunTimelineRecorder.Record("Capstone", _arc.capstone_title);
      ArenaMomentUI.ShowBanner(
        $"{_arc.capstone_title}\n<size=18>{_arc.capstone_subtitle}</size>",
        ArenaBuildBootstrap.GetIdentityColor(ArenaBuildBootstrap.SelectedBuildId));
    }
  }
}
