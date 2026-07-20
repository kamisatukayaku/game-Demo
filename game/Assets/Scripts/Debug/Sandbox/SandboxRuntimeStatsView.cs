using System.Text;

using Game.Modes.Roguelike.Archetypes.Warrior;
using Game.Modes.Roguelike.Build.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  /// <summary>顶部实时属性（最终 Runtime 计算值，非配置）。</summary>
  public class SandboxRuntimeStatsView
  {
    readonly Text _text;

    public SandboxRuntimeStatsView(Text text) => _text = text;

    public void Refresh()
    {
      if (_text == null)
        return;

      WeaponThemeDatabase.EnsureLoaded();
      AttackProfileDatabase.EnsureLoaded();

      var themeId = RunBuildState.WeaponTheme;
      var theme = WeaponThemeDatabase.Get(themeId);
      var profile = theme != null && !string.IsNullOrEmpty(theme.attack_profile_id)
        ? AttackProfileDatabase.Get(theme.attack_profile_id)
        : null;

      var baseDmg = profile?.base_damage ?? 0f;
      var dmgMult = RunBuildState.GetWeaponDamageMult() * RunBuildCombatHooks.GetEffectiveDamageMult();
      var attack = baseDmg * dmgMult + RunBuildState.GetWeaponFlatAdd();

      var cooldown = profile?.cooldown ?? 1f;
      var atkSpd = RunBuildCombatHooks.GetEffectiveAttackSpeedMult() / Mathf.Max(0.05f, cooldown);

      var critChance = RunBuildCombatHooks.GetEffectiveCritChance();
      var critDmg = RunBuildState.GetCritDamageMult();
      var projCount = (profile?.projectile_count ?? 1) + RunBuildState.GetWeaponExtraProjectiles();
      var rangeMult = RunBuildState.GetSkillRangeMult();

      var sb = new StringBuilder();
      sb.Append($"HP: ∞    ");
      sb.Append($"攻击力: {attack:0.#}    ");
      sb.Append($"攻速: {atkSpd:0.##}    ");
      sb.Append($"暴击率: {critChance * 100f:0.#}%    ");
      sb.Append($"暴击伤害: {critDmg * 100f:0.#}%    ");
      sb.Append($"投射物数量: {projCount}    ");
      sb.Append($"范围倍率: {rangeMult * 100f:0.#}%");

      // Warrior-specific runtime stats
      if (themeId == "warrior" && WarriorProgressionDatabase.IsValid)
      {
        var ctx = WarriorContext.FromBuild();
        sb.AppendLine();
        sb.Append($"● 卫星数: {ctx.EffectiveWeaponCount}    ");
        sb.Append($"尺寸: {ctx.EffectiveWeaponSize:0.##}    ");
        sb.Append($"半径: {ctx.Radius:0.##}    ");
        sb.Append($"伤害: {ctx.EffectiveDamage:0.#}    ");
        if (ctx.SpiritEnabled)
        {
          var launchCount = ctx.SpiritLaunchCount + 1; // +1 base
          if (ctx.SpiritLaunchCount >= 99) launchCount = ctx.EffectiveWeaponCount;
          sb.Append($"灵刃数: {launchCount}    ");
          sb.Append($"灵刃速: {ctx.SpiritBladeSpeed:0.#}/{ctx.SpiritBladeReturnSpeed:0.#}    ");
        }
      }

      _text.text = sb.ToString();
    }
  }
}
