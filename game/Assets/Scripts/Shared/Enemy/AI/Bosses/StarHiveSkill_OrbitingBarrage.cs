using UnityEngine;
using Game.Shared.Core;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 环绕弹幕 — 高速移动至玩家正上方指定距离处，围绕玩家旋转，
  /// 旋转期间多次发射霰弹。Boss 相对位置始终为保持指定距离围绕玩家旋转。
  /// Priority: 4, Cooldown: 12s
  /// </summary>
  public class StarHiveSkill_OrbitingBarrage : BossSkillBase
  {
    const float APPROACH_SPEED   = 8f;    // 移动到轨道位置的速度
    const float ORBIT_RADIUS     = 5f;    // 环绕半径
    const float ORBIT_DURATION   = 3.5f;  // 环绕总时长
    const float ORBIT_DEG_PER_SEC = 120f; // 绕行速度（度/秒）
    const float FIRE_INTERVAL    = 0.5f;  // 霰弹间隔
    const int   PELLETS          = 5;
    const float SPREAD_DEG       = 58f;

    enum Phase { Approaching, Orbiting }

    Phase _phase;
    float _orbitAngle;    // 当前环绕角度（从 0 → 360+）
    float _orbitElapsed;
    float _fireTimer;

    public StarHiveSkill_OrbitingBarrage()
    {
      Id       = WildBossStarHive.SKILL_ORBIT;
      Priority = 4;
      Cooldown = 12f;
    }

    public override bool CanTrigger(BossCore boss) => true;

    public override void OnEnter(BossCore boss)
    {
      _phase        = Phase.Approaching;
      _orbitAngle   = 90f;  // 从正上方开始
      _orbitElapsed = 0f;
      _fireTimer    = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return State.Completed;

      var playerPos = hive.GetPlayerPos();

      if (_phase == Phase.Approaching)
      {
        // 高速移动到目标轨道位置（玩家正上方 offset）
        Vector2 targetPos = playerPos + new Vector2(0f, ORBIT_RADIUS);
        Vector2 toTarget  = targetPos - hive.Position;
        float dist = toTarget.magnitude;

        if (dist <= 0.3f)
        {
          _phase = Phase.Orbiting;
          _orbitAngle = 90f;
          _orbitElapsed = 0f;
          _fireTimer = 0f;
        }
        else
        {
          Vector2 dir = toTarget.normalized;
          float speed = Mathf.Min(APPROACH_SPEED, dist / dt); // 防止 overshoot
          hive.MoveInDirection(dir, speed, dt);
        }
      }
      else // Orbiting
      {
        _orbitElapsed += dt;
        _fireTimer    += dt;

        // 绕玩家旋转
        _orbitAngle += ORBIT_DEG_PER_SEC * dt;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        Vector2 orbitOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ORBIT_RADIUS;
        Vector2 desiredPos = playerPos + orbitOffset;

        // 直接设位置（不受玩家移动影响 — 相对位置始终是围绕玩家）
        GameplayPlane.SetPosition2D(boss.transform, desiredPos);

        // 间歇发射霰弹
        if (_fireTimer >= FIRE_INTERVAL)
        {
          _fireTimer -= FIRE_INTERVAL;
          FireShotgun(hive, playerPos);
        }

        if (_orbitElapsed >= ORBIT_DURATION)
          return State.Completed;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireShotgun(WildBossStarHive hive, Vector2 playerPos)
    {
      var toPlayer = playerPos - hive.Position;
      if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector2.right;
      var baseDir = toPlayer.normalized;

      float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
      var origin = hive.AttackOrigin;
      var req    = hive.BuildDamageReq(hive.AttackDamage * 0.4f);
      float speed  = hive.ProjectileSpeed * 0.8f;

      for (int i = 0; i < PELLETS; i++)
      {
        float t = PELLETS <= 1 ? 0f : (i / (float)(PELLETS - 1) - 0.5f);
        float rad = (baseAngle + t * SPREAD_DEG) * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

        Game.Shared.Projectile.EnemyTriangleProjectile.SpawnDirectional(
          origin + dir * 0.45f, dir, req, speed,
          hive.ProjectileScale, hive.ProjectileColor, maxRange: 22f, hitRadius: 0f);
      }
    }
  }
}
