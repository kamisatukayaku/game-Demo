using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  sealed class MissileBurstPool
  {
    readonly MissileBurstFx[] _items;

    public MissileBurstPool(Transform parent, int count)
    {
      _items = new MissileBurstFx[count];
      for (var i = 0; i < count; i++)
        _items[i] = MissileBurstFx.Create(parent, i);
    }

    public void Play(Vector3 position, bool hit)
    {
      foreach (var item in _items)
      {
        if (!item.Active)
        {
          item.Play(position, hit);
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

  sealed class MissileBurstFx
  {
    readonly GameObject _root;
    readonly LineRenderer _ring;
    readonly LineRenderer _secondRing;
    readonly LineRenderer _rays;
    float _age;
    float _duration;
    bool _hit;

    public bool Active => _root.activeSelf;

    MissileBurstFx(GameObject root, LineRenderer ring, LineRenderer secondRing, LineRenderer rays)
    {
      _root = root;
      _ring = ring;
      _secondRing = secondRing;
      _rays = rays;
      root.SetActive(false);
      root.AddComponent<MissileBurstDriver>().Initialize(this);
    }

    public static MissileBurstFx Create(Transform parent, int index)
    {
      var root = new GameObject($"MissileBurst_{index + 1}");
      root.transform.SetParent(parent, false);
      var ring = CreateLine(root.transform, "Ring", LaserVfxShared.CreateFlatBeamMaterialInstance(), 48);
      ring.loop = true;
      ring.positionCount = 17;
      for (var i = 0; i < 17; i++)
      {
        var angle = i / 16f * Mathf.PI * 2f;
        ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
      }
      var secondRing = CreateLine(root.transform, "SecondaryRing", LaserVfxShared.CreateFlatBeamMaterialInstance(), 47);
      secondRing.loop = true;
      secondRing.positionCount = 17;
      for (var i = 0; i < 17; i++)
      {
        var angle = i / 16f * Mathf.PI * 2f;
        secondRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
      }
      var rays = CreateLine(root.transform, "Fragments", LaserVfxShared.CreateFlatBeamMaterialInstance(), 49);
      rays.positionCount = 16;
      for (var i = 0; i < 8; i++)
      {
        var angle = i / 8f * Mathf.PI * 2f;
        var inner = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.16f;
        var outer = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 1f : 0.62f);
        rays.SetPosition(i * 2, inner);
        rays.SetPosition(i * 2 + 1, outer);
      }
      return new MissileBurstFx(root, ring, secondRing, rays);
    }

    static LineRenderer CreateLine(Transform parent, string name, Material material, int order)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.material = material;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.numCapVertices = 3;
      return line;
    }

    public void Play(Vector3 position, bool hit)
    {
      _hit = hit;
      _age = 0f;
      _duration = hit ? 0.16f : 0.09f;
      _root.transform.position = position;
      _root.SetActive(true);
      Tick(0f);
    }

    public void Tick(float deltaTime)
    {
      if (!Active)
        return;
      _age += deltaTime;
      var t = Mathf.Clamp01(_age / _duration);
      var alpha = 1f - t;
      var color = _hit ? new Color(1f, 0.42f, 0.08f, alpha) : new Color(1f, 0.68f, 0.18f, alpha);
      var core = _hit ? new Color(1f, 0.92f, 0.62f, alpha * 0.95f) : new Color(1f, 0.96f, 0.78f, alpha);
      LaserVfxShared.SetLineColor(_ring, color, color);
      LaserVfxShared.SetLineColor(_secondRing, new Color(1f, 0.28f, 0.04f, alpha * (_hit ? 0.42f : 0.18f)), new Color(1f, 0.28f, 0.04f, alpha * (_hit ? 0.42f : 0.18f)));
      LaserVfxShared.SetLineColor(_rays, core, color);
      _ring.startWidth = _ring.endWidth = _hit ? 0.08f : 0.045f;
      _secondRing.startWidth = _secondRing.endWidth = (_hit ? 0.035f : 0.018f) * alpha;
      _rays.startWidth = _rays.endWidth = (_hit ? 0.055f : 0.03f) * alpha;
      _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _hit ? 0.92f : 0.46f, t);
      var secondaryT = Mathf.Clamp01((t - 0.22f) / 0.78f);
      _secondRing.transform.localScale = Vector3.one * Mathf.Lerp(0.04f, _hit ? 1.25f : 0.58f, secondaryT);
      _rays.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _hit ? 0.74f : 0.32f, t);
      if (t >= 1f)
        _root.SetActive(false);
    }

    public void Deactivate() => _root.SetActive(false);
  }

  sealed class MissileBurstDriver : MonoBehaviour
  {
    MissileBurstFx _effect;
    public void Initialize(MissileBurstFx effect) => _effect = effect;
    void Update() => _effect?.Tick(Time.unscaledDeltaTime);
  }
}
