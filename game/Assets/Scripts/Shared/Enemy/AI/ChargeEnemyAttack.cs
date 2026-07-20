using System.Collections;
using System;
using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Vfx;
using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 冲锋攻击：蓄力预??直线冲刺 ?接触伤害。预警由 <see cref="ChargeWarningIndicator"/> 负责?
  /// </summary>
  public static class ChargeEnemyAttack
  {
    public struct Params
    {
      public Transform Owner;
      public Transform Target;
      public float Windup;
      public float DashSpeedMult;
      public float DashSpeedScaling;
      public float DashDistance;
      public float AttackRange;
      public float VisualRadius;
      public DamageRequest Request;
      public Action<Health> OnHit;
      public int Layer;
      /// <summary>冲刺阶段每帧回调：u, 当前位置, 起点, 终点, 是否竞技场弦线?/summary>
      public Action<float, Vector2, Vector2, Vector2, bool> OnDashStep;
    }

    public static IEnumerator Execute(Params p, Action<ChargeWarningIndicator> onIndicatorAssigned = null, Action onDashBegin = null)
    {
      if (p.Owner == null || p.Target == null)
        yield break;

      var toTarget = GameplayPlane.Position2D(p.Target) - GameplayPlane.Position2D(p.Owner);
      var dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
      var dashDist = p.DashDistance > 0f
        ? p.DashDistance
        : WorldGridConstants.HalfViewportWorldSize();
      var arenaDash = ArenaLayoutLocator.Layout.IsActive;

      if (arenaDash)
        dashDist = ClampDashDistanceToArena(GameplayPlane.Position2D(p.Owner), dir, dashDist);

      var effectiveDashSpeed = 9f * p.DashSpeedMult * p.DashSpeedScaling;
      var dashTime = Mathf.Clamp(dashDist / effectiveDashSpeed, 0.18f, 0.58f);

      var origin3 = p.Owner.position;
      origin3.z = 0f;
      var dir3 = new Vector3(dir.x, dir.y, 0f);
      onIndicatorAssigned?.Invoke(null);

      var elapsed = 0f;
      while (elapsed < p.Windup)
      {
        if (p.Owner == null || p.Target == null)
        {
          onIndicatorAssigned?.Invoke(null);
          yield break;
        }

        var enemyPlanar = GameplayPlane.Position2D(p.Owner);
        var targetPlanar = GameplayPlane.Position2D(p.Target);
        var liveDir = targetPlanar - enemyPlanar;
        if (liveDir.sqrMagnitude > 0.0001f)
          dir = liveDir.normalized;

        if (arenaDash)
          dashDist = ClampDashDistanceToArena(enemyPlanar, dir, p.DashDistance);

        dir3 = new Vector3(dir.x, dir.y, 0f);
        origin3 = p.Owner.position;
        origin3.z = 0f;

        elapsed += Time.deltaTime;
        yield return null;
      }

      onIndicatorAssigned?.Invoke(null);
      onDashBegin?.Invoke();

      var start = GameplayPlane.Position2D(p.Owner);
      if (arenaDash)
        dashDist = ClampDashDistanceToArena(start, dir, p.DashDistance);
      var end = start + dir * dashDist;
      dashTime = Mathf.Clamp(dashDist / effectiveDashSpeed, 0.18f, 0.58f);

      p.OnDashStep?.Invoke(0f, start, start, end, arenaDash);

      var perp = new Vector2(-dir.y, dir.x);
      const float arcAmp = 0.38f;

      elapsed = 0f;
      var hit = false;
      while (elapsed < dashTime)
      {
        if (p.Owner == null)
          yield break;

        elapsed += Time.deltaTime;
        var u = elapsed / dashTime;
        var pos = Vector2.Lerp(start, end, u);
        if (!arenaDash)
          pos += perp * (Mathf.Sin(u * Mathf.PI) * arcAmp);

        var influence = ChargeDashInfluenceLocator.Provider.GetDashOffset(
          p.Owner.gameObject,
          pos,
          dir,
          Time.deltaTime);
        if (influence.sqrMagnitude > 0.000001f)
        {
          pos += influence;
          end += influence;
          if (arenaDash)
            dashDist = Mathf.Max(0.5f, Vector2.Distance(start, end));
        }

        GameplayPlane.SetPosition2D(p.Owner, pos);
        p.OnDashStep?.Invoke(u, pos, start, end, arenaDash);

        if (!hit && p.Target != null)
        {
          var planarDist = Vector2.Distance(pos, GameplayPlane.Position2D(p.Target));
          if (planarDist <= p.AttackRange * 1.15f)
          {
            var playerHealth = p.Target.GetComponent<Health>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
              var result = DamagePipeline.Apply(p.Request, playerHealth);
              if (result.FinalDamage > 0f)
                p.OnHit?.Invoke(playerHealth);
            }

            hit = true;
          }
        }

        yield return null;
      }

      if (arenaDash && p.Owner != null)
        GameplayPlane.SetPosition2D(p.Owner, end);
    }

    static float ClampDashDistanceToArena(Vector2 start, Vector2 dir, float requestedDistance)
    {
      var distance = requestedDistance > 0f
        ? requestedDistance
        : WorldGridConstants.HalfViewportWorldSize();
      var layout = ArenaLayoutLocator.Layout;
      var radius = Mathf.Max(0.5f, layout.PathRadius - 0.35f);
      var offset = start - layout.Center;
      var along = Vector2.Dot(offset, dir);
      var discriminant = along * along - (offset.sqrMagnitude - radius * radius);
      if (discriminant <= 0f)
        return Mathf.Max(0.5f, distance);

      var available = -along + Mathf.Sqrt(discriminant);
      return Mathf.Clamp(distance, 0.5f, Mathf.Max(0.5f, available));
    }

    /// <summary>预览台专用：冲刺距离与方向由调用方指定?/summary>
    public static IEnumerator ExecutePreview(
      Transform owner,
      Transform target,
      float windup,
      float dashDistance,
      float dashTime,
      int layer = -1,
      Action<Vector3> onDashStep = null)
    {
      if (owner == null)
        yield break;

      var toTarget = target != null
        ? GameplayPlane.Position2D(target) - GameplayPlane.Position2D(owner)
        : Vector2.right;
      var dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
      var dashDist = dashDistance > 0f ? dashDistance : toTarget.magnitude;

      var origin3 = owner.position;
      origin3.z = 0f;
      var dir3 = new Vector3(dir.x, dir.y, 0f);

      var elapsed = 0f;
      while (elapsed < windup)
      {
        if (owner == null)
        {
          yield break;
        }

        if (target != null)
        {
          var liveDir = GameplayPlane.Position2D(target) - GameplayPlane.Position2D(owner);
          if (liveDir.sqrMagnitude > 0.0001f)
            dir = liveDir.normalized;
          dashDist = dashDistance > 0f ? dashDistance : liveDir.magnitude;
        }

        dir3 = new Vector3(dir.x, dir.y, 0f);
        origin3 = owner.position;
        origin3.z = 0f;
        elapsed += Time.deltaTime;
        yield return null;
      }

      var start = owner.localPosition;
      var end = start + new Vector3(dir.x, dir.y, 0f) * dashDist;
      elapsed = 0f;
      dashTime = Mathf.Max(0.12f, dashTime);

      while (elapsed < dashTime)
      {
        elapsed += Time.deltaTime;
        owner.localPosition = Vector3.Lerp(start, end, elapsed / dashTime);
        onDashStep?.Invoke(owner.localPosition);
        yield return null;
      }

      owner.localPosition = end;
      onDashStep?.Invoke(end);
    }
  }
}
