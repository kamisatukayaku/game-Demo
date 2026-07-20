using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedPulseVfx : MonoBehaviour
  {
    const int WavePoolSize = 36;
    const int ZonePoolSize = 12;
    const int ChargePoolSize = 16;
    static DetachedPulseVfx s_instance;
    PulseRingFx[] _waves;
    ResonanceFx[] _zones;
    PulseChargeFx[] _charges;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_DetachedPulseVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<DetachedPulseVfx>();
    }

    public static void Play(string effectId, Vector3 position, float radius, float duration, bool secondary)
    {
      EnsureExists();
      if (effectId == "DetachedPulseResonance")
      {
        foreach (var zone in s_instance._zones)
        {
          if (!zone.Active)
          {
            zone.Play(position, radius, duration);
            return;
          }
        }
        return;
      }

      if (effectId == "DetachedPulseCharge")
      {
        foreach (var charge in s_instance._charges)
        {
          if (!charge.Active)
          {
            charge.Play(position, radius, duration);
            return;
          }
        }
        return;
      }

      foreach (var wave in s_instance._waves)
      {
        if (!wave.Active)
        {
          wave.Play(position, radius, Mathf.Max(0.15f, duration), secondary, effectId == "DetachedArenaPulse");
          return;
        }
      }
    }

    public static void ResetAll()
    {
      EnsureExists();
      if (s_instance == null)
        return;
      foreach (var wave in s_instance._waves)
        wave.ForceStop();
      foreach (var zone in s_instance._zones)
        zone.ForceStop();
      foreach (var charge in s_instance._charges)
        charge.ForceStop();
    }

    public static string GetDebugSummary()
    {
      if (s_instance == null)
        return "Pulse inactive";
      var active = 0;
      foreach (var wave in s_instance._waves)
        if (wave.Active)
          active++;
      return $"Pulse {active}/{s_instance._waves.Length}";
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
      _waves = new PulseRingFx[WavePoolSize];
      for (var i = 0; i < _waves.Length; i++)
        _waves[i] = PulseRingFx.Create(transform, i);
      _zones = new ResonanceFx[ZonePoolSize];
      for (var i = 0; i < _zones.Length; i++)
        _zones[i] = ResonanceFx.Create(transform, i);
      _charges = new PulseChargeFx[ChargePoolSize];
      for (var i = 0; i < _charges.Length; i++)
        _charges[i] = PulseChargeFx.Create(transform, i);
    }

    void Update()
    {
      var dt = Time.unscaledDeltaTime;
      foreach (var wave in _waves)
        wave.Tick(dt);
      foreach (var zone in _zones)
        zone.Tick(dt);
      foreach (var charge in _charges)
        charge.Tick(dt);
    }

    static LineRenderer CreateLoop(Transform parent, string name, int order, int segments)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.numCapVertices = 3;
      line.positionCount = segments + 1;
      for (var i = 0; i <= segments; i++)
      {
        var angle = i / (float)segments * Mathf.PI * 2f;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
      }
      return line;
    }

    sealed class PulseRingFx
    {
      readonly GameObject _root;
      readonly LineRenderer _brightEdge;
      readonly LineRenderer _softEdge;
      readonly LineRenderer _fractureEdge;
      float _age;
      float _duration;
      float _radius;
      bool _secondary;
      bool _arena;

      public bool Active => _root.activeSelf;

      PulseRingFx(GameObject root, LineRenderer brightEdge, LineRenderer softEdge, LineRenderer fractureEdge)
      {
        _root = root;
        _brightEdge = brightEdge;
        _softEdge = softEdge;
        _fractureEdge = fractureEdge;
        root.SetActive(false);
      }

      public static PulseRingFx Create(Transform parent, int index)
      {
        var root = new GameObject($"PulseWave_{index + 1}");
        root.transform.SetParent(parent, false);
        return new PulseRingFx(
          root,
          CreateLoop(root.transform, "BrightEdge", 46, 48),
          CreateLoop(root.transform, "SoftEdge", 45, 48),
          CreateJaggedLoop(root.transform, "FractureEdge", 47, 36));
      }

      static LineRenderer CreateJaggedLoop(Transform parent, string name, int order, int segments)
      {
        var line = CreateLoop(parent, name, order, segments);
        for (var i = 0; i <= segments; i++)
        {
          var angle = i / (float)segments * Mathf.PI * 2f;
          var mod = i % 3 == 0 ? 1.06f : i % 3 == 1 ? 0.96f : 1f;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * mod);
        }
        return line;
      }

      public void Play(Vector3 position, float radius, float duration, bool secondary, bool arena)
      {
        _root.transform.position = position;
        _radius = radius;
        _duration = duration;
        _secondary = secondary;
        _arena = arena;
        _age = 0f;
        _root.SetActive(true);
        Tick(0f);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var scale = Mathf.Lerp(0.01f, _radius, 1f - Mathf.Pow(1f - t, 3f));
        var fadeIn = Mathf.Clamp01(t * 8f);
        var alpha = fadeIn * (1f - Mathf.SmoothStep(0.68f, 1f, t));
        var bright = _arena
          ? new Color(0.86f, 0.48f, 1f, alpha * 0.9f)
          : _secondary
            ? new Color(0.58f, 0.22f, 1f, alpha * 0.68f)
            : new Color(0.78f, 0.32f, 1f, alpha * 0.86f);
        var soft = new Color(0.32f, 0.08f, 0.92f, alpha * (_arena ? 0.5f : 0.34f));
        var fracture = new Color(0.92f, 0.72f, 1f, alpha * (_secondary ? 0.18f : 0.26f));
        LaserVfxShared.SetLineColor(_brightEdge, bright, bright);
        LaserVfxShared.SetLineColor(_softEdge, soft, soft);
        LaserVfxShared.SetLineColor(_fractureEdge, fracture, fracture);
        _brightEdge.startWidth = _brightEdge.endWidth = _arena ? 0.18f : _secondary ? 0.07f : 0.13f;
        _softEdge.startWidth = _softEdge.endWidth = _arena ? 0.42f : 0.28f;
        _fractureEdge.startWidth = _fractureEdge.endWidth = _arena ? 0.08f : 0.04f;
        _brightEdge.transform.localScale = Vector3.one * scale;
        _softEdge.transform.localScale = Vector3.one * scale;
        _fractureEdge.transform.localScale = Vector3.one * scale * (1f + Mathf.Sin(_age * 32f) * 0.012f);
        _fractureEdge.transform.Rotate(0f, 0f, (_secondary ? -36f : 54f) * deltaTime);
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);
    }

    sealed class PulseChargeFx
    {
      readonly GameObject _root;
      readonly LineRenderer _outer;
      readonly LineRenderer _hex;
      readonly LineRenderer _core;
      float _age;
      float _duration;
      float _radius;

      public bool Active => _root.activeSelf;

      PulseChargeFx(GameObject root, LineRenderer outer, LineRenderer hex, LineRenderer core)
      {
        _root = root;
        _outer = outer;
        _hex = hex;
        _core = core;
        root.SetActive(false);
      }

      public static PulseChargeFx Create(Transform parent, int index)
      {
        var root = new GameObject($"PulseCharge_{index + 1}");
        root.transform.SetParent(parent, false);
        return new PulseChargeFx(
          root,
          CreateLoop(root.transform, "ChargeOuter", 46, 32),
          CreateLoop(root.transform, "ChargeHex", 47, 6),
          CreateLoop(root.transform, "ChargeCore", 48, 24));
      }

      public void Play(Vector3 position, float radius, float duration)
      {
        _root.transform.position = position;
        _radius = Mathf.Max(0.5f, radius);
        _duration = Mathf.Clamp(duration, 0.12f, 0.8f);
        _age = 0f;
        _root.SetActive(true);
        Tick(0f);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var charge = Mathf.SmoothStep(0f, 1f, t);
        var alpha = Mathf.Sin(t * Mathf.PI) * 0.72f + charge * 0.18f;
        var outerScale = Mathf.Lerp(_radius * 0.42f, _radius * 0.12f, charge);
        var coreScale = Mathf.Lerp(_radius * 0.08f, _radius * 0.2f, charge);
        var edge = new Color(0.45f, 0.08f, 1f, alpha * 0.7f);
        var core = new Color(0.92f, 0.82f, 1f, alpha);
        var halo = new Color(0.78f, 0.32f, 1f, alpha * 0.35f);
        LaserVfxShared.SetLineColor(_outer, edge, edge);
        LaserVfxShared.SetLineColor(_hex, halo, halo);
        LaserVfxShared.SetLineColor(_core, core, core);
        _outer.startWidth = _outer.endWidth = Mathf.Lerp(0.035f, 0.08f, charge);
        _hex.startWidth = _hex.endWidth = Mathf.Lerp(0.026f, 0.055f, charge);
        _core.startWidth = _core.endWidth = Mathf.Lerp(0.025f, 0.07f, charge);
        _outer.transform.localScale = Vector3.one * outerScale;
        _hex.transform.localScale = Vector3.one * Mathf.Lerp(_radius * 0.26f, _radius * 0.11f, charge);
        _core.transform.localScale = Vector3.one * coreScale;
        _outer.transform.Rotate(0f, 0f, -80f * deltaTime);
        _hex.transform.Rotate(0f, 0f, 120f * deltaTime);
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);
    }

    sealed class ResonanceFx
    {
      readonly GameObject _root;
      readonly LineRenderer _hex;
      readonly LineRenderer _ring;
      float _age;
      float _duration;
      float _radius;

      public bool Active => _root.activeSelf;

      ResonanceFx(GameObject root, LineRenderer hex, LineRenderer ring)
      {
        _root = root;
        _hex = hex;
        _ring = ring;
        root.SetActive(false);
      }

      public static ResonanceFx Create(Transform parent, int index)
      {
        var root = new GameObject($"PulseResonance_{index + 1}");
        root.transform.SetParent(parent, false);
        return new ResonanceFx(
          root,
          CreateLoop(root.transform, "HexField", 43, 6),
          CreateLoop(root.transform, "ResonanceRing", 44, 32));
      }

      public void Play(Vector3 position, float radius, float duration)
      {
        _root.transform.position = position;
        _radius = radius;
        _duration = Mathf.Max(0.2f, duration);
        _age = 0f;
        _root.SetActive(true);
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;
        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var pulse = 0.86f + Mathf.Sin(_age * 9f) * 0.1f;
        var alpha = Mathf.Sin(t * Mathf.PI) * 0.52f;
        var color = new Color(0.78f, 0.32f, 1f, alpha);
        LaserVfxShared.SetLineColor(_hex, color, color);
        LaserVfxShared.SetLineColor(_ring, new Color(0.92f, 0.82f, 1f, alpha * 0.7f), new Color(0.92f, 0.82f, 1f, alpha * 0.7f));
        _hex.startWidth = _hex.endWidth = 0.07f;
        _ring.startWidth = _ring.endWidth = 0.045f;
        _hex.transform.localScale = Vector3.one * _radius * pulse;
        _ring.transform.localScale = Vector3.one * _radius * (1.02f - pulse * 0.08f);
        _hex.transform.Rotate(0f, 0f, 24f * deltaTime);
        if (t >= 1f)
          _root.SetActive(false);
      }

      public void ForceStop() => _root.SetActive(false);
    }
  }
}
