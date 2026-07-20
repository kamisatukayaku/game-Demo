using UnityEngine;
using Game.Shared.Core;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 脱离 — HP &lt; 50% 时触发（限一次），清除所有其他技能 CD。
  /// 以高速移动至安全距离；若已在安全距离外，则高速绕玩家旋转 120°。
  /// 移动期间持续以自身为中心向四周发射弹速极慢、射程很近的子弹。
  /// Priority: 0（最高优先）, Cooldown: 20s
  /// </summary>
  public class StarHiveSkill_Escape : BossSkillBase
  {
    const float ESCAPE_SPEED       = 8f;
    const float SAFE_DISTANCE      = 10f;
    const float ORBIT_ARC_DEG      = 120f;
    const float ORBIT_RADIUS       = 6f;
    const float ORBIT_DEG_PER_SEC  = 160f;
    const float RADIAL_FIRE_INTERVAL = 0.1f;  // 每 0.1s 发射一轮
    const int   RADIAL_COUNT       = 12;      // 12 方向
    const float SLOW_BULLET_SPEED  = 1.5f;
    const float SLOW_BULLET_RANGE  = 9f;
    const float SLOW_BULLET_SCALE  = 0.38f;

    enum Phase { Escaping, Orbiting }

    Phase  _phase;
    float  _orbitStartAngle;
    float  _orbitTraveled;
    float  _fireTimer;

    public StarHiveSkill_Escape()
    {
      Id       = WildBossStarHive.SKILL_ESCAPE;
      Priority = 0;
      Cooldown = 20f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return false;
      return hive.GetHpRatio() < 0.5f && !hive.IsEscapeUsed;
    }

    public override void OnEnter(BossCore boss)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return;

      // 清除所有其他技能 CD（仅一次）
      hive.SetSkillCooldown(WildBossStarHive.SKILL_SCATTER, 0f);
      hive.SetSkillCooldown(WildBossStarHive.SKILL_SWEEP,   0f);
      hive.SetSkillCooldown(WildBossStarHive.SKILL_LASER,   0f);
      hive.SetSkillCooldown(WildBossStarHive.SKILL_ORBIT,   0f);
      hive.MarkEscapeUsed();

      _phase          = Phase.Escaping;
      _orbitTraveled  = 0f;
      _fireTimer      = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return State.Completed;

      var playerPos = hive.GetPlayerPos();
      float dist    = Vector2.Distance(hive.Position, playerPos);

      // 持续发射径向慢速弹
      _fireTimer += dt;
      if (_fireTimer >= RADIAL_FIRE_INTERVAL)
      {
        _fireTimer -= RADIAL_FIRE_INTERVAL;
        FireRadialBullets(hive);
      }

      if (_phase == Phase.Escaping)
      {
        if (dist >= SAFE_DISTANCE)
        {
          // 已经够远 → 切换为环绕阶段
          _phase = Phase.Orbiting;
          _orbitStartAngle = Mathf.Atan2(
            hive.Position.y - playerPos.y,
            hive.Position.x - playerPos.x) * Mathf.Rad2Deg;
          _orbitTraveled = 0f;
        }
        else
        {
          // 高速远离
          Vector2 away = (hive.Position - playerPos).normalized;
          hive.MoveInDirection(away, ESCAPE_SPEED, dt);
        }
      }

      if (_phase == Phase.Orbiting)
      {
        _orbitTraveled += ORBIT_DEG_PER_SEC * dt;
        float currentAngle = _orbitStartAngle + _orbitTraveled;
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector2 desiredPos = playerPos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ORBIT_RADIUS;

        GameplayPlane.SetPosition2D(boss.transform, desiredPos);

        if (_orbitTraveled >= ORBIT_ARC_DEG)
          return State.Completed;
      }

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireRadialBullets(WildBossStarHive hive)
    {
      var origin  = hive.AttackOrigin;
      var req     = hive.BuildDamageReq(hive.AttackDamage * 0.25f);

      for (int i = 0; i < RADIAL_COUNT; i++)
      {
        float angle = (360f / RADIAL_COUNT) * i;
        float rad   = angle * Mathf.Deg2Rad;
        var dir     = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

        Game.Shared.Projectile.EnemyTriangleProjectile.SpawnDirectional(
          origin, dir, req,
          speed: SLOW_BULLET_SPEED,
          scale: SLOW_BULLET_SCALE,
          color: hive.ProjectileColor,
          maxRange: SLOW_BULLET_RANGE,
          hitRadius: 0f);
      }
    }
  }
}
