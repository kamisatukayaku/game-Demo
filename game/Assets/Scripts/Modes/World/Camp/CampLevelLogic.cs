using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 营地等级成长逻辑（纯数据/逻辑类，不继?MonoBehaviour）?
  ///
  /// 负责计算营地等级的变化速率，受以下因素影响?
  ///   1. 自然成长 ?随时间缓慢提升（基础速率来自 CampTypeDef.growth_rate?
  ///   2. 玩家靠近加??玩家进入 proximity 范围时成长倍率提升
  ///   3. 追赶机制 ??CampLevel 低于 WorldLevel 时成长大幅加速"
  ///
  /// 使用方式?
  ///   var logic = new CampLevelLogic(campTypeDef);
  ///   每帧调用 logic.Tick(deltaTime, playerDistance, worldLevel);
  ///   读取 logic.CurrentLevel 获取当前营地等级
  ///
  /// 参见：docs/design.md §3.3（怪物营地?
  /// </summary>
  public class CampLevelLogic
  {
    // ══════════════════════════════════════════════════════
    //  配置（只读）
    // ══════════════════════════════════════════════════════

    readonly WorldDatabase.CampTypeDef _typeDef;

    /// <summary>玩家靠近判定距离（世界单位）</summary>
    public float ProximityRadius { get; private set; }

    // ══════════════════════════════════════════════════════
    //  运行时状态"
    // ══════════════════════════════════════════════════════

    int _currentLevel;
    float _levelProgress;    // 0~1，到?1 时升纀"

    public int CurrentLevel => _currentLevel;

    /// <summary>当前等级对应的数值定义（来自 WorldDatabase 配表?/summary>
    public WorldDatabase.CampLevelDef CurrentLevelDef =>
      WorldDatabase.GetCampLevel(_currentLevel);

    /// <summary>本帧是否发生了升?/summary>
    public bool LeveledUpThisFrame { get; private set; }

    // ══════════════════════════════════════════════════════
    //  构速"
    // ══════════════════════════════════════════════════════

    public CampLevelLogic(WorldDatabase.CampTypeDef typeDef, float proximityRadius = 12f)
    {
      _typeDef = typeDef;
      ProximityRadius = proximityRadius;
      _currentLevel = typeDef != null ? typeDef.base_level : 1;
      _levelProgress = 0f;
    }

    // ══════════════════════════════════════════════════════
    //  Tick ?每帧调用
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 更新营地等级成长?
    /// </summary>
    /// <param name="deltaTime">帧间隔（秒）</param>
    /// <param name="playerDistance">玩家到营地中心的距离（世界单位）?1 表示未知/不在范围?/param>
    /// <param name="worldLevel">当前世界等级</param>
    public void Tick(float deltaTime, float playerDistance, int worldLevel)
    {
      LeveledUpThisFrame = false;

      if (deltaTime <= 0f) return;

      // Step 1: 计算本帧成长釀"
      var growth = CalculateGrowth(deltaTime, playerDistance, worldLevel);

      // Step 2: 累积进度
      _levelProgress += growth;

      // Step 3: 检查升纀"
      while (_levelProgress >= 1f && _currentLevel < (_typeDef?.max_level ?? 999))
      {
        _levelProgress -= 1f;
        _currentLevel++;
        LeveledUpThisFrame = true;
      }

      // Step 4: 已达上限时清零溢出进度"
      if (_currentLevel >= (_typeDef?.max_level ?? 999))
        _levelProgress = 0f;
    }

    // ══════════════════════════════════════════════════════
    //  成长计算
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 计算本帧的等级成长进度（0~1 之间的增量）?
    ///
    /// 公式?
    ///   growthRate = baseRate × playerProximityBonus × catchUpBonus
    ///   baseRate      = camp_type.growth_rate × levelDifficultyMult
    ///   proximityBonus = camp_type.player_proximity_growth_bonus（玩家靠近时?
    ///   catchUpBonus  = 大幅加速（营地等级低于世界等级时）
    /// </summary>
    float CalculateGrowth(float deltaTime, float playerDistance, int worldLevel)
    {
      if (_typeDef == null) return 0f;

      // 基础成长速率（每秒），受营地类型的 natural_growth_mult 控制
      var def = WorldDatabase.GetWorldLevel(worldLevel);
      var naturalMult = _typeDef.natural_growth_mult > 0f ? _typeDef.natural_growth_mult : 1f;
      float baseRate = _typeDef.growth_rate * def.growth_rate * naturalMult;

      // 玩家靠近倍率（越高级营地 proximity 加成越小 = 更依赖自然成长）
      float proximityMult = 1f;
      if (playerDistance >= 0f && playerDistance <= ProximityRadius)
      {
        proximityMult = Mathf.Max(1f, _typeDef.player_proximity_growth_bonus);
      }

      // 追赶倍率：营地等级低于世界等级时大幅加速"
      float catchUpMult = 1f;
      if (_currentLevel < worldLevel)
      {
        // 差值越大，追赶越快（二次增长）
        var gap = worldLevel - _currentLevel;
        catchUpMult = 1f + gap * gap * 0.5f;
      }

      // 合成成长?= 速率 × 倍率 × 时间
      return baseRate * proximityMult * catchUpMult * deltaTime;
    }

    // ══════════════════════════════════════════════════════
    //  查询
    // ══════════════════════════════════════════════════════

    /// <summary>获取当前营地等级的怪物属性倍率</summary>
    public float GetEnemyHpMult()  => CurrentLevelDef.enemy_hp_mult;
    public float GetEnemyDamageMult() => CurrentLevelDef.enemy_damage_mult;
    public float GetEnemySpeedMult() => CurrentLevelDef.enemy_speed_mult;
    public float GetEnemyCountMult() => CurrentLevelDef.enemy_count_mult;

    /// <summary>清剿奖励</summary>
    public int GetXpReward()  => CurrentLevelDef.xp_reward;
    public int GetGoldReward() => CurrentLevelDef.gold_reward;

    /// <summary>刷怪参?/summary>
    public float GetSpawnInterval() => CurrentLevelDef.spawn_interval;
    public int   GetMaxAliveEnemies() => CurrentLevelDef.max_alive_enemies;

    /// <summary>
    /// 营地怪物游荡回血速率（maxHP 比例/秒）。
    /// 公式：营地等级 × 3%（即 LV.1→3%/s, LV.5→15%/s, LV.10→30%/s）
    /// </summary>
    public float GetRegenRate() => CurrentLevel * 0.03f;

    /// <summary>重置等级到初始值（用于测试/重置?/summary>
    public void Reset()
    {
      _currentLevel = _typeDef != null ? _typeDef.base_level : 1;
      _levelProgress = 0f;
      LeveledUpThisFrame = false;
    }

    // ── 辅助 ──────────────────────────────────────────

    static class Mathf
    {
      public static float Max(float a, float b) => a > b ? a : b;
    }
  }
}
