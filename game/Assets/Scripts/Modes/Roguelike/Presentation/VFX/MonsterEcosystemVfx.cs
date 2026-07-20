using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class MonsterEcosystemVfx : MonoBehaviour
  {
    const int PoolSize = 32;
    static MonsterEcosystemVfx s_instance;
    readonly Queue<EcosystemPulse> _free = new();
    Material _material;

    public static void Play(string id, Vector3 position, float radius, float duration, GameObject source)
    {
      EnsureExists();
      if (s_instance == null) return;
      var pulse = s_instance.GetPulse();
      pulse.Play(id, position, Mathf.Max(0.5f, radius), Mathf.Clamp(duration, 0.25f, 2f), source);
    }

    static void EnsureExists()
    {
      if (s_instance != null) return;
      var root = new GameObject("_MonsterEcosystemVfx");
      DontDestroyOnLoad(root);
      s_instance = root.AddComponent<MonsterEcosystemVfx>();
    }

    void Awake()
    {
      _material = new Material(Shader.Find("Sprites/Default")) { name = "MonsterEcosystemLine_Runtime" };
      for (var i = 0; i < PoolSize; i++)
        _free.Enqueue(CreatePulse(i));
    }

    EcosystemPulse GetPulse()
    {
      if (_free.Count > 0) return _free.Dequeue();
      return CreatePulse(PoolSize);
    }

    EcosystemPulse CreatePulse(int index)
    {
      var go = new GameObject("EcosystemPulse_" + index);
      go.transform.SetParent(transform, false);
      var pulse = go.AddComponent<EcosystemPulse>();
      pulse.Initialize(_material, () => _free.Enqueue(pulse));
      go.SetActive(false);
      return pulse;
    }
  }

  sealed class EcosystemPulse : MonoBehaviour
  {
    const int Segments = 48;
    LineRenderer _outer;
    LineRenderer _inner;
    System.Action _release;
    GameObject _source;
    Color _color;
    float _radius;
    float _duration;
    float _age;

    public void Initialize(Material material, System.Action release)
    {
      _release = release;
      _outer = CreateLine("Outer", material, 0.07f, 91);
      _inner = CreateLine("Inner", material, 0.025f, 92);
    }

    public void Play(string id, Vector3 position, float radius, float duration, GameObject source)
    {
      _source = source;
      _radius = radius;
      _duration = duration;
      _age = 0f;
      _color = ResolveColor(id);
      transform.position = position;
      gameObject.SetActive(true);
      DrawRing(_outer, radius, 0f);
      DrawRing(_inner, radius * 0.72f, id.GetHashCode() % 17f);
    }

    void Update()
    {
      _age += Time.unscaledDeltaTime;
      if (_source != null) transform.position = _source.transform.position;
      var t = Mathf.Clamp01(_age / _duration);
      var alpha = Mathf.Sin(t * Mathf.PI) * 0.78f;
      var scale = Mathf.Lerp(0.72f, 1.08f, Mathf.SmoothStep(0f, 1f, t));
      transform.localScale = Vector3.one * scale;
      SetColor(_outer, _color, alpha);
      SetColor(_inner, Color.Lerp(_color, Color.white, 0.45f), alpha * 0.65f);
      if (t < 1f) return;
      gameObject.SetActive(false);
      _source = null;
      _release?.Invoke();
    }

    LineRenderer CreateLine(string name, Material material, float width, int order)
    {
      var child = new GameObject(name);
      child.transform.SetParent(transform, false);
      var line = child.AddComponent<LineRenderer>();
      line.material = material;
      line.useWorldSpace = false;
      line.loop = true;
      line.positionCount = Segments;
      line.startWidth = line.endWidth = width;
      line.numCornerVertices = 2;
      line.sortingOrder = order;
      return line;
    }

    static void DrawRing(LineRenderer line, float radius, float phase)
    {
      for (var i = 0; i < Segments; i++)
      {
        var angle = Mathf.PI * 2f * i / Segments + phase;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    static void SetColor(LineRenderer line, Color color, float alpha)
    {
      color.a = alpha;
      line.startColor = line.endColor = color;
    }

    static Color ResolveColor(string id)
    {
      if (id.Contains("Support")) return new Color(0.25f, 1f, 0.72f);
      if (id.Contains("Bomber") || id.Contains("Explosion")) return new Color(1f, 0.22f, 0.08f);
      if (id.Contains("Disruptor") || id.Contains("Gravity")) return new Color(0.3f, 0.68f, 1f);
      if (id.Contains("Elite")) return new Color(1f, 0.78f, 0.2f);
      if (id.Contains("Splitter")) return new Color(0.72f, 0.35f, 1f);
      return new Color(1f, 0.42f, 0.22f);
    }
  }
}
