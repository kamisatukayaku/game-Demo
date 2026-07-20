using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Combat
{
  // ────────────────────────────────────────────────────────
  //  类型定义
  // ────────────────────────────────────────────────────────

  /// <summary>玩家近期平均速度来源接口（后续实现）</summary>
  public interface IPlayerVelocitySource
  {
    /// <summary>获取指定时间窗口内玩家的平均速度（世界坐标 XZ 平面）</summary>
    Vector2 GetAverageVelocity(float windowSeconds);
  }

  /// <summary>投射物信息快照 — 调用方从 StraightProjectile 提取</summary>
  public readonly struct ProjectileInfo
  {
    /// <summary>当前世界位置（2D）</summary>
    public readonly Vector2 Position;
    /// <summary>飞行方向（已归一化）</summary>
    public readonly Vector2 Direction;
    /// <summary>碰撞半径</summary>
    public readonly float HitRadius;
    /// <summary>是否为直线方向弹（true = 非追踪、可闪避；false = 追踪弹）</summary>
    public readonly bool IsDirectional;

    public ProjectileInfo(Vector2 position, Vector2 direction, float hitRadius, bool isDirectional)
    {
      Position = position;
      Direction = direction;
      HitRadius = hitRadius > 0f ? hitRadius : 0.5f;
      IsDirectional = isDirectional;
    }
  }

  /// <summary>策略返回的附加提示</summary>
  [System.Flags]
  public enum MoveStrategyHint
  {
    None = 0,
    /// <summary>建议切换为奔跑模式</summary>
    ShouldSprint = 1 << 0,
  }

  /// <summary>单次策略计算结果</summary>
  public readonly struct MoveStrategyResult
  {
    /// <summary>建议的归一化移动方向（零向量 = 无建议）</summary>
    public readonly Vector2 Direction;
    /// <summary>0-1，策略权重/可信度（0 = 根本不应用）</summary>
    public readonly float Weight;
    /// <summary>附加提示标志</summary>
    public readonly MoveStrategyHint Hint;

    public bool ShouldSprint => (Hint & MoveStrategyHint.ShouldSprint) != 0;
    public bool HasDirection => Direction.sqrMagnitude > 0.0001f;

    public static readonly MoveStrategyResult None = default;

    public MoveStrategyResult(Vector2 direction, float weight, MoveStrategyHint hint = MoveStrategyHint.None)
    {
      Direction = direction;
      Weight = Mathf.Clamp01(weight);
      Hint = hint;
    }
  }

  // ────────────────────────────────────────────────────────
  //  策略计算类
  // ────────────────────────────────────────────────────────

  /// <summary>
  /// 敌人移动策略计算库。
  /// 所有方法均为纯函数，不依赖全局状态，不修改任何对象。
  /// 调用方负责收集输入数据（盟友列表、投射物列表等），并混合多个策略的结果驱动敌人移动。
  /// </summary>
  public static class EnemyMovementStrategies
  {
    const float kEpsilonSq = 1e-6f;
    const float kDefaultEnemyRadius = 0.45f;

    // ── 策略 1：近战基础型 ──────────────────────────────

    /// <summary>
    /// 直线靠近玩家；计算非奔跑状态下到达玩家所需时间，
    /// 超出阈值时建议切换奔跑。
    /// </summary>
    /// <param name="enemyPos">敌人当前 2D 位置</param>
    /// <param name="playerPos">玩家当前 2D 位置</param>
    /// <param name="moveSpeed">敌人非奔跑时的基础移速</param>
    /// <param name="velocitySource">玩家速度来源（后续实现）</param>
    /// <param name="approachTimeThreshold">奔跑触发时间阈值（秒），当前值越小越不容易奔跑</param>
    /// <param name="playerSpeedRatio">玩家速度影响比例 [0,1]，0=忽略玩家移动</param>
    /// <param name="playerSpeedWindow">计算玩家平均速度的时间窗口（秒）</param>
    public static MoveStrategyResult MeleeBasic(
      Vector2 enemyPos,
      Vector2 playerPos,
      float moveSpeed,
      IPlayerVelocitySource velocitySource,
      float approachTimeThreshold = 2.0f,
      float playerSpeedRatio = 0.7f,
      float playerSpeedWindow = 1.0f)
    {
      var toPlayer = playerPos - enemyPos;
      var distSq = toPlayer.sqrMagnitude;
      if (distSq < kEpsilonSq)
        return MoveStrategyResult.None;

      var dir = toPlayer / Mathf.Sqrt(distSq);
      var distance = Mathf.Sqrt(distSq);

      // 计算相对闭合速度
      float closingSpeed = moveSpeed;
      if (velocitySource != null && playerSpeedRatio > 0f && playerSpeedWindow > 0f)
      {
        var playerAvgVel = velocitySource.GetAverageVelocity(playerSpeedWindow);
        // 玩家沿"敌人→玩家"方向的速度分量（正值 = 玩家朝敌人移动）
        var playerAlongLine = Vector2.Dot(playerAvgVel * playerSpeedRatio, dir);
        closingSpeed = moveSpeed - playerAlongLine;
      }

      // 闭合速度 ≤ 0 → 无法追上，建议奔跑
      bool shouldSprint;
      if (closingSpeed <= 0.01f)
      {
        shouldSprint = true;
      }
      else
      {
        var approachTime = distance / closingSpeed;
        shouldSprint = approachTime > approachTimeThreshold;
      }

      return new MoveStrategyResult(
        dir,
        shouldSprint ? 1f : 0.3f,
        shouldSprint ? MoveStrategyHint.ShouldSprint : MoveStrategyHint.None);
    }

    // ── 策略 2 / 6：闪避玩家非追踪子弹 ─────────────────

    /// <summary>
    /// 检测朝向自己的玩家非追踪子弹，沿垂直方向闪避。
    /// 仅处理 IsDirectional == true 的投射物（非追踪弹）。
    /// </summary>
    /// <param name="enemyPos">敌人当前 2D 位置</param>
    /// <param name="enemyRadius">敌人的碰撞/接受半径，默认 0.45</param>
    /// <param name="projectiles">活跃投射物快照列表</param>
    /// <param name="dodgeDetectionRange">子弹前瞻检测距离，越小子弹需越逼近才触发闪避</param>
    /// <param name="enemyVelocity">敌人当前速度（用于方向判定），若与子弹同向则跳过闪避</param>
    public static MoveStrategyResult DodgeProjectile(
      Vector2 enemyPos,
      float enemyRadius,
      IReadOnlyList<ProjectileInfo> projectiles,
      float dodgeDetectionRange = 5.0f,
      Vector2 enemyVelocity = default)
    {
      if (projectiles == null || projectiles.Count == 0)
        return MoveStrategyResult.None;

      if (enemyRadius <= 0f) enemyRadius = kDefaultEnemyRadius;
      if (dodgeDetectionRange <= 0.01f) dodgeDetectionRange = 0.01f;

      var enemyVelDir = enemyVelocity.sqrMagnitude > 0.01f
        ? enemyVelocity.normalized : (Vector2?)null;

      var sum = Vector2.zero;

      for (int i = 0; i < projectiles.Count; i++)
      {
        var proj = projectiles[i];
        if (!proj.IsDirectional) continue; // 追踪弹不适用于垂直闪避

        // 点到射线的最短距离
        var toEnemy = enemyPos - proj.Position;
        var alongLine = Vector2.Dot(toEnemy, proj.Direction);

        // 子弹尚未接近或在身后 → 跳过
        if (alongLine < 0f || alongLine > dodgeDetectionRange)
          continue;

        // 自身位置若与子弹飞行方向相反也不触发闪避：
        // 如果敌人速度方向与子弹方向相同（敌人正在沿子弹方向远离来源），不闪避
        if (enemyVelDir != null && Vector2.Dot(enemyVelDir.Value, proj.Direction) > 0.3f)
          continue;

        // 射线上的最近点
        var closestPoint = proj.Position + proj.Direction * alongLine;
        var projDist = (enemyPos - closestPoint).magnitude;

        var threatRadius = enemyRadius + proj.HitRadius + 0.1f;
        if (projDist >= threatRadius)
          continue;

        // 闪避方向：垂直于子弹飞行方向，选使 enemyPos 远离弹道的一侧
        var perp = new Vector2(-proj.Direction.y, proj.Direction.x);
        var dotPerp = Vector2.Dot(toEnemy, perp);
        if (dotPerp < 0f) perp = -perp;

        // 越逼近威胁越大
        var minCloseDist = 1f - Mathf.Clamp01(projDist / threatRadius);
        var minApproachDist = 1f - Mathf.Clamp01(alongLine / dodgeDetectionRange);
        var weight = minCloseDist * minApproachDist;

        sum += perp * weight;
      }

      if (sum.sqrMagnitude < kEpsilonSq)
        return MoveStrategyResult.None;

      var dir = sum.normalized;
      var totalWeight = Mathf.Clamp01(sum.magnitude);
      return new MoveStrategyResult(dir, totalWeight);
    }

    // ── 策略 3：近战包围型 ──────────────────────────────

    /// <summary>
    /// 排斥周围其他近战敌人，形成分散包围圈。
    /// </summary>
    /// <param name="enemyPos">自身 2D 位置</param>
    /// <param name="meleeAllyPositions">附近其他近战敌人的位置列表</param>
    /// <param name="spreadRadius">期望间距，越小排斥半径越小</param>
    public static MoveStrategyResult MeleeSpread(
      Vector2 enemyPos,
      IReadOnlyList<Vector2> meleeAllyPositions,
      float spreadRadius = 2.5f)
    {
      if (meleeAllyPositions == null || meleeAllyPositions.Count == 0)
        return MoveStrategyResult.None;

      if (spreadRadius <= 0.01f) spreadRadius = 0.01f;

      var sum = Vector2.zero;

      for (int i = 0; i < meleeAllyPositions.Count; i++)
      {
        var toAlly = enemyPos - meleeAllyPositions[i];
        var distSq = toAlly.sqrMagnitude;
        if (distSq < kEpsilonSq)
          continue;

        var dist = Mathf.Sqrt(distSq);
        if (dist >= spreadRadius)
          continue;

        var awayDir = toAlly / dist;
        var distFactor = 1f - Mathf.Clamp01(dist / spreadRadius);
        var weight = distFactor * distFactor; // 二次衰减，近距离权重更大
        sum += awayDir * weight;
      }

      if (sum.sqrMagnitude < kEpsilonSq)
        return MoveStrategyResult.None;

      var dir = sum.normalized;
      var totalWeight = Mathf.Clamp01(sum.magnitude);
      return new MoveStrategyResult(dir, totalWeight);
    }

    // ── 策略 4：近战协作型 ──────────────────────────────

    /// <summary>
    /// 以最短距离移动至离自身最近的远程敌人与玩家的连线上，为远程队友阻挡玩家射击线。
    /// </summary>
    /// <param name="enemyPos">自身 2D 位置</param>
    /// <param name="playerPos">玩家 2D 位置</param>
    /// <param name="rangedAllyPositions">远程敌人位置列表</param>
    /// <param name="blockRange">远程队友搜索半径，越小仅越近的队友触发保护</param>
    public static MoveStrategyResult MeleeCooperative(
      Vector2 enemyPos,
      Vector2 playerPos,
      IReadOnlyList<Vector2> rangedAllyPositions,
      float blockRange = 6.0f)
    {
      if (rangedAllyPositions == null || rangedAllyPositions.Count == 0)
        return MoveStrategyResult.None;

      if (blockRange <= 0.01f) blockRange = 0.01f;

      var blockRangeSq = blockRange * blockRange;
      float bestDistSq = float.MaxValue;
      Vector2 bestDir = Vector2.zero;

      for (int i = 0; i < rangedAllyPositions.Count; i++)
      {
        var allyPos = rangedAllyPositions[i];
        var toAlly = allyPos - enemyPos;
        var distToAllySq = toAlly.sqrMagnitude;
        if (distToAllySq > blockRangeSq)
          continue;

        // 求 enemyPos 在"玩家→远程队友"线段上的投影点
        var lineStart = playerPos;
        var lineEnd = allyPos;
        var lineVec = lineEnd - lineStart;
        var lineLenSq = lineVec.sqrMagnitude;

        Vector2 closestPt;
        if (lineLenSq < kEpsilonSq)
        {
          closestPt = lineStart;
        }
        else
        {
          var t = Vector2.Dot(enemyPos - lineStart, lineVec) / lineLenSq;
          t = Mathf.Clamp01(t);
          closestPt = lineStart + lineVec * t;
        }

        // 选取最近的远程队友（距离自身最近）
        if (distToAllySq < bestDistSq)
        {
          bestDistSq = distToAllySq;
          var toClosest = closestPt - enemyPos;
          if (toClosest.sqrMagnitude > kEpsilonSq)
            bestDir = toClosest.normalized;
        }
      }

      if (bestDir.sqrMagnitude < kEpsilonSq)
        return MoveStrategyResult.None;

      var bestDist = Mathf.Sqrt(bestDistSq);
      var weight = 1f - Mathf.Clamp01(bestDist / blockRange);
      return new MoveStrategyResult(bestDir, weight);
    }

    // ── 策略 5：远程基础型 ──────────────────────────────

    /// <summary>
    /// 玩家快超出攻击范围时靠近，太近时后退，保持在舒适区内。
    /// </summary>
    /// <param name="enemyPos">自身 2D 位置</param>
    /// <param name="playerPos">玩家 2D 位置</param>
    /// <param name="attackRange">敌人攻击范围</param>
    /// <param name="comfortZone">舒适距离占攻击范围的比率 [0,1]，
    ///   越小越容忍玩家靠近，后退意愿越低</param>
    public static MoveStrategyResult RangedBasic(
      Vector2 enemyPos,
      Vector2 playerPos,
      float attackRange,
      float comfortZone = 0.65f)
    {
      var toPlayer = playerPos - enemyPos;
      var distSq = toPlayer.sqrMagnitude;
      if (distSq < kEpsilonSq)
        return MoveStrategyResult.None;

      var dist = Mathf.Sqrt(distSq);
      var idealDist = attackRange * Mathf.Clamp01(comfortZone);

      if (dist > attackRange * 0.95f)
      {
        // 快超出射程 → 靠近
        var dir = toPlayer / dist;
        var excessRange = attackRange * 0.3f;
        var weight = excessRange > 0.01f
          ? Mathf.Clamp01((dist - attackRange * 0.95f) / excessRange)
          : 1f;
        var shouldSprint = dist > attackRange * 1.4f;
        return new MoveStrategyResult(dir, weight,
          shouldSprint ? MoveStrategyHint.ShouldSprint : MoveStrategyHint.None);
      }

      if (dist < idealDist)
      {
        // 太近 → 后退
        var dir = -toPlayer / dist;
        var weight = idealDist > 0.01f ? 1f - Mathf.Clamp01(dist / idealDist) : 0f;
        return new MoveStrategyResult(dir, weight);
      }

      return MoveStrategyResult.None; // 在舒适区内
    }

    // ── 策略 6：远程躲避型（转发至策略 2） ──────────────

    /// <summary>
    /// 同 <see cref="DodgeProjectile"/>，检测朝向自己的非追踪弹并闪避。
    /// </summary>
    public static MoveStrategyResult RangedDodge(
      Vector2 enemyPos,
      float enemyRadius,
      IReadOnlyList<ProjectileInfo> projectiles,
      float dodgeDetectionRange = 4.5f,
      Vector2 enemyVelocity = default)
    {
      return DodgeProjectile(enemyPos, enemyRadius, projectiles, dodgeDetectionRange, enemyVelocity);
    }

    // ── 策略 7：远程协作型 ──────────────────────────────

    /// <summary>
    /// 以最短距离移动至离自身最近的近战敌人与玩家的连线上，
    /// 躲在近战队友身后以遮挡玩家射击线。
    /// </summary>
    /// <param name="enemyPos">自身 2D 位置</param>
    /// <param name="playerPos">玩家 2D 位置</param>
    /// <param name="meleeAllyPositions">近战敌人位置列表</param>
    /// <param name="coverRange">寻找掩护的搜索半径，越小仅越近的近战队友被选为掩护</param>
    public static MoveStrategyResult RangedCooperative(
      Vector2 enemyPos,
      Vector2 playerPos,
      IReadOnlyList<Vector2> meleeAllyPositions,
      float coverRange = 5.0f)
    {
      if (meleeAllyPositions == null || meleeAllyPositions.Count == 0)
        return MoveStrategyResult.None;

      if (coverRange <= 0.01f) coverRange = 0.01f;

      var coverRangeSq = coverRange * coverRange;
      float bestDistSq = float.MaxValue;
      Vector2 bestDir = Vector2.zero;

      for (int i = 0; i < meleeAllyPositions.Count; i++)
      {
        var allyPos = meleeAllyPositions[i];
        var toAlly = allyPos - enemyPos;
        var distToAllySq = toAlly.sqrMagnitude;
        if (distToAllySq > coverRangeSq)
          continue;

        // 求 enemyPos 在"玩家→近战队友"线段上的投影点
        var lineStart = playerPos;
        var lineEnd = allyPos;
        var lineVec = lineEnd - lineStart;
        var lineLenSq = lineVec.sqrMagnitude;

        Vector2 closestPt;
        if (lineLenSq < kEpsilonSq)
        {
          closestPt = lineStart;
        }
        else
        {
          var t = Vector2.Dot(enemyPos - lineStart, lineVec) / lineLenSq;
          t = Mathf.Clamp01(t);
          closestPt = lineStart + lineVec * t;
        }

        // 选取最近的近战队友（距离自身最近）
        if (distToAllySq < bestDistSq)
        {
          bestDistSq = distToAllySq;
          var toClosest = closestPt - enemyPos;
          if (toClosest.sqrMagnitude > kEpsilonSq)
            bestDir = toClosest.normalized;
        }
      }

      if (bestDir.sqrMagnitude < kEpsilonSq)
        return MoveStrategyResult.None;

      var bestDist = Mathf.Sqrt(bestDistSq);
      var weight = 1f - Mathf.Clamp01(bestDist / coverRange);
      return new MoveStrategyResult(bestDir, weight);
    }
  }
}
