using System;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Combat.Events;
using UnityEngine;

namespace Game.World
{
  public readonly struct WorldCombatSample
  {
    public readonly Vector2 Position;
    public readonly float Danger;

    public WorldCombatSample(Vector2 position, float danger)
    {
      Position = position;
      Danger = danger;
    }
  }
  /// <summary>
  /// World 模式 ↔ Combat 系统的桥接层。
  ///
  /// 职责：
  ///   1. 战斗事件监听——将 CombatEventBus 的击杀/伤害事件转换为 World 层事件
  ///   2. 世界等级注入——将世界等级作为怪物等级基准传给刷怪系统
  ///
  /// 不继承 MonoBehaviour，由 WorldManager 创建和管理生命周期。
  /// </summary>
  public class WorldCombatBridge : IDisposable
  {
    public WorldCombatBridge()
    {
    }

    /// <summary>
    /// 批量采样（用于视野范围内的地形查询，如小地图渲染）?
    /// </summary>
    public WorldCombatSample[] SampleRegion(Vector2 center, float radius)
    {
      // TODO: 批量采样优化
      throw new NotImplementedException(
        "[WorldCombatBridge] SampleRegion() not yet implemented.");
    }

    /// <summary>
    /// 开始监?CombatEventBus，将战斗事件转换?World 层事件?
    /// ?WorldManager.InitWorld() 调用?
    /// </summary>
    public void StartListening()
    {
      CombatEventBus.OnKill += HandleCombatKill;
      CombatEventBus.PostDamage += HandleCombatPostDamage;
    }

    /// <summary>
    /// 停止监听。由 WorldManager.Shutdown() 调用?
    /// </summary>
    public void StopListening()
    {
      CombatEventBus.OnKill -= HandleCombatKill;
      CombatEventBus.PostDamage -= HandleCombatPostDamage;
    }

    /// <summary>
    /// 击杀事件 → World 层处理：
    ///   - XP 分配给 World 模式玩家等级
    ///   - 检查是否击杀领先怪物（触发世界等级下降）
    ///   - 检查是否击杀营地怪物（触发营地摧毁）
    /// </summary>
    void HandleCombatKill(CombatEventBus.KillArgs args)
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      // TODO: 根据 args.VictimId 判断被杀的是否是野外怪物/营地怪物/Boss
      // TODO: 分配 WorldPlayerXp
      // TODO: 若击杀领先怪物 → ModifyWorldLevel(-delta)
      // TODO: 若击杀营地守卫 → 检查营地摧毁条件 → FireCampDestroyed
    }

    /// <summary>
    /// 伤害结算后 → 统计世界模式相关数据（如 Boss 伤害占比等）。
    /// </summary>
    void HandleCombatPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      // TODO: 统计玩家对野外 Boss 的累计伤害
      // TODO: 检查是否满足召唤物掉落条件
    }

    /// <summary>
    /// 获取当前世界等级对应的怪物难度倍率。
    /// 供 EnemySpawner 在生成野外怪物时调用。
    /// </summary>
    public float GetWorldLevelDifficultyMult()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return 1f;

      // TODO: 从 data/world_level.json 读取当前世界等级的数值倍率
      return 1f + (WorldRuntimeContext.WorldLevel - 1) * 0.05f;
    }

    public void Dispose()
    {
      StopListening();
    }
  }
}
