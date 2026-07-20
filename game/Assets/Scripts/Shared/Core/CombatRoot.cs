using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using Game.Shared.UI;
using Game.Shared.Gameplay.Bridges;

namespace Game.Shared.Core
{
  /// <summary>
  /// 战斗根节点。项目唯一 [RuntimeInitializeOnLoadMethod] 入口。
  /// 统一创建所有全局管理器。
  ///
  /// 初始化分两阶段：
  ///   Phase 1 (BeforeSceneLoad / Awake):  核心系统 (registries, EventSystem)
  ///   Phase 2 (MainScene 激活后):         ICombatSceneBootstrap 模式插件初始化
  /// </summary>
  public class CombatRoot : MonoBehaviour
  {
    [SerializeField] EnemyRegistry enemyRegistry;

    public static EnemyRegistry EnemyRegistry => s_instance?.enemyRegistry;

    static CombatRoot s_instance;
    public static CombatRoot Instance => s_instance;

    bool _combatInitialized;
    Coroutine _mainSceneInitRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
      if (s_instance != null) return;
      var go = new GameObject("_CombatRoot");
      go.AddComponent<CombatRoot>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
      OnStart();
    }

    void OnStart()
    {
      if (enemyRegistry == null)
        enemyRegistry = gameObject.AddComponent<EnemyRegistry>();

      UiBootstrap.EnsureEventSystem();

      GameInputBindings.EnsureLoaded();
      GameCursorVisual.EnsureExists();
    }

    void OnEnable()
    {
      SceneManager.sceneLoaded += OnSceneLoaded;
      SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
      SceneManager.sceneLoaded -= OnSceneLoaded;
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void OnSceneUnloaded(Scene scene)
    {
      if (scene.name != "MainScene")
        return;

      _combatInitialized = false;
      if (_mainSceneInitRoutine != null)
      {
        StopCoroutine(_mainSceneInitRoutine);
        _mainSceneInitRoutine = null;
      }
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      if (scene.name != "MainScene")
        return;

      RequestMainSceneInitialization(scene);
    }

    public static void RequestMainSceneInitialization(Scene scene)
    {
      if (s_instance == null)
        return;

      s_instance.BeginMainSceneInitialization(scene);
    }

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
    public static void ValidationResetMainSceneInit() => ResetMainSceneInitialization();
#endif

    public static void ResetMainSceneInitialization()
    {
      if (s_instance == null)
        return;

      s_instance._combatInitialized = false;
      if (s_instance._mainSceneInitRoutine != null)
      {
        s_instance.StopCoroutine(s_instance._mainSceneInitRoutine);
        s_instance._mainSceneInitRoutine = null;
      }
    }

    void BeginMainSceneInitialization(Scene scene)
    {
      if (_mainSceneInitRoutine != null)
        StopCoroutine(_mainSceneInitRoutine);

      _mainSceneInitRoutine = StartCoroutine(InitializeWhenMainSceneActive(scene));
    }

    IEnumerator InitializeWhenMainSceneActive(Scene scene)
    {
      while (SceneManager.GetActiveScene() != scene)
        yield return null;

      yield return null;

      GameSceneTransitionCurtain.DismissIfVisible();

      if (SceneManager.GetActiveScene().name != "MainScene")
      {
        _mainSceneInitRoutine = null;
        yield break;
      }

      if (_combatInitialized)
      {
        _mainSceneInitRoutine = null;
        yield break;
      }

      InitializeCombatSystems();
      _combatInitialized = true;
      _mainSceneInitRoutine = null;
    }

    void InitializeCombatSystems()
    {
      GameInputBindings.EnsureLoaded();
      GameCursorVisual.EnsureExists();
      KeyBindingsUI.EnsureExists();
      StreamModeSettings.EnsureExists();

      var cam = Camera.main;
      if (cam != null)
      {
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.18f, 1f);
        cam.allowHDR = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
      }

      CombatSceneBootstrapLocator.Bootstrap?.InitializeCombatSystems();
      TryApplyPlayerGameplayComponents();
    }

    static void TryApplyPlayerGameplayComponents()
    {
      var playerGo = GameObject.FindWithTag("Player");
      if (playerGo == null)
        playerGo = GameObject.Find("Player");

      if (playerGo == null)
        return;

      if (playerGo.GetComponent<PlayerAimController>() == null)
        playerGo.AddComponent<PlayerAimController>();

      PlayerSpriteVisual.EnsureOnPlayer(playerGo);

      CombatSceneBootstrapLocator.Bootstrap?.ApplyPlayerComponents(playerGo);
    }
  }
}
