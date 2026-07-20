using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.UI;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>B15: Blocks one arena quadrant after telegraph — dim overlay + player clamp.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaQuadrantBlocker : MonoBehaviour
  {
    const int SegmentCount = 48;

    static ArenaQuadrantBlocker s_instance;

    int _blockedQuadrant = -1;
    int _pendingQuadrant = -1;
    float _warningTimer;
    float _activeTimer;
    bool _warningActive;
    SpriteRenderer _overlay;
    LineRenderer _borderLine;
    LineRenderer _warningLine;
    float _pulseAge;
    EventListenerHandle _bossPhaseHandle;
    EventListenerHandle _enemyKilledHandle;

    public static bool IsActive => s_instance != null && s_instance._blockedQuadrant >= 0;

    public static int BlockedQuadrant => s_instance != null ? s_instance._blockedQuadrant : -1;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      if (GameSessionConfig.SelectedMode != GameSessionConfig.GameMode.Arena)
        return;

      var go = new GameObject("_ArenaQuadrantBlocker");
      s_instance = go.AddComponent<ArenaQuadrantBlocker>();
    }

    void OnEnable()
    {
      _bossPhaseHandle = GameEventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDisable()
    {
      if (_bossPhaseHandle.Valid)
        GameEventBus.Unsubscribe(_bossPhaseHandle);
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);
      _bossPhaseHandle = EventListenerHandle.Invalid;
      _enemyKilledHandle = EventListenerHandle.Invalid;
    }

    static void OnBossPhaseChanged(BossPhaseChangedEvent evt)
    {
      if (evt.BossId != "prism_nexus" || evt.ToPhase < 2)
        return;

      EnsureExists();
      if (s_instance == null)
        return;

      s_instance.BeginWarning(Random.Range(0, 4));
      ArenaMomentUI.ShowBanner("象限封锁", new Color(1f, 0.42f, 0.28f, 1f));
    }

    static void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (!evt.IsBoss)
        return;

      if (evt.EnemyId != null && evt.EnemyId.Contains("prism_nexus"))
        Clear();
    }

    public static void ActivateRandom()
    {
      EnsureExists();
      s_instance?.BeginWarning(Random.Range(0, 4));
    }

    public static void Activate(int quadrant)
    {
      EnsureExists();
      s_instance?.BeginWarning(Mathf.Clamp(quadrant, 0, 3));
    }

    public static void Clear()
    {
      if (s_instance == null)
        return;

      s_instance._blockedQuadrant = -1;
      s_instance._pendingQuadrant = -1;
      s_instance._warningActive = false;
      s_instance._warningTimer = 0f;
      s_instance._activeTimer = 0f;
      if (s_instance._overlay != null)
        s_instance._overlay.enabled = false;
      if (s_instance._borderLine != null)
        s_instance._borderLine.enabled = false;
      if (s_instance._warningLine != null)
        s_instance._warningLine.enabled = false;
    }

    public static bool IsPositionBlocked(Vector2 worldPos)
    {
      if (!IsActive)
        return false;

      return GetQuadrant(worldPos) == s_instance._blockedQuadrant;
    }

    public static Vector2 ClampPosition(Vector2 worldPos, float entityRadius = 0.5f)
    {
      if (!IsActive || !IsPositionBlocked(worldPos))
        return worldPos;

      var center = CircleArenaController.Center;
      var offset = worldPos - center;
      if (offset.sqrMagnitude < 0.0001f)
        offset = Vector2.right * 0.1f;

      var angle = Mathf.Atan2(offset.y, offset.x);
      var start = QuadrantStartRad(s_instance._blockedQuadrant);
      var end = start + Mathf.PI * 0.5f;
      var distToStart = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, start * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
      var distToEnd = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, end * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
      var edgeAngle = distToStart < distToEnd ? start : end;
      var radius = offset.magnitude;
      var clamped = center + new Vector2(Mathf.Cos(edgeAngle), Mathf.Sin(edgeAngle)) * radius;
      clamped += new Vector2(Mathf.Cos(edgeAngle + Mathf.PI * 0.5f), Mathf.Sin(edgeAngle + Mathf.PI * 0.5f)) * entityRadius;
      return CircleArenaController.ClampPosition(clamped, entityRadius);
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      BossBalanceDatabase.EnsureLoaded();
      BuildVisuals();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BeginWarning(int quadrant)
    {
      _pendingQuadrant = quadrant;
      _warningActive = true;
      _warningTimer = BossBalanceDatabase.QuadrantBlock.warning_sec;
      _blockedQuadrant = -1;
      EnsureVisuals();
      if (_warningLine != null)
        _warningLine.enabled = true;
      if (_overlay != null)
        _overlay.enabled = false;
      if (_borderLine != null)
        _borderLine.enabled = false;
      UpdateWarningVisuals();
    }

    void ApplyBlock(int quadrant)
    {
      _blockedQuadrant = quadrant;
      _pendingQuadrant = -1;
      _warningActive = false;
      _activeTimer = BossBalanceDatabase.QuadrantBlock.duration_sec;
      EnsureVisuals();
      _overlay.enabled = true;
      _borderLine.enabled = true;
      if (_warningLine != null)
        _warningLine.enabled = false;
      UpdateVisuals();
      ArenaMomentUI.PlaySignatureFlash(new Color(1f, 0.35f, 0.22f, 0.92f));
    }

    void LateUpdate()
    {
      if (_warningActive)
      {
        _warningTimer -= Time.deltaTime;
        _pulseAge += Time.deltaTime * 3f;
        UpdateWarningVisuals();
        if (_warningTimer <= 0f)
          ApplyBlock(_pendingQuadrant);
        return;
      }

      if (!IsActive)
        return;

      _activeTimer -= Time.deltaTime;
      _pulseAge += Time.deltaTime * 2.2f;
      UpdateVisuals();
      if (_activeTimer <= 0f)
        Clear();
    }

    void BuildVisuals()
    {
      var overlayGo = new GameObject("BlockedQuadrantOverlay");
      overlayGo.transform.SetParent(transform, false);
      _overlay = overlayGo.AddComponent<SpriteRenderer>();
      _overlay.sortingOrder = 6;
      _overlay.enabled = false;
      SpriteMaterialUtility.ApplyUnlit(_overlay);

      var borderGo = new GameObject("BlockedQuadrantBorder");
      borderGo.transform.SetParent(transform, false);
      _borderLine = borderGo.AddComponent<LineRenderer>();
      _borderLine.useWorldSpace = true;
      _borderLine.loop = true;
      _borderLine.material = new Material(Shader.Find("Sprites/Default"));
      _borderLine.sortingOrder = 7;
      _borderLine.enabled = false;

      var warningGo = new GameObject("BlockedQuadrantWarning");
      warningGo.transform.SetParent(transform, false);
      _warningLine = warningGo.AddComponent<LineRenderer>();
      _warningLine.useWorldSpace = true;
      _warningLine.loop = true;
      _warningLine.material = new Material(Shader.Find("Sprites/Default"));
      _warningLine.sortingOrder = 8;
      _warningLine.enabled = false;
    }

    void EnsureVisuals()
    {
      if (_overlay.sprite == null)
        _overlay.sprite = CreateWedgeSprite();
    }

    void UpdateWarningVisuals()
    {
      if (!_warningActive || _pendingQuadrant < 0)
        return;

      var center = CircleArenaController.Center;
      var radius = CircleArenaController.EffectiveRadius;
      var start = QuadrantStartRad(_pendingQuadrant);
      var pulse = 0.5f + 0.5f * Mathf.Sin(_pulseAge);
      DrawWedgeBorder(_warningLine, center, radius, start, start + Mathf.PI * 0.5f);
      _warningLine.startWidth = 0.18f + pulse * 0.08f;
      _warningLine.endWidth = _warningLine.startWidth;
      var c = new Color(1f, 0.55f, 0.18f, 0.45f + pulse * 0.45f);
      _warningLine.startColor = _warningLine.endColor = c;
    }

    void UpdateVisuals()
    {
      if (!IsActive)
        return;

      var center = CircleArenaController.Center;
      var radius = CircleArenaController.EffectiveRadius;
      var start = QuadrantStartRad(_blockedQuadrant);
      var pulse = 0.55f + 0.45f * Mathf.Sin(_pulseAge);

      _overlay.transform.position = new Vector3(center.x, center.y, 0f);
      _overlay.transform.localScale = Vector3.one * (radius * 2f / _overlay.sprite.bounds.size.x);
      _overlay.transform.rotation = Quaternion.Euler(0f, 0f, start * Mathf.Rad2Deg);
      _overlay.color = new Color(0.08f, 0.02f, 0.06f, 0.42f + pulse * 0.18f);

      DrawWedgeBorder(_borderLine, center, radius, start, start + Mathf.PI * 0.5f);
      _borderLine.startWidth = 0.14f + pulse * 0.06f;
      _borderLine.endWidth = _borderLine.startWidth;
      var borderColor = new Color(1f, 0.28f, 0.22f, 0.55f + pulse * 0.35f);
      _borderLine.startColor = _borderLine.endColor = borderColor;
    }

    static void DrawWedgeBorder(LineRenderer line, Vector2 center, float radius, float startRad, float endRad)
    {
      if (line == null)
        return;

      line.positionCount = SegmentCount + 2;
      line.SetPosition(0, new Vector3(center.x, center.y, 0f));
      for (var i = 0; i <= SegmentCount; i++)
      {
        var t = i / (float)SegmentCount;
        var angle = Mathf.Lerp(startRad, endRad, t);
        var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        line.SetPosition(i + 1, new Vector3(pos.x, pos.y, 0f));
      }
    }

    static float QuadrantStartRad(int quadrant)
    {
      return quadrant switch
      {
        0 => -Mathf.PI * 0.25f,
        1 => Mathf.PI * 0.25f,
        2 => Mathf.PI * 0.75f,
        _ => -Mathf.PI * 0.75f
      };
    }

    static int GetQuadrant(Vector2 worldPos)
    {
      var offset = worldPos - CircleArenaController.Center;
      var angle = Mathf.Atan2(offset.y, offset.x);
      if (angle < -Mathf.PI * 0.25f)
        return 3;
      if (angle < Mathf.PI * 0.25f)
        return 0;
      if (angle < Mathf.PI * 0.75f)
        return 1;
      return 2;
    }

    static Sprite CreateWedgeSprite()
    {
      const int size = 256;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };

      var pixels = new Color[size * size];
      var center = new Vector2(size * 0.5f, size * 0.5f);
      for (var y = 0; y < size; y++)
      {
        for (var x = 0; x < size; x++)
        {
          var offset = new Vector2(x + 0.5f, y + 0.5f) - center;
          var dist = offset.magnitude / (size * 0.5f);
          var angle = Mathf.Atan2(offset.y, offset.x);
          var inWedge = angle >= -Mathf.PI * 0.25f && angle <= Mathf.PI * 0.25f && dist <= 1f;
          var alpha = inWedge ? Mathf.SmoothStep(1f, 0.65f, dist) : 0f;
          pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
      }

      tex.SetPixels(pixels);
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
    }
  }
}
