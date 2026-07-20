using System.Collections.Generic;
using UnityEngine;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Combat.Events;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Laser;
using Game.Shared.Vfx;

namespace Game.Modes.Roguelike.Presentation.Audio
{
  [DisallowMultipleComponent]
  public sealed class RoguelikeAudioEventListener : MonoBehaviour
  {
    const string ResourceRoot = "SFX/";

    static RoguelikeAudioEventListener s_instance;

    readonly Dictionary<string, AudioClip> _clips = new();
    readonly Dictionary<string, float> _nextPlayTime = new();

    AudioSource _source;
    AudioSource _hitSource;
    AudioSource _arcaneSource;
    EventListenerHandle _enemyDeathHandle;
    EventListenerHandle _triggerHandle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeAfterSceneLoad() => EnsureExists();

    static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_RoguelikeAudioEventListener");
      DontDestroyOnLoad(go);
      go.AddComponent<RoguelikeAudioEventListener>();
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

      _source = gameObject.AddComponent<AudioSource>();
      _source.playOnAwake = false;
      _source.loop = false;
      _source.spatialBlend = 0f;
      _source.volume = 0.82f;

      var hitChannel = new GameObject("EnemyHitAudio");
      hitChannel.transform.SetParent(transform, false);
      _hitSource = hitChannel.AddComponent<AudioSource>();
      _hitSource.playOnAwake = false;
      _hitSource.loop = false;
      _hitSource.spatialBlend = 0f;
      _hitSource.volume = 0.78f;

      var arcaneChannel = new GameObject("ArcaneMissileAudio");
      arcaneChannel.transform.SetParent(transform, false);
      _arcaneSource = arcaneChannel.AddComponent<AudioSource>();
      _arcaneSource.playOnAwake = false;
      _arcaneSource.loop = false;
      _arcaneSource.spatialBlend = 0f;
      _arcaneSource.dopplerLevel = 0f;
      _arcaneSource.pitch = 0.94f;
      _arcaneSource.volume = 0.76f;
      var arcaneLowPass = arcaneChannel.AddComponent<AudioLowPassFilter>();
      arcaneLowPass.cutoffFrequency = 5800f;
      arcaneLowPass.lowpassResonanceQ = 1f;

      Load("gravity_well");
      Load("enemy_death");
      Load("enemy_hit");
      Load("warrior_orbit_hit");
      Load("tidal_wave");
      Load("flame_nova");
      Load("explosion");
      Load("chain_lightning");
      Load("laser_shoot");
    }

    void OnEnable()
    {
      _enemyDeathHandle = GameEventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
      _triggerHandle = GameEventBus.Subscribe<TriggerActivatedEvent>(OnTriggerActivated);
      RangedExplosionVfx.Spawned += OnRangedExplosion;
      RangedChainLightningVfx.Spawned += OnChainLightningSpawned;
      LaserEnemyAttack.Fired += OnEnemyLaserFired;
      CombatEventBus.PostDamage += OnPostDamage;
    }

    void OnDisable()
    {
      GameEventBus.Unsubscribe(_enemyDeathHandle);
      GameEventBus.Unsubscribe(_triggerHandle);
      RangedExplosionVfx.Spawned -= OnRangedExplosion;
      RangedChainLightningVfx.Spawned -= OnChainLightningSpawned;
      LaserEnemyAttack.Fired -= OnEnemyLaserFired;
      CombatEventBus.PostDamage -= OnPostDamage;
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Load(string cueId)
    {
      var clip = Resources.Load<AudioClip>(ResourceRoot + cueId);
      if (clip != null)
        _clips[cueId] = clip;
      else
        Debug.LogWarning($"[RoguelikeAudio] Missing clip: {ResourceRoot}{cueId}");
    }

    void OnEnemyDeath(EnemyDeathEvent evt) => Play("enemy_death", 0.5f, 0.045f);

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (args.Target == null || args.Result.FinalDamage <= 0.01f || IsPlayerTarget(args.Target))
        return;

      if (!IsEnemyTarget(args.Target))
        return;

      var volume = Mathf.Clamp(0.28f + args.Result.FinalDamage * 0.008f, 0.28f, 0.52f);
      Play("enemy_hit", volume, 0.055f, _hitSource, "enemy_hit_mix");
    }

    static bool IsEnemyTarget(GameObject target) =>
      target.GetComponentInParent<EnemyCore>() != null
      || target.CompareTag("Enemy")
      || target.CompareTag("Elite");

    static bool IsPlayerTarget(GameObject target)
    {
      if (target == null)
        return false;

      var root = target.transform.root.gameObject;
      return root.CompareTag("Player") || root.name == "Player";
    }

    void OnRangedExplosion(Vector3 position) => Play("explosion", 0.34f, 0.16f);

    void OnChainLightningSpawned(Vector3 position) => Play("chain_lightning", 0.54f, 0.28f);

    void OnEnemyLaserFired(Vector3 position) => Play("laser_shoot", 0.58f, 0.1f);

    void OnTriggerActivated(TriggerActivatedEvent evt)
    {
      switch (evt.TriggerId)
      {
        case "MageGravityWell":
          Play("gravity_well", 0.72f, 0.25f);
          break;
        case "MageTidalWave":
          Play("tidal_wave", 0.65f, 0.75f);
          break;
        case "MageFlameNova":
          Play("flame_nova", 0.85f, 0.2f);
          break;
        case "WarriorOrbitHit":
          Play("warrior_orbit_hit", 0.38f, 0.12f);
          break;
        case "Explosion":
          Play("explosion", 0.34f, 0.16f);
          break;
      }
    }

    void Play(string cueId, float volume, float minimumInterval, AudioSource output = null, string throttleKey = null)
    {
      output ??= cueId == "arcane_missile" ? _arcaneSource : _source;
      throttleKey ??= cueId;
      if (output == null || !_clips.TryGetValue(cueId, out var clip) || clip == null)
        return;

      var now = Time.unscaledTime;
      if (_nextPlayTime.TryGetValue(throttleKey, out var nextTime) && now < nextTime)
        return;

      _nextPlayTime[throttleKey] = now + Mathf.Max(0f, minimumInterval);
      output.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
  }
}
