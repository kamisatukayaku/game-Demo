using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedTrailVfx : MonoBehaviour
  {
    const int PoolSize = 128;
    static DetachedTrailVfx s_instance;
    TrailLineFx[] _pool;
    TrailImpactFx[] _impacts;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_DetachedTrailVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<DetachedTrailVfx>();
    }

    public static void Play(
      Vector3 start,
      Vector3 end,
      float width,
      float lifetime,
      bool branch,
      bool network)
    {
      EnsureExists();
      foreach (var effect in s_instance._pool)
      {
        if (!effect.Active)
        {
          effect.Play(start, end, width, lifetime, branch, network);
          return;
        }
      }

      var oldest = s_instance._pool[0];
      foreach (var effect in s_instance._pool)
      {
        if (effect.NormalizedAge > oldest.NormalizedAge)
          oldest = effect;
      }
      oldest.Play(start, end, width, lifetime, branch, network);
    }

    public static void PlayImpact(Vector3 position, bool critical)
    {
      EnsureExists();
      foreach (var effect in s_instance._impacts)
      {
        if (!effect.Active)
        {
          effect.Play(position, critical);
          return;
        }
      }
    }

    public static void ResetAll()
    {
      EnsureExists();
      if (s_instance == null)
        return;
      foreach (var effect in s_instance._pool)
        effect.ForceStop();
      foreach (var impact in s_instance._impacts)
        impact.ForceStop();
    }

    public static string GetDebugSummary()
    {
      if (s_instance == null)
        return "Trail inactive";
      var active = 0;
      foreach (var effect in s_instance._pool)
        if (effect.Active)
          active++;
      return $"Trail {active}/{s_instance._pool.Length}";
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
      _pool = new TrailLineFx[PoolSize];
      for (var i = 0; i < _pool.Length; i++)
        _pool[i] = TrailLineFx.Create(transform, i);
      _impacts = new TrailImpactFx[48];
      for (var i = 0; i < _impacts.Length; i++)
        _impacts[i] = TrailImpactFx.Create(transform, i);
    }

    void Update()
    {
      var dt = Time.unscaledDeltaTime;
      foreach (var effect in _pool)
        effect.Tick(dt);
      foreach (var impact in _impacts)
        impact.Tick(dt);
    }

    sealed class TrailLineFx
    {
      readonly GameObject _root;
      readonly LineRenderer _glow;
      readonly LineRenderer _core;
      readonly LineRenderer _edge;
      readonly LineRenderer _edgeB;
      readonly SpriteRenderer[] _flowNodes;
      readonly SpriteRenderer _intersection;
      Vector3 _start;
      Vector3 _end;
      Vector3 _normal;
      float _age;
      float _lifetime;
      float _width;
      bool _branch;
      bool _network;

      public bool Active => _root.activeSelf;
      public float NormalizedAge => _lifetime > 0f ? _age / _lifetime : 1f;

      TrailLineFx(
        GameObject root,
        LineRenderer glow,
        LineRenderer core,
        LineRenderer edge,
        LineRenderer edgeB,
        SpriteRenderer[] flowNodes,
        SpriteRenderer intersection)
      {
        _root = root;
        _glow = glow;
        _core = core;
        _edge = edge;
        _edgeB = edgeB;
        _flowNodes = flowNodes;
        _intersection = intersection;
        root.SetActive(false);
      }

      public static TrailLineFx Create(Transform parent, int index)
      {
        var root = new GameObject($"TrailSegment_{index + 1}");
        root.transform.SetParent(parent, false);
        return new TrailLineFx(
          root,
          CreateLine(root.transform, "TrailHazardBand", 49),
          CreateLine(root.transform, "TrailCore", 52),
          CreateLine(root.transform, "TrailEdgeA", 51),
          CreateLine(root.transform, "TrailEdgeB", 51),
          CreateFlowNodes(root.transform),
          CreateIntersection(root.transform));
      }

      static LineRenderer CreateLine(Transform parent, string name, int order)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.numCapVertices = 4;
        return line;
      }

      static SpriteRenderer[] CreateFlowNodes(Transform parent)
      {
        var nodes = new SpriteRenderer[4];
        for (var i = 0; i < nodes.Length; i++)
        {
          var go = new GameObject($"FlowNode_{i + 1}");
          go.transform.SetParent(parent, false);
          var sprite = go.AddComponent<SpriteRenderer>();
          sprite.sprite = LaserVfxShared.SoftGlowSprite;
          sprite.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
          sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
          sprite.sortingOrder = 53;
          nodes[i] = sprite;
        }
        return nodes;
      }

      static SpriteRenderer CreateIntersection(Transform parent)
      {
        var go = new GameObject("TrailIntersectionNode");
        go.transform.SetParent(parent, false);
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = LaserVfxShared.SoftGlowSprite;
        sprite.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
        sprite.sortingOrder = 54;
        sprite.enabled = false;
        return sprite;
      }

      public void Play(Vector3 start, Vector3 end, float width, float lifetime, bool branch, bool network)
      {
        _start = start;
        _end = end;
        // The supplied width is the gameplay damage width; keep the bright hazard band
        // on that boundary. Only the low-alpha glow is allowed to extend beyond it.
        _width = width;
        _lifetime = Mathf.Max(0.1f, lifetime);
        _branch = branch;
        _network = network;
        _age = 0f;
        var direction = end - start;
        _normal = direction.sqrMagnitude > 0.0001f
          ? new Vector3(-direction.y, direction.x, 0f).normalized
          : Vector3.up;
        _root.SetActive(true);
        _glow.SetPosition(0, start);
        _glow.SetPosition(1, start);
        _core.SetPosition(0, start);
        _core.SetPosition(1, start);
        _edge.SetPosition(0, start);
        _edge.SetPosition(1, start);
        _edgeB.SetPosition(0, start);
        _edgeB.SetPosition(1, start);
        _intersection.enabled = network;
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _lifetime);
        var reveal = Mathf.Clamp01(t * 9f);
        var end = Vector3.Lerp(_start, _end, 1f - (1f - reveal) * (1f - reveal));
        var birth = 1f - Mathf.SmoothStep(0f, 0.18f, t);
        var fade = 1f - Mathf.SmoothStep(_network ? 0.72f : 0.58f, 1f, t);
        var pulse = (_network || !_branch) ? 0.84f + Mathf.Sin(_age * (_network ? 18f : 13f)) * 0.16f : 0.88f;
        var dangerPulse = 0.82f + Mathf.Sin(_age * 22f) * 0.18f;
        var core = _network
          ? new Color(0.78f, 0.92f, 0.84f, fade * pulse * 0.78f)
          : _branch
            ? new Color(0.62f, 0.88f, 0.56f, fade * 0.58f)
            : new Color(0.62f, 0.88f, 0.74f, fade * 0.62f);
        var glow = _network
          ? new Color(0.42f, 0.74f, 0.58f, fade * 0.2f)
          : new Color(0.18f, 0.58f, 0.38f, fade * (0.18f + birth * 0.18f));
        var edgeColor = _network
          ? new Color(0.84f, 0.94f, 0.86f, fade * 0.32f)
          : new Color(0.34f, 0.78f, 0.52f, fade * (0.28f + birth * 0.18f));
        var shrink = Mathf.Lerp(1f, 0.36f, Mathf.SmoothStep(0.65f, 1f, t));
        var edgeWobble = Mathf.Sin(_age * 31f) * _width * 0.035f;
        var edgeOffset = _normal * ((_width * 0.42f + edgeWobble) * shrink);
        _edge.SetPosition(0, _start + edgeOffset * 0.88f);
        _edge.SetPosition(1, end + edgeOffset);
        _edgeB.SetPosition(0, _start - edgeOffset);
        _edgeB.SetPosition(1, end - edgeOffset * 0.88f);
        _core.SetPosition(0, _start);
        _core.SetPosition(1, end);
        _glow.SetPosition(0, _start);
        _glow.SetPosition(1, end);
        LaserVfxShared.SetLineColor(_core, new Color(0.84f, 0.96f, 0.88f, fade * (0.62f + birth * 0.22f)), core);
        LaserVfxShared.SetLineColor(_edge, edgeColor, edgeColor);
        LaserVfxShared.SetLineColor(_edgeB, edgeColor, edgeColor);
        LaserVfxShared.SetLineColor(_glow, glow, glow);
        _core.startWidth = _core.endWidth = _width * (_network ? 0.24f : 0.32f) * shrink * (1f + birth * 0.18f);
        _edge.startWidth = _edge.endWidth = _width * (_network ? 0.14f : 0.18f) * shrink * dangerPulse;
        _edgeB.startWidth = _edgeB.endWidth = _edge.startWidth;
        _glow.startWidth = _glow.endWidth = _width * (_network ? 1.65f : 1.45f) * shrink * (1f + birth * 0.22f);

        UpdateFlowNodes(end, fade, reveal);
        UpdateIntersection(fade, t);
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);

      void UpdateFlowNodes(Vector3 revealedEnd, float fade, float reveal)
      {
        var lengthReady = reveal > 0.2f;
        for (var i = 0; i < _flowNodes.Length; i++)
        {
          var node = _flowNodes[i];
          var active = lengthReady && (!_branch || i < 2) && (_network || i < 3);
          node.enabled = active;
          if (!active)
            continue;

          var phase = Mathf.Repeat(_age * (_network ? 1.1f : 1.75f) + i * 0.23f, 1f);
          var pos = Vector3.Lerp(_start, revealedEnd, phase);
          var side = Mathf.Sin((_age * 13f) + i * 1.7f) * _width * (_network ? 0.08f : 0.14f);
          node.transform.position = pos + _normal * side;
          var scale = _width * (_network ? 0.18f : 0.2f) * Mathf.Lerp(0.65f, 1f, fade);
          node.transform.localScale = Vector3.one * Mathf.Max(0.035f, scale);
          var alpha = fade * (_network ? 0.38f : 0.36f) * (0.75f + Mathf.Sin(_age * 12f + i) * 0.2f);
          LaserVfxShared.SetSpriteColor(node, new Color(0.44f, 0.84f, 0.58f, alpha * 0.72f));
        }
      }

      void UpdateIntersection(float fade, float t)
      {
        if (!_network)
          return;
        _intersection.enabled = true;
        _intersection.transform.position = _end;
        var pulse = 0.72f + Mathf.Sin(_age * 16f) * 0.22f;
        _intersection.transform.localScale = Vector3.one * Mathf.Lerp(_width * 0.9f, _width * 0.28f, t);
        LaserVfxShared.SetSpriteColor(_intersection, new Color(0.66f, 0.92f, 0.52f, fade * pulse * 0.32f));
      }
    }

    sealed class TrailImpactFx
    {
      readonly GameObject _root;
      readonly LineRenderer _ring;
      readonly LineRenderer _fragments;
      readonly SpriteRenderer _flash;
      float _age;
      float _duration;
      bool _critical;

      public bool Active => _root.activeSelf;

      TrailImpactFx(GameObject root, LineRenderer ring, LineRenderer fragments, SpriteRenderer flash)
      {
        _root = root;
        _ring = ring;
        _fragments = fragments;
        _flash = flash;
        root.SetActive(false);
      }

      public static TrailImpactFx Create(Transform parent, int index)
      {
        var root = new GameObject($"TrailImpact_{index + 1}");
        root.transform.SetParent(parent, false);
        var material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        var ring = CreateLine(root.transform, "ImpactPulse", material, 58, true);
        ring.positionCount = 18;
        for (var i = 0; i < ring.positionCount; i++)
        {
          var angle = i * Mathf.PI * 2f / ring.positionCount;
          ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
        var fragments = CreateLine(root.transform, "ImpactFragments", material, 59, false);
        fragments.positionCount = 10;
        for (var i = 0; i < 5; i++)
        {
          var angle = i / 5f * Mathf.PI * 2f;
          fragments.SetPosition(i * 2, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.08f);
          fragments.SetPosition(i * 2 + 1, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.28f + 0.08f * (i % 2)));
        }
        var flashGo = new GameObject("ImpactFlash");
        flashGo.transform.SetParent(root.transform, false);
        var flash = flashGo.AddComponent<SpriteRenderer>();
        flash.sprite = LaserVfxShared.SoftGlowSprite;
        flash.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        flash.sortingLayerName = LaserVfxShared.SortingLayerName;
        flash.sortingOrder = 57;
        return new TrailImpactFx(root, ring, fragments, flash);
      }

      static LineRenderer CreateLine(Transform parent, string name, Material material, int order, bool loop)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.material = material;
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.numCapVertices = 2;
        return line;
      }

      public void Play(Vector3 position, bool critical)
      {
        _critical = critical;
        _age = 0f;
        _duration = critical ? 0.22f : 0.15f;
        _root.transform.position = position;
        _root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _root.SetActive(true);
        Tick(0f);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var fade = 1f - Mathf.SmoothStep(0f, 1f, t);
        var core = _critical ? new Color(0.86f, 0.96f, 0.86f, fade * 0.78f) : new Color(0.62f, 0.88f, 0.68f, fade * 0.58f);
        var edge = _critical ? new Color(0.42f, 0.86f, 0.52f, fade * 0.56f) : new Color(0.22f, 0.66f, 0.36f, fade * 0.42f);
        LaserVfxShared.SetLineColor(_ring, edge, edge);
        LaserVfxShared.SetLineColor(_fragments, core, edge);
        _ring.startWidth = _ring.endWidth = (_critical ? 0.055f : 0.036f) * fade;
        _fragments.startWidth = _fragments.endWidth = (_critical ? 0.04f : 0.026f) * fade;
        _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _critical ? 0.72f : 0.48f, t);
        _fragments.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, _critical ? 0.9f : 0.58f, t);
        _flash.transform.localScale = Vector3.one * Mathf.Lerp(_critical ? 0.32f : 0.22f, 0.04f, t);
        LaserVfxShared.SetSpriteColor(_flash, new Color(0.46f, 0.84f, 0.56f, fade * (_critical ? 0.34f : 0.2f)));
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);
    }
  }
}
