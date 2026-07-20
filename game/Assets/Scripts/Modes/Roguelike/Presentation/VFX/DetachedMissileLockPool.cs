using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  sealed class MissileLockPool
  {
    readonly MissileLockFx[] _items;

    public MissileLockPool(Transform parent, int count)
    {
      _items = new MissileLockFx[count];
      for (var i = 0; i < count; i++)
        _items[i] = MissileLockFx.Create(parent, i);
    }

    public void Play(Transform source, Transform target, bool child)
    {
      foreach (var item in _items)
      {
        if (!item.Active)
        {
          item.Play(source, target, child);
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

  sealed class MissileLockFx
  {
    readonly GameObject _root;
    readonly LineRenderer _outer;
    readonly LineRenderer _inner;
    readonly LineRenderer _link;
    Transform _source;
    Transform _target;
    float _age;
    float _duration;
    bool _child;

    public bool Active => _root.activeSelf;

    MissileLockFx(GameObject root, LineRenderer outer, LineRenderer inner, LineRenderer link)
    {
      _root = root;
      _outer = outer;
      _inner = inner;
      _link = link;
      root.SetActive(false);
      root.AddComponent<MissileLockDriver>().Initialize(this);
    }

    public static MissileLockFx Create(Transform parent, int index)
    {
      var root = new GameObject($"MissileLock_{index + 1}");
      root.transform.SetParent(parent, false);
      var outer = CreateCircle(root.transform, "OuterLock", LaserVfxShared.CreateFlatBeamMaterialInstance(), 57, 24, 1f);
      var inner = CreateCircle(root.transform, "InnerLock", LaserVfxShared.CreateFlatBeamMaterialInstance(), 58, 16, 0.72f);
      var link = CreateLink(root.transform, "TargetLink", LaserVfxShared.CreateFlatBeamMaterialInstance(), 56);
      return new MissileLockFx(root, outer, inner, link);
    }

    static LineRenderer CreateLink(Transform parent, string name, Material material, int order)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.loop = false;
      line.positionCount = 2;
      line.material = material;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.numCapVertices = 2;
      return line;
    }

    static LineRenderer CreateCircle(Transform parent, string name, Material material, int order, int segments, float radius)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.positionCount = segments;
      line.material = material;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = order;
      line.numCapVertices = 2;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        var mod = i % 4 == 0 ? 0.92f : 1f;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius * mod, Mathf.Sin(angle) * radius * mod));
      }
      return line;
    }

    public void Play(Transform source, Transform target, bool child)
    {
      _source = source;
      _target = target;
      _child = child;
      _age = 0f;
      _duration = child ? 0.22f : 0.32f;
      _root.SetActive(true);
      Tick(0f);
    }

    public void Tick(float deltaTime)
    {
      if (!Active)
        return;
      if (_target == null)
      {
        _root.SetActive(false);
        return;
      }

      _age += deltaTime;
      var t = Mathf.Clamp01(_age / _duration);
      _root.transform.position = _target.position + Vector3.up * 0.08f;
      var alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
      var scale = Mathf.Lerp(_child ? 0.78f : 1.15f, _child ? 0.42f : 0.56f, t);
      _outer.transform.localScale = Vector3.one * scale;
      _inner.transform.localScale = Vector3.one * (scale * Mathf.Lerp(0.78f, 0.56f, t));
      _outer.transform.Rotate(0f, 0f, -160f * deltaTime);
      _inner.transform.Rotate(0f, 0f, 210f * deltaTime);
      var edge = new Color(1f, 0.38f, 0.06f, alpha * (_child ? 0.42f : 0.68f));
      var core = new Color(1f, 0.82f, 0.32f, alpha * (_child ? 0.38f : 0.62f));
      LaserVfxShared.SetLineColor(_outer, edge, edge);
      LaserVfxShared.SetLineColor(_inner, core, core);
      _outer.startWidth = _outer.endWidth = _child ? 0.026f : 0.04f;
      _inner.startWidth = _inner.endWidth = _child ? 0.018f : 0.03f;
      if (_link != null)
      {
        var sourcePosition = _source != null ? _source.position : _target.position;
        var targetPosition = _target.position + Vector3.up * 0.08f;
        _link.enabled = _source != null;
        _link.SetPosition(0, sourcePosition);
        _link.SetPosition(1, targetPosition);
        LaserVfxShared.SetLineColor(_link, new Color(1f, 0.78f, 0.28f, alpha * (_child ? 0.32f : 0.48f)), new Color(1f, 0.22f, 0.04f, alpha * 0.05f));
        _link.startWidth = _child ? 0.018f : 0.028f;
        _link.endWidth = 0.004f;
      }

      if (t >= 1f)
      {
        _source = null;
        _target = null;
        _root.SetActive(false);
      }
    }

    public void Deactivate()
    {
      _source = null;
      _target = null;
      _root.SetActive(false);
    }
  }

  sealed class MissileLockDriver : MonoBehaviour
  {
    MissileLockFx _effect;
    public void Initialize(MissileLockFx effect) => _effect = effect;
    void Update() => _effect?.Tick(Time.unscaledDeltaTime);
  }
}
