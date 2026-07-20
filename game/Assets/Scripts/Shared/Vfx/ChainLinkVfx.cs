using UnityEngine;

namespace Game.Shared.Vfx
{
  /// <summary>连锁弹命中时在目标间闪过的短促电弧?/summary>
  [DisallowMultipleComponent]
  public class ChainLinkVfx : MonoBehaviour
  {
    LineRenderer _line;
    float _remaining = 0.12f;

    public static void Spawn(Vector3 from, Vector3 to)
    {
      var go = new GameObject("ChainLinkVfx");
      var fx = go.AddComponent<ChainLinkVfx>();
      fx.Build(from, to);
    }

    void Build(Vector3 from, Vector3 to)
    {
      _line = gameObject.AddComponent<LineRenderer>();
      _line.useWorldSpace = true;
      _line.positionCount = 2;
      _line.startWidth = 0.07f;
      _line.endWidth = 0.03f;
      _line.material = new Material(Shader.Find("Sprites/Default"));
      _line.startColor = new Color(0.94f, 1f, 1f, 0.95f);
      _line.endColor = new Color(0.2f, 0.72f, 1f, 0.38f);
      _line.sortingOrder = 44;
      _line.SetPosition(0, from);
      _line.SetPosition(1, to);
    }

    void Update()
    {
      _remaining -= Time.deltaTime;
      if (_line != null)
      {
        var alpha = Mathf.Clamp01(_remaining / 0.12f);
        _line.startColor = new Color(0.94f, 1f, 1f, alpha);
        _line.endColor = new Color(0.12f, 0.52f, 1f, alpha * 0.38f);
      }
      if (_remaining <= 0f)
        Destroy(gameObject);
    }

    void OnDestroy()
    {
      if (_line != null && _line.material != null)
        Destroy(_line.material);
    }
  }
}
