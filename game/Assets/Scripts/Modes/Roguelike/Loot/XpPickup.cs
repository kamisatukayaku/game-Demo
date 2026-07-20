using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Loot
{
  [DisallowMultipleComponent]
  public sealed class XpPickup : MonoBehaviour
  {
    const int CircleSegments = 32;
    const int PrewarmCount = 128;

    static readonly Queue<XpPickup> s_pool = new();
    static readonly Queue<XpAbsorbFx> s_absorbPool = new();
    static Transform s_poolRoot;
    static Material s_lineMaterial;
    static bool s_prewarmed;

    [SerializeField] int xpAmount = 10;
    [SerializeField] float pickupRadius = 0.28f;
    [SerializeField] float lifetime = 35f;
    [SerializeField] float magnetRange = 2.8f;
    [SerializeField] float maxMagnetSpeed = 16f;
    [SerializeField] float magnetAcceleration = 34f;
    [SerializeField] float bobSpeed = 2.2f;
    [SerializeField] float bobHeight = 0.08f;

    readonly Vector3[] _circle = new Vector3[CircleSegments];
    readonly Vector3[] _hex = new Vector3[6];

    Transform _player;
    LineRenderer _core;
    LineRenderer _orbit;
    LineRenderer _hexRing;
    TrailRenderer _trail;
    int _amount;
    float _age;
    float _scale;
    float _magnetSpeed;
    float _spawnAge;
    bool _magnetized;
    Vector3 _groundPos;
    Vector3 _scatterStart;
    Vector3 _scatterTarget;
    Color _mainColor;
    Color _accentColor;

    public int XpAmount => _amount;

    public static void Spawn(Vector3 position, int amount)
    {
      if (amount <= 0)
        return;

      EnsurePool();
      var pickup = Acquire();
      pickup.Activate(position, amount);
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      Game.Modes.Roguelike.Diagnostics.RuntimeValidation.CombatChainProbe.NotifyXpPickupSpawn();
#endif
    }

    public static void ResetPoolForNewRun()
    {
      var active = Object.FindObjectsByType<XpPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var pickup in active)
      {
        if (pickup != null && pickup.gameObject.activeInHierarchy)
          Release(pickup);
      }
    }

    public void SetAmount(int amount)
    {
      _amount = Mathf.Max(1, amount);
      xpAmount = _amount;
    }

    void Awake()
    {
      EnsureVisuals();
    }

    void Update()
    {
      _age += Time.deltaTime;
      if (_age > lifetime)
      {
        Release(this);
        return;
      }

      if (_player == null)
        _player = FindPlayer();

      UpdatePickupMotion();
      UpdateVisuals();
    }

    void Activate(Vector3 position, int amount)
    {
      EnsureVisuals();
      SetAmount(amount);

      _age = 0f;
      _spawnAge = 0f;
      _magnetSpeed = 0f;
      _magnetized = false;
      _player = FindPlayer();

      _scale = Mathf.Clamp(0.68f + Mathf.Sqrt(_amount) * 0.055f, 0.75f, 1.85f);
      _mainColor = Color.Lerp(new Color(0.18f, 0.95f, 1f, 1f), Color.white, Mathf.Clamp01((_amount - 10f) / 110f) * 0.25f);
      _accentColor = new Color(0.72f, 0.95f, 1f, 1f);

      var scatter = Random.insideUnitCircle.normalized;
      if (scatter.sqrMagnitude < 0.01f)
        scatter = Vector2.right;
      var distance = Random.Range(0.3f, Mathf.Lerp(0.75f, 1.05f, Mathf.Clamp01(_amount / 80f)));

      _scatterStart = position;
      _scatterTarget = position + new Vector3(scatter.x, scatter.y, 0f) * distance;
      _groundPos = _scatterTarget;
      transform.position = _scatterStart;
      transform.localScale = Vector3.one * 0.02f;
      gameObject.SetActive(true);

      if (_trail != null)
      {
        _trail.Clear();
        _trail.emitting = false;
      }
    }

    void UpdatePickupMotion()
    {
      if (_spawnAge < 0.18f)
      {
        _spawnAge += Time.deltaTime;
        var t = Mathf.Clamp01(_spawnAge / 0.18f);
        var eased = 1f - Mathf.Pow(1f - t, 2.4f);
        var overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
        transform.position = Vector3.Lerp(_scatterStart, _scatterTarget, eased);
        transform.localScale = Vector3.one * (_scale * overshoot);
        return;
      }

      if (_player != null)
      {
        var toPlayer = _player.position - transform.position;
        toPlayer.z = 0f;
        var dist = toPlayer.magnitude;
        var planarPos = new Vector2(transform.position.x, transform.position.y);
        var magnetMult = ArenaXpZoneController.GetMagnetRangeMultiplier(planarPos)
                         * RunBuildState.GetXpMagnetRangeMult();
        var effectiveMagnetRange = magnetRange * magnetMult;
        var xpMult = ArenaXpZoneController.GetXpMultiplier(planarPos);

        if (dist <= pickupRadius)
        {
          var gained = Mathf.Max(1, Mathf.RoundToInt(_amount * xpMult));
          ExperienceSystem.Gain(gained);
          PlayAbsorbFx(transform.position, gained);
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
          Game.Modes.Roguelike.Diagnostics.RuntimeValidation.CombatChainProbe.NotifyXpPickupCollect();
#endif
          GameEventBus.Publish(new TriggerActivatedEvent("XpPickup", transform.position, gameObject, value: gained));
          Release(this);
          return;
        }

        if (dist <= effectiveMagnetRange || _magnetized)
        {
          _magnetized = true;
          if (_trail != null)
            _trail.emitting = true;

          _magnetSpeed = Mathf.Min(maxMagnetSpeed * magnetMult, _magnetSpeed + magnetAcceleration * Time.deltaTime);
          var curveBoost = 1f + Mathf.Clamp01(1f - dist / effectiveMagnetRange) * 2.2f;
          transform.position += toPlayer.normalized * (_magnetSpeed * curveBoost * Time.deltaTime);
          transform.localScale = Vector3.one * (_scale * Mathf.Lerp(0.88f, 1.08f, Mathf.PingPong(_age * 8f, 1f)));
          return;
        }
      }

      if (_trail != null)
        _trail.emitting = false;

      var bobOffset = Mathf.Sin(_age * bobSpeed) * bobHeight;
      transform.position = _groundPos + Vector3.up * bobOffset;
      transform.localScale = Vector3.one * _scale;
    }

    void UpdateVisuals()
    {
      var radius = 0.14f;
      DrawCircle(_core, radius * 0.56f, CircleSegments);
      DrawCircle(_orbit, radius * (1.15f + Mathf.Sin(_age * 2.1f) * 0.04f), CircleSegments);
      DrawHex(_hexRing, radius * 1.48f);

      _core.startColor = WithAlpha(Color.white, 0.92f);
      _core.endColor = WithAlpha(_mainColor, 0.82f);
      _orbit.startColor = WithAlpha(_mainColor, _magnetized ? 0.9f : 0.62f);
      _orbit.endColor = WithAlpha(_accentColor, _magnetized ? 0.48f : 0.28f);
      _hexRing.startColor = WithAlpha(_accentColor, 0.34f);
      _hexRing.endColor = WithAlpha(_mainColor, 0.24f);

      _orbit.transform.localRotation = Quaternion.Euler(0f, 0f, _age * (_magnetized ? 240f : 75f));
      _hexRing.transform.localRotation = Quaternion.Euler(0f, 0f, -_age * 38f);
    }

    void EnsureVisuals()
    {
      EnsureMaterials();
      if (_core != null)
        return;

      _core = CreateLine("XpCore", 0.055f, 103);
      _orbit = CreateLine("XpOrbit", 0.032f, 102);
      _hexRing = CreateLine("XpHexOrbit", 0.025f, 101);

      _trail = gameObject.AddComponent<TrailRenderer>();
      _trail.material = s_lineMaterial;
      _trail.time = 0.18f;
      _trail.startWidth = 0.12f;
      _trail.endWidth = 0f;
      _trail.sortingOrder = 100;
      _trail.emitting = false;
      _trail.startColor = new Color(0.7f, 0.95f, 1f, 0.6f);
      _trail.endColor = new Color(0.15f, 0.85f, 1f, 0f);
    }

    LineRenderer CreateLine(string name, float width, int sortingOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.material = s_lineMaterial;
      line.startWidth = width;
      line.endWidth = width;
      line.sortingOrder = sortingOrder;
      line.numCapVertices = 2;
      return line;
    }

    void DrawCircle(LineRenderer line, float radius, int segments)
    {
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        _circle[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
      }
      line.SetPositions(_circle);
    }

    void DrawHex(LineRenderer line, float radius)
    {
      line.positionCount = 6;
      for (var i = 0; i < 6; i++)
      {
        var angle = i * Mathf.PI * 2f / 6f + Mathf.PI / 6f;
        _hex[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
      }
      line.SetPositions(_hex);
    }

    static XpPickup Acquire()
    {
      while (s_pool.Count > 0)
      {
        var pickup = s_pool.Dequeue();
        if (pickup != null)
          return pickup;
      }

      return CreatePickup();
    }

    static XpPickup CreatePickup()
    {
      EnsurePoolRoot();
      var go = new GameObject("XpPickup");
      go.transform.SetParent(s_poolRoot, false);
      var pickup = go.AddComponent<XpPickup>();
      pickup.EnsureVisuals();
      go.SetActive(false);
      return pickup;
    }

    static void Release(XpPickup pickup)
    {
      if (pickup == null)
        return;

      if (pickup._trail != null)
      {
        pickup._trail.emitting = false;
        pickup._trail.Clear();
      }

      pickup.gameObject.SetActive(false);
      pickup.transform.SetParent(s_poolRoot, false);
      s_pool.Enqueue(pickup);
    }

    static void EnsurePool()
    {
      EnsurePoolRoot();
      if (s_prewarmed)
        return;

      s_prewarmed = true;
      for (var i = 0; i < PrewarmCount; i++)
        Release(CreatePickup());
    }

    static void EnsurePoolRoot()
    {
      EnsureMaterials();
      if (s_poolRoot != null)
        return;

      var root = new GameObject("_RoguelikeXpPickupPool");
      if (Application.isPlaying)
        Object.DontDestroyOnLoad(root);
      s_poolRoot = root.transform;
    }

    static void EnsureMaterials()
    {
      if (s_lineMaterial != null)
        return;

      var shader = Shader.Find("Sprites/Default");
      s_lineMaterial = new Material(shader) { name = "XpPickupLine_Runtime" };
    }

    static void PlayAbsorbFx(Vector3 position, int amount)
    {
      EnsurePoolRoot();
      var fx = AcquireAbsorbFx();
      fx.Play(position, Mathf.Clamp(0.45f + Mathf.Sqrt(amount) * 0.035f, 0.6f, 1.5f), ReleaseAbsorbFx);
    }

    static XpAbsorbFx AcquireAbsorbFx()
    {
      while (s_absorbPool.Count > 0)
      {
        var fx = s_absorbPool.Dequeue();
        if (fx != null)
          return fx;
      }

      return XpAbsorbFx.Create(s_poolRoot, s_lineMaterial);
    }

    static void ReleaseAbsorbFx(XpAbsorbFx fx)
    {
      if (fx == null)
        return;

      fx.gameObject.SetActive(false);
      fx.transform.SetParent(s_poolRoot, false);
      s_absorbPool.Enqueue(fx);
    }

    static Transform FindPlayer()
    {
      var go = GameObject.Find("Player");
      if (go != null) return go.transform;
      var tagged = GameObject.FindGameObjectWithTag("Player");
      return tagged != null ? tagged.transform : null;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }

  sealed class XpAbsorbFx : MonoBehaviour
  {
    const int Segments = 40;
    readonly Vector3[] _points = new Vector3[Segments];
    LineRenderer _ring;
    float _age;
    float _duration;
    float _radius;
    System.Action<XpAbsorbFx> _release;

    public static XpAbsorbFx Create(Transform root, Material material)
    {
      var go = new GameObject("XpAbsorbFx");
      go.transform.SetParent(root, false);
      var fx = go.AddComponent<XpAbsorbFx>();
      fx._ring = go.AddComponent<LineRenderer>();
      fx._ring.useWorldSpace = false;
      fx._ring.loop = true;
      fx._ring.material = material;
      fx._ring.startWidth = 0.045f;
      fx._ring.endWidth = 0.045f;
      fx._ring.sortingOrder = 104;
      fx._ring.numCapVertices = 2;
      go.SetActive(false);
      return fx;
    }

    public void Play(Vector3 position, float radius, System.Action<XpAbsorbFx> release)
    {
      transform.position = position;
      _radius = radius;
      _duration = 0.16f;
      _age = 0f;
      _release = release;
      gameObject.SetActive(true);
      UpdateVisual(0f);
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / _duration);
      UpdateVisual(t);
      if (_age >= _duration)
        _release?.Invoke(this);
    }

    void UpdateVisual(float t)
    {
      var radius = Mathf.Lerp(_radius * 0.35f, _radius, t);
      _ring.positionCount = Segments;
      for (var i = 0; i < Segments; i++)
      {
        var angle = i * Mathf.PI * 2f / Segments;
        _points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
      }
      _ring.SetPositions(_points);

      var alpha = Mathf.Sin(t * Mathf.PI);
      _ring.startColor = new Color(0.68f, 0.96f, 1f, alpha * 0.8f);
      _ring.endColor = new Color(1f, 1f, 1f, alpha * 0.35f);
    }
  }
}
