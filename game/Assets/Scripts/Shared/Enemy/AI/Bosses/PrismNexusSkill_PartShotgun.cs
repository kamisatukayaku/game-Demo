using UnityEngine;
using Game.Shared.Core;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 3 — 部件固定角度霰弹。
  /// 从 6 个子部件各向外发射一组霰弹（固定角度，不瞄准玩家）。
  /// P0: 标准霰弹；P1: 散射数量提高。
  /// 三阶段无效。
  /// Priority: 7, Cooldown: 4s
  /// </summary>
  public class PrismNexusSkill_PartShotgun : BossSkillBase
  {
    const float CAST_TIME = 0.2f;
    int   PelletsPerPart => GamePhase == 1 ? 5 : 3;
    const float SPREAD   = 20f;
    const float RANGE    = 12f;
    const float SPEED    = 5f;
    const float DMG_MULT = 0.4f;

    int   GamePhase;
    float _elapsed;
    bool  _fired;

    public PrismNexusSkill_PartShotgun()
    {
      Id = FinalBossPrismNexus.SK_SHOTGUN; Priority = 7; Cooldown = 4f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.HasParts && nexus.GamePhase < 2;
    }

    public override void OnEnter(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      GamePhase = nexus != null ? nexus.GamePhase : 0;
      _elapsed  = 0f;
      _fired    = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;
      if (!_fired && _elapsed >= CAST_TIME)
      {
        _fired = true;
        var nexus = boss as FinalBossPrismNexus;
        if (nexus != null) Fire(nexus);
      }
      return _elapsed >= CAST_TIME + 0.3f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void Fire(FinalBossPrismNexus nexus)
    {
      foreach (var part in nexus.Parts)
      {
        if (part == null || part.IsDestroyed) continue;
        var pos = part.transform.position;
        var outward = (GameplayPlane.Position2D(part.transform) - nexus.Position).normalized;
        if (outward.sqrMagnitude < 0.0001f) continue;

        float baseAngle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
        for (int i = 0; i < PelletsPerPart; i++)
        {
          float t = PelletsPerPart <= 1 ? 0f : (i / (float)(PelletsPerPart - 1) - 0.5f);
          float rad = (baseAngle + t * SPREAD) * Mathf.Deg2Rad;
          nexus.SpawnProjFrom(pos, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)),
            DMG_MULT, SPEED, RANGE);
        }
      }
    }
  }
}
