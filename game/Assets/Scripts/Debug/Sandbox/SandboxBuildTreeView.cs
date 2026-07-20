using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;

namespace Game.DevTools.Sandbox
{
  public class SandboxBuildTreeView
  {
    readonly RectTransform _content;
    readonly Action<LevelUpChoiceDatabase.UpgradeDef> _onApply;
    readonly List<LevelUpChoiceDatabase.UpgradeDef> _upgrades = new();

    static readonly (string group, string label)[] GroupOrder =
    {
      ("career", "职业强化"),
      ("player", "角色强化"),
      ("detached", "外置武器强化"),
      ("numeric", "数值强化"),
      ("other", "其他"),
    };

    public SandboxBuildTreeView(RectTransform content, Action<LevelUpChoiceDatabase.UpgradeDef> onApply)
    {
      _content = content;
      _onApply = onApply;
    }

    public void Load(string weaponTheme)
    {
      _upgrades.Clear();
      var allByRoute = LevelUpChoiceDatabase.GetAllUpgradesForClass(weaponTheme);

      foreach (var kv in allByRoute)
      {
        foreach (var def in kv.Value)
        {
          if (def == null || string.IsNullOrEmpty(def.id))
            continue;
          if (def.modifiers == null || def.modifiers.Length == 0)
            continue;
          if (kv.Key == "equipment" && !string.IsNullOrEmpty(def.weapon_theme)
              && def.weapon_theme != weaponTheme)
            continue;
          _upgrades.Add(def);
        }
      }
    }

    public void Rebuild(
      Action<string, RectTransform, float> createRouteHeader,
      Action<string, RectTransform, float, bool, Action> createUpgradeButton)
    {
      if (_content == null)
        return;

      for (var i = _content.childCount - 1; i >= 0; i--)
        UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

      var y = -4f;
      foreach (var (group, label) in GroupOrder)
      {
        var groupUpgrades = _upgrades.FindAll(u => ResolveSandboxGroup(u) == group);
        if (groupUpgrades.Count == 0)
          continue;

        createRouteHeader?.Invoke(label, _content, y);
        y -= 24f;

        groupUpgrades.Sort((a, b) =>
        {
          var routeCompare = string.CompareOrdinal(
            LevelUpChoiceDatabase.ResolveRoute(a),
            LevelUpChoiceDatabase.ResolveRoute(b));
          if (routeCompare != 0)
            return routeCompare;
          var tierCompare = a.tier.CompareTo(b.tier);
          return tierCompare != 0
            ? tierCompare
            : string.CompareOrdinal(a.display_name, b.display_name);
        });

        foreach (var def in groupUpgrades)
        {
          var stacks = RunBuildState.GetPickCount(def.id);
          var suffix = stacks > 0 ? $" x{stacks}" : "";
          var routeLabel = ResolveRouteLabel(LevelUpChoiceDatabase.ResolveRoute(def));
          var labelText = $"[{routeLabel}] T{def.tier} {def.display_name}{suffix}";
          var captured = def;
          createUpgradeButton?.Invoke(labelText, _content, y, stacks > 0, () => _onApply?.Invoke(captured));
          y -= 30f;
        }

        y -= 6f;
      }

      _content.sizeDelta = new Vector2(0f, Mathf.Abs(y) + 8f);
    }

    static string ResolveSandboxGroup(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return "other";
      if (UpgradeOfferGroupPolicy.Resolve(def) == UpgradeOfferGroup.Player)
        return "player";
      if (IsDetachedWeaponUpgrade(def))
        return "detached";
      if (IsCareerUpgrade(def))
        return "career";
      if (IsNumeric(def))
        return "numeric";
      return "other";
    }

    static bool IsCareerUpgrade(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;

      var route = LevelUpChoiceDatabase.ResolveRoute(def);
      if (string.Equals(route, "skill", StringComparison.OrdinalIgnoreCase))
        return true;
      if (ContainsKey(def.id, "mage")
          || ContainsKey(def.id, "ranged")
          || ContainsKey(def.id, "ranger")
          || ContainsKey(def.weapon_theme, "mage")
          || ContainsKey(def.weapon_theme, "ranged")
          || ContainsTag(def, "spell")
          || ContainsTag(def, "projectile"))
        return true;

      if (def.classes != null)
      {
        foreach (var cls in def.classes)
          if (ContainsKey(cls, "mage") || ContainsKey(cls, "ranged") || ContainsKey(cls, "ranger"))
            return true;
      }

      return false;
    }

    static bool IsDetachedWeaponUpgrade(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;

      if (ContainsKey(def.id, "num_part")
          || ContainsKey(def.id, "detached")
          || ContainsKey(def.id, "contact_core")
          || ContainsKey(def.mechanic_id, "detached")
          || ContainsTag(def, "detached_weapon")
          || ContainsTag(def, "part_damage")
          || ContainsTag(def, "part_count")
          || ContainsModifierStat(def, "detached_"))
        return true;

      if (ContainsKey(def.id, "evo_")
          || ContainsKey(def.mechanic_id, "laser")
          || ContainsKey(def.mechanic_id, "missile")
          || ContainsKey(def.mechanic_id, "boomerang")
          || ContainsKey(def.mechanic_id, "pulse")
          || ContainsKey(def.mechanic_id, "trail"))
        return true;

      return false;
    }

    static bool IsNumeric(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return false;
      if (string.Equals(def.category, "attribute", StringComparison.OrdinalIgnoreCase)
          || string.Equals(def.category, "numeric", StringComparison.OrdinalIgnoreCase))
        return true;
      if (def.tags == null)
        return false;
      foreach (var tag in def.tags)
      {
        if (string.Equals(tag, "numeric", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "attribute", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "stat", StringComparison.OrdinalIgnoreCase))
          return true;
      }
      return false;
    }

    static bool ContainsModifierStat(LevelUpChoiceDatabase.UpgradeDef def, string value)
    {
      if (def?.modifiers == null)
        return false;
      foreach (var modifier in def.modifiers)
        if (ContainsKey(modifier?.stat, value))
          return true;
      return false;
    }

    static bool ContainsTag(LevelUpChoiceDatabase.UpgradeDef def, string value)
    {
      if (def?.tags == null)
        return false;
      foreach (var tag in def.tags)
        if (ContainsKey(tag, value))
          return true;
      return false;
    }

    static bool ContainsKey(string text, string value) =>
      !string.IsNullOrEmpty(text)
      && !string.IsNullOrEmpty(value)
      && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    static string ResolveRouteLabel(string route)
    {
      return route switch
      {
        "equipment" => "外置",
        "skill" => "技能",
        "player" => "玩家",
        _ => route
      };
    }
  }
}
