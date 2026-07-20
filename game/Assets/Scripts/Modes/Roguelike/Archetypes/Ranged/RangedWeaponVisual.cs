using System.Collections;

using UnityEngine;



using Game.Shared.Enemy.Visual;

using Game.Shared.Player;



namespace Game.Modes.Roguelike.Archetypes.Ranged

{

  /// <summary>远程主题攻击视觉：发射时角色 Visual 子节点缩放脉冲。</summary>

  [DisallowMultipleComponent]

  public class RangedWeaponVisual : MonoBehaviour

  {

    Transform _visualRoot;

    Coroutine _anim;

    Vector3 _homeScale = Vector3.one;

    bool _homeScaleReady;



    public static RangedWeaponVisual Ensure(GameObject player)

    {

      if (player == null)

        return null;



      var visual = player.GetComponent<RangedWeaponVisual>();

      return visual != null ? visual : player.AddComponent<RangedWeaponVisual>();

    }



    void Awake()

    {

      PlayerAttackDirector.AttackPerformed += OnAttackPerformed;

    }



    void Start() => RefreshVisualAnchor(force: true);



    void OnDestroy()

    {

      PlayerAttackDirector.AttackPerformed -= OnAttackPerformed;

    }



    void RefreshVisualAnchor(bool force = false)

    {

      var visual = transform.Find(EntityPlaceholderVisual.DefaultChildName);

      var root = visual != null ? visual : transform;



      if (!force && _visualRoot == root && _homeScaleReady)

        return;



      if (_anim != null)

      {

        StopCoroutine(_anim);

        _anim = null;

      }



      if (_visualRoot != null && _visualRoot != root)

        _visualRoot.localScale = _homeScaleReady ? _homeScale : _visualRoot.localScale;



      _visualRoot = root;

      _homeScale = _visualRoot.localScale;

      _homeScaleReady = _homeScale.sqrMagnitude > 0.0001f;

    }



    void ResetScale()

    {

      if (_visualRoot == null || !_homeScaleReady)

        return;



      _visualRoot.localScale = _homeScale;

    }



    void OnAttackPerformed(string theme, string delivery)

    {

      if (!enabled || theme != "ranged")

        return;

      RefreshVisualAnchor(force: true);

      ResetScale();



      if (_anim != null)

        StopCoroutine(_anim);



      _anim = StartCoroutine(AnimateRangedPulse());

    }



    IEnumerator AnimateRangedPulse()

    {

      if (_visualRoot == null || !_homeScaleReady)

        yield break;



      var peak = _homeScale * 1.18f;

      float elapsed = 0f;

      const float duration = 0.14f;



      while (elapsed < duration)

      {

        elapsed += Time.deltaTime;

        var t = elapsed / duration;

        var s = t < 0.45f

          ? Mathf.SmoothStep(0f, 1f, t / 0.45f)

          : Mathf.SmoothStep(1f, 0f, (t - 0.45f) / 0.55f);

        _visualRoot.localScale = Vector3.LerpUnclamped(_homeScale, peak, s);

        yield return null;

      }



      _visualRoot.localScale = _homeScale;

      _anim = null;

    }



    void OnDisable()

    {

      if (_anim != null)

      {

        StopCoroutine(_anim);

        _anim = null;

      }



      ResetScale();

    }

  }

}
