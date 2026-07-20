using System.Text;

using Game.Modes.Roguelike.Progression;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  /// <summary>右侧职业说明（数据来自 weapon_themes + player_class_skills JSON）。</summary>
  public class SandboxClassDescriptionPanel
  {
    readonly Text _text;
    string _weaponTheme;

    public SandboxClassDescriptionPanel(Text text) => _text = text;

    public void SetTheme(string weaponTheme) => _weaponTheme = weaponTheme;

    public void Refresh()
    {
      if (_text == null)
        return;

      WeaponThemeDatabase.EnsureLoaded();
      PlayerClassSkillDatabase.EnsureLoaded();

      var theme = WeaponThemeDatabase.Get(_weaponTheme);
      var sb = new StringBuilder();

      if (theme != null)
      {
        sb.AppendLine(theme.display_name);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(theme.description))
          sb.AppendLine(theme.description);
      }
      else
      {
        sb.AppendLine(_weaponTheme ?? "-");
        sb.AppendLine();
        sb.AppendLine("（未找到 weapon_themes 配置）");
      }

      var skills = PlayerClassSkillDatabase.Get(_weaponTheme);
      if (skills?.slots != null && skills.slots.Length > 0)
      {
        sb.AppendLine();
        foreach (var slot in skills.slots)
        {
          if (slot == null || string.IsNullOrWhiteSpace(slot.display_name))
            continue;

          sb.AppendLine(slot.display_name);
          if (slot.cooldown > 0f)
            sb.AppendLine($"  冷却 {slot.cooldown:0.#} 秒");
          if (slot.base_radius > 0f)
            sb.AppendLine($"  半径 {slot.base_radius:0.#} 米");
          if (slot.duration > 0f)
            sb.AppendLine($"  持续 {slot.duration:0.#} 秒");
        }
      }

      _text.text = sb.ToString().TrimEnd();
    }
  }
}
