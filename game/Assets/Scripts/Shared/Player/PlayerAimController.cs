using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Player
{
  /// <summary>玩家朝向：始终面向鼠标在世界平面上的位置?/summary>
  [DisallowMultipleComponent]
  public class PlayerAimController : MonoBehaviour
  {
    [SerializeField] float minAimSqrMagnitude = 0.0004f;

    Vector2 _aimDirection = Vector2.right;
    float _aimAngleDeg;

    public Vector2 AimDirection => _aimDirection;
    public float AimAngleDeg => _aimAngleDeg;
    public Vector2 AimWorldPoint { get; private set; }

    public static PlayerAimController Instance { get; private set; }

    void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(this);
        return;
      }

      Instance = this;
    }

    void OnDestroy()
    {
      if (Instance == this)
        Instance = null;
    }

    void Update()
    {
      RefreshAim();
    }

    void RefreshAim()
    {
      var cam = Camera.main;
      if (cam == null)
        return;

      var mouse = Input.mousePosition;
      mouse.z = Mathf.Abs(cam.transform.position.z);
      var world = cam.ScreenToWorldPoint(mouse);
      AimWorldPoint = new Vector2(world.x, world.y);

      var origin = GameplayPlane.Position2D(transform);
      var delta = AimWorldPoint - origin;
      if (delta.sqrMagnitude < minAimSqrMagnitude)
        return;

      _aimDirection = delta.normalized;
      _aimAngleDeg = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg;
    }

    public static Vector2 GetAimDirectionOrDefault(Vector2 fallback = default)
    {
      if (Instance != null && Instance._aimDirection.sqrMagnitude > 0.0001f)
        return Instance._aimDirection;

      return fallback == default ? Vector2.right : fallback;
    }
  }
}