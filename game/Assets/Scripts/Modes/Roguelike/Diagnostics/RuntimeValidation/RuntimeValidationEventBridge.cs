#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Events;
using Game.Shared.Gameplay.Events;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Subscribes combat events for objective runtime telemetry.</summary>
  [DisallowMultipleComponent]
  public sealed class RuntimeValidationEventBridge : MonoBehaviour
  {
    static RuntimeValidationEventBridge s_instance;

    EventListenerHandle _enemyKilledHandle;
    EventListenerHandle _playerDeathHandle;
    bool _logHooked;

    public static void EnsureExists()
    {
      if (s_instance == null)
      {
        var go = new GameObject("_RuntimeValidationEventBridge");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<RuntimeValidationEventBridge>();
        return;
      }

      if (!s_instance.gameObject.activeInHierarchy)
        s_instance.gameObject.SetActive(true);

      if (!s_instance.enabled)
        s_instance.enabled = true;

      s_instance.EnsureSubscribed();
    }

    void EnsureSubscribed()
    {
      if (!_enemyKilledHandle.Valid)
        _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
      if (!_playerDeathHandle.Valid)
        _playerDeathHandle = GameEventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
      if (!_logHooked)
      {
        Application.logMessageReceived += OnLogMessage;
        _logHooked = true;
      }
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
    }

    void OnEnable() => EnsureSubscribed();

    void OnDisable()
    {
      if (_enemyKilledHandle.Valid)
      {
        GameEventBus.Unsubscribe(_enemyKilledHandle);
        _enemyKilledHandle = default;
      }
      if (_playerDeathHandle.Valid)
      {
        GameEventBus.Unsubscribe(_playerDeathHandle);
        _playerDeathHandle = default;
      }
      if (_logHooked)
      {
        Application.logMessageReceived -= OnLogMessage;
        _logHooked = false;
      }
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    static void OnEnemyKilled(EnemyKilledEvent evt)
    {
      var isBoss = evt.IsBoss
                   || (!string.IsNullOrEmpty(evt.EnemyId)
                       && (evt.EnemyId.StartsWith("wild_boss_")
                           || evt.EnemyId.StartsWith("mini_boss_")
                           || evt.EnemyId.StartsWith("final_boss_")));
      RuntimeValidationTelemetry.RecordEnemyKill(isBoss);
    }

    static void OnPlayerDeath(PlayerDeathEvent evt) =>
      RuntimeValidationTelemetry.RecordPlayerDeath();

    static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
      if (type == LogType.Exception)
        RuntimeValidationTelemetry.RecordException();
      else if (type == LogType.Error)
        RuntimeValidationTelemetry.RecordErrorLog();
    }
  }
}
#endif
