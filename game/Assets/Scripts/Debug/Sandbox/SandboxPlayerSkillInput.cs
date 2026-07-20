using UnityEngine;

namespace Game.DevTools.Sandbox
{
  /// <summary>沙盒中键盘 1-4 释放法师/光环技能（不依赖 MainScene 输入门控）。</summary>
  [DisallowMultipleComponent]
  public class SandboxPlayerSkillInput : MonoBehaviour
  {
    SandboxSkillCastService _skills;

    void Awake() => _skills = GetComponent<SandboxSkillCastService>();

    void Update()
    {
      if (!SandboxMode.Active || _skills == null)
        return;

      if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        _skills.ToggleSlot(0);
      else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        _skills.ToggleSlot(1);
      else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        _skills.ToggleSlot(2);
      else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        _skills.ToggleSlot(3);
    }
  }
}
