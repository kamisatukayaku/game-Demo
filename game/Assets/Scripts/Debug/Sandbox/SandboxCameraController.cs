using UnityEngine;

namespace Game.DevTools.Sandbox
{
  [DisallowMultipleComponent]
  public class SandboxCameraController : MonoBehaviour
  {
    public const float MinOrthoSize = 5f;
    public const float MaxOrthoSize = 20f;
    public const float DefaultOrthoSize = 8f;

    Camera _camera;
    Transform _target;
    float _orthoSize = DefaultOrthoSize;

    public float OrthoSize => _orthoSize;
    public RenderTexture Texture { get; private set; }

    public void EnsureBuilt(Transform target, Vector3 worldCenter, int width = 640, int height = 480)
    {
      _target = target;
      if (_camera == null)
      {
        var go = new GameObject("SandboxCamera");
        go.transform.SetParent(transform, false);
        _camera = go.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 1f);
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 100f;
      }

      SetZoom(_orthoSize <= 0f ? DefaultOrthoSize : _orthoSize);

      if (Texture == null || Texture.width != width || Texture.height != height)
      {
        if (Texture != null)
          Texture.Release();

        Texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
          name = "SandboxViewRT"
        };
        _camera.targetTexture = Texture;
      }

      SnapTo(worldCenter);
    }

    public void SetZoom(float size)
    {
      _orthoSize = Mathf.Clamp(size, MinOrthoSize, MaxOrthoSize);
      if (_camera != null)
        _camera.orthographicSize = _orthoSize;
    }

    public void AdjustZoom(float scrollDelta) =>
      SetZoom(_orthoSize - scrollDelta * 0.6f);

    public void SnapTo(Vector3 center)
    {
      if (_camera == null)
        return;

      _camera.transform.position = new Vector3(center.x, center.y, -20f);
    }

    void LateUpdate()
    {
      if (_camera == null || _target == null)
        return;

      var pos = _target.position;
      _camera.transform.position = new Vector3(pos.x, pos.y, -20f);
    }

    public void Dispose()
    {
      if (Texture != null)
      {
        Texture.Release();
        Texture = null;
      }

      if (_camera != null)
        Destroy(_camera.gameObject);
    }
  }
}
