using UnityEngine;

namespace Game.Shared.Laser
{
  /// <summary>激光光束视觉，到期淡出并销毁?/summary>
  [DisallowMultipleComponent]
  public class LaserBeamVisual : MonoBehaviour
  {
    struct SpriteState
    {
      public SpriteRenderer Renderer;
      public Color BaseColor;
    }

    float _remaining;
    float _duration;
    SpriteState[] _sprites;
    ParticleSystem[] _particleSystems;

    public void Init(float duration)
    {
      _duration = Mathf.Max(0.01f, duration);
      _remaining = _duration;
      _particleSystems = GetComponentsInChildren<ParticleSystem>();

      var renderers = GetComponentsInChildren<SpriteRenderer>();
      _sprites = new SpriteState[renderers.Length];
      for (var i = 0; i < renderers.Length; i++)
      {
        var r = renderers[i];
        _sprites[i] = new SpriteState
        {
          Renderer = r,
          BaseColor = LaserVfxShared.GetSpriteColor(r)
        };
      }
    }

    void Update()
    {
      _remaining -= Time.deltaTime;
      var t = Mathf.Clamp01(_remaining / _duration);

      if (_sprites != null)
      {
        foreach (var state in _sprites)
        {
          if (state.Renderer == null)
            continue;

          var c = state.BaseColor;
          c.a *= t;
          LaserVfxShared.SetSpriteColor(state.Renderer, c);
        }
      }

      if (_remaining <= 0f)
        Destroy(gameObject);
    }

    void OnDestroy()
    {
      if (_particleSystems == null)
        return;

      foreach (var ps in _particleSystems)
      {
        if (ps != null && ps.isPlaying)
          ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
      }
    }
  }
}