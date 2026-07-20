using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Shared.UI;

namespace Game.DevTools.Sandbox
{
  public class ContextInspector
  {
    readonly Text _mageText;
    readonly Text _rangeText;

    public ContextInspector(Text mage, Text range)
    {
      _mageText = mage;
      _rangeText = range;
    }

    public void Refresh()
    {
      if (_mageText != null)
        _mageText.text = FormatStruct("MageContext", MageSystemLocator.Context);

      if (_rangeText != null)
        _rangeText.text = FormatStruct("RangedAuxiliaryContext", RangedAuxiliaryContextBuilder.Build());
    }

    static string FormatStruct<T>(string title, T ctx)
    {
      var sb = new StringBuilder();
      sb.AppendLine(title);
      sb.AppendLine("────────────");

      foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
      {
        var val = field.GetValue(ctx);
        if (val is float f)
        {
          if (Mathf.Abs(f) < 0.0001f)
            continue;
          sb.AppendLine($"{field.Name}: {f:0.###}");
        }
        else if (val is int i)
        {
          if (i == 0)
            continue;
          sb.AppendLine($"{field.Name}: {i}");
        }
        else if (val is bool b)
        {
          if (!b)
            continue;
          sb.AppendLine($"{field.Name}: {b}");
        }
        else if (val != null)
        {
          sb.AppendLine($"{field.Name}: {val}");
        }
      }

      return sb.ToString();
    }
  }
}

