using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  /// <summary>左下角当前 Build 增益列表（数据来自 upgrade JSON）。</summary>
  public class SandboxBuildDescriptionPanel
  {
    readonly Text _text;
    string _weaponTheme;

    public SandboxBuildDescriptionPanel(Text text) => _text = text;

    public void SetTheme(string weaponTheme) => _weaponTheme = weaponTheme;

    public void Refresh()
    {
      if (_text == null)
        return;

      _text.text = SandboxBuildDescriptionFormatter.FormatActiveBuild(_weaponTheme);
    }
  }
}
