using UnityEngine;

using Game.Modes.Roguelike.UI;
using Game.Modes.Roguelike.Progression;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>C6: Triggers a 3-step narrative chain at W6 / W12 / W18 via ArenaMomentUI.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaNarrativeEventDirector : MonoBehaviour
  {
    static ArenaNarrativeEventDirector s_instance;

    readonly System.Collections.Generic.HashSet<int> _playedWaves = new();
    float _spawnIntervalMult = 1f;
    bool _capstoneBoostActive;

    public static float SpawnIntervalMult => s_instance != null ? s_instance._spawnIntervalMult : 1f;
    public static bool CapstoneBoostActive => s_instance != null && s_instance._capstoneBoostActive;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaNarrativeEventDirector");
      go.AddComponent<ArenaNarrativeEventDirector>();
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
      NarrativeEventDatabase.EnsureLoaded();
      WaveDirector.PhaseChanged += OnPhaseChanged;
    }

    void OnDestroy()
    {
      WaveDirector.PhaseChanged -= OnPhaseChanged;
      if (s_instance == this)
        s_instance = null;
    }

    void ResetRun()
    {
      _playedWaves.Clear();
      _spawnIntervalMult = 1f;
      _capstoneBoostActive = false;
      NarrativeEventDatabase.EnsureLoaded();
    }

    void OnPhaseChanged(WaveDirector.Phase phase, int wave)
    {
      if (phase != WaveDirector.Phase.BuildPhase || wave <= 1)
        return;

      var step = NarrativeEventDatabase.GetStepForWave(wave);
      if (step == null || _playedWaves.Contains(wave))
        return;

      _playedWaves.Add(wave);
      RunTimelineRecorder.Record("Story", step.title);
      ArenaMomentUI.ShowBanner(
        $"{step.title}\n<size=18>{step.body}</size>",
        NarrativeEventDatabase.ParseColor(step.banner_color, new Color(0.7f, 0.9f, 1f, 1f)));

      if (step.spawn_interval_mult > 0f && step.spawn_interval_mult < 1f)
        _spawnIntervalMult = step.spawn_interval_mult;

      if (step.xp_bonus > 0)
      {
        _capstoneBoostActive = true;
        if (ExperienceSystem.Exists)
          ExperienceSystem.GrantBonusXp(step.xp_bonus);
      }
    }
  }
}
