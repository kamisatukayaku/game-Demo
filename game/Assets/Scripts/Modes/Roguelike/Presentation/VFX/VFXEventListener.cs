using UnityEngine;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.UI;
using Game.Shared.Combat;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class VFXEventListener : MonoBehaviour
  {
    static VFXEventListener s_instance;

    EventListenerHandle _damageHandle;
    EventListenerHandle _enemyDeathHandle;
    EventListenerHandle _projectileSpawnHandle;
    EventListenerHandle _projectileHitHandle;
    EventListenerHandle _trailSegmentHandle;
    EventListenerHandle _upgradeHandle;
    EventListenerHandle _triggerHandle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeAfterSceneLoad()
    {
      EnsureExists();
    }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_RoguelikeVFXEventListener");
      DontDestroyOnLoad(go);
      go.AddComponent<VFXEventListener>();
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
      VFXManager.EnsureExists();
    }

    void OnEnable()
    {
      _damageHandle = GameEventBus.Subscribe<DamageEvent>(OnDamage);
      _enemyDeathHandle = GameEventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
      _projectileSpawnHandle = GameEventBus.Subscribe<ProjectileSpawnEvent>(OnProjectileSpawn);
      _projectileHitHandle = GameEventBus.Subscribe<ProjectileHitEvent>(OnProjectileHit);
      _trailSegmentHandle = GameEventBus.Subscribe<TrailSegmentEvent>(OnTrailSegment);
      _upgradeHandle = GameEventBus.Subscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
      _triggerHandle = GameEventBus.Subscribe<TriggerActivatedEvent>(OnTriggerActivated);
    }

    void OnDisable()
    {
      GameEventBus.Unsubscribe(_damageHandle);
      GameEventBus.Unsubscribe(_enemyDeathHandle);
      GameEventBus.Unsubscribe(_projectileSpawnHandle);
      GameEventBus.Unsubscribe(_projectileHitHandle);
      GameEventBus.Unsubscribe(_trailSegmentHandle);
      GameEventBus.Unsubscribe(_upgradeHandle);
      GameEventBus.Unsubscribe(_triggerHandle);
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    static void OnDamage(DamageEvent evt)
    {
      EnemyHitReactionVfx.Play(evt);
      if (evt.DamageSource == "detached_laser" || evt.DamageSource == "detached_prism")
        DetachedWeaponPresentationSystem.PlayLaserHitSpark(evt.Position, evt.DamageSource == "detached_prism", evt.Amount);
      if (evt.DamageSource == "detached_trail" || evt.DamageSource == "detached_trail_network")
        DetachedTrailVfx.PlayImpact(evt.Position, evt.Amount >= 45f || evt.DamageSource == "detached_trail_network");
    }

    static void OnEnemyDeath(EnemyDeathEvent evt)
    {
      // RoguelikeEnemyDeathFeedbackSystem handles death dissolve via CombatEventBus.OnKill.
    }

    static void OnProjectileSpawn(ProjectileSpawnEvent evt)
    {
      if (evt.ProjectileId == "detached_missile" || evt.ProjectileId == "detached_missile_child")
      {
        DetachedWeaponPresentationSystem.AttachMissile(evt.Projectile, evt.ProjectileId == "detached_missile_child");
        DetachedWeaponPresentationSystem.PlayMissileBurst(evt.Position, false);
        DetachedWeaponPresentationSystem.PlayMissileLock(evt.Projectile, evt.Target, evt.ProjectileId == "detached_missile_child");
        return;
      }
      VFXManager.Instance.PlayEvent("ProjectileSpawn", evt.Position);
    }

    static void OnProjectileHit(ProjectileHitEvent evt)
    {
      if (evt.ProjectileId == "detached_missile" || evt.ProjectileId == "detached_missile_child")
      {
        DetachedWeaponPresentationSystem.PlayMissileBurst(evt.Position, true);
        return;
      }
      VFXManager.Instance.PlayEvent("ProjectileHit", evt.Position);
    }

    static void OnTrailSegment(TrailSegmentEvent evt) =>
      DetachedTrailVfx.Play(evt.Start, evt.End, evt.Width, evt.Lifetime, evt.Branch, evt.Network);

    static void OnUpgradeApplied(UpgradeAppliedEvent evt)
    {
      EvolutionMomentUI.PlayIfCapstone(evt.UpgradeId);
      DetachedWeaponEvolutionVfx.Play(evt.Player, evt.UpgradeId);
      VFXManager.Instance.PlayEvent("Upgrade", evt.Position);
    }

    static void OnTriggerActivated(TriggerActivatedEvent evt)
    {
      if (!string.IsNullOrEmpty(evt.TriggerId) && evt.TriggerId.StartsWith("Monster"))
      {
        MonsterEcosystemVfx.Play(evt.TriggerId, evt.Position, evt.Scale, evt.Value, evt.Source);
        return;
      }

      if (evt.TriggerId == "XpPickup")
      {
        VFXManager.Instance.ShowNumber(evt.Position, evt.Value, DamageNumberStyle.Tech);
        return;
      }

      if (evt.TriggerId == "PlayerDamaged")
      {
        VFXManager.Instance.ShowNumber(evt.Position, evt.Value, DamageNumberStyle.Player);
        return;
      }

      if (evt.TriggerId == "MageGravityWell")
      {
        VFXManager.Instance.PlayTrigger(evt.TriggerId, evt.Position, evt.Scale, evt.Value, evt.Alternate);
        return;
      }

      if (evt.TriggerId == "DetachedExplosion" || evt.TriggerId == "DetachedNuclearExplosion")
      {
        DetachedWeaponPresentationSystem.PlayExplosionCoreFlash(evt.Source);
        DetachedExplosionVfx.Play(
          evt.Position,
          evt.Scale,
          evt.TriggerId == "DetachedNuclearExplosion",
          evt.Alternate,
          Mathf.RoundToInt(evt.Value));
        return;
      }

      if (evt.TriggerId == "DetachedPulseWave" ||
          evt.TriggerId == "DetachedArenaPulse" ||
          evt.TriggerId == "DetachedPulseCharge" ||
          evt.TriggerId == "DetachedPulseResonance")
      {
        DetachedPulseVfx.Play(
          evt.TriggerId,
          evt.Position,
          evt.Scale,
          evt.Value,
          evt.Alternate);
        return;
      }

      if (evt.TriggerId == "DetachedBoomerangAttach")
      {
        DetachedBoomerangVfx.Attach(evt.Source, evt.Alternate);
        DetachedBoomerangVfx.SetReturning(evt.Source, false);
        DetachedBoomerangVfx.PlayBurst(evt.Position, 0.65f, DetachedBoomerangVfx.BurstKind.Launch);
        return;
      }

      if (evt.TriggerId == "DetachedBoomerangTurn")
      {
        DetachedBoomerangVfx.PlayTurn(evt.Source, evt.Position, evt.Scale);
        return;
      }

      if (evt.TriggerId == "DetachedBoomerangHit")
      {
        DetachedBoomerangVfx.PlayBurst(
          evt.Position,
          evt.Scale,
          evt.Alternate ? DetachedBoomerangVfx.BurstKind.HitReturn : DetachedBoomerangVfx.BurstKind.HitOutbound);
        return;
      }

      if (evt.TriggerId == "DetachedBoomerangRecast")
      {
        DetachedBoomerangVfx.SetReturning(evt.Source, false);
        DetachedBoomerangVfx.PlayBurst(evt.Position, evt.Scale, DetachedBoomerangVfx.BurstKind.Recast);
        return;
      }

      if (evt.TriggerId == "DetachedBoomerangReturn")
      {
        DetachedBoomerangVfx.PlayBurst(evt.Position, evt.Scale, DetachedBoomerangVfx.BurstKind.Return);
        return;
      }

      if (evt.TriggerId == "MageFlameNovaWarning")
      {
        VFXManager.Instance.PlayTrigger(evt.TriggerId, evt.Position, evt.Scale, evt.Value, evt.Alternate);
        return;
      }

      if (evt.TriggerId == "MageGravityTether")
      {
        VFXManager.Instance.PlayGravityTether(evt.Position, evt.Source != null ? evt.Source.transform : null);
        return;
      }

      if (evt.TriggerId == "MageArcaneMissileAttach")
      {
        VFXManager.Instance.AttachArcaneMissile(evt.Source);
        return;
      }

      if (evt.TriggerId == "WarriorOrbitAttach")
      {
        VFXManager.Instance.AttachWarriorOrbitWeapon(
          evt.Source,
          evt.Source != null && evt.Source.transform.parent != null ? evt.Source.transform.parent.parent : null,
          Mathf.RoundToInt(evt.Value),
          1,
          evt.Scale,
          evt.Source != null ? evt.Source.transform.localScale.x : 1f,
          180f,
          evt.Alternate);
        return;
      }

      VFXManager.Instance.PlayEvent(evt.TriggerId, evt.Position, evt.Scale, evt.Alternate);
    }

  }
}
