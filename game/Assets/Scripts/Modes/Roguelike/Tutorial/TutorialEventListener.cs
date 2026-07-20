using UnityEngine;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>Subscribes to gameplay events and forwards tutorial triggers to the director.</summary>
  public sealed class TutorialEventListener : MonoBehaviour
  {
    static TutorialEventListener s_instance;

    EventListenerHandle _movedHandle;
    EventListenerHandle _dashedHandle;
    EventListenerHandle _damageHandle;
    EventListenerHandle _xpPickupHandle;
    EventListenerHandle _upgradeHandle;
    EventListenerHandle _zoneSpawnHandle;
    EventListenerHandle _zoneEnterHandle;
    EventListenerHandle _weaponHandle;
    EventListenerHandle _weaponImpactHandle;
    EventListenerHandle _uiBlockHandle;
    EventListenerHandle _waveHandle;
    EventListenerHandle _triggerHandle;

    bool _autoAttackStepOffered;
    bool _xpStepOffered;
    bool _levelUpUiOpen;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_TutorialEventListener");
      DontDestroyOnLoad(go);
      go.AddComponent<TutorialEventListener>();
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

      _movedHandle = GameEventBus.Subscribe<PlayerMovedEvent>(OnPlayerMoved);
      _dashedHandle = GameEventBus.Subscribe<PlayerDashedEvent>(OnPlayerDashed);
      _damageHandle = GameEventBus.Subscribe<DamageEvent>(OnDamage);
      _xpPickupHandle = GameEventBus.Subscribe<XpPickupCollectedEvent>(OnXpPickup);
      _upgradeHandle = GameEventBus.Subscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
      _zoneSpawnHandle = GameEventBus.Subscribe<GroundZoneSpawnedEvent>(OnZoneSpawned);
      _zoneEnterHandle = GameEventBus.Subscribe<GroundZoneEnteredEvent>(OnZoneEntered);
      _weaponHandle = GameEventBus.Subscribe<DetachedWeaponAcquiredEvent>(OnWeaponAcquired);
      _weaponImpactHandle = GameEventBus.Subscribe<DetachedWeaponImpactEvent>(OnWeaponImpact);
      _uiBlockHandle = GameEventBus.Subscribe<TutorialUiBlockingEvent>(OnUiBlocking);
      _waveHandle = GameEventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
      _triggerHandle = GameEventBus.Subscribe<TriggerActivatedEvent>(OnTrigger);
    }

    void OnDestroy()
    {
      GameEventBus.Unsubscribe(_movedHandle);
      GameEventBus.Unsubscribe(_dashedHandle);
      GameEventBus.Unsubscribe(_damageHandle);
      GameEventBus.Unsubscribe(_xpPickupHandle);
      GameEventBus.Unsubscribe(_upgradeHandle);
      GameEventBus.Unsubscribe(_zoneSpawnHandle);
      GameEventBus.Unsubscribe(_zoneEnterHandle);
      GameEventBus.Unsubscribe(_weaponHandle);
      GameEventBus.Unsubscribe(_weaponImpactHandle);
      GameEventBus.Unsubscribe(_uiBlockHandle);
      GameEventBus.Unsubscribe(_waveHandle);
      GameEventBus.Unsubscribe(_triggerHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void OnWaveStarted(WaveStartedEvent evt)
    {
      if (evt.WaveNumber == 1)
        RoguelikeTutorialDirector.Instance?.OnArenaEntered();
    }

    void OnPlayerMoved(PlayerMovedEvent evt)
    {
      if (evt.Distance < 0.15f)
        return;
      RoguelikeTutorialDirector.Instance?.OnPlayerMoved();
    }

    void OnPlayerDashed(PlayerDashedEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnPlayerDashed();

    void OnDamage(DamageEvent evt)
    {
      if (evt.Amount <= 0f)
        return;

      if (IsPlayerAttacker(evt.Attacker))
      {
        if (!_autoAttackStepOffered)
        {
          _autoAttackStepOffered = true;
          RoguelikeTutorialDirector.Instance?.OnEnemyInAttackRange();
        }
        RoguelikeTutorialDirector.Instance?.OnAutoAttackHit();
        return;
      }

      if (IsDetachedWeapon(evt.Attacker))
        GameEventBus.Publish(new DetachedWeaponImpactEvent(evt.Attacker, evt.Position));
    }

    void OnXpPickup(XpPickupCollectedEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnXpPickupCollected();

    void OpenLevelUpUi()
    {
      if (_levelUpUiOpen)
        return;
      _levelUpUiOpen = true;
      RoguelikeTutorialDirector.Instance?.OnLevelUpUiOpened();
    }

    void OnUpgradeApplied(UpgradeAppliedEvent evt)
    {
      if (IsDetachedWeaponUpgrade(evt.UpgradeId))
        RoguelikeTutorialDirector.Instance?.OnDetachedWeaponAcquired();
    }

    void OnZoneSpawned(GroundZoneSpawnedEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnGroundZoneSpawned(evt.ZoneId, evt.Center, evt.Radius);

    void OnZoneEntered(GroundZoneEnteredEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnGroundZoneEntered(evt.ZoneId);

    void OnWeaponAcquired(DetachedWeaponAcquiredEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnDetachedWeaponAcquired();

    void OnWeaponImpact(DetachedWeaponImpactEvent evt) =>
      RoguelikeTutorialDirector.Instance?.OnDetachedWeaponImpact();

    void OnUiBlocking(TutorialUiBlockingEvent evt)
    {
      RoguelikeTutorialDirector.Instance?.OnUiBlockingChanged(evt.Blocking);
      if (evt.Reason == "level_up" && evt.Blocking)
        OpenLevelUpUi();
      else if (evt.Reason == "level_up" && !evt.Blocking)
      {
        _levelUpUiOpen = false;
        RoguelikeTutorialDirector.Instance?.OnLevelUpUiClosed();
      }
    }

    void OnTrigger(TriggerActivatedEvent evt)
    {
      if (evt.TriggerId == "XpPickup")
      {
        if (!_xpStepOffered)
        {
          _xpStepOffered = true;
          RoguelikeTutorialDirector.Instance?.OnFirstXpOrbSpawned();
        }
        GameEventBus.Publish(new XpPickupCollectedEvent(evt.Position, evt.Value));
      }
    }

    static bool IsPlayerAttacker(GameObject attacker)
    {
      if (attacker == null)
        return false;
      return attacker.CompareTag("Player") || attacker.name == "Player";
    }

    static bool IsDetachedWeapon(GameObject attacker)
    {
      if (attacker == null)
        return false;
      return attacker.GetComponent<Gameplay.DetachedWeapons.DetachedWeaponController>() != null;
    }

    static bool IsDetachedWeaponUpgrade(string upgradeId)
    {
      if (string.IsNullOrEmpty(upgradeId))
        return false;
      return upgradeId.Contains("detached") || upgradeId.Contains("starter_contact");
    }
  }
}
