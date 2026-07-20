using UnityEngine;
using Game.Shared.Combat.Damage;
using Game.Shared.Laser;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 双激光旋转 — 两道激光，初始蓄力时角度为玩家角度偏左和偏右，
  /// 随后均向玩家方向缓慢旋转。
  /// Priority: 3, Cooldown: 10s
  /// </summary>
  public class StarHiveSkill_Laser : BossSkillBase
  {
    const float WINDUP_DURATION   = 0.80f;  // 蓄力阶段
    const float ROTATE_DURATION   = 2.0f;  // 旋转阶段
    const float INITIAL_OFFSET    = 40f;   // 初始偏左/偏右角度
    const float LASER_RANGE       = 36f;
    const float BEAM_HALF_WIDTH   = 0.26f;
    const float REFRESH_INTERVAL  = 0.30f; // 激光刷新间隔（创建新实例来更新方向）
    static readonly Color LASER_COLOR = new(1f, 0.42f, 0.15f, 1f);

    // 两条激光相对于 Boss 的当前角度（从玩家方向偏移）
    float _angleLeft;
    float _angleRight;
    float _refreshTimer;
    float _elapsed;
    bool  _aimLineShown;

    public StarHiveSkill_Laser()
    {
      Id       = WildBossStarHive.SKILL_LASER;
      Priority = 3;
      Cooldown = 10f;
      Category = BossSkillCategory.AreaDenial;
    }

    public override bool CanTrigger(BossCore boss) => true;

    public override void OnEnter(BossCore boss)
    {
      _elapsed       = 0f;
      _refreshTimer  = 0f;
      _aimLineShown  = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var hive = boss as WildBossStarHive;
      if (hive == null) return State.Completed;

      _elapsed += dt;

      var toPlayer = hive.DirToPlayer();
      float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

      if (_elapsed <= WINDUP_DURATION)
      {
        _angleLeft  = targetAngle - INITIAL_OFFSET;
        _angleRight = targetAngle + INITIAL_OFFSET;

        // ── 蓄力瞄准线（红色细线，无伤害）──
        if (!_aimLineShown)
        {
          _aimLineShown = true;
          var origin = hive.AttackOrigin;
          float radL = _angleLeft * Mathf.Deg2Rad;
          float radR = _angleRight * Mathf.Deg2Rad;
          ShowLaserAimLine(origin, new Vector2(Mathf.Cos(radL), Mathf.Sin(radL)), LASER_RANGE, WINDUP_DURATION);
          ShowLaserAimLine(origin, new Vector2(Mathf.Cos(radR), Mathf.Sin(radR)), LASER_RANGE, WINDUP_DURATION);
        }
      }
      else
      {
        float rotateElapsed = _elapsed - WINDUP_DURATION;
        float progress = Mathf.Clamp01(rotateElapsed / ROTATE_DURATION);
        float t = 1f - (1f - progress) * (1f - progress);

        float startLeft  = targetAngle - INITIAL_OFFSET;
        float startRight = targetAngle + INITIAL_OFFSET;
        _angleLeft  = Mathf.LerpAngle(startLeft,  targetAngle, t);
        _angleRight = Mathf.LerpAngle(startRight, targetAngle, t);

        // 蓄力结束后才开始发射激光
        _refreshTimer += dt;
        if (_refreshTimer >= REFRESH_INTERVAL)
        {
          _refreshTimer -= REFRESH_INTERVAL;
          FireLaserPair(hive, _angleLeft, _angleRight);
        }
      }

      // 总时长结束
      if (_elapsed >= WINDUP_DURATION + ROTATE_DURATION)
        return State.Completed;

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireLaserPair(WildBossStarHive hive, float angleLeftDeg, float angleRightDeg)
    {
      var origin  = hive.AttackOrigin;
      var request = hive.BuildDamageReq(hive.AttackDamage * 0.35f, "laser");

      FireLaserAt(origin, angleLeftDeg,  request);
      FireLaserAt(origin, angleRightDeg, request);
    }

    void FireLaserAt(Vector3 origin, float angleDeg, in DamageRequest request)
    {
      float rad = angleDeg * Mathf.Deg2Rad;
      var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

      // 通过锁定方向创建激光 — 避开玩家追踪逻辑
      var settings = LaserBeamSettings.FromProfile(
        LASER_COLOR, LASER_RANGE, BEAM_HALF_WIDTH,
        duration: 0.32f, tickInterval: 0.30f);

      var attack = LaserBeamPool.Acquire();
      // 需要一个 dummy target 来满足 Begin 的签名（lockedFireDirection 会覆盖方向）
      var playerGO = GameObject.FindGameObjectWithTag("Player");
      if (playerGO == null) { LaserBeamPool.Release(attack); return; }

      attack.Begin(
        owner: request.Attacker != null ? request.Attacker.transform : null,
        target: playerGO.transform,
        settings: settings,
        request: request,
        lockedFireDirection: new Vector2(dir.x, dir.y));
    }
  }
}
