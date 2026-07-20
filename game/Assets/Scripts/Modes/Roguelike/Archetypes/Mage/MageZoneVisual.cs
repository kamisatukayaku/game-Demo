using UnityEngine;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>A4: Purple gravity grid visualization scaling with zone radius.</summary>
  public sealed class MageZoneVisual
  {
    readonly Transform _root;
    LineRenderer[] _gridLines;
    Material _lineMaterial;
    float _builtRadius;

    static readonly Color GridColor = new(0.62f, 0.28f, 0.95f, 0.42f);

    public MageZoneVisual(Transform root) => _root = root;

    public void EnsureBuilt()
    {
      if (_gridLines != null)
        return;

      _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "MageZoneGrid_Runtime" };
      _gridLines = new LineRenderer[6];
      for (var i = 0; i < _gridLines.Length; i++)
      {
        var go = new GameObject($"MageZoneGrid_{i}");
        go.transform.SetParent(_root, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.material = _lineMaterial;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.sortingOrder = 3;
        line.startColor = line.endColor = GridColor;
        _gridLines[i] = line;
      }
    }

    public void Update(Vector3 center, float radius)
    {
      EnsureBuilt();
      if (_gridLines == null)
        return;

      _root.position = new Vector3(center.x, center.y, -0.08f);
      if (Mathf.Abs(radius - _builtRadius) > 0.05f)
      {
        RebuildGrid(radius);
        _builtRadius = radius;
      }

      var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3.2f);
      var c = GridColor;
      c.a *= pulse;
      foreach (var line in _gridLines)
      {
        if (line == null)
          continue;
        line.startColor = line.endColor = c;
      }
    }

    void RebuildGrid(float radius)
    {
      var rings = 3;
      var spokes = 6;
      var idx = 0;
      for (var r = 1; r <= rings && idx < _gridLines.Length; r++, idx++)
        DrawCircle(_gridLines[idx], radius * r / rings, 32);

      for (var s = 0; s < spokes && idx < _gridLines.Length; s++, idx++)
      {
        var angle = s * Mathf.PI * 2f / spokes;
        var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        SetLine(_gridLines[idx], Vector3.zero, dir);
      }
    }

    static void DrawCircle(LineRenderer line, float radius, int segments)
    {
      line.loop = true;
      line.positionCount = segments;
      for (var i = 0; i < segments; i++)
      {
        var angle = i * Mathf.PI * 2f / segments;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    static void SetLine(LineRenderer line, Vector3 from, Vector3 to)
    {
      line.loop = false;
      line.positionCount = 2;
      line.SetPosition(0, from);
      line.SetPosition(1, to);
    }

    public void Shutdown()
    {
      if (_gridLines == null)
        return;

      foreach (var line in _gridLines)
      {
        if (line != null)
          Object.Destroy(line.gameObject);
      }

      _gridLines = null;
      _builtRadius = 0f;
    }
  }
}
