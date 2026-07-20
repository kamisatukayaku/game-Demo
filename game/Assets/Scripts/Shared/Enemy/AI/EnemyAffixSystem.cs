using System.Collections.Generic;
using Game.Shared.Enemy.AI;
using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  // ── 枚举 ──────────────────────────────────────────

  public enum MoveAffixType
  {
    MeleeBasic,
    DodgeProjectile,
    MeleeSpread,
    MeleeCooperative,
    RangedBasic,
    RangedDodge,
    RangedCooperative
  }

  public enum AttackAffixType
  {
    Burst,
    Prediction,
    Suppression,
    Shotgun,
    Turn,
    Sweep,
    MultiCharge
  }

  // ── 数据结构 ──────────────────────────────────────

  /// <summary>移动词条：策略类型 + 权重 + 参数</summary>
  [System.Serializable]
  public struct EnemyMoveAffix
  {
    public MoveAffixType Type;
    public float Weight;
    public float P0, P1, P2, P3;

    public EnemyMoveAffix(MoveAffixType type, float weight,
      float p0 = 0f, float p1 = 0f, float p2 = 0f, float p3 = 0f)
    {
      Type = type;
      Weight = Mathf.Clamp01(weight);
      P0 = p0; P1 = p1; P2 = p2; P3 = p3;
    }

    public float GetParam(int index, float fallback = 0f)
    {
      return index switch
      {
        0 => P0,
        1 => P1,
        2 => P2,
        3 => P3,
        _ => fallback
      };
    }
  }

  /// <summary>攻击词条：类型 + 参数</summary>
  [System.Serializable]
  public struct EnemyAttackAffix
  {
    public AttackAffixType Type;
    public float P0, P1, P2, P3;

    public EnemyAttackAffix(AttackAffixType type,
      float p0 = 0f, float p1 = 0f, float p2 = 0f, float p3 = 0f)
    {
      Type = type;
      P0 = p0; P1 = p1; P2 = p2; P3 = p3;
    }

    public float GetParam(int index, float fallback = 0f)
    {
      return index switch
      {
        0 => P0,
        1 => P1,
        2 => P2,
        3 => P3,
        _ => fallback
      };
    }
  }

  /// <summary>词条集合：移动词条列表 + 攻击词条列表</summary>
  [System.Serializable]
  public class EnemyAffixSet
  {
    public List<EnemyMoveAffix> MovementAffixes = new List<EnemyMoveAffix>();
    public List<EnemyAttackAffix> AttackAffixes = new List<EnemyAttackAffix>();

    public bool HasMoveAffix(MoveAffixType type)
    {
      for (int i = 0; i < MovementAffixes.Count; i++)
        if (MovementAffixes[i].Type == type) return true;
      return false;
    }

    public bool HasAttackAffix(AttackAffixType type)
    {
      for (int i = 0; i < AttackAffixes.Count; i++)
        if (AttackAffixes[i].Type == type) return true;
      return false;
    }

    public EnemyAttackAffix? TryGetAttackAffix(AttackAffixType type)
    {
      for (int i = 0; i < AttackAffixes.Count; i++)
        if (AttackAffixes[i].Type == type) return AttackAffixes[i];
      return null;
    }

    /// <summary>判断攻击词条对当前攻击类型是否有效</summary>
    public bool IsAttackAffixValidForDelivery(
      EnemyDeliveryType delivery, EnemyAttackAffix affix)
    {
      switch (affix.Type)
      {
        case AttackAffixType.Burst:
        case AttackAffixType.Prediction:
        case AttackAffixType.Suppression:
        case AttackAffixType.Shotgun:
          return delivery == EnemyDeliveryType.Projectile;
        case AttackAffixType.Turn:
          return delivery == EnemyDeliveryType.Beam
              || delivery == EnemyDeliveryType.ChargeDash;
        case AttackAffixType.Sweep:
          return delivery == EnemyDeliveryType.Beam;
        case AttackAffixType.MultiCharge:
          return delivery == EnemyDeliveryType.ChargeDash;
        default:
          return false;
      }
    }

    /// <summary>获取对当前攻击类型有效的词条列表</summary>
    public List<EnemyAttackAffix> GetValidAttackAffixes(EnemyDeliveryType delivery)
    {
      var valid = new List<EnemyAttackAffix>();
      for (int i = 0; i < AttackAffixes.Count; i++)
        if (IsAttackAffixValidForDelivery(delivery, AttackAffixes[i]))
          valid.Add(AttackAffixes[i]);
      return valid;
    }

    /// <summary>
    /// 生成默认词条集合。不再强制加入基础移动词条（MeleeBasic/RangedBasic），
    /// 使无词条时回退到 AI 风格逻辑（EnemyCore.ResolveMoveDirection）。
    /// </summary>
    public static EnemyAffixSet CreateDefault(EnemyAttackKind attackKind)
    {
      var set = new EnemyAffixSet();
      if (attackKind == EnemyAttackKind.Ranged)
      {
        set.MovementAffixes.Add(new EnemyMoveAffix(
          MoveAffixType.RangedDodge, 0.6f, 4.5f));
      }
      else
      {
        set.MovementAffixes.Add(new EnemyMoveAffix(
          MoveAffixType.MeleeSpread, 0.5f, 2.5f));
        set.MovementAffixes.Add(new EnemyMoveAffix(
          MoveAffixType.DodgeProjectile, 0.4f, 5f));
      }
      return set;
    }
  }

  // ── 预设生成器 ────────────────────────────────────

  /// <summary>按类型名 + 强度(0-1) 生成单个预设词条</summary>
  public static class EnemyAffixPresets
  {
    public static EnemyMoveAffix CreateMoveAffix(
      MoveAffixType type, float strength)
    {
      strength = Mathf.Clamp01(strength);
      float lerp(float from, float to) => Mathf.Lerp(from, to, strength);

      return type switch
      {
        MoveAffixType.MeleeBasic => new EnemyMoveAffix(
          type, 1f, lerp(999f, 2.0f), lerp(0f, 0.7f), lerp(0f, 1.0f)),
        MoveAffixType.DodgeProjectile => new EnemyMoveAffix(
          type, 1f, lerp(0.01f, 5.0f)),
        MoveAffixType.MeleeSpread => new EnemyMoveAffix(
          type, 1f, lerp(0.01f, 2.5f)),
        MoveAffixType.MeleeCooperative => new EnemyMoveAffix(
          type, 1f, lerp(0.01f, 6.0f)),
        MoveAffixType.RangedBasic => new EnemyMoveAffix(
          type, 1f, lerp(0f, 0.65f)),
        MoveAffixType.RangedDodge => new EnemyMoveAffix(
          type, 1f, lerp(0.01f, 4.5f)),
        MoveAffixType.RangedCooperative => new EnemyMoveAffix(
          type, 1f, lerp(0.01f, 5.0f)),
        _ => new EnemyMoveAffix(type, 1f)
      };
    }

    public static EnemyAttackAffix CreateAttackAffix(
      AttackAffixType type, float strength)
    {
      strength = Mathf.Clamp01(strength);
      float lerp(float from, float to) => Mathf.Lerp(from, to, strength);

      return type switch
      {
        AttackAffixType.Burst => new EnemyAttackAffix(
          type, lerp(1f, 5f), 1f),                       // P0=burstCount, P1=intervalRatio
        AttackAffixType.Prediction => new EnemyAttackAffix(
          type, lerp(0f, 2.0f)),                         // P0=playerSpeedWindow
        AttackAffixType.Suppression => new EnemyAttackAffix(
          type, lerp(1.0f, 0.6f), lerp(0f, 30f)),        // P0=intervalMult, P1=spreadUpperLimit
        AttackAffixType.Shotgun => new EnemyAttackAffix(
          type, lerp(1.0f, 3.0f), lerp(1f, 5f), lerp(0f, 15f)), // P0=intervalMult, P1=pelletCount, P2=spreadPerShot
        AttackAffixType.Turn => new EnemyAttackAffix(
          type, lerp(0f, 180f), lerp(0f, 0.4f)),         // P0=maxTurnRateDeg, P1=turnAllowedTimeRemaining
        AttackAffixType.Sweep => new EnemyAttackAffix(
          type, lerp(0f, 90f)),                          // P0=maxTurnRateDeg
        AttackAffixType.MultiCharge => new EnemyAttackAffix(
          type, lerp(1f, 3f), lerp(1.0f, 3.0f)),         // P0=maxCharges, P1=intervalMult
        _ => new EnemyAttackAffix(type)
      };
    }
  }

  // ── 远程攻击词条处理器 ────────────────────────────

  /// <summary>
  /// 处理远程投射物攻击的连发/预判/压制/霰弹词条。
  /// 由 EnemySphereController 在发射子弹前调用。
  /// </summary>
  public static class RangedAffixHandler
  {
    public struct FireConfig
    {
      public int FireCount;         // 输入/输出：本次攻击发射次数
      public float FireInterval;    // 输入/输出：每次发射间隔
      public Vector2 AimDirection;  // 输入/输出：瞄准方向（可能被预判偏移）
      public float BaseCooldown;    // 输入：基础冷却时间（用于连发均速计算）
      public float BaseSpread;      // 输入：基准散布角度
    }

    /// <summary>应用所有有效远程攻击词条，修改发射配置。</summary>
    public static void PrepareFire(
      List<EnemyAttackAffix> affixes,
      ref FireConfig config,
      Vector2? playerVelocity = null)
    {
      if (affixes == null || affixes.Count == 0)
        return;

      bool hasBurst = false;
      bool hasShotgun = false;

      for (int i = 0; i < affixes.Count; i++)
      {
        switch (affixes[i].Type)
        {
          case AttackAffixType.Burst:
            hasBurst = true;
            break;
          case AttackAffixType.Shotgun:
            hasShotgun = true;
            break;
        }
      }

      // 处理连发 + 预判 + 压制 + 霰弹

      for (int i = 0; i < affixes.Count; i++)
      {
        var affix = affixes[i];
        switch (affix.Type)
        {
          case AttackAffixType.Burst:
          {
            int burstCount = Mathf.Max(1, Mathf.RoundToInt(affix.GetParam(0, 1)));
            float intervalRatio = affix.GetParam(1, 1f);

            // 连发 + 霰弹共存时，连发优先作用于霰弹（每轮霰弹视为一次"攻击"）
            if (hasShotgun)
            {
              // 保持此设计，不重复发射
              config.FireCount = burstCount;
            }
            else
            {
              config.FireCount = burstCount;
            }

            // 平均攻速不变：调整发射间隔
            // 总时间不变：config.BaseCooldown = FireCount * FireInterval
            // 新的 FireInterval = BaseCooldown / FireCount
            if (config.FireCount > 1)
              config.FireInterval = config.BaseCooldown / config.FireCount * intervalRatio;
            break;
          }

          case AttackAffixType.Prediction:
          {
            float window = affix.GetParam(0, 1f);
            if (playerVelocity.HasValue && window > 0.01f)
            {
              var predictedOffset = playerVelocity.Value * window;
              var predictedPoint = config.AimDirection + predictedOffset;
              if (predictedPoint.sqrMagnitude > 0.001f)
                config.AimDirection = predictedPoint.normalized;
            }
            break;
          }

          case AttackAffixType.Suppression:
          {
            // 加速攻击间隔（intervalMult < 1）
            float intervalMult = affix.GetParam(0, 1f);
            config.FireInterval *= intervalMult;

            // 散布上限
            float spreadUpper = affix.GetParam(1, 0f);
            if (spreadUpper > 0f)
              config.BaseSpread = Mathf.Max(config.BaseSpread, spreadUpper);
            break;
          }

          case AttackAffixType.Shotgun:
          {
            // 减速攻击间隔（intervalMult > 1）+ 多弹丸 + 散布
            float intervalMult = affix.GetParam(0, 1f);
            int pelletCount = Mathf.Max(1, Mathf.RoundToInt(affix.GetParam(1, 1)));
            float spreadPerShot = affix.GetParam(2, 0f);

            config.FireInterval *= intervalMult;
            // 霰弹：一次发射多发弹丸
            // 如果已经连发，连发优先；否则仅霰弹
            if (!hasBurst)
              config.FireCount = 1;

            // 散布叠加
            if (spreadPerShot > 0f)
              config.BaseSpread = Mathf.Max(config.BaseSpread, spreadPerShot * pelletCount * 0.5f);
            break;
          }
        }
      }
    }
  }

  // ── 特殊攻击词条处理器（激光/冲撞） ──────────────

  /// <summary>
  /// 处理激光/冲撞的转向/预判/扫射/连冲词条。
  /// 由 EnemySphereController 在对应协程中调用。
  /// </summary>
  public static class SpecialAffixHandler
  {
    /// <summary>
    /// 在蓄力期间更新方向：应用转向 + 预判词条。
    /// 返回修正后的方向，调用方应使用此方向覆盖原方向。
    /// </summary>
    public static Vector2 UpdateWindupDirection(
      List<EnemyAttackAffix> affixes,
      Vector2 currentDir,
      Vector2 toTarget,
      float dt,
      Vector2? playerVelocity = null)
    {
      if (affixes == null || affixes.Count == 0)
        return currentDir;

      var targetDir = toTarget.sqrMagnitude > 0.001f
        ? toTarget.normalized : currentDir;

      // 预判：偏移目标方向
      for (int i = 0; i < affixes.Count; i++)
      {
        if (affixes[i].Type == AttackAffixType.Prediction)
        {
          float window = affixes[i].GetParam(0, 1f);
          if (playerVelocity.HasValue && window > 0.01f)
          {
            var predicted = targetDir + playerVelocity.Value * window;
            if (predicted.sqrMagnitude > 0.001f)
              targetDir = predicted.normalized;
          }
        }
      }

      // 转向：旋转至目标方向（限于 maxTurnRateDeg/秒）
      for (int i = 0; i < affixes.Count; i++)
      {
        if (affixes[i].Type == AttackAffixType.Turn)
        {
          float maxDegPerSec = affixes[i].GetParam(0, 180f);
          if (maxDegPerSec <= 0f) break;

          // 检查转向时间剩余窗口
          // 在蓄力期间仍然可以转向（turnAllowedTimeRemaining 未在协程中体现，
          // 这里直接按 turn rate 限制旋转）
          float maxTurnRad = maxDegPerSec * Mathf.Deg2Rad * dt;
          float angle = SignedAngle(currentDir, targetDir);
          float clamped = Mathf.Clamp(angle, -maxTurnRad, maxTurnRad);
          return RotateVector(currentDir, clamped);
        }
      }

      return targetDir;
    }

    /// <summary>
    /// 扫射词条：在激光发射期间更新方向。
    /// 返回修正后的方向，调用方应替换原方向。
    /// </summary>
    public static Vector2 UpdateBeamDirection(
      List<EnemyAttackAffix> affixes,
      Vector2 currentDir,
      float dt)
    {
      if (affixes == null || affixes.Count == 0)
        return currentDir;

      for (int i = 0; i < affixes.Count; i++)
      {
        if (affixes[i].Type == AttackAffixType.Sweep)
        {
          float maxDegPerSec = affixes[i].GetParam(0, 90f);
          if (maxDegPerSec <= 0f) continue;

          // 沿切线方向旋转
          float sweepRad = maxDegPerSec * Mathf.Deg2Rad * dt;
          // 默认顺时针旋转（可根据需要调整方向）
          return RotateVector(currentDir, sweepRad);
        }
      }

      return currentDir;
    }

    /// <summary>
    /// 连冲词条：检查是否应再次冲撞。
    /// 返回 true 表示应继续冲撞。
    /// </summary>
    public static bool ShouldMultiCharge(
      List<EnemyAttackAffix> affixes,
      int chargesUsed,
      bool lastChargeHit,
      out float chargeIntervalMult)
    {
      chargeIntervalMult = 1f;
      if (affixes == null || affixes.Count == 0)
        return false;

      for (int i = 0; i < affixes.Count; i++)
      {
        if (affixes[i].Type == AttackAffixType.MultiCharge)
        {
          int maxCharges = Mathf.RoundToInt(affixes[i].GetParam(0, 1f));
          chargeIntervalMult = affixes[i].GetParam(1, 1f);

          if (maxCharges > 1 && chargesUsed < maxCharges)
          {
            // 未命中才继续冲撞（intervalMult > 1）
            return !lastChargeHit;
          }
        }
      }

      return false;
    }

    // ── 内部工具 ────────────────────────────────────

    static float SignedAngle(Vector2 from, Vector2 to)
    {
      float signed = Mathf.Atan2(
        from.x * to.y - from.y * to.x,
        from.x * to.x + from.y * to.y);
      return signed;
    }

    static Vector2 RotateVector(Vector2 v, float radians)
    {
      float cos = Mathf.Cos(radians);
      float sin = Mathf.Sin(radians);
      return new Vector2(
        v.x * cos - v.y * sin,
        v.x * sin + v.y * cos);
    }
  }
}
