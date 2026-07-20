using System.Text;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  public class SandboxDpsView
  {
    readonly Text _text;

    public SandboxDpsView(Text text)
    {
      _text = text;
      SandboxCombatMetrics.Changed += Refresh;
    }

    public void Dispose() => SandboxCombatMetrics.Changed -= Refresh;

    public void Refresh()
    {
      if (_text == null)
        return;

      var sb = new StringBuilder();
      sb.AppendLine("DPS Statistics");
      sb.AppendLine("───────────────");
      sb.AppendLine($"当前 DPS: {SandboxCombatMetrics.CurrentDps:0.0}");
      sb.AppendLine($"10秒平均 DPS: {SandboxCombatMetrics.Average10sDps:0.0}");
      sb.AppendLine($"累计伤害: {SandboxCombatMetrics.TotalDamage:0}");
      sb.AppendLine($"Hits: {SandboxCombatMetrics.HitCount}");
      sb.AppendLine($"Session: {SandboxCombatMetrics.SessionSeconds:0.0}s");
      _text.text = sb.ToString();
    }
  }
}
