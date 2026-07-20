using System.Text;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  public class FeatureChecklistView
  {
    readonly Text _text;

    public FeatureChecklistView(Text text) => _text = text;

    public void Refresh()
    {
      if (_text == null)
        return;

      var sb = new StringBuilder();
      AppendGroup(sb, "Mage", FeatureCatalog.MageFeatures);
      sb.AppendLine();
      AppendGroup(sb, "Range", FeatureCatalog.RangeFeatures);
      _text.text = sb.ToString();
    }

    static void AppendGroup(StringBuilder sb, string title, System.Collections.Generic.IReadOnlyList<FeatureExecutionRecord> features)
    {
      sb.AppendLine(title);
      foreach (var f in features)
      {
        var mark = FeatureExecutionTracker.WasExecuted(f.FeatureId) ? "x" : " ";
        sb.Append("[ ").Append(mark).Append("] ").AppendLine(f.DisplayName);
      }
    }
  }
}
