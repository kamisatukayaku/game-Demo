using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Player
{
    /// <summary>
    /// 正交相机跟随玩家（保?Z 偏移），并锁?gameplay 缩放?
    /// 镜头缩放锁定?GameplayViewportTiles（默?32×32 ?屏）?
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField] float smoothTime = 0.12f;
        [SerializeField] float arenaSmoothTime = 0.4f;
        [SerializeField] float orthographicSize = WorldGridConstants.GameplayOrthographicSize;

        Camera _camera;
        Vector3 _velocity;
        Vector2 _lookAhead;
        Vector2 _lookAheadVelocity;
        Vector3 _lastTargetPosition;
        bool _arenaFollowConfigured;
        float _lookAheadDistance;
        float _lookAheadSmoothTime = 0.35f;
        float _maxLookAhead = 4.5f;
        float _edgeFramingDistance;
        float _edgeFramingPullStrength;

        public void SetTarget(Transform t) => target = t;

        public void SetOrthographicSize(float size)
        {
          orthographicSize = size;
          if (_camera != null)
            ApplyGameplayZoom(_camera, orthographicSize);
        }

        public void ConfigureArenaFollow(
          float smoothTime,
          float lookAheadDistance,
          float lookAheadSmoothness,
          float maxLookAhead,
          float edgeFramingDistance,
          float edgeFramingPullStrength)
        {
          arenaSmoothTime = smoothTime;
          _lookAheadDistance = lookAheadDistance;
          _lookAheadSmoothTime = lookAheadSmoothness;
          _maxLookAhead = maxLookAhead;
          _edgeFramingDistance = edgeFramingDistance;
          _edgeFramingPullStrength = edgeFramingPullStrength;
          _arenaFollowConfigured = true;
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyGameplayZoom(_camera);
        }

        void OnEnable()
        {
            if (target != null)
                _lastTargetPosition = target.position;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (target == null)
            {
                var player = GameObject.Find("Player");
                if (player != null)
                    target = player.transform;
            }

            if (_camera == null)
                _camera = GetComponent<Camera>();

            ApplyGameplayZoom(_camera);
        }
#endif

        void LateUpdate()
        {
            if (_camera != null && _camera.orthographic
                && !Mathf.Approximately(_camera.orthographicSize, orthographicSize))
            {
                ApplyGameplayZoom(_camera);
            }

            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
                if (player == null)
                    return;
                target = player.transform;
            }

            var followPos = target.position;
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null)
              followPos = GameplayPlane.ToWorld(rb.position, target.position.z);

            var desired = followPos + offset;
            var followSmoothTime = ArenaLayoutLocator.Layout.IsActive ? arenaSmoothTime : smoothTime;

            if (ArenaLayoutLocator.Layout.IsActive && _arenaFollowConfigured)
            {
              var planarDelta = followPos - _lastTargetPosition;
              _lastTargetPosition = followPos;

              var moveDir = new Vector2(planarDelta.x, planarDelta.y);
              if (moveDir.sqrMagnitude > 0.0001f)
                moveDir.Normalize();
              else if (rb != null && rb.velocity.sqrMagnitude > 0.04f)
                moveDir = rb.velocity.normalized;

              var targetLookAhead = moveDir * Mathf.Min(_maxLookAhead, _lookAheadDistance);
              _lookAhead = Vector2.SmoothDamp(
                _lookAhead,
                targetLookAhead,
                ref _lookAheadVelocity,
                _lookAheadSmoothTime);

              desired += new Vector3(_lookAhead.x, _lookAhead.y, 0f);

              if (_edgeFramingDistance > 0f)
              {
                var layout = ArenaLayoutLocator.Layout;
                var playerPlanar = new Vector2(followPos.x, followPos.y);
                var toCenter = layout.Center - playerPlanar;
                var distFromCenter = toCenter.magnitude;
                var edgeStart = layout.PathRadius - _edgeFramingDistance;
                if (distFromCenter > edgeStart && distFromCenter > 0.01f)
                {
                  var pull = (distFromCenter - edgeStart) * _edgeFramingPullStrength;
                  desired += new Vector3(toCenter.x / distFromCenter * pull, toCenter.y / distFromCenter * pull, 0f);
                }
              }
            }

            if (followSmoothTime <= 0f)
                transform.position = desired;
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, followSmoothTime);
            }
        }

        public static void ApplyGameplayZoom(Camera cam, float size = WorldGridConstants.GameplayOrthographicSize)
        {
            if (cam == null || !cam.orthographic)
                return;

            cam.orthographicSize = size;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        void ApplyGameplayZoom(Camera cam)
        {
            ApplyGameplayZoom(cam, orthographicSize);
        }
    }
}
