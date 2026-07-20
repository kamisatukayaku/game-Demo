using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 扫射弹幕 — 十五连发普通子弹，每发存在较大角度随机偏移。
  /// Priority: 2, Cooldown: 6s
  /// </summary>
  public class StarHiveSkill_SweepBarrage : BossSkillBase
  {
    const int   SHOT_COUNT     = 15;
    const float SHOT_INTERVAL  = 0.1f;
    const float ANGLE_SPREAD   = 72f; // 弹幕散布范围 ±36°

    int   _shotIndex;
    float _shotTimer;

    public StarHiveSkill_SweepBarrage()
    {
      Id       = WildBossStarHive.SKILL_SWEEP;
      Priority = 2;
      Cooldown = 6f;
    }

    public override bool CanTrigger(BossCore boss) => true;

    public override void OnEnter(BossCore boss)
    {
      _shotIndex = 0;
      _shotTimer = 0f;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return State.Completed;

      _shotTimer += dt;

      // 在射击间隔内尽可能多地补射（处理低帧率时一次 Tick 跳过多发的情况）
      while (_shotTimer >= SHOT_INTERVAL && _shotIndex < SHOT_COUNT)
      {
        _shotTimer -= SHOT_INTERVAL;
        _shotIndex++;
        FireShot(hive);
      }

      return _shotIndex >= SHOT_COUNT ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireShot(WildBossStarHive hive)
    {
      var baseDir = hive.DirToPlayer();
      float randomAngle = Random.Range(-ANGLE_SPREAD * 0.5f, ANGLE_SPREAD * 0.5f);
      float baseDeg = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg + randomAngle;
      float rad = baseDeg * Mathf.Deg2Rad;
      var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

      hive.SpawnDirectionalProjectile(dir, damageMult: 0.4f);
    }
  }
}
