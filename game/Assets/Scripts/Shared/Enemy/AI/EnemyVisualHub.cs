using UnityEngine;
using Game.Shared.Enemy.Visual;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 敌人视觉组件：运动视觉、Debuff 视觉、冲锋提示、奔跑比例、朝向。
  /// 附加到敌人 GameObject 上，由 EnemyCore 驱动。
  /// 不挂载此组件 → 无视觉反馈（仅静态精灵）。
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyVisualHub : MonoBehaviour
  {
    EnemyMotionVisual _motionVisual;
    EnemyDebuffVisual _debuffVisual;
    bool _sprintOriginalScaleValid;
    Vector3 _sprintOriginalScale;

    void Start()
    {
      _motionVisual = EnemyMotionVisual.Ensure(gameObject);
      _debuffVisual = EnemyDebuffVisual.Ensure(gameObject);
    }

    // ── 运动 ──────────────────────────────────────

    public void SetMoving(bool moving) => _motionVisual?.SetMoving(moving);

    public void PlayRangedAttackPulse(float windup) => _motionVisual?.PlayRangedAttackPulse(windup);

    public void BeginLaserCharge(float windup, Vector2 dir) => _motionVisual?.BeginLaserCharge(windup, dir);
    public void EndLaserCharge(Vector2 dir) => _motionVisual?.EndLaserCharge(dir);
    public void CancelLaserCharge() => _motionVisual?.CancelLaserCharge();

    public void UseChargeSpin() => _motionVisual?.UseChargeSpin();
    public void UseReflectReturnSpin() => _motionVisual?.UseReflectReturnSpin();
    public void ResetSpinMultiplier() => _motionVisual?.ResetSpinMultiplier();

    public void EnableSlowIdleSpin() => _motionVisual?.EnableSlowIdleSpin();
    public void EnableSlowOrbitSpin(float speed) => _motionVisual?.EnableSlowOrbitSpin(speed);

    // ── 朝向 ──────────────────────────────────────

    public void FaceDirection(Vector2 dir)
    {
      transform.rotation = Quaternion.LookRotation(Vector3.forward, new Vector3(dir.x, dir.y, 0f));
    }

    public void FaceDirectionSlerp(Vector2 dir, float dt, float speed = 8f)
    {
      var targetRot = Quaternion.LookRotation(Vector3.forward, new Vector3(dir.x, dir.y, 0f));
      transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, speed * dt);
    }

    // ── 奔跑视觉（缩放 + 风粒子由 EnemyCore 管理）──

    public void ApplySprintScale()
    {
      var visual = transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual == null) return;
      _sprintOriginalScale = visual.localScale;
      _sprintOriginalScaleValid = true;
      visual.localScale = _sprintOriginalScale * 0.85f;
    }

    public void RestoreSprintScale()
    {
      if (!_sprintOriginalScaleValid) return;
      var visual = transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual != null) visual.localScale = _sprintOriginalScale;
      _sprintOriginalScaleValid = false;
    }

    // ── Beam 交付类型 ────────────────────────

    public void SetupBeamVisual()
    {
      _motionVisual ??= EnemyMotionVisual.Ensure(gameObject);
      _motionVisual.EnableSlowIdleSpin();
    }

    public void SetupOrbitVisual(float speed)
    {
      _motionVisual ??= EnemyMotionVisual.Ensure(gameObject);
      _motionVisual.EnableSlowOrbitSpin(speed);
    }
  }
}
