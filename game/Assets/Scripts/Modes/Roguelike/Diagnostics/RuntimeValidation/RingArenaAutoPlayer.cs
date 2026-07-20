using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using UnityEngine;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>
  /// Permitted auto-player: orbit movement, flee threats, dash on proximity, auto-pick upgrades.
  /// No invincibility or direct damage.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class RingArenaAutoPlayer : MonoBehaviour
  {
    [SerializeField] float orbitRadius = 8.5f;
    [SerializeField] float orbitSpeed = 0.85f;
    [SerializeField] float threatFleeDistance = 5.5f;
    [SerializeField] float dashThreatDistance = 4f;

    float _orbitAngle;
    Transform _player;

    public static RingArenaAutoPlayer Ensure()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
        return null;

      var existing = player.GetComponent<RingArenaAutoPlayer>();
      if (existing != null)
      {
        EnsurePlayerCombatReady(player);
        return existing;
      }

      var auto = player.AddComponent<RingArenaAutoPlayer>();
      auto._player = player.transform;
      RuntimeValidationSettings.EnableSyntheticInput();
      EnsurePlayerCombatReady(player);
      return auto;
    }

    static void EnsurePlayerCombatReady(GameObject player)
    {
      var autoAttack = player.GetComponent<PlayerAutoAttack>();
      if (autoAttack != null)
        autoAttack.enabled = true;

      var applier = player.GetComponent<RunBuildApplier>();
      if (applier != null)
        applier.Apply();
    }

    void Awake()
    {
      _player = transform;
      RuntimeValidationSettings.EnableSyntheticInput();
      EnsurePlayerCombatReady(gameObject);
    }

    void OnDestroy()
    {
      GameInputBindings.ClearSyntheticInput();
      RuntimeValidationSettings.DisableSyntheticInput();
    }

    void Update()
    {
      ValidationBlockingUiAutoResponder.Tick();
      RuntimeValidationCombatAssist.Tick();
      if (_player == null || LevelUpController.IsWaiting)
      {
        GameInputBindings.SetSyntheticMoveVector(Vector2.zero);
        return;
      }

      var center = Vector3.zero;
      if (CircleArenaController.IsActive)
        center = CircleArenaController.Center;

      _orbitAngle += orbitSpeed * Time.deltaTime;
      var orbitTarget = center + new Vector3(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle), 0f) * orbitRadius;
      var flee = ComputeFleeVector();
      var toOrbit = (Vector2)(orbitTarget - _player.position);
      var move = toOrbit.normalized * 0.38f + flee * 1.05f;

      if (move.sqrMagnitude > 0.0001f)
        move = move.normalized;

      GameInputBindings.SetSyntheticMoveVector(move);

      var nearest = FindNearestThreatDistance();
      if (nearest <= dashThreatDistance && Game.Modes.Roguelike.Gameplay.Player.PlayerDashController.IsReady)
        GameInputBindings.QueueSyntheticDash();

      TickTelemetry();
    }

    Vector2 ComputeFleeVector()
    {
      var pos = (Vector2)_player.position;
      var flee = Vector2.zero;
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return flee;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null || !enemy.gameObject.activeInHierarchy)
          continue;

        var delta = pos - (Vector2)enemy.transform.position;
        var dist = delta.magnitude;
        if (dist > threatFleeDistance || dist < 0.01f)
          continue;

        flee += delta.normalized * (1f - dist / threatFleeDistance);
      }

      return flee.sqrMagnitude > 1f ? flee.normalized : flee;
    }

    float FindNearestThreatDistance()
    {
      var pos = (Vector2)_player.position;
      var best = float.MaxValue;
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return best;

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null || !enemy.gameObject.activeInHierarchy)
          continue;

        var dist = Vector2.Distance(pos, enemy.transform.position);
        if (dist < best)
          best = dist;
      }

      return best;
    }

    void TickTelemetry()
    {
      if (WaveDirector.Instance == null)
        return;

      RuntimeValidationTelemetry.RecordWaveDirectorState(
        WaveDirector.Instance.CurrentPhase,
        WaveDirector.Instance.CurrentWave);

      var registry = CombatRoot.EnemyRegistry;
      if (registry != null)
        RuntimeValidationTelemetry.RecordLiveEnemyCount(registry.AllEnemies.Count);
    }
  }
}
