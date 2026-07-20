using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Laser;
namespace Game.Shared.Enemy.Visual
{
  /// <summary>
  /// 怪物运动视觉：移动时自转；激光怪缓?idle 自转；远程大小波动；激光蓄力委?<see cref="LaserChargeEffect"/>?
  /// </summary>
  [DisallowMultipleComponent]
  public class EnemyMotionVisual : MonoBehaviour
  {
    [SerializeField] float baseSpinDegPerSec = 48f;
    [SerializeField] float chargeSpinMultiplier = 4.2f;
    [SerializeField] float slowIdleSpinDegPerSec = 28f;

    Transform _visual;
    LaserChargeEffect _laserCharge;
    Coroutine _pulseRoutine;

    bool _isMoving;
    bool _slowIdleSpin;
    bool _slowOrbitSpin;
    float _slowOrbitSpinDegPerSec = 24f;
    float _spinMult = 1f;
    float _extraSpin;

    public static EnemyMotionVisual Ensure(GameObject enemyRoot)
    {
      if (enemyRoot == null)
        return null;

      var existing = enemyRoot.GetComponent<EnemyMotionVisual>();
      if (existing != null)
        return existing;

      return enemyRoot.AddComponent<EnemyMotionVisual>();
    }

    void Awake()
    {
      _visual = transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (_visual == null)
        _visual = transform;

      _laserCharge = LaserChargeEffect.Ensure(gameObject);
    }

    void Update()
    {
      if (_visual == null)
        return;

      if (_slowOrbitSpin)
      {
        _visual.Rotate(0f, 0f, _slowOrbitSpinDegPerSec * Time.deltaTime, Space.Self);
        return;
      }

      if (_slowIdleSpin && _spinMult <= 1.01f && _extraSpin < 120f && !_isMoving)
      {
        _visual.Rotate(0f, 0f, slowIdleSpinDegPerSec * Time.deltaTime, Space.Self);
        return;
      }

      var spin = (baseSpinDegPerSec + _extraSpin) * _spinMult;
      if (spin > 0.01f && (_isMoving || _spinMult > 1.05f || _extraSpin > 120f))
        _visual.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
    }

    public void SetMoving(bool moving) => _isMoving = moving;

    public void EnableSlowIdleSpin(float degPerSec = -1f)
    {
      _slowIdleSpin = true;
      if (degPerSec > 0f)
        slowIdleSpinDegPerSec = degPerSec;
    }

    /// <summary>竞技场圆环移动时的缓慢自转（覆盖常规移动自转）?/summary>
    public void EnableSlowOrbitSpin(float degPerSec = 24f)
    {
      _slowOrbitSpin = true;
      _slowOrbitSpinDegPerSec = degPerSec > 0f ? degPerSec : 24f;
    }

    public void SetSpinMultiplier(float mult) => _spinMult = Mathf.Max(0f, mult);

    public void ResetSpinMultiplier()
    {
      _spinMult = 1f;
      _extraSpin = 0f;
    }

    public void UseChargeSpin() => _spinMult = chargeSpinMultiplier;

    /// <summary>沿原路弹回时的高速自转?/summary>
    public void UseReflectReturnSpin(float degPerSec = -1f)
    {
      _slowOrbitSpin = false;
      _slowIdleSpin = false;
      _spinMult = 1f;
      _extraSpin = degPerSec > 0f ? degPerSec : 900f;
      _isMoving = true;
    }

    public void PlayRangedAttackPulse(float duration)
    {
      if (_visual == null)
        return;

      if (_pulseRoutine != null)
        StopCoroutine(_pulseRoutine);

      _pulseRoutine = StartCoroutine(RangedPulseRoutine(duration));
    }

    public void BeginLaserCharge(float duration, Vector2 fireDirection = default)
    {
      UseChargeSpin();
      _laserCharge?.BeginCharge(duration, fireDirection.sqrMagnitude > 0.0001f ? fireDirection : Vector2.right);
    }

    public void EndLaserCharge(Vector2 fireDirection = default)
    {
      ResetSpinMultiplier();
      _laserCharge?.EndCharge(fireDirection);
    }

    public void CancelLaserCharge()
    {
      ResetSpinMultiplier();
      _laserCharge?.Cancel();
    }

    System.Collections.IEnumerator RangedPulseRoutine(float duration)
    {
      var baseScale = _visual.localScale;
      var peak = baseScale * 1.22f;
      var elapsed = 0f;
      duration = Mathf.Max(0.05f, duration);

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        var t = elapsed / duration;
        var s = t < 0.42f
          ? Mathf.SmoothStep(0f, 1f, t / 0.42f)
          : Mathf.SmoothStep(1f, 0f, (t - 0.42f) / 0.58f);
        _visual.localScale = Vector3.LerpUnclamped(baseScale, peak, s);
        yield return null;
      }

      _visual.localScale = baseScale;
      _pulseRoutine = null;
    }
  }
}