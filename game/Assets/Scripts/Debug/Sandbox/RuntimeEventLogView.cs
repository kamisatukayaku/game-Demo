using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  public class RuntimeEventLogView
  {
    readonly Text _text;
    readonly StringBuilder _sb = new();

    public RuntimeEventLogView(Text text)
    {
      _text = text;
      CombatDebugBus.OnEvent += OnEvent;
    }

    public void Dispose() => CombatDebugBus.OnEvent -= OnEvent;

    void OnEvent(DebugEvent evt) => Refresh();

    public void Refresh()
    {
      if (_text == null)
        return;

      _sb.Clear();
      var history = CombatDebugBus.History;
      var start = Mathf.Max(0, history.Count - 80);

      for (var i = start; i < history.Count; i++)
      {
        var evt = history[i];
        _sb.Append('[').Append(evt.Time.ToString("0.00")).Append("] ")
          .AppendLine(evt.Description);
      }

      _text.text = _sb.Length > 0 ? _sb.ToString() : "(no events yet)";
    }
  }
}
