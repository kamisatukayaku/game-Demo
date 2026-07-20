using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 散射弹幕 — 三连发霰弹，每发带随机角度偏移。
  /// Priority: 1, Cooldown: 4s
  /// </summary>
  public class StarHiveSkill_ScatterBarrage : BossSkillBase
  {
    const int   BURST_COUNT    = 3;
    const float BURST_INTERVAL = 0.25f;
    const int   PELLETS        = 5;
    const float SPREAD_DEG     = 55f;
    const float ANGLE_JITTER   = 12f; // 每次霰弹中心 ±12° 随机偏移

    int   _burstIndex;
    float _burstTimer;

    public StarHiveSkill_ScatterBarrage()
    {
      Id       = WildBossStarHive.SKILL_SCATTER;
      Priority = 1;
      Cooldown = 4f;
    }

    public override bool CanTrigger(BossCore boss) => true;

    public override void OnEnter(BossCore boss)
    {
      _burstIndex = 0;
      _burstTimer = 0f;
      FireBurst(boss);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _burstTimer += dt;
      if (_burstTimer >= BURST_INTERVAL)
      {
        _burstTimer -= BURST_INTERVAL;
        _burstIndex++;
        if (_burstIndex < BURST_COUNT)
          FireBurst(boss);
      }
      return _burstIndex >= BURST_COUNT ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireBurst(BossCore boss)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return;

      // 基础方向 + 随机偏移
      var baseDir = hive.DirToPlayer();
      float jitter = Random.Range(-ANGLE_JITTER, ANGLE_JITTER);
      float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + jitter;

      var origin = hive.AttackOrigin;
      var req    = hive.BuildDamageReq(hive.AttackDamage * 0.55f);
      float speed  = hive.ProjectileSpeed * 0.9f;

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
