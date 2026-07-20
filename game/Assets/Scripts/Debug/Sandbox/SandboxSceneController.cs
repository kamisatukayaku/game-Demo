using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Debugging;
using Game.Modes.Roguelike.Gameplay;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using Game.Shared.Runtime.Physics;

namespace Game.DevTools.Sandbox
{
  [DisallowMultipleComponent]
  public class SandboxSceneController : MonoBehaviour
  {
    public static readonly Vector3 WorldOrigin = new(8200f, 8200f, 0f);

    Transform _worldRoot;
    Transform _arenaAnchor;
    GameObject _player;
    EnemySpawner _enemySpawner;
    SandboxSpawner _spawner;
    SandboxCameraController _camera;

    public GameObject Player => _player;
    public SandboxSpawner Spawner => _spawner;
    public SandboxCameraController CameraController => _camera;
    public Vector3 WorldCenter => _worldRoot != null ? _worldRoot.position : WorldOrigin;

    public SandboxAutoCombat AutoCombat { get; private set; }

    public void SetArenaAnchor(Transform arena) => _arenaAnchor = arena;

    Vector3 WorldAnchor =>
      _arenaAnchor != null ? _arenaAnchor.position : WorldOrigin;

    Vector3 PlayerSpawnLocal => Vector3.zero;

    public void EnsureBuilt(string weaponTheme)
    {
      SandboxMode.Active = true;
      EvolutionBuildGatesDatabase.BypassDetachedRouteFilterForDebug = true;
      SandboxGameplayBootstrap.EnsureInstalled();

      if (_worldRoot == null)
      {
        var rootGo = new GameObject("SandboxWorldRoot");
        rootGo.transform.localScale = Vector3.one;
        if (_arenaAnchor != null)
        {
          rootGo.transform.SetParent(_arenaAnchor, false);
          rootGo.transform.localPosition = Vector3.zero;
        }
        else
        {
          rootGo.transform.position = WorldOrigin;
        }

        _worldRoot = rootGo.transform;
      }
      else
      {
        if (_arenaAnchor != null)
        {
          _worldRoot.SetParent(_arenaAnchor, false);
          _worldRoot.localPosition = Vector3.zero;
        }
        else
        {
          _worldRoot.SetParent(null);
          _worldRoot.position = WorldOrigin;
        }
      }

      EnsureRegistry();
      EnsureSpawner();
      EnsurePlayer(weaponTheme);
      EnsureCamera();
      EnsureCombatSystems();
      RefreshDetachedWeaponPresentation();

      EnemySpawner.SpawningEnabled = true;
    }

    void EnsureCombatSystems()
    {
      if (_player == null)
        return;

      AutoCombat = _player.GetComponent<SandboxAutoCombat>();
      if (AutoCombat == null)
        AutoCombat = _player.AddComponent<SandboxAutoCombat>();

      SandboxCombatMetrics.Begin(_player);
    }

    void EnsureRegistry()
    {
      if (CombatRoot.Instance == null)
        return;

      if (CombatRoot.EnemyRegistry != null)
        return;

      var reg = CombatRoot.Instance.GetComponent<EnemyRegistry>();
      if (reg == null)
        reg = CombatRoot.Instance.gameObject.AddComponent<EnemyRegistry>();

      var field = typeof(CombatRoot).GetField("enemyRegistry",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      field?.SetValue(CombatRoot.Instance, reg);
    }

    void EnsureSpawner()
    {
      if (_enemySpawner != null)
        return;

      var go = new GameObject("SandboxEnemySpawner");
      go.transform.SetParent(_worldRoot, false);
      _enemySpawner = go.AddComponent<EnemySpawner>();
      _spawner = new SandboxSpawner(_worldRoot, _enemySpawner);
    }

    void EnsurePlayer(string weaponTheme)
    {
      if (_player != null)
      {
        _player.transform.SetParent(_worldRoot, false);
        _player.transform.localPosition = PlayerSpawnLocal;
        if (_player.GetComponent<SandboxSkillCastService>() == null)
          _player.AddComponent<SandboxSkillCastService>();
        if (_player.GetComponent<SandboxPlayerSkillInput>() == null)
          _player.AddComponent<SandboxPlayerSkillInput>();
        PlayerActiveSkillController.Ensure(_player);
        ApplyTheme(weaponTheme);
        RefreshDetachedWeaponPresentation();
        SandboxEntityRules.ConfigurePlayer(_player);
        return;
      }

      _player = new GameObject("SandboxPlayer");
      _player.tag = "Player";
      _player.transform.SetParent(_worldRoot, false);
      _player.transform.localPosition = PlayerSpawnLocal;
      _player.transform.localScale = Vector3.one;

      _player.AddComponent<Rigidbody2D>().gravityScale = 0f;
      _player.AddComponent<CircleCollider2D>().radius = 0.5f;
      _player.AddComponent<Health>().Configure(200f);
      _player.AddComponent<BuffContainer>();
      _player.AddComponent<PlayerSphereController>();
      _player.AddComponent<PlayerAutoAttack>();
      _player.AddComponent<PlayerAttackDirector>();
      _player.AddComponent<PlayerAimController>();
      _player.AddComponent<RunBuildApplier>();

      PlayerSpriteVisual.EnsureOnPlayer(_player);
      RoguelikeCombatSceneBootstrap.Instance.ApplyPlayerComponents(_player);

      if (_player.GetComponent<SandboxSkillCastService>() == null)
        _player.AddComponent<SandboxSkillCastService>();
      if (_player.GetComponent<SandboxPlayerSkillInput>() == null)
        _player.AddComponent<SandboxPlayerSkillInput>();
      PlayerActiveSkillController.Ensure(_player);

      ApplyTheme(weaponTheme);
      RefreshDetachedWeaponPresentation();
      SandboxEntityRules.ConfigurePlayer(_player);
    }

    void EnsureCamera()
    {
      if (_camera == null)
        _camera = gameObject.AddComponent<SandboxCameraController>();

      _camera.EnsureBuilt(_player.transform, _worldRoot.position);
    }

    public void ApplyTheme(string weaponTheme)
    {
      MageZonePool.ResetAll();
      ThemeState.Reset(weaponTheme);
      ThemeState.SetWeaponTheme(weaponTheme);
      RoguelikeDebugBridge.RefreshBuildContexts();

      if (_player != null)
      {
        DetachedWeaponSystem.Ensure(_player);
        var applier = _player.GetComponent<RunBuildApplier>();
        applier?.Apply();
        RefreshDetachedWeaponPresentation();
      }
    }

    void RefreshDetachedWeaponPresentation()
    {
      if (_player == null)
        return;

      DetachedWeaponPresentationSystem.EnsureExists();
      DetachedWeaponSystem.Ensure(_player);
      DetachedWeaponPresentationSystem.RefreshExistingWeapons();
    }

    public void ResetWorld()
    {
      _spawner?.ClearAll();
      if (_player != null)
        _player.transform.localPosition = PlayerSpawnLocal;
    }

    public void DisposeScene()
    {
      MageZonePool.ResetAll();
      ContactDashPresentationSystem.ResetAll();
      EvolutionBuildGatesDatabase.BypassDetachedRouteFilterForDebug = false;
      SandboxMode.Active = false;
      SandboxCombatMetrics.End();
      SandboxEntityRules.ClearPlayer(_player);
      _spawner?.ClearAll();
      _camera?.Dispose();

      if (_player != null)
        Destroy(_player);

      if (_enemySpawner != null)
        Destroy(_enemySpawner.gameObject);

      if (_worldRoot != null)
        Destroy(_worldRoot.gameObject);

      _player = null;
      _enemySpawner = null;
      _spawner = null;
      _worldRoot = null;
    }
  }
}
