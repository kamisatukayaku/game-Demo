using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Laser;
using Health = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Vfx
{
  /// <summary>
  /// 受击时闪烁（玩家常用红色，怪物常用白色）。
  /// 支持 MeshRenderer / SpriteRenderer / LineRenderer，可在子物体 Visual 上换贴图而不改逻辑。
  /// </summary>
  [DisallowMultipleComponent]
  public class HitFlash : MonoBehaviour
  {
    [SerializeField] float flashDuration = 0.1f;
    [SerializeField] Color flashColor = Color.white;

    readonly List<ColorTarget> _targets = new();
    Coroutine _flashRoutine;
    Health _health;

    struct ColorTarget
    {
      public Material Material;
      public SpriteRenderer Sprite;
      public Color BaseColor;
    }

    void Awake()
    {
      CacheTargets();
      _health = GetComponentInParent<Health>();
      if (_health != null)
        _health.Damaged += OnDamaged;
    }

    void OnDestroy()
    {
      if (_health != null)
        _health.Damaged -= OnDamaged;
    }

    void CacheTargets()
    {
      _targets.Clear();

      foreach (var r in GetComponentsInChildren<Renderer>(true))
      {
        if (r is ParticleSystemRenderer)
          continue;

        if (r is SpriteRenderer spriteRenderer)
        {
          _targets.Add(new ColorTarget
          {
            Sprite = spriteRenderer,
            BaseColor = LaserVfxShared.GetSpriteColor(spriteRenderer)
          });
          continue;
        }

        var shared = r.sharedMaterial;
        if (shared == null)
          continue;

        var mat = new Material(shared);
        r.material = mat;
        _targets.Add(new ColorTarget
        {
          Material = mat,
          BaseColor = LaserVfxShared.GetMaterialColor(mat)
        });
      }

      foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
      {
        if (_targets.Exists(t => t.Sprite == sr))
          continue;

        _targets.Add(new ColorTarget
        {
          Sprite = sr,
          BaseColor = LaserVfxShared.GetSpriteColor(sr)
        });
      }
    }

    void OnDamaged(float _)
    {
      if (_targets.Count == 0)
        CacheTargets();

      if (_flashRoutine != null)
        StopCoroutine(_flashRoutine);

      _flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
      ApplyColor(flashColor);
      yield return new WaitForSeconds(flashDuration);
      RestoreBaseColors();
      _flashRoutine = null;
    }

    void ApplyColor(Color c)
    {
      foreach (var t in _targets)
      {
        if (t.Material != null)
          LaserVfxShared.SetMaterialColor(t.Material, c);
        if (t.Sprite != null)
          LaserVfxShared.SetSpriteColor(t.Sprite, c);
      }
    }

    void RestoreBaseColors()
    {
      foreach (var t in _targets)
      {
        if (t.Material != null)
          LaserVfxShared.SetMaterialColor(t.Material, t.BaseColor);
        if (t.Sprite != null)
          LaserVfxShared.SetSpriteColor(t.Sprite, t.BaseColor);
      }
    }

    /// <summary>Setup 菜单：同步基准色与受击闪色。</summary>
    public void Configure(Color baseColor, Color? hitFlashColor = null)
    {
      if (hitFlashColor.HasValue)
        flashColor = hitFlashColor.Value;

      if (!Application.isPlaying)
        return;

      CacheTargets();
      for (var i = 0; i < _targets.Count; i++)
      {
        var t = _targets[i];
        t.BaseColor = baseColor;
        _targets[i] = t;
      }

      if (_flashRoutine == null)
        RestoreBaseColors();
    }

    public void SetBaseColor(Color color) => Configure(color);
  }
}
