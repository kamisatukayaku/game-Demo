using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Debugging;
using Game.Shared.Core;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.DevTools.Sandbox
{
  public class SandboxRuntimeMonitorView
  {
    readonly Text _text;
    SandboxAutoCombat _autoCombat;
    GameObject _player;

    public SandboxRuntimeMonitorView(Text text) => _text = text;

    public void Bind(GameObject player, SandboxAutoCombat autoCombat)
    {
      _player = player;
      _autoCombat = autoCombat;
    }

    public void Refresh()
    {
      if (_text == null)
        return;

      var sb = new StringBuilder();
      sb.AppendLine("Runtime Monitor");
      sb.AppendLine("───────────────");

      var theme = RunBuildState.WeaponTheme;
      sb.AppendLine($"Class: {theme}");
      sb.AppendLine($"Skill T{RunBuildState.SkillTier}  Player T{RunBuildState.PlayerTier}");
      sb.AppendLine($"Tags: {RunBuildState.ActiveTags.Count}");

      sb.AppendLine();
      sb.AppendLine("Archetypes");
      sb.AppendLine($"  Mage: {(RoguelikeDebugBridge.IsMageActive ? "ON" : "off")}");
      sb.AppendLine($"  Range: {(RoguelikeDebugBridge.IsRangedActive ? "ON" : "off")}");

      sb.AppendLine();
      sb.AppendLine("Combat");
      sb.AppendLine($"  Enemies: {CountLivingEnemies()}");
      sb.AppendLine($"  Auto: {(_autoCombat != null && _autoCombat.AutoCombatEnabled ? "ON" : "OFF")}");

      if (_player != null)
      {
        var hp = _player.GetComponent<Health>();
        if (hp != null)
          sb.AppendLine($"  Player HP: {hp.CurrentHp:0}/{hp.MaxHp:0}");
      }

      sb.AppendLine();
      sb.AppendLine("Picks");
      var pickCount = 0;
      foreach (var _ in RunBuildState.PickStacks)
        pickCount++;
      sb.AppendLine($"  Upgrades taken: {pickCount}");

      _text.text = sb.ToString();
    }

    static int CountLivingEnemies()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return 0;

      var count = 0;
      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;
        var hp = enemy.GetComponent<Health>();
        if (hp == null || !hp.IsDead)
          count++;
      }

      return count;
    }
  }
}
