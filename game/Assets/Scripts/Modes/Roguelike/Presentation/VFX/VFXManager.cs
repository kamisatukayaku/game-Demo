using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Laser;
using Game.Shared.Vfx;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class VFXManager : MonoBehaviour
  {
    static VFXManager s_instance;

    readonly Dictionary<string, Queue<GameObject>> _pools = new();
    VFXDatabase _database;

    public static VFXManager Instance
    {
      get
      {
        EnsureExists();
        return s_instance;
      }
    }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_RoguelikeVFXManager");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<VFXManager>();
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
      _database = Resources.Load<VFXDatabase>("RoguelikeVFXDatabase")
        ?? VFXDatabase.CreateRuntimeDefaults();
    }

    public void PlayEvent(string eventId, Vector3 position, float scale = 1f, bool alternate = false)
    {
      PlayTrigger(eventId, position, scale, 0f, alternate);
    }

    public void PlayTrigger(string eventId, Vector3 position, float scale = 1f, float value = 0f, bool alternate = false)
    {
      Play(_database.ResolveEffectId(eventId), position, scale, value, alternate);
    }

    public void Play(string effectId, Vector3 position, float scale = 1f, bool alternate = false)
    {
      Play(effectId, position, scale, 0f, alternate);
    }

    public void Play(string effectId, Vector3 position, float scale, float value, bool alternate = false)
    {
      switch (effectId)
      {
        case "MageFlameNovaWarning":
          MageFlameNovaWarningVfx.Spawn(position, scale, value);
          return;
        case "MageFlameNova":
          MageFlameNovaVfx.Spawn(position, scale);
          return;
        case "MageFlameNovaHit":
          MageFlameNovaHitVfx.Spawn(position, scale);
          return;
        case "MageGravityWell":
          MageGravityWellVfx.Spawn(position, scale, value);
          return;
        case "MageGravityPulse":
          MageGravityWellVfx.PulseNearest(position, scale);
          return;
        case "MageGravityWellEnd":
          MageGravityWellVfx.ReleaseNearest(position, scale);
          return;
        case "MageArcaneMissileHit":
          MageArcaneMissileHitVfx.Spawn(position, scale, alternate);
          return;
        case "MageTidalHit":
        case "MageFrostSlow":
          MageFrostSlowVfx.Spawn(position, scale);
          return;
        case "MageTidalWave":
          MageTidalWaveVfx.Spawn(position, scale, value);
          return;
        case "MageFrostShatter":
          MageFrostShatterVfx.Spawn(position, scale);
          return;
        case "WarriorOrbitHit":
          WarriorOrbitHitVfx.Spawn(position, scale, alternate);
          return;
        case "WarriorOrbitSpeedUp":
          WarriorOrbitWeaponVfx.BoostAll();
          return;
        case "WarriorOrbitResonance":
          WarriorOrbitResonanceVfx.Spawn(position, scale);
          return;
        case "EnemyExplode":
        case "HitSpark":
        case "MagicCircle":
          BulletExplosionEffect.Spawn(position, scale, alternate);
          return;
      }

      if (!_database.TryGetEffect(effectId, out var entry) || entry.prefab == null)
        return;

      var instance = Acquire(effectId, entry.prefab);
      instance.transform.SetPositionAndRotation(position, Quaternion.identity);
      instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
      instance.SetActive(true);

      foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
      {
        particles.Clear(true);
        particles.Play(true);
      }

      foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
        animator.Rebind();

      StartCoroutine(ReleaseAfter(effectId, instance, entry.autoReleaseSeconds));
    }

    public void PlayGravityTether(Vector3 center, Transform target)
    {
      if (target == null)
        return;
      MageGravityTetherVfx.Spawn(center, target.position);
    }

    public void AttachArcaneMissile(GameObject projectile)
    {
      if (projectile == null)
        return;
      MageArcaneMissileProjectileVfx.Attach(projectile);
    }

    public void AttachWarriorOrbitWeapon(GameObject weapon, Transform owner, int index, int count, float radius, float size, float rotationSpeed, bool titan)
    {
      if (weapon == null)
        return;
      WarriorOrbitWeaponVfx.Attach(weapon, owner, index, count, radius, size, rotationSpeed, titan);
    }

    public void ShowNumber(Vector3 position, float value, DamageNumberStyle style)
    {
      if (CombatFeedbackManager.Exists)
        CombatFeedbackManager.ShowDamageNumber(position, value, style);
    }

    public void ShowNumber(Vector3 position, float value, Color color)
    {
      if (CombatFeedbackManager.Exists)
        CombatFeedbackManager.ShowDamageNumber(position, value, color);
    }

    GameObject Acquire(string effectId, GameObject prefab)
    {
      if (_pools.TryGetValue(effectId, out var pool))
      {
        while (pool.Count > 0)
        {
          var instance = pool.Dequeue();
          if (instance != null)
            return instance;
        }
      }

      var created = Instantiate(prefab, transform);
      created.name = prefab.name;
      return created;
    }

    IEnumerator ReleaseAfter(string effectId, GameObject instance, float delay)
    {
      yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
      if (instance == null)
        yield break;

      foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      instance.SetActive(false);
      instance.transform.SetParent(transform, false);

      if (!_pools.TryGetValue(effectId, out var pool))
      {
        pool = new Queue<GameObject>();
        _pools[effectId] = pool;
      }
      pool.Enqueue(instance);
    }

  }
}
