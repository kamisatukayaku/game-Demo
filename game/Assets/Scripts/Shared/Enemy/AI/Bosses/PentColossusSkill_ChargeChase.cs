using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 3 — 转弯限速追逐近战。
  /// 以略高速度持续追逐玩家试图近战攻击，具有转弯速度限制（模拟惯性）。
  /// Priority: 3, Cooldown: 8s
  /// </summary>
  public class PentColossusSkill_ChargeChase : BossSkillBase
  {
    const float CHASE_SPEED        = 3.5f;
    const float TURN_RATE_DEG      = 120f;  // 最大转弯速度（度/秒）
    const float STRIKE_RANGE       = 1.8f;
    const float MAX_CHASE_DURATION = 2.5f;
    const float STRIKE_DAMAGE_MULT = 1.5f;

    Vector2 _currentFacingDir;
    float   _elapsed;
    bool    _struck;

    public PentColossusSkill_ChargeChase()
    {
      Id       = WildBossPentColossus.SKILL_CHASE;
      Priority = 3;
      Cooldown = 8f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      return colossus != null;
    }

    public override void OnEnter(BossCore boss)
    {
      var colossus = boss as WildBossPentColossus;
      _currentFacingDir = colossus != null ? colossus.DirToPlayer() : Vector2.right;
      _elapsed = 0f;
      _struck  = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var colossus = boss as WildBossPentColossus;
      if (colossus == null) return State.Completed;

      _elapsed += dt;

      if (_struck)
      {
        // 命中后有短暂硬直
        return _elapsed >= 0.3f ? State.Completed : State.Running;
      }

      var playerPos  = colossus.GetPlayerPos();
      var toPlayer   = playerPos - colossus.Position;
      float dist     = toPlayer.magnitude;

      // 检查是否进入近战范围
      if (dist <= STRIKE_RANGE)
      {
        colossus.MeleeHit(damageMult: STRIKE_DAMAGE_MULT);
        _struck  = true;
        _elapsed = 0f;
        return State.Running;
      }

      // 超时
      if (_elapsed >= MAX_CHASE_DURATION)
        return State.Completed;

      // 计算目标方向
      var targetDir = toPlayer.normalized;

      // 转弯限速：当前朝向平滑旋转至目标方向
      float currentAngle = Mathf.Atan2(_currentFacingDir.y, _currentFacingDir.x) * Mathf.Rad2Deg;
      float targetAngle  = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
      float maxTurn      = TURN_RATE_DEG * dt;
      float newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxTurn);
      float rad          = newAngle * Mathf.Deg2Rad;
      _currentFacingDir  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

      // 朝当前面向方向移动
      colossus.MoveInDirection(_currentFacingDir, CHASE_SPEED, dt);

      return State.Running;
    }

    public override void OnExit(BossCore boss) { }
  }
}
