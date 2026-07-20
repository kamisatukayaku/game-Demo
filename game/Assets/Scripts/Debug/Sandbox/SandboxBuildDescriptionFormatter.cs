using System.Collections.Generic;
using System.Text;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Progression;
using UnityEngine;

namespace Game.DevTools.Sandbox
{
  /// <summary>从 upgrade JSON 字段 + modifiers 生成中文 Build 描述。</summary>
  public static class SandboxBuildDescriptionFormatter
  {
    static readonly Dictionary<string, string> StatLabels = new()
    {
      [StatKeys.WeaponDamageMult] = "攻击力",
      [StatKeys.WeaponAttackSpeedMult] = "攻速",
      [StatKeys.CritChance] = "暴击率",
      [StatKeys.CritDamageMult] = "暴击伤害",
      [StatKeys.MoveSpeedMult] = "移速",
      [StatKeys.MaxHpMult] = "最大生命",
      [StatKeys.MaxHpFlat] = "最大生命",
      [StatKeys.WeaponExtraProjectile] = "投射物数量",
      [StatKeys.ProjectileSpeedMult] = "投射物速度",
      [StatKeys.ProjectilePierce] = "穿透",
      [StatKeys.SkillRangeMult] = "范围倍率",
      [StatKeys.WeaponRangeAdd] = "攻击范围",
      [StatKeys.SkillCooldownReduce] = "冷却缩减",
      [StatKeys.ProjectileBurnDps] = "DOT伤害",
      [StatKeys.SkillBurnDps] = "DOT伤害",
      [StatKeys.MeleeBurnDps] = "DOT伤害",
      [StatKeys.AllDamageMult] = "全伤害",
      [StatKeys.SkillDamageMult] = "技能伤害",
      [StatKeys.ExplosionDamageMult] = "爆炸伤害",
      [StatKeys.Lifesteal] = "吸血",
      // ── Warrior rework stats ──
      [StatKeys.WarriorWeaponCount] = "星环数量",
      [StatKeys.WarriorRotationSpeed] = "旋转速度",
      [StatKeys.WarriorRadius] = "轨道半径",
      [StatKeys.WarriorDamage] = "星环伤害",
      [StatKeys.WarriorWeaponSize] = "武器体积",
      [StatKeys.WarriorRangeExpansion] = "范围扩展",
      [StatKeys.WarriorOrbitSplashRatio] = "溅射比例",
      [StatKeys.WarriorOrbitSyncBonus] = "轨道共鸣",
      [StatKeys.WarriorSpiritBladeCount] = "灵刃数量",
      [StatKeys.WarriorSpiritBladePierce] = "灵刃穿透",
      [StatKeys.WarriorSpiritBladeReturnSpeed] = "灵刃归返速度",
      [StatKeys.WarriorSpiritBladeSpeed] = "灵刃飞行速度",
      [StatKeys.MeleeExplosionRadius] = "爆炸半径",
      [StatKeys.MeleeKnockbackChance] = "击退几率",
      [StatKeys.WarriorProjectileBounce] = "弹射次数",
    };

    public static string FormatActiveBuild(string weaponTheme)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      var sb = new StringBuilder();
      var any = false;

      foreach (var kv in RunBuildState.PickStacks)
      {
        if (kv.Value <= 0)
          continue;

        var def = LevelUpChoiceDatabase.FindById(kv.Key);
        if (def == null)
          continue;

        var line = FormatUpgradeLine(def, kv.Value);
        if (string.IsNullOrEmpty(line))
          continue;

        sb.AppendLine(line);
        any = true;
      }

      if (!any)
        sb.AppendLine("（未选择升级，点击左侧 Build 树添加）");

      return sb.ToString().TrimEnd();
    }

    public static string FormatUpgradeLine(LevelUpChoiceDatabase.UpgradeDef def, int stacks)
    {
      if (def == null)
        return null;

      var stackSuffix = stacks > 1 ? $" x{stacks}" : "";
      if (!string.IsNullOrWhiteSpace(def.description))
        return $"{def.description}{stackSuffix}";

      if (!string.IsNullOrWhiteSpace(def.display_name))
        return $"{def.display_name}{stackSuffix}";

      if (def.modifiers == null || def.modifiers.Length == 0)
        return null;

      var parts = new List<string>();
      foreach (var mod in def.modifiers)
      {
        var part = FormatModifier(mod);
        if (!string.IsNullOrEmpty(part))
          parts.Add(part);
      }

      if (parts.Count == 0)
        return def.id + stackSuffix;

      return string.Join("，", parts) + stackSuffix;
    }

    static string FormatModifier(LevelUpChoiceDatabase.StatModifier mod)
    {
      if (mod == null || string.IsNullOrEmpty(mod.stat))
        return null;

      if (!StatLabels.TryGetValue(mod.stat, out var label))
        label = mod.stat.Replace('_', ' ');

      if (mod.op == "mul")
        return $"{label} +{mod.value * 100f:0.#}%";

      if (mod.stat.Contains("chance") || mod.stat.Contains("_pct"))
        return $"{label} +{mod.value * 100f:0.#}%";

      if (Mathf.Abs(mod.value) >= 1f && mod.value == Mathf.Round(mod.value))
        return $"{label} +{(int)mod.value}";

      return $"{label} +{mod.value:0.##}";
    }
  }
}
