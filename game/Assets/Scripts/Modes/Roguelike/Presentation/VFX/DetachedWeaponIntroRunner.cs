using System.Collections;
using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Shared.Core;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class DetachedWeaponIntroRunner : MonoBehaviour
  {
    Coroutine _routine;
    DetachedWeaponVisual _visual;

    public bool IsRunning { get; private set; }

    public void Begin(Transform owner, Vector3 targetWorld, float delay, string weaponId)
    {
      if (_routine != null)
        StopCoroutine(_routine);
      _visual = GetComponent<DetachedWeaponVisual>();
      _routine = StartCoroutine(Run(owner, targetWorld, delay, weaponId));
    }

    IEnumerator Run(Transform owner, Vector3 targetWorld, float delay, string weaponId)
    {
      IsRunning = true;
      var state = GetComponent<DetachedWeaponVisualState>();
      state?.SetIntroActive(true);
      state?.ResetPresentationState();
      state?.SetIntroActive(true);
      _visual?.ResetForSpawn();

      transform.localScale = Vector3.zero;
      if (owner != null)
        transform.position = owner.position;

      if (delay > 0f)
        yield return new WaitForSeconds(delay);

      var elapsed = 0f;
      var start = owner != null ? owner.position : transform.position;
      transform.position = start;

      while (elapsed < DetachedWeaponSpawnSettings.TotalIntroDuration)
      {
        elapsed += Time.deltaTime;
        var t = elapsed;

        if (t < DetachedWeaponSpawnSettings.InvisibleDuration)
        {
          transform.localScale = Vector3.zero;
        }
        else if (t < DetachedWeaponSpawnSettings.InvisibleDuration + DetachedWeaponSpawnSettings.CoreScaleInDuration)
        {
          var local = (t - DetachedWeaponSpawnSettings.InvisibleDuration) / DetachedWeaponSpawnSettings.CoreScaleInDuration;
          var eased = Mathf.SmoothStep(0f, 1f, local);
          transform.localScale = Vector3.one * (eased * DetachedWeaponSpawnSettings.CoreScaleInTarget);
          transform.position = start;
        }
        else if (t < DetachedWeaponSpawnSettings.InvisibleDuration + DetachedWeaponSpawnSettings.CoreScaleInDuration + DetachedWeaponSpawnSettings.FlyDuration)
        {
          transform.localScale = Vector3.one * DetachedWeaponSpawnSettings.CoreScaleInTarget;
          var flyT = (t - DetachedWeaponSpawnSettings.InvisibleDuration - DetachedWeaponSpawnSettings.CoreScaleInDuration)
            / DetachedWeaponSpawnSettings.FlyDuration;
          var easedFly = Mathf.SmoothStep(0f, 1f, flyT);
          var overshoot = Vector3.Lerp(start, targetWorld, easedFly);
          if (flyT > 0.82f)
          {
            var settleT = (flyT - 0.82f) / 0.18f;
            var overshootPos = Vector3.Lerp(start, targetWorld, 1f) +
              (targetWorld - start).normalized * Vector3.Distance(start, targetWorld) * (DetachedWeaponSpawnSettings.OvershootFactor - 1f);
            overshoot = Vector3.Lerp(overshootPos, targetWorld, Mathf.SmoothStep(0f, 1f, settleT));
          }
          transform.position = overshoot;
        }
        else
        {
          transform.localScale = Vector3.one;
          var settleElapsed = t - (DetachedWeaponSpawnSettings.InvisibleDuration + DetachedWeaponSpawnSettings.CoreScaleInDuration + DetachedWeaponSpawnSettings.FlyDuration);
          var settleT = Mathf.Clamp01(settleElapsed / DetachedWeaponSpawnSettings.SettleDuration);
          transform.position = Vector3.Lerp(transform.position, targetWorld, Mathf.SmoothStep(0f, 1f, settleT));
        }

        yield return null;
      }

      transform.position = targetWorld;
      transform.localScale = Vector3.one;
      state?.SetIntroActive(false);
      IsRunning = false;
      _routine = null;
    }

    void OnDisable()
    {
      if (_routine != null)
      {
        StopCoroutine(_routine);
        _routine = null;
      }
      IsRunning = false;
      GetComponent<DetachedWeaponVisualState>()?.SetIntroActive(false);
    }
  }
}
