using UnityEngine;
using Game.Shared.Core;
using Game.Shared.Laser;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 4 — 部件旋转激光 + 本体环形弹幕。
  /// 子部件顺时针旋转，持续向外发射激光（固定角度，不追踪玩家）。
  /// P1: 本体发射高密度环形短射程弹幕（射程至部件位置），旋转加快。
  /// 三阶段无效。
  /// Priority: 8, Cooldown: 10s
  /// </summary>
  public class PrismNexusSkill_PartLaser : BossSkillBase
  {
    const float DURATION         = 2.5f;
    const float LASER_REFRESH    = 0.12f;
    const float LASER_RANGE      = 10f;
    const float BEAM_HALF_W      = 0.22f;
    const float LASER_DMG_MULT   = 0.3f;
    const float ROTATE_SPEED_P0  = 60f;   // 度/秒
    const float ROTATE_SPEED_P1  = 110f;

    const float RING_INTERVAL    = 0.2f;  // P1 环形弹幕间隔
    const int   RING_COUNT       = 12;
    const float RING_SPEED       = 3f;
    const float RING_DMG_MULT    = 0.25f;

    static readonly Color LASER_COLOR = new(1f, 0.55f, 0.1f, 1f);

    float _elapsed;
    float _laserTimer;
    float _ringTimer;
    float _currentAngles; // 当前旋转偏移角度
    bool  _isP1;

    public PrismNexusSkill_PartLaser()
    {
      Id = FinalBossPrismNexus.SK_LASER; Priority = 8; Cooldown = 10f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.HasParts && nexus.GamePhase < 2;
    }

    public override void OnEnter(BossCore boss)
    {
      _elapsed       = 0f;
      _laserTimer    = 0f;
      _ringTimer     = 0f;
      _currentAngles = 0f;
      var nexus      = boss as FinalBossPrismNexus;
      _isP1          = nexus != null && nexus.GamePhase >= 1;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var nexus = boss as FinalBossPrismNexus;
      if (nexus == null) return State.Completed;

      _elapsed    += dt;
      _laserTimer += dt;
      _ringTimer  += dt;

      float rotateSpeed = _isP1 ? ROTATE_SPEED_P1 : ROTATE_SPEED_P0;
      _currentAngles += rotateSpeed * dt;

      // 更新子部件旋转位置
      var ang = new float[6];
      for (int i = 0; i < 6; i++)
        ang[i] = (360f / 6) * i + _currentAngles;
      nexus.SyncPartsToAngles(ang);

      // 每帧刷新激光
      if (_laserTimer >= LASER_REFRESH)
      {
        _laserTimer -= LASER_REFRESH;
        FireLasers(nexus, ang);
      }

      // P1: 本体环形弹幕
      if (_isP1 && _ringTimer >= RING_INTERVAL)
      {
        _ringTimer -= RING_INTERVAL;
        FireRingBarrage(nexus);
      }

      return _elapsed >= DURATION ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireLasers(FinalBossPrismNexus nexus, float[] angles)
    {
      foreach (var part in nexus.Parts)
      {
        if (part == null || part.IsDestroyed) continue;
        var pos = part.transform.position;
        var outward = (GameplayPlane.Position2D(part.transform) - nexus.Position).normalized;
        if (outward.sqrMagnitude < 0.0001f) continue;

        var settings = LaserBeamSettings.FromProfile(LASER_COLOR, LASER_RANGE, BEAM_HALF_W, 0.18f, 0.15f);
        var attack = LaserBeamPool.Acquire();
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) { LaserBeamPool.Release(attack); return; }

        // 需要用一个临时 owner 来让激光从部件位置发出
        var dummyOwner = new GameObject("LaserDummy");
        dummyOwner.transform.position = pos;
        attack.Begin(dummyOwner.transform, playerGO.transform, settings,
          nexus.BuildReq(nexus.AttackDmg * LASER_DMG_MULT, "laser"),
          lockedFireDirection: outward);
        // 激光结束后清理 dummy
        GameObject.Destroy(dummyOwner, 0.25f);
      }
    }

    void FireRingBarrage(FinalBossPrismNexus nexus)
    {
      float partDist = 5.5f; // 弹幕射程到子部件位置
      for (int i = 0; i < RING_COUNT; i++)
      {
        float angle = (360f / RING_COUNT) * i + Random.Range(-5f, 5f);
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        nexus.SpawnProj(dir, RING_DMG_MULT, RING_SPEED, partDist);
      }
    }
  }
}
