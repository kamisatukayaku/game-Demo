using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedWeaponPresentationSystem : MonoBehaviour
  {
    static DetachedWeaponPresentationSystem s_instance;
    MissileBurstPool _missileBursts;
    MissileLockPool _missileLocks;
    LaserHitSparkPool _laserHitSparks;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      CreateRootInstance();
    }

    static void CreateRootInstance()
    {
      var go = new GameObject("_DetachedWeaponPresentation");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      var system = go.AddComponent<DetachedWeaponPresentationSystem>();
      system.EnsurePoolsInitialized();
      if (s_instance == null)
        s_instance = system;
    }

    /// <summary>Clears stale DontDestroyOnLoad instances before editor regression tests.</summary>
    public static void ForceRecreateForTest()
    {
      var stale = s_instance;
      s_instance = null;
      if (stale != null)
        DestroyImmediate(stale.gameObject);
      CreateRootInstance();
    }

    public static void RefreshExistingWeapons()
    {
      EnsureExists();
      AttachExistingWeapons();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }
      s_instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
      EnsurePoolsInitialized();
    }

    void EnsurePoolsInitialized()
    {
      if (_missileBursts != null)
        return;
      _missileBursts = new MissileBurstPool(transform, 20);
      _missileLocks = new MissileLockPool(transform, 24);
      _laserHitSparks = new LaserHitSparkPool(transform, 48);
      DetachedExplosionVfx.EnsureExists();
      DetachedPulseVfx.EnsureExists();
      DetachedBoomerangVfx.EnsureExists();
      DetachedTrailVfx.EnsureExists();
      DetachedWeaponEvolutionVfx.EnsureExists();
      EnemyHitReactionVfx.EnsureExists();
    }

    void OnEnable()
    {
      DetachedWeaponSystem.WeaponSpawned += Attach;
      AttachExistingWeapons();
    }

    void OnDisable() => DetachedWeaponSystem.WeaponSpawned -= Attach;

    void OnDestroy()
    {
      DetachedWeaponSystem.WeaponSpawned -= Attach;
      if (s_instance == this)
        s_instance = null;
    }

    public static void ResetPoolsForNewRun()
    {
      EnsureExists();
      if (s_instance == null)
        return;

      s_instance._missileBursts?.ResetAll();
      s_instance._missileLocks?.ResetAll();
      s_instance._laserHitSparks?.ResetAll();
      DetachedExplosionVfx.ResetAll();
      DetachedPulseVfx.ResetAll();
      DetachedTrailVfx.ResetAll();
      DetachedBoomerangVfx.ResetAll();
      DetachedWeaponEvolutionVfx.ResetAll();
      EnemyHitReactionVfx.ResetAll();
      ClearOrphanedPresentationRenderers();
    }

    /// <summary>Sandbox alias — forwards to run reset.</summary>
    public static void ResetPoolsForSandbox() => ResetPoolsForNewRun();

    static void ClearOrphanedPresentationRenderers()
    {
      foreach (var line in Object.FindObjectsOfType<LineRenderer>())
      {
        if (line == null || line.gameObject == null)
          continue;
        var root = line.transform.root;
        if (root == null || s_instance == null)
          continue;
        if (!root.IsChildOf(s_instance.transform))
          continue;
        if (line.gameObject.activeInHierarchy)
          line.gameObject.SetActive(false);
        line.positionCount = 0;
      }

      foreach (var trail in Object.FindObjectsOfType<TrailRenderer>())
      {
        if (trail == null || trail.gameObject == null)
          continue;
        var root = trail.transform.root;
        if (root == null || s_instance == null)
          continue;
        if (!root.IsChildOf(s_instance.transform))
          continue;
        trail.Clear();
        trail.emitting = false;
        trail.gameObject.SetActive(false);
      }

      foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
      {
        if (ps == null || s_instance == null)
          continue;
        if (!ps.transform.IsChildOf(s_instance.transform))
          continue;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
      }
    }

    public static int CountActiveIntros()
    {
      var count = 0;
      foreach (var weapon in Object.FindObjectsOfType<DetachedWeaponController>())
      {
        if (weapon == null)
          continue;
        var runner = weapon.GetComponent<DetachedWeaponIntroRunner>();
        if (runner != null && runner.IsRunning)
          count++;
      }
      return count;
    }

    public static void PlayWeaponIntro(
      GameObject weapon,
      Transform owner,
      int slotIndex,
      int totalSlots,
      string weaponId)
    {
      if (weapon == null || owner == null || string.IsNullOrEmpty(weaponId))
        return;

      EnsureExists();
      Attach(weapon);

      var visual = weapon.GetComponent<DetachedWeaponVisual>();
      visual?.ResetForSpawn();

      var state = weapon.GetComponent<DetachedWeaponVisualState>();
      state?.ResetPresentationState();

      var target = DetachedWeaponSpawnTargets.Compute(owner, weaponId, slotIndex, totalSlots);
      var delay = slotIndex * Random.Range(
        DetachedWeaponSpawnSettings.StaggerMin,
        DetachedWeaponSpawnSettings.StaggerMax);

      var runner = weapon.GetComponent<DetachedWeaponIntroRunner>()
        ?? weapon.AddComponent<DetachedWeaponIntroRunner>();
      runner.Begin(owner, target, delay, weaponId);
    }

    public static string GetPoolDebugSummary()
    {
      if (s_instance == null)
        return "DetachedWeaponPresentation: inactive";
      return $"Burst {s_instance._missileBursts?.GetDebugSummary()} | " +
             $"Lock {s_instance._missileLocks?.GetDebugSummary()} | " +
             $"Spark {s_instance._laserHitSparks?.GetDebugSummary()} | " +
             DetachedExplosionVfx.GetDebugSummary() + " | " +
             DetachedPulseVfx.GetDebugSummary() + " | " +
             DetachedTrailVfx.GetDebugSummary() + " | " +
             DetachedBoomerangVfx.GetDebugSummary() + " | " +
             EnemyHitReactionVfx.GetDebugSummary();
    }

    static void Attach(GameObject weapon)
    {
      if (weapon == null)
        return;
      if (weapon.GetComponent<DetachedWeaponVisual>() == null)
        weapon.AddComponent<DetachedWeaponVisual>();
    }

    static void AttachExistingWeapons()
    {
      var weapons = Object.FindObjectsOfType<DetachedWeaponController>();
      foreach (var weapon in weapons)
        if (weapon != null)
          Attach(weapon.gameObject);
    }

    public static void AttachMissile(GameObject projectile, bool child)
    {
      if (projectile == null)
        return;
      var visual = projectile.GetComponent<DetachedMissileVisual>();
      if (visual == null)
        visual = projectile.AddComponent<DetachedMissileVisual>();
      visual.Configure(child);
    }

    public static void PlayMissileBurst(Vector3 position, bool hit)
    {
      EnsureExists();
      s_instance?._missileBursts?.Play(position, hit);
    }

    public static void PlayMissileLock(GameObject source, GameObject target, bool child)
    {
      if (target == null)
        return;
      EnsureExists();
      s_instance?._missileLocks?.Play(source != null ? source.transform : null, target.transform, child);
    }

    public static void PlayLaserHitSpark(Vector3 position, bool prism, float amount)
    {
      EnsureExists();
      s_instance?._laserHitSparks?.Play(position, prism, amount);
    }

    public static int CountWeaponVisuals()
    {
      var count = 0;
      foreach (var weapon in Object.FindObjectsOfType<DetachedWeaponController>())
      {
        if (weapon != null && weapon.GetComponent<DetachedWeaponVisual>() != null)
          count++;
      }
      return count;
    }

    public static void PlayExplosionCoreFlash(GameObject source)
    {
      if (source == null)
        return;
      source.GetComponent<DetachedWeaponVisual>()?.PlayExplosionFlash();
    }
  }
}
