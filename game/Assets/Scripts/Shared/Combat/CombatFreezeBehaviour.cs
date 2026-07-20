using UnityEngine;

namespace Game.Shared.Combat
{
  /// <summary>标记实体不参与移动/攻击（沙盒木桩等 DevTools 场景使用）。</summary>
  [DisallowMultipleComponent]
  public class CombatFreezeBehaviour : MonoBehaviour
  {
  }
}
