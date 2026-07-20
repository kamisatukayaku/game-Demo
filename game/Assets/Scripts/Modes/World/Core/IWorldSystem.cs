using System;

namespace Game.World
{
  /// <summary>
  /// World 模式子系统的统一生命周期接口?
  ///
  /// 所?World 专属系统（营地、经济、事件、局外成长树等）均实现此接口?
  /// ?<see cref="WorldManager"/> 统一驱动生命周期?
  ///
  /// 设计约束?
  ///   - 实现类不得自行在 Awake/Start 中初始化，必须由 WorldManager 显式调用?
  ///   - 实现类不得依?static singleton，应从构造函数或 Init() 注入依赖?
  /// </summary>
  public interface IWorldSystem
  {
    /// <summary>
    /// 系统初始化，?WorldManager ?World 模式启动时调用?
    /// 此时 WorldRuntimeContext ?WorldDatabase 已就绪?
    /// </summary>
    void Initialize();

    /// <summary>
    /// 每帧更新，由 WorldManager.UnityUpdate 驱动?
    /// 频率?WorldManager 控制（支持暂?时间缩放）?
    /// </summary>
    /// <param name="deltaTime">经过的时间（秒），已考虑暂停和缩?/param>
    void Tick(float deltaTime);

    /// <summary>
    /// World 模式暂停（如打开 UI、升级弹窗）?
    /// 系统应在暂停期间停止自主逻辑（如营地成长计时）?
    /// </summary>
    void OnPause();

    /// <summary>
    /// World 模式恢复?
    /// </summary>
    void OnResume();

    /// <summary>
    /// World 模式结束（胜?失败/放弃），清理本系统资源?
    /// </summary>
    void Shutdown();
  }

  /// <summary>
  /// 可选扩展接口：World 系统如果需要在 WorldGenerator 生成地图后接收回调?
  /// 典型实现：营地系统根据生成的地图数据放置营地实体?
  /// </summary>
  public interface IWorldSystem_OnWorldGenerated
  {
    /// <summary>
    /// 世界地图生成完毕后回调?
    /// 此时 WorldRuntimeContext.MapData 已就绪?
    /// </summary>
    void OnWorldGenerated(WorldGenResult result);
  }

  /// <summary>
  /// 可选扩展接口：World 系统如果需要对玩家升级做出响应?
  /// 典型实现：局外成长树在玩家达到特定等级时解锁节点?
  /// </summary>
  public interface IWorldSystem_OnPlayerLevelUp
  {
    /// <summary>
    /// 玩家在世界模式中升级时回调?
    /// 注意：这?World 模式的玩家等级（独立?Arena ?ExperienceSystem）?
    /// </summary>
    /// <param name="newLevel">新等?/param>
    void OnPlayerWorldLevelUp(int newLevel);
  }
}
