using UnityEngine;

using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;

namespace Game.DevTools.Sandbox
{
  /// <summary>Combat Sandbox 编排：Build、实体、自动战斗、指标。</summary>
  public class SandboxController : MonoBehaviour
  {
    SandboxSceneController _scene;
    string _weaponTheme = "ranged";

    public SandboxSceneController Scene => _scene;
    public string WeaponTheme => _weaponTheme;
    public SandboxAutoCombat AutoCombat => _scene?.AutoCombat;

    public void Initialize(string weaponTheme, Transform combatArena = null)
    {
      _weaponTheme = string.IsNullOrEmpty(weaponTheme) ? "ranged" : weaponTheme;
      SandboxMode.Active = true;

      if (_scene == null)
        _scene = gameObject.AddComponent<SandboxSceneController>();

      if (combatArena != null)
        _scene.SetArenaAnchor(combatArena);

      RunBuildState.Reset(_weaponTheme);
      _scene.EnsureBuilt(_weaponTheme);
      ConfigureSkillAutoCastForTheme();
    }

    public void SetWeaponTheme(string theme)
    {
      _weaponTheme = string.IsNullOrEmpty(theme) ? "ranged" : theme;
      _scene?.Player?.GetComponent<SandboxSkillCastService>()?.DismissAll();
      RunBuildState.Reset(_weaponTheme);
      _scene?.ApplyTheme(_weaponTheme);
      ConfigureSkillAutoCastForTheme();
      SandboxCombatMetrics.Reset();
    }

    void ConfigureSkillAutoCastForTheme()
    {
      if (_scene?.AutoCombat == null)
        return;

      _scene.AutoCombat.AutoCastSkills = _weaponTheme == "mage";
    }

    public void SetAutoCastSkills(bool enabled)
    {
      if (_scene?.AutoCombat != null)
        _scene.AutoCombat.AutoCastSkills = enabled;
    }

    public bool IsAutoCastSkills =>
      _scene?.AutoCombat != null && _scene.AutoCombat.AutoCastSkills;

    public bool ApplyUpgrade(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;

      if (!SandboxBuildStateHelper.ToggleUpgrade(_weaponTheme, def))
        return false;

      _scene?.ApplyTheme(_weaponTheme);
      return true;
    }

    public void TriggerFeature(string triggerId) =>
      SandboxTriggerService.Trigger(triggerId, _scene);

    public void SetAutoCombat(bool enabled)
    {
      if (_scene?.AutoCombat != null)
        _scene.AutoCombat.AutoCombatEnabled = enabled;
    }

    public void SpawnDefaultTargets()
    {
      if (_scene?.Spawner == null)
        return;

      _scene.Spawner.SpawnDummy(Vector2.right * 4f);
      _scene.Spawner.SpawnDummy(Vector2.right * 5.5f + Vector2.up * 1.5f);
      _scene.Spawner.SpawnElite(Vector2.right * 7f);
    }

    public void ResetSandbox()
    {
      _scene?.Player?.GetComponent<SandboxSkillCastService>()?.DismissAll();
      _scene?.ResetWorld();
      RunBuildState.Reset(_weaponTheme);
      _scene?.ApplyTheme(_weaponTheme);
      SandboxCombatMetrics.Reset();
      SpawnDefaultTargets();
    }

    public void ResetContext()
    {
      RunBuildState.Reset(_weaponTheme);
      _scene?.ApplyTheme(_weaponTheme);
      SandboxCombatMetrics.Reset();
    }

    public void ClearEvents() => CombatDebugBus.Clear();

    public void ClearChecklist() => FeatureExecutionTracker.Clear();

    public void ResetDps() => SandboxCombatMetrics.Reset();

    public void CastSkillSlot(int index)
    {
      var service = _scene?.Player?.GetComponent<SandboxSkillCastService>();
      service?.ToggleSlot(index);
    }

    public bool IsSkillSlotActive(int index)
    {
      var service = _scene?.Player?.GetComponent<SandboxSkillCastService>();
      return service != null && service.IsSlotActive(index);
    }

    public void ResetSkillCooldowns()
    {
      var skills = _scene?.Player?.GetComponent<PlayerActiveSkillController>();
      skills?.ResetAllCooldowns();
    }

    public void Dispose()
    {
      SandboxMode.Active = false;
      SandboxCombatMetrics.End();
      _scene?.DisposeScene();
      if (_scene != null)
        Destroy(_scene);
      _scene = null;
    }
  }
}
