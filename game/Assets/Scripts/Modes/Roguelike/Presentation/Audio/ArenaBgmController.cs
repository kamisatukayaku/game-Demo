using UnityEngine;

using Game.Modes.Roguelike.Combat;
using Game.Shared.Gameplay.Events;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Presentation.Audio
{
  /// <summary>B12: Layered arena BGM — base / combat density / boss / near-death / evolution via AudioSource volume mixing.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaBgmController : MonoBehaviour
  {
    enum Layer { Base, CombatDensity, Boss, NearDeath, Evolution }

    const float NearDeathThreshold = 0.15f;
    const int WaveGate = 8;
    const float FadeSpeed = 2.4f;

    static ArenaBgmController s_instance;

    readonly AudioSource[] _layers = new AudioSource[5];
    readonly float[] _targets = new float[5];
    readonly float[] _baseLayerVolumes = { 0.28f, 0.38f, 0.42f, 0.35f, 0.45f };

    Health _playerHealth;
    float _refFindTimer;
    bool _inNearDeath;
    bool _evolutionActive;
    bool _bossActive;
    float _evolutionTimer;
    EventListenerHandle _bossSpawnHandle;
    EventListenerHandle _bossKillHandle;
    EventListenerHandle _bossPhaseHandle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaBgmController");
      DontDestroyOnLoad(go);
      go.AddComponent<ArenaBgmController>();
    }

    public static void NotifyEvolutionMoment(float duration = 1.1f)
    {
      EnsureExists();
      if (s_instance == null)
        return;

      s_instance._evolutionActive = true;
      s_instance._evolutionTimer = Mathf.Max(s_instance._evolutionTimer, duration);
      s_instance._targets[(int)Layer.Evolution] = s_instance._baseLayerVolumes[(int)Layer.Evolution];
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildLayers();
      WaveDirector.PhaseChanged += OnWavePhaseChanged;
      WaveDirector.WaveCompleted += OnWaveCompleted;
    }

    void OnEnable()
    {
      _bossSpawnHandle = GameEventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
      _bossKillHandle = GameEventBus.Subscribe<BossKilledEvent>(OnBossKilled);
      _bossPhaseHandle = GameEventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
    }

    void OnDisable()
    {
      if (_bossSpawnHandle.Valid)
        GameEventBus.Unsubscribe(_bossSpawnHandle);
      if (_bossKillHandle.Valid)
        GameEventBus.Unsubscribe(_bossKillHandle);
      if (_bossPhaseHandle.Valid)
        GameEventBus.Unsubscribe(_bossPhaseHandle);
    }

    void OnDestroy()
    {
      WaveDirector.PhaseChanged -= OnWavePhaseChanged;
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      if (s_instance == this)
        s_instance = null;
    }

    void BuildLayers()
    {
      var freqs = new[] { 110f, 165f, 82f, 58f, 220f };
      for (var i = 0; i < _layers.Length; i++)
      {
        var child = new GameObject($"BgmLayer_{(Layer)i}");
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.clip = CreateToneClip($"ArenaBgm_{(Layer)i}", freqs[i], i == (int)Layer.CombatDensity ? 0.55f : 0.4f);
        source.Play();
        _layers[i] = source;
      }

      _targets[(int)Layer.Base] = _baseLayerVolumes[(int)Layer.Base];
    }

    void Update()
    {
      if (!CircleArenaController.IsActive)
        return;

      RefreshNearDeath();
      RefreshCombatDensity();
      TickEvolution();

      for (var i = 0; i < _layers.Length; i++)
      {
        if (_layers[i] == null)
          continue;

        var current = _layers[i].volume;
        _layers[i].volume = Mathf.MoveTowards(current, _targets[i], FadeSpeed * Time.unscaledDeltaTime);
      }
    }

    void TickEvolution()
    {
      if (!_evolutionActive)
        return;

      _evolutionTimer -= Time.unscaledDeltaTime;
      if (_evolutionTimer <= 0f)
      {
        _evolutionActive = false;
        _targets[(int)Layer.Evolution] = 0f;
      }
    }

    void RefreshNearDeath()
    {
      if (_playerHealth == null || _playerHealth.IsDead)
      {
        FindPlayerHealth();
        if (_playerHealth == null)
          return;
      }

      var gated = IsWaveGateMet();
      var hpRatio = _playerHealth.HpPercent;
      _inNearDeath = gated && hpRatio > 0f && hpRatio < NearDeathThreshold;
      _targets[(int)Layer.NearDeath] = _inNearDeath ? _baseLayerVolumes[(int)Layer.NearDeath] : 0f;
    }

    void RefreshCombatDensity()
    {
      if (!IsWaveGateMet())
      {
        _targets[(int)Layer.CombatDensity] = 0f;
        return;
      }

      var director = WaveDirector.Instance;
      if (director == null)
        return;

      var waveFactor = Mathf.Clamp01((director.CurrentWave - WaveGate) / 8f);
      var active = director.CurrentPhase == WaveDirector.Phase.WaveActive;
      var remaining = director.EnemiesRemaining;
      var density = active ? Mathf.Clamp01(remaining / 24f) : 0f;
      _targets[(int)Layer.CombatDensity] = _baseLayerVolumes[(int)Layer.CombatDensity] * waveFactor * Mathf.Lerp(0.35f, 1f, density);
    }

    void OnWavePhaseChanged(WaveDirector.Phase phase, int wave)
    {
      if (phase == WaveDirector.Phase.WaveActive && wave >= WaveGate)
        _targets[(int)Layer.Base] = Mathf.Min(0.42f, _baseLayerVolumes[(int)Layer.Base] + wave * 0.004f);
      else if (phase == WaveDirector.Phase.BuildPhase)
        _targets[(int)Layer.CombatDensity] = 0f;
    }

    void OnWaveCompleted(int wave)
    {
      if (!_bossActive)
        _targets[(int)Layer.Boss] = 0f;
    }

    void OnBossSpawned(BossSpawnedEvent evt)
    {
      if (!IsWaveGateMet())
        return;

      _bossActive = true;
      var isFinal = evt.BossId != null && evt.BossId.StartsWith("final_boss_");
      _targets[(int)Layer.Boss] = isFinal ? 0.52f : _baseLayerVolumes[(int)Layer.Boss];
    }

    void OnBossKilled(BossKilledEvent evt)
    {
      _bossActive = false;
      _targets[(int)Layer.Boss] = 0f;
    }

    void OnBossPhaseChanged(BossPhaseChangedEvent evt)
    {
      if (!IsWaveGateMet() || evt.ToPhase < 2)
        return;

      if (evt.BossId == "prism_nexus")
        _targets[(int)Layer.Boss] = 0.58f;
    }

    void FindPlayerHealth()
    {
      _refFindTimer -= Time.deltaTime;
      if (_refFindTimer > 0f)
        return;

      _refFindTimer = 0.75f;
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      _playerHealth = player != null ? player.GetComponent<Health>() : null;
    }

    static bool IsWaveGateMet()
    {
      var director = WaveDirector.Instance;
      return director != null && director.CurrentWave >= WaveGate;
    }

    static AudioClip CreateToneClip(string name, float frequency, float duration)
    {
      const int sampleRate = 44100;
      var count = Mathf.CeilToInt(sampleRate * duration);
      var samples = new float[count];
      for (var i = 0; i < count; i++)
      {
        var t = i / (float)sampleRate;
        var env = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / duration);
        samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * env * 0.12f;
      }

      var clip = AudioClip.Create(name, count, 1, sampleRate, false);
      clip.SetData(samples, 0);
      return clip;
    }
  }
}
