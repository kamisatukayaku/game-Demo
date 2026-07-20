using UnityEngine;
using Game.Shared.Gameplay.Bridges;

namespace Game.DevTools.Sandbox
{
  /// <summary>
  /// ArchetypeSandbox 场景入口：挂载于 SandboxBootstrap，引用各 UI / 战斗区域锚点。
  /// </summary>
  [DisallowMultipleComponent]
  public class SandboxBootstrap : MonoBehaviour
  {
    public static SandboxBootstrap Instance { get; private set; }

    [SerializeField] Transform buildPanel;
    [SerializeField] Transform contextInspectorPanel;
    [SerializeField] Transform runtimeValidationPanel;
    [SerializeField] Transform vfxPreviewPanel;
    [SerializeField] Transform combatArena;
    [SerializeField] string weaponTheme = "ranged";

    public Transform BuildPanel => buildPanel;
    public Transform ContextInspectorPanel => contextInspectorPanel;
    public Transform RuntimeValidationPanel => runtimeValidationPanel;
    public Transform VfxPreviewPanel => vfxPreviewPanel;
    public Transform CombatArena => combatArena;
    public string WeaponTheme => weaponTheme;

    void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(gameObject);
        return;
      }

      Instance = this;
      CombatDebugHookLocator.MageHook = SandboxRuntimeHooks.Mage;
      CombatDebugHookLocator.RangeHook = SandboxRuntimeHooks.Range;
      ResolveMissingReferences();
    }

    void Start()
    {
      SandboxWindow.Open(transform, weaponTheme, this);
    }

    void OnDestroy()
    {
      if (Instance == this)
      {
        CombatDebugHookLocator.Clear();
        Instance = null;
      }
    }

    void ResolveMissingReferences()
    {
      buildPanel ??= transform.Find("BuildPanel");
      contextInspectorPanel ??= transform.Find("ContextInspectorPanel");
      runtimeValidationPanel ??= transform.Find("RuntimeValidationPanel");
      vfxPreviewPanel ??= transform.Find("VfxPreviewPanel");
      combatArena ??= transform.Find("CombatArena");
    }
  }
}
