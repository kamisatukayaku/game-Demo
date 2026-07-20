using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedExplosionVfx : MonoBehaviour
  {
    const int PoolSize = 32;
    static DetachedExplosionVfx s_instance;
    ExplosionFx[] _pool;
    Vector3 _lastBurstPosition;
    float _lastBurstTime = -999f;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_DetachedExplosionVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<DetachedExplosionVfx>();
    }

    public static void Play(Vector3 position, float radius, bool nuclear, bool expanding, int kind)
    {
      EnsureExists();
      var chain = (kind == 2 || kind == 3) &&
        Time.unscaledTime - s_instance._lastBurstTime < 0.36f &&
        Vector3.Distance(s_instance._lastBurstPosition, position) <= Mathf.Max(2.2f, radius * 2.4f);
      var chainStart = s_instance._lastBurstPosition;
      foreach (var effect in s_instance._pool)
      {
        if (!effect.Active)
        {
          effect.Play(position, radius, nuclear, expanding, kind, chain, chainStart);
          s_instance._lastBurstPosition = position;
          s_instance._lastBurstTime = Time.unscaledTime;
          return;
        }
      }
      s_instance._lastBurstPosition = position;
      s_instance._lastBurstTime = Time.unscaledTime;
    }

    public static void ResetAll()
    {
      EnsureExists();
      if (s_instance?._pool == null)
        return;
      foreach (var effect in s_instance._pool)
        effect.ForceStop();
      s_instance._lastBurstTime = -999f;
    }

    public static string GetDebugSummary()
    {
      if (s_instance?._pool == null)
        return "Explosion inactive";
      var active = 0;
      foreach (var effect in s_instance._pool)
        if (effect.Active)
          active++;
      return $"Explosion {active}/{s_instance._pool.Length}";
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
      _pool = new ExplosionFx[PoolSize];
      for (var i = 0; i < _pool.Length; i++)
        _pool[i] = ExplosionFx.Create(transform, i);
    }

    void Update()
    {
      var deltaTime = Time.unscaledDeltaTime;
      foreach (var effect in _pool)
        effect.Tick(deltaTime);
    }

    sealed class ExplosionFx
    {
      readonly GameObject _root;
      readonly LineRenderer _inner;
      readonly LineRenderer _outer;
      readonly LineRenderer _shockwave;
      readonly LineRenderer _fragments;
      readonly LineRenderer _chain;
      readonly SpriteRenderer _core;
      float _age;
      float _duration;
      float _radius;
      bool _nuclear;
      bool _expanding;
      bool _chainActive;
      int _kind;

      public bool Active => _root.activeSelf;

      ExplosionFx(
        GameObject root,
        LineRenderer inner,
        LineRenderer outer,
        LineRenderer shockwave,
        LineRenderer fragments,
        LineRenderer chain,
        SpriteRenderer core)
      {
        _root = root;
        _inner = inner;
        _outer = outer;
        _shockwave = shockwave;
        _fragments = fragments;
        _chain = chain;
        _core = core;
        root.SetActive(false);
      }

      public static ExplosionFx Create(Transform parent, int index)
      {
        var root = new GameObject($"ExplosionFx_{index + 1}");
        root.transform.SetParent(parent, false);
        var material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        var inner = CreateRing(root.transform, "InnerRing", material, 54, 24);
        var outer = CreateRing(root.transform, "OuterRing", material, 53, 32);
        var shockwave = CreateRing(root.transform, "Shockwave", material, 52, 40);
        var fragments = CreateFragments(root.transform, material);
        var chain = CreateChain(root.transform, material);
        var coreGo = new GameObject("ExplosionCore");
        coreGo.transform.SetParent(root.transform, false);
        var core = coreGo.AddComponent<SpriteRenderer>();
        core.sprite = LaserVfxShared.SoftGlowSprite;
        core.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        core.sortingLayerName = LaserVfxShared.SortingLayerName;
        core.sortingOrder = 56;
        return new ExplosionFx(root, inner, outer, shockwave, fragments, chain, core);
      }

      static LineRenderer CreateRing(Transform parent, string name, Material material, int order, int segments)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = ConfigureLine(go, material, order);
        line.loop = true;
        line.positionCount = segments + 1;
        for (var i = 0; i <= segments; i++)
        {
          var angle = i / (float)segments * Mathf.PI * 2f;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
        return line;
      }

      static LineRenderer CreateFragments(Transform parent, Material material)
      {
        var go = new GameObject("GeometricFragments");
        go.transform.SetParent(parent, false);
        var line = ConfigureLine(go, material, 55);
        line.loop = false;
        line.positionCount = 20;
        for (var i = 0; i < 10; i++)
        {
          var angle = i / 10f * Mathf.PI * 2f;
          var inner = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.18f;
          var outer = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 1f : 0.62f);
          line.SetPosition(i * 2, inner);
          line.SetPosition(i * 2 + 1, outer);
        }
        return line;
      }

      static LineRenderer CreateChain(Transform parent, Material material)
      {
        var go = new GameObject("ChainLink");
        go.transform.SetParent(parent, false);
        var line = ConfigureLine(go, material, 57);
        line.useWorldSpace = true;
        line.positionCount = 5;
        line.enabled = false;
        return line;
      }

      static LineRenderer ConfigureLine(GameObject go, Material material, int order)
      {
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = material;
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.numCapVertices = 3;
        return line;
      }

      public void Play(Vector3 position, float radius, bool nuclear, bool expanding, int kind, bool chainActive, Vector3 chainStart)
      {
        _root.transform.position = position;
        _root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
        _radius = Mathf.Max(0.2f, radius);
        _nuclear = nuclear;
        _expanding = expanding;
        _kind = kind;
        _chainActive = chainActive && !_nuclear;
        _duration = nuclear ? 0.68f : expanding ? 0.42f : kind == 2 || kind == 3 ? 0.34f : 0.24f;
        _age = 0f;
        _chain.enabled = _chainActive;
        if (_chainActive)
          DrawChain(chainStart, position);
        _root.SetActive(true);
        Tick(0f);
      }

      void DrawChain(Vector3 start, Vector3 end)
      {
        var direction = end - start;
        var normal = direction.sqrMagnitude > 0.001f
          ? new Vector3(-direction.y, direction.x, 0f).normalized
          : Vector3.up;
        _chain.SetPosition(0, start);
        _chain.SetPosition(1, Vector3.Lerp(start, end, 0.28f) + normal * 0.16f);
        _chain.SetPosition(2, Vector3.Lerp(start, end, 0.5f) - normal * 0.12f);
        _chain.SetPosition(3, Vector3.Lerp(start, end, 0.74f) + normal * 0.1f);
        _chain.SetPosition(4, end);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var eased = 1f - (1f - t) * (1f - t);
        var alpha = (1f - t) * (_expanding ? 0.62f : 1f);
        var secondary = _expanding || _kind == 2 || _kind == 3;
        var hot = _nuclear
          ? new Color(1f, 0.9f, 0.72f, alpha)
          : new Color(1f, 0.72f, 0.34f, alpha);
        var edge = _nuclear
          ? new Color(1f, 0.22f, 0.04f, alpha * 0.92f)
          : secondary
            ? new Color(1f, 0.34f, 0.04f, alpha * 0.82f)
            : new Color(1f, 0.46f, 0.06f, alpha * 0.86f);
        var amber = new Color(0.95f, 0.08f, 0.02f, alpha * (_nuclear ? 0.42f : 0.28f));

        _inner.startColor = _inner.endColor = hot;
        _outer.startColor = _outer.endColor = edge;
        _shockwave.startColor = _shockwave.endColor = amber;
        _fragments.startColor = hot;
        _fragments.endColor = edge;
        LaserVfxShared.ApplyVfxTint(_inner.material, hot);
        LaserVfxShared.ApplyVfxTint(_outer.material, edge);
        LaserVfxShared.ApplyVfxTint(_shockwave.material, amber);
        LaserVfxShared.ApplyVfxTint(_fragments.material, hot);
        _inner.startWidth = _inner.endWidth = (_nuclear ? 0.18f : 0.095f) * alpha;
        _outer.startWidth = _outer.endWidth = (_nuclear ? 0.12f : 0.058f) * alpha;
        _shockwave.startWidth = _shockwave.endWidth = (_nuclear ? 0.08f : 0.035f) * alpha;
        _fragments.startWidth = _fragments.endWidth = (_nuclear ? 0.09f : 0.047f) * alpha;
        _core.transform.localScale = Vector3.one * Mathf.Lerp(_nuclear ? 0.65f : 0.38f, 0.04f, Mathf.Clamp01(t * 1.4f));
        LaserVfxShared.SetSpriteColor(_core, new Color(1f, 0.62f, 0.28f, Mathf.Clamp01((1f - t * 1.6f) * (_nuclear ? 0.9f : 0.62f))));

        _inner.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, _radius, eased);
        _outer.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, _radius * (_nuclear ? 1.18f : 1.08f), Mathf.Clamp01(eased * 0.86f));
        var shockT = Mathf.Clamp01((t - (_nuclear ? 0.08f : 0.16f)) / 0.84f);
        _shockwave.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _radius * (_nuclear ? 1.55f : 1.32f), shockT);
        _fragments.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _radius * (_nuclear ? 0.98f : 0.72f), eased);
        _fragments.transform.Rotate(0f, 0f, (_nuclear ? 220f : secondary ? 165f : 125f) * deltaTime);

        if (_chainActive)
        {
          var chainAlpha = Mathf.Clamp01((1f - t) * 0.86f);
          _chain.startColor = new Color(1f, 0.48f, 0.08f, chainAlpha);
          _chain.endColor = new Color(0.95f, 0.08f, 0.02f, chainAlpha * 0.5f);
          _chain.startWidth = 0.055f * chainAlpha;
          _chain.endWidth = 0.026f * chainAlpha;
        }

        if (t >= 1f)
        {
          _chain.enabled = false;
          _root.SetActive(false);
        }
      }

      public void ForceStop()
      {
        _age = 0f;
        _chain.enabled = false;
        _root.SetActive(false);
      }
    }
  }
}
