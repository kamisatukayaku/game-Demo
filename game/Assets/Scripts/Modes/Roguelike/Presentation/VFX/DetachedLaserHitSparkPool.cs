using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  sealed class LaserHitSparkPool
  {
    readonly LaserHitSparkFx[] _items;

    public LaserHitSparkPool(Transform parent, int count)
    {
      _items = new LaserHitSparkFx[count];
      for (var i = 0; i < count; i++)
        _items[i] = LaserHitSparkFx.Create(parent, i);
    }

    public void Play(Vector3 position, bool prism, float amount)
    {
      foreach (var item in _items)
      {
        if (!item.Active)
        {
          item.Play(position, prism, amount);
          return;
        }
      }
    }

    public void ResetAll()
    {
      foreach (var item in _items)
        item.Deactivate();
    }

    public string GetDebugSummary()
    {
      var active = 0;
      foreach (var item in _items)
        if (item.Active)
          active++;
      return $"{active}/{_items.Length} active";
    }
  }

  sealed class LaserHitSparkFx
  {
    readonly GameObject _root;
    readonly LineRenderer _ring;
    readonly LineRenderer _rays;
    readonly LineRenderer _arc;
    float _age;
    float _duration;
    bool _prism;
    float _power;

    public bool Active => _root.activeSelf;

    LaserHitSparkFx(GameObject root, LineRenderer ring, LineRenderer rays, LineRenderer arc)
    {
      _root = root;
      _ring = ring;
      _rays = rays;
      _arc = arc;
      root.SetActive(false);
      root.AddComponent<LaserHitSparkDriver>().Initialize(this);
    }

    public static LaserHitSparkFx Create(Transform parent, int index)
    {
      var root = new GameObject($"LaserHitSpark_{index + 1}");
      root.transform.SetParent(parent, false);
      var ring = CreateLine(root.transform, "HitRing", LaserVfxShared.CreateBeamMaterialInstance(), 58, true);
      DrawCircle(ring, 16, 1f);
      var rays = CreateLine(root.transform, "EnergyFragments", LaserVfxShared.CreateBeamMaterialInstance(), 59, false);
      rays.positionCount = 12;
      for (var i = 0; i < 6; i++)
      {
        var angle = i / 6f * Mathf.PI * 2f;
        var inner = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.12f;
        var outer = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.55f + (i % 2) * 0.22f);
        rays.SetPosition(i * 2, inner);
        rays.SetPosition(i * 2 + 1, outer);
      }
      var arc = CreateLine(root.transform, "MicroArc", LaserVfxShared.CreateBeamMaterialInstance(), 60, false);
      arc.positionCount = 5;
      arc.SetPosition(0, new Vector3(-0.36f, -0.08f));
      arc.SetPosition(1, new Vector3(-0.12f, 0.18f));
      arc.SetPosition(2, new Vector3(0.08f, -0.04f));
      arc.SetPosition(3, new Vector3(0.24f, 0.14f));
      arc.SetPosition(4, new Vector3(0.42f, -0.1f));
      return new LaserHitSparkFx(root, ring, rays, arc);
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
      line.numCapVertices = 3;
      return line;
    }

    static void DrawCircle(LineRenderer line, int segments, float radius)
    {
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
      }
    }

    public void Play(Vector3 position, bool prism, float amount)
    {
      _age = 0f;
      _duration = prism ? 0.22f : 0.16f;
      _prism = prism;
      _power = Mathf.Clamp01(amount / 55f);
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
      var alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
      var core = _prism
        ? Color.Lerp(new Color(0.42f, 1f, 0.95f, alpha * 0.92f), new Color(0.82f, 0.28f, 1f, alpha * 0.92f), 0.5f)
        : Color.Lerp(new Color(1f, 0.92f, 0.38f, alpha * 0.95f), new Color(0.35f, 0.98f, 1f, alpha * 0.95f), 0.55f);
      var edge = _prism
        ? new Color(0.18f, 0.72f, 1f, alpha * 0.72f)
        : new Color(1f, 0.22f, 0.62f, alpha * 0.72f);
      var flash = new Color(1f, 0.98f, 0.82f, alpha * 0.88f);
      LaserVfxShared.SetLineColor(_ring, edge, core);
      LaserVfxShared.SetLineColor(_rays, core, edge);
      LaserVfxShared.SetLineColor(_arc, flash, edge);
      _ring.startWidth = _ring.endWidth = 0.04f * alpha;
      _rays.startWidth = _rays.endWidth = Mathf.Lerp(0.025f, 0.06f, _power) * alpha;
      _arc.startWidth = _arc.endWidth = 0.026f * alpha;
      _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.06f, _prism ? 0.52f : 0.38f, t);
      _rays.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _prism ? 0.74f : 0.56f, t);
      _arc.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.95f, t);
      if (t >= 1f)
        _root.SetActive(false);
    }

    public void Deactivate() => _root.SetActive(false);
  }

  sealed class LaserHitSparkDriver : MonoBehaviour
  {
    LaserHitSparkFx _effect;
    public void Initialize(LaserHitSparkFx effect) => _effect = effect;
    void Update() => _effect?.Tick(Time.unscaledDeltaTime);
  }
}
