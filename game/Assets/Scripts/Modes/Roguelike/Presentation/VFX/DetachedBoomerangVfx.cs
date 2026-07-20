using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedBoomerangVfx : MonoBehaviour
  {
    const int BurstPoolSize = 20;
    static DetachedBoomerangVfx s_instance;
    BurstFx[] _bursts;

    public enum BurstKind
    {
      Launch,
      HitOutbound,
      HitReturn,
      Turn,
      Recast,
      Return
    }

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_DetachedBoomerangVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<DetachedBoomerangVfx>();
    }

    public static void Attach(GameObject body, bool echo)
    {
      if (body == null)
        return;
      var visual = body.GetComponent<BoomerangBodyVisual>();
      if (visual == null)
        visual = body.AddComponent<BoomerangBodyVisual>();
      visual.Configure(echo);
    }

    public static void SetReturning(GameObject body, bool returning)
    {
      if (body == null)
        return;
      var visual = body.GetComponent<BoomerangBodyVisual>();
      if (visual != null)
        visual.SetReturning(returning);
    }

    public static void PlayTurn(GameObject body, Vector3 position, float scale)
    {
      SetReturning(body, true);
      PlayBurst(position, scale, BurstKind.Turn);
    }

    public static void PlayBurst(Vector3 position, float scale, bool impact)
    {
      PlayBurst(position, scale, impact ? BurstKind.HitOutbound : BurstKind.Launch);
    }

    public static void PlayBurst(Vector3 position, float scale, BurstKind kind)
    {
      EnsureExists();
      foreach (var burst in s_instance._bursts)
      {
        if (!burst.Active)
        {
          burst.Play(position, scale, kind);
          return;
        }
      }
    }

    public static void ResetAll()
    {
      EnsureExists();
      if (s_instance == null)
        return;
      foreach (var burst in s_instance._bursts)
        burst.ForceStop();
    }

    public static string GetDebugSummary()
    {
      if (s_instance == null)
        return "Boomerang inactive";
      var active = 0;
      foreach (var burst in s_instance._bursts)
        if (burst.Active)
          active++;
      return $"Boomerang {active}/{s_instance._bursts.Length}";
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }
      s_instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
      _bursts = new BurstFx[BurstPoolSize];
      for (var i = 0; i < _bursts.Length; i++)
        _bursts[i] = BurstFx.Create(transform, i);
    }

    void Update()
    {
      var dt = Time.unscaledDeltaTime;
      foreach (var burst in _bursts)
        burst.Tick(dt);
    }

    [DisallowMultipleComponent]
    sealed class BoomerangBodyVisual : MonoBehaviour
    {
      DetachedWeaponVisualState _state;
      LineRenderer _blade;
      LineRenderer _innerRing;
      LineRenderer _edge;
      SpriteRenderer _core;
      SpriteRenderer _glow;
      SpriteRenderer[] _sparks;
      TrailRenderer _trail;
      TrailRenderer _returnTrail;
      float _rotation;
      float _turnFlash;
      bool _echo;
      bool _returning;
      bool _built;

      void Awake()
      {
        EnsureBuilt();
      }

      void EnsureBuilt()
      {
        _state = GetComponent<DetachedWeaponVisualState>();
        if (_built && _blade != null && _innerRing != null && _edge != null && _core != null && _glow != null && _trail != null && _returnTrail != null && _sparks != null)
          return;
        _built = false;
        var material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        if (_blade == null)
          _blade = gameObject.AddComponent<LineRenderer>();
        if (_blade == null)
          return;
        _blade.useWorldSpace = false;
        _blade.loop = true;
        _blade.positionCount = 13;
        _blade.material = material;
        _blade.sortingLayerName = LaserVfxShared.SortingLayerName;
        _blade.sortingOrder = 59;
        _blade.startWidth = _blade.endWidth = 0.075f;
        BuildWheel(_blade, 6, 0.64f, 0.39f, 0f);

        if (_innerRing == null)
          _innerRing = CreateLoop("BoomerangInnerRing", 12, 58, 0.042f);
        if (_innerRing == null)
          return;
        BuildRing(_innerRing, 0.32f);

        if (_edge == null)
          _edge = CreateLoop("BoomerangCuttingEdge", 6, 60, 0.055f);
        if (_edge == null)
          return;
        BuildWheel(_edge, 3, 0.68f, 0.52f, 30f);

        if (_core == null)
        {
          var coreGo = new GameObject("BoomerangCore");
          coreGo.transform.SetParent(transform, false);
          _core = coreGo.AddComponent<SpriteRenderer>();
          coreGo.transform.localScale = Vector3.one * 0.34f;
        }
        if (_core == null)
          return;
        _core.sprite = LaserVfxShared.SoftGlowSprite;
        _core.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        _core.sortingLayerName = LaserVfxShared.SortingLayerName;
        _core.sortingOrder = 61;

        if (_glow == null)
        {
          var glowGo = new GameObject("BoomerangGlow");
          glowGo.transform.SetParent(transform, false);
          _glow = glowGo.AddComponent<SpriteRenderer>();
          glowGo.transform.localScale = Vector3.one * 1.18f;
        }
        if (_glow == null)
          return;
        _glow.sprite = LaserVfxShared.SoftGlowSprite;
        _glow.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        _glow.sortingLayerName = LaserVfxShared.SortingLayerName;
        _glow.sortingOrder = 57;

        if (_sparks == null || _sparks.Length < 5)
        {
          _sparks = new SpriteRenderer[5];
          for (var i = 0; i < _sparks.Length; i++)
          {
            var sparkGo = new GameObject($"CutSpark_{i + 1}");
            sparkGo.transform.SetParent(transform, false);
            var spark = sparkGo.AddComponent<SpriteRenderer>();
            spark.sprite = LaserVfxShared.SoftGlowSprite;
            spark.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
            spark.sortingLayerName = LaserVfxShared.SortingLayerName;
            spark.sortingOrder = 62;
            _sparks[i] = spark;
          }
        }

        if (_trail == null)
          _trail = CreateTrail("OutboundTrail");
        if (_trail == null)
          return;
        _trail.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        _trail.time = 0.18f;
        _trail.startWidth = 0.12f;
        _trail.endWidth = 0f;
        _trail.minVertexDistance = 0.075f;
        _trail.sortingLayerName = LaserVfxShared.SortingLayerName;
        _trail.sortingOrder = 56;
        _trail.numCapVertices = 1;

        if (_returnTrail == null)
          _returnTrail = CreateTrail("ReturnTrail");
        if (_returnTrail == null)
          return;
        _returnTrail.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        _returnTrail.time = 0.14f;
        _returnTrail.startWidth = 0.16f;
        _returnTrail.endWidth = 0f;
        _returnTrail.minVertexDistance = 0.07f;
        _returnTrail.sortingLayerName = LaserVfxShared.SortingLayerName;
        _returnTrail.sortingOrder = 55;
        _returnTrail.numCapVertices = 1;
        _returnTrail.emitting = false;
        _built = true;
      }

      TrailRenderer CreateTrail(string trailName)
      {
        var go = new GameObject(trailName);
        go.transform.SetParent(transform, false);
        return go.AddComponent<TrailRenderer>();
      }

      LineRenderer CreateLoop(string name, int count, int order, float width)
      {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = count + 1;
        line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 3;
        return line;
      }

      static void BuildRing(LineRenderer line, float radius)
      {
        var count = line.positionCount;
        for (var i = 0; i < count; i++)
        {
          var angle = i / (float)(count - 1) * Mathf.PI * 2f;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
      }

      static void BuildWheel(LineRenderer line, int segments, float outerRadius, float innerRadius, float offsetDegrees)
      {
        var count = line.positionCount;
        for (var i = 0; i < count; i++)
        {
          var step = i % (segments * 2);
          var radius = step % 2 == 0 ? outerRadius : innerRadius;
          var angle = (offsetDegrees + step / (segments * 2f) * 360f) * Mathf.Deg2Rad;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
      }

      public void Configure(bool echo)
      {
        EnsureBuilt();
        if (!_built)
          return;
        _echo = echo;
        _returning = false;
        ApplyColors();
        _trail.Clear();
        _returnTrail.Clear();
      }

      public void SetReturning(bool returning)
      {
        EnsureBuilt();
        if (!_built)
          return;
        if (_returning == returning)
          return;
        _returning = returning;
        _turnFlash = returning ? 0.22f : 0.1f;
        ApplyColors();
        if (returning)
          _returnTrail.Clear();
        else
          _trail.Clear();
      }

      void ApplyColors()
      {
        EnsureBuilt();
        if (_blade == null || _innerRing == null || _edge == null || _trail == null || _returnTrail == null)
          return;
        var alpha = _echo ? 0.68f : 1f;
        var core = _returning
          ? new Color(1f, 0.9f, 0.52f, alpha)
          : new Color(1f, 0.74f, 0.24f, alpha);
        var edge = _returning
          ? new Color(1f, 0.46f, 0.12f, _echo ? 0.34f : 0.58f)
          : new Color(1f, 0.34f, 0.06f, _echo ? 0.28f : 0.46f);
        var halo = _returning
          ? new Color(1f, 0.38f, 0.08f, _echo ? 0.1f : 0.16f)
          : new Color(1f, 0.3f, 0.04f, _echo ? 0.08f : 0.13f);
        LaserVfxShared.SetLineColor(_blade, core, edge);
        LaserVfxShared.SetLineColor(_innerRing, new Color(core.r, core.g, core.b, core.a * 0.72f), new Color(core.r, core.g, core.b, core.a * 0.72f));
        LaserVfxShared.SetLineColor(_edge, edge, edge);
        _trail.startColor = new Color(core.r, core.g, core.b, _echo ? 0.16f : 0.26f);
        _trail.endColor = new Color(edge.r, edge.g, edge.b, 0f);
        _returnTrail.startColor = new Color(1f, 0.78f, 0.28f, _echo ? 0.2f : 0.34f);
        _returnTrail.endColor = new Color(1f, 0.26f, 0.04f, 0f);
        LaserVfxShared.SetSpriteColor(_core, new Color(core.r, core.g, core.b, _echo ? 0.28f : 0.46f));
        LaserVfxShared.SetSpriteColor(_glow, halo);
        if (_sparks != null)
        {
          foreach (var spark in _sparks)
            LaserVfxShared.SetSpriteColor(spark, new Color(1f, 0.66f, 0.18f, 0.2f));
        }
      }

      void LateUpdate()
      {
        EnsureBuilt();
        if (_blade == null || _innerRing == null || _edge == null || _core == null || _glow == null || _trail == null || _returnTrail == null)
          return;
        var visible = _state != null && _state.AttackActive;
        _blade.enabled = visible;
        _innerRing.enabled = visible;
        _edge.enabled = visible;
        _core.enabled = visible;
        _glow.enabled = visible;
        SetSparksVisible(visible);
        _trail.emitting = visible && !_returning;
        _returnTrail.emitting = visible && _returning;
        if (!visible)
        {
          _trail.Clear();
          _returnTrail.Clear();
          _returning = false;
          return;
        }

        var dt = Time.unscaledDeltaTime;
        _rotation += (_returning ? 760f : 520f) * dt;
        _turnFlash = Mathf.Max(0f, _turnFlash - dt);
        var flash = Mathf.Clamp01(_turnFlash / 0.22f);
        var pulse = 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.04f + flash * 0.18f;
        _blade.transform.localRotation = Quaternion.Euler(0f, 0f, _rotation);
        _innerRing.transform.localRotation = Quaternion.Euler(0f, 0f, -_rotation * 0.55f);
        _edge.transform.localRotation = Quaternion.Euler(0f, 0f, _rotation * 1.35f);
        _blade.transform.localScale = Vector3.one * pulse;
        _edge.transform.localScale = Vector3.one * (1f + flash * 0.28f);
        _glow.transform.localScale = Vector3.one * (_returning ? 1.32f : 1.18f) * (1f + flash * 0.22f);
        UpdateSparks(flash);
      }

      void SetSparksVisible(bool visible)
      {
        if (_sparks == null)
          return;
        foreach (var spark in _sparks)
          if (spark != null)
            spark.enabled = visible;
      }

      void UpdateSparks(float flash)
      {
        if (_sparks == null)
          return;
        var time = Time.unscaledTime;
        var speedBoost = _returning ? 1.35f : 1f;
        for (var i = 0; i < _sparks.Length; i++)
        {
          var spark = _sparks[i];
          if (spark == null)
            continue;
          var phase = Mathf.Repeat(time * (2.8f + i * 0.27f) * speedBoost + i * 0.19f, 1f);
          var angle = (_rotation + i * 73f + phase * 120f) * Mathf.Deg2Rad;
          var radius = Mathf.Lerp(0.32f, 0.82f, phase);
          spark.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
          spark.transform.localScale = Vector3.one * Mathf.Lerp(0.075f, 0.022f, phase) * (_returning ? 1.18f : 1f);
          var alpha = (1f - phase) * (_echo ? 0.18f : 0.34f) * (1f + flash * 0.85f);
          LaserVfxShared.SetSpriteColor(spark, new Color(Mathf.Lerp(0.22f, 0.82f, 1f - phase), 1f, Mathf.Lerp(0.28f, 0.48f, 1f - phase), alpha));
        }
      }
    }

    sealed class BurstFx
    {
      readonly GameObject _root;
      readonly LineRenderer _ring;
      readonly LineRenderer _cross;
      readonly LineRenderer _fan;
      float _age;
      float _duration;
      float _scale;
      BurstKind _kind;

      public bool Active => _root.activeSelf;

      BurstFx(GameObject root, LineRenderer ring, LineRenderer cross, LineRenderer fan)
      {
        _root = root;
        _ring = ring;
        _cross = cross;
        _fan = fan;
        root.SetActive(false);
      }

      public static BurstFx Create(Transform parent, int index)
      {
        var root = new GameObject($"BoomerangBurst_{index + 1}");
        root.transform.SetParent(parent, false);
        var ring = CreateLine(root.transform, "Ring", 61, 17);
        ring.loop = true;
        for (var i = 0; i < 17; i++)
        {
          var angle = i / 16f * Mathf.PI * 2f;
          ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
        var cross = CreateLine(root.transform, "ImpactCross", 62, 9);
        cross.loop = true;
        for (var i = 0; i < 9; i++)
        {
          var angle = i / 8f * Mathf.PI * 2f;
          cross.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 1f : 0.2f));
        }
        var fan = CreateLine(root.transform, "CutFan", 63, 7);
        fan.loop = false;
        for (var i = 0; i < 7; i++)
        {
          var angle = Mathf.Lerp(-55f, 55f, i / 6f) * Mathf.Deg2Rad;
          var radius = i == 0 || i == 6 ? 0.45f : 1f;
          fan.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return new BurstFx(root, ring, cross, fan);
      }

      static LineRenderer CreateLine(Transform parent, string name, int order, int count)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.numCapVertices = 3;
        return line;
      }

      public void Play(Vector3 position, float scale, BurstKind kind)
      {
        _root.transform.position = position;
        _root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _scale = Mathf.Max(0.2f, scale);
        _kind = kind;
        _duration = kind switch
        {
          BurstKind.HitOutbound => 0.16f,
          BurstKind.HitReturn => 0.18f,
          BurstKind.Turn => 0.2f,
          BurstKind.Recast => 0.16f,
          BurstKind.Return => 0.14f,
          _ => 0.12f
        };
        _age = 0f;
        _root.SetActive(true);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var alpha = 1f - t;
        var color = _kind switch
        {
          BurstKind.HitReturn => new Color(1f, 0.92f, 0.5f, alpha),
          BurstKind.HitOutbound => new Color(1f, 0.62f, 0.08f, alpha),
          BurstKind.Turn => new Color(1f, 0.36f, 0.04f, alpha * 0.9f),
          BurstKind.Return => new Color(1f, 0.78f, 0.22f, alpha * 0.75f),
          _ => new Color(1f, 0.7f, 0.12f, alpha * 0.82f)
        };
        var ringScale = _kind == BurstKind.HitReturn
          ? Mathf.Lerp(_scale, 0.08f, t)
          : Mathf.Lerp(0.04f, _scale, t);
        var fanAlpha = (_kind == BurstKind.HitOutbound || _kind == BurstKind.HitReturn) ? alpha : 0f;
        _ring.startColor = _ring.endColor = color;
        _cross.startColor = _cross.endColor = color;
        _fan.startColor = _fan.endColor = new Color(color.r, color.g, color.b, fanAlpha);
        _ring.startWidth = _ring.endWidth = 0.05f * alpha;
        _cross.startWidth = _cross.endWidth = 0.085f * alpha;
        _fan.startWidth = _fan.endWidth = 0.09f * fanAlpha;
        _ring.transform.localScale = Vector3.one * ringScale;
        _cross.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, _scale * 0.72f, t);
        _fan.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, _scale * 0.82f, t);
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);
    }
  }
}
