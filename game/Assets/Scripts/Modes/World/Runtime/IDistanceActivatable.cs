using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 距离激活接??由需要距?LOD 优化的实体实现?
  ///
  /// 实现者（?CampController）将其受到的 LOD Level 变化通知?DistanceActivationSystem?
  /// 由后者统一管理视觉/物理组件的启?禁用?
  ///
  /// Level 映射?
  ///   Level0 = 超远距离 ?仅保留成长计算，关闭碰撞/Renderer/AI
  ///   Level1 = 中距禀"  ?允许刷怪，AI 休眠
  ///   Level2 = 近距禀"  ?完全激洀"
  ///
  /// ?CampController 状态机的关系：
  ///   Dormant  + Level0 ?仅成长（组件关闭?
  ///   Dormant  + Level1 ?仅成?+ 可视
  ///   Alert    + Level1 ?慢速刷?+ AI休眠
  ///   Active   + Level2 ?全速刷?+ 完全激洀"
  ///   Destroyed (any)   ?所有组件关闭"
  /// </summary>
  public enum DistanceActivationLevel
  {
    /// <summary>超远距离：仅保留成长计算，关闭碰?Renderer/AI</summary>
    Level0_Far = 0,
    /// <summary>中距离：允许刷怪，AI休眠，保留Renderer</summary>
    Level1_Mid = 1,
    /// <summary>近距离：完全激?/summary>
    Level2_Near = 2,
    /// <summary>已摧?无效：全部关?/summary>
    Inactive = -1
  }

  /// <summary>
  /// 实现此接口的实体可被 DistanceActivationSystem 管理?
  /// </summary>
  public interface IDistanceActivatable
  {
    /// <summary>实体唯一标识</summary>
    string ActivatableId { get; }

    /// <summary>世界坐标位置</summary>
    Vector2 WorldPosition { get; }

    /// <summary>当前激活等级（?DistanceActivationSystem 设置?/summary>
    DistanceActivationLevel CurrentActivationLevel { get; set; }

    /// <summary>是否?Boss（Boss 休眠而非回收?/summary>
    bool IsBossEntity { get; }

    /// <summary>切换激活等级时调用。实现者在此方法中启用/禁用组件?/summary>
    void OnActivationLevelChanged(DistanceActivationLevel newLevel, DistanceActivationLevel oldLevel);
  }
}
