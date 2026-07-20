using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>B13: Red synergy tint when support buffs a shooter ally.</summary>
  [DisallowMultipleComponent]
  public sealed class MonsterSynergyTintVfx : MonoBehaviour
  {
    const float DefaultDuration = 2f;
    static readonly Color SynergyRed = new(1f, 0.28f, 0.22f, 1f);

    readonly List<SpriteRenderer> _sprites = new();
    readonly List<Color> _baseColors = new();
    float _duration = DefaultDuration;
    float _age;
    bool _active;

    public static void Play(GameObject target, float duration = DefaultDuration)
    {
      if (target == null)
        return;

      var vfx = target.GetComponent<MonsterSynergyTintVfx>();
      if (vfx == null)
        vfx = target.AddComponent<MonsterSynergyTintVfx>();
      vfx.Begin(duration);
    }

    void Begin(float duration)
    {
      CacheSprites();
      _duration = Mathf.Max(0.1f, duration);
      _age = 0f;
      _active = true;
      enabled = true;
    }

    void Update()
    {
      if (!_active)
        return;

      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / _duration);
      var blend = Mathf.Sin(t * Mathf.PI);
      for (var i = 0; i < _sprites.Count; i++)
      {
        if (_sprites[i] == null)
          continue;
        _sprites[i].color = Color.Lerp(_baseColors[i], SynergyRed, blend * 0.88f);
      }

      if (t < 1f)
        return;

      _active = false;
      Restore();
      enabled = false;
    }

    void OnDisable()
    {
      if (_active)
        Restore();
      _active = false;
    }

    void CacheSprites()
    {
      if (_sprites.Count > 0)
        return;

      GetComponentsInChildren(true, _sprites);
      foreach (var sprite in _sprites)
        _baseColors.Add(sprite != null ? sprite.color : Color.white);
    }

    void Restore()
    {
      for (var i = 0; i < _sprites.Count; i++)
      {
        if (_sprites[i] == null)
          continue;
        _sprites[i].color = _baseColors[i];
      }
    }
  }
}
