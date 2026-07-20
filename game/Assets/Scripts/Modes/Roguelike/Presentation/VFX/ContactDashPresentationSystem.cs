using System.Collections.Generic;
using UnityEngine;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Gameplay.Player;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>Event-driven pooled VFX for player dash and contact dash strike.</summary>
  public sealed class ContactDashPresentationSystem : MonoBehaviour
  {
    const int PoolSize = 10;
    static ContactDashPresentationSystem s_instance;
    static Material s_sharedLineMaterial;

    readonly Queue<LineFx> _linePool = new();
    readonly Queue<SlashFx> _slashPool = new();
    Transform _root;
    EventListenerHandle _startedHandle;
    EventListenerHandle _hitHandle;
    EventListenerHandle _endedHandle;
    EventListenerHandle _aftershockHandle;
    EventListenerHandle _damageTrailHandle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      new GameObject("_ContactDashPresentation").AddComponent<ContactDashPresentationSystem>();
    }

    public static void ResetAll()
    {
      if (s_instance == null)
        return;
      s_instance.StopAll();
      if (s_instance._root != null)
        Destroy(s_instance._root.gameObject);
      s_instance._linePool.Clear();
      s_instance._slashPool.Clear();
      s_instance._root = null;
      if (s_sharedLineMaterial != null)
      {
        Destroy(s_sharedLineMaterial);
        s_sharedLineMaterial = null;
      }
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      _root = new GameObject("ContactDashVfxRoot").transform;
      _root.SetParent(transform, false);
      EnsureSharedMaterial();
      for (var i = 0; i < PoolSize; i++)
      {
        _linePool.Enqueue(LineFx.Create(_root, s_sharedLineMaterial));
        _slashPool.Enqueue(SlashFx.Create(_root, s_sharedLineMaterial));
      }

      _startedHandle = GameEventBus.Subscribe<DashStartedEvent>(OnDashStarted);
      _hitHandle = GameEventBus.Subscribe<DashEnemyHitEvent>(OnDashHit);
      _endedHandle = GameEventBus.Subscribe<DashEndedEvent>(OnDashEnded);
      _aftershockHandle = GameEventBus.Subscribe<DashAftershockEvent>(OnAftershock);
      _damageTrailHandle = GameEventBus.Subscribe<DashDamageTrailEvent>(OnDamageTrail);
    }

    void OnDestroy()
    {
      GameEventBus.Unsubscribe(_startedHandle);
      GameEventBus.Unsubscribe(_hitHandle);
      GameEventBus.Unsubscribe(_endedHandle);
      GameEventBus.Unsubscribe(_aftershockHandle);
      GameEventBus.Unsubscribe(_damageTrailHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void StopAll()
    {
      foreach (var fx in _linePool)
        fx.ForceStop();
      foreach (var fx in _slashPool)
        fx.ForceStop();
    }

    static void EnsureSharedMaterial()
    {
      if (s_sharedLineMaterial != null)
        return;
      s_sharedLineMaterial = new Material(Shader.Find("Sprites/Default"))
      {
        name = "ContactDashSharedLine"
      };
    }

    void OnDashStarted(DashStartedEvent evt)
    {
      if (evt.Player == null)
        return;

      var color = ResolveDashColor();
      PlayBurst(evt.Start, color, 0.78f);
      PlayTrail(evt.Start, evt.Start + evt.Direction * evt.ActualDistance, color, 0.18f, 0.2f);
      StartCoroutine(ScalePulse(evt.Player.transform));
      StartCoroutine(CameraPulse());
    }

    void OnDashHit(DashEnemyHitEvent evt)
    {
      if (evt.IsAftershock)
        return;
      PlayHitSlash(evt.HitPosition, evt.Direction, 0.95f);
    }

    void OnDashEnded(DashEndedEvent evt)
    {
      if (evt.Player == null)
        return;
      var color = ResolveDashColor();
      PlayBurst(evt.End, color, evt.TriggeredAftershock ? 1.55f : 1.35f);
      if (evt.IsContactStrike && !evt.TriggeredAftershock)
        PlayHitSlash(evt.End, evt.Direction, 2.6f);
    }

    void OnAftershock(DashAftershockEvent evt)
    {
      PlayRing(evt.Position, new Color(1f, 0.72f, 0.28f, 0.85f), evt.Radius, 0.34f);
    }

    void OnDamageTrail(DashDamageTrailEvent evt)
    {
      var color = new Color(0.56f, 0.9f, 1f, 0.58f);
      var visualWidth = Mathf.Clamp(evt.Width, 0.28f, 1.65f);
      PlayTrail(evt.Start, evt.End, color, visualWidth, Mathf.Max(0.24f, evt.Lifetime));
    }

    void PlayBurst(Vector2 pos, Color color, float size)
    {
      var fx = AcquireLine();
      fx.PlayRing(pos, color, size, 0.24f, ReleaseLine);
    }

    void PlayTrail(Vector2 from, Vector2 to, Color color, float width, float lifetime)
    {
      var fx = AcquireLine();
      fx.PlayTrail(from, to, color, width, lifetime, ReleaseLine);
    }

    void PlayRing(Vector2 pos, Color color, float size, float lifetime)
    {
      var fx = AcquireLine();
      fx.PlayRing(pos, color, size, lifetime, ReleaseLine);
    }

    void PlayHitSlash(Vector2 origin, Vector2 dir, float radius)
    {
      var fx = AcquireSlash();
      fx.Play(origin, dir, radius, ReleaseSlash);
    }

    LineFx AcquireLine() =>
      _linePool.Count > 0 ? _linePool.Dequeue() : LineFx.Create(_root, s_sharedLineMaterial);

    SlashFx AcquireSlash() =>
      _slashPool.Count > 0 ? _slashPool.Dequeue() : SlashFx.Create(_root, s_sharedLineMaterial);

    void ReleaseLine(LineFx fx)
    {
      fx.ResetState();
      fx.transform.SetParent(_root, false);
      _linePool.Enqueue(fx);
    }

    void ReleaseSlash(SlashFx fx)
    {
      fx.ResetState();
      fx.transform.SetParent(_root, false);
      _slashPool.Enqueue(fx);
    }

    static Color ResolveDashColor() =>
      GameSessionConfig.SelectedWeaponTheme switch
      {
        "warrior" => new Color(1f, 0.62f, 0.18f, 1f),
        "mage" => new Color(0.35f, 0.85f, 1f, 1f),
        "ranged" => new Color(0.45f, 1f, 0.95f, 1f),
        _ => new Color(0.72f, 0.95f, 1f, 1f)
      };

    static System.Collections.IEnumerator ScalePulse(Transform player)
    {
      var visual = player.Find("Visual");
      var target = visual != null ? visual : player;
      var original = target.localScale;
      var elapsed = 0f;
      const float duration = 0.05f;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var scale = t < 0.5f
          ? Mathf.Lerp(1f, 0.85f, t / 0.5f)
          : Mathf.Lerp(0.85f, 1f, (t - 0.5f) / 0.5f);
        target.localScale = original * scale;
        yield return null;
      }
      target.localScale = original;
    }

    static System.Collections.IEnumerator CameraPulse()
    {
      var cam = Camera.main;
      if (cam == null)
        yield break;

      var ortho = cam.orthographic;
      var startSize = cam.orthographicSize;
      var startFov = cam.fieldOfView;
      var elapsed = 0f;
      const float duration = 0.18f;
      while (elapsed < duration)
      {
        if (cam == null)
          yield break;
        elapsed += Time.unscaledDeltaTime;
        var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
        if (ortho)
          cam.orthographicSize = Mathf.Lerp(startSize, startSize * 0.965f, pulse);
        else
          cam.fieldOfView = Mathf.Lerp(startFov, startFov - 2f, pulse);
        yield return null;
      }

      if (cam == null)
        yield break;
      if (ortho) cam.orthographicSize = startSize;
      else cam.fieldOfView = startFov;
    }

    sealed class LineFx : MonoBehaviour
    {
      LineRenderer _line;
      float _age;
      float _lifetime;
      Color _color;
      bool _ring;
      float _size;
      System.Action<LineFx> _release;

      public static LineFx Create(Transform root, Material material)
      {
        var go = new GameObject("DashLineFx");
        go.transform.SetParent(root, false);
        var fx = go.AddComponent<LineFx>();
        fx._line = go.AddComponent<LineRenderer>();
        fx._line.useWorldSpace = true;
        fx._line.material = material;
        fx._line.sortingOrder = 138;
        go.SetActive(false);
        return fx;
      }

      public void PlayTrail(Vector2 from, Vector2 to, Color color, float width, float lifetime, System.Action<LineFx> release)
      {
        _ring = false;
        _age = 0f;
        _lifetime = Mathf.Max(0.04f, lifetime);
        _color = color;
        _release = release;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        gameObject.SetActive(true);
        _line.useWorldSpace = true;
        _line.loop = false;
        _line.positionCount = 2;
        _line.numCapVertices = 3;
        _line.startWidth = width;
        _line.endWidth = width;
        _line.SetPosition(0, GameplayPlane.ToWorld(from, 0f));
        _line.SetPosition(1, GameplayPlane.ToWorld(to, 0f));
      }

      public void PlayRing(Vector2 position, Color color, float size, float lifetime, System.Action<LineFx> release)
      {
        _ring = true;
        _age = 0f;
        _lifetime = Mathf.Max(0.04f, lifetime);
        _color = color;
        _size = size;
        _release = release;
        transform.position = GameplayPlane.ToWorld(position, 0f);
        gameObject.SetActive(true);
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.startWidth = 0.08f;
        _line.endWidth = 0.08f;
        DrawCircle(_line, 0.15f, 6);
      }

      public void ResetState()
      {
        _release = null;
        _line.positionCount = 0;
        gameObject.SetActive(false);
      }

      public void ForceStop() => ResetState();

      void Update()
      {
        if (!gameObject.activeSelf)
          return;

        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _lifetime);
        var alpha = 1f - t;
        _line.startColor = WithAlpha(_color, alpha * 0.72f);
        _line.endColor = WithAlpha(new Color(0.9f, 0.98f, 1f, 1f), alpha * 0.36f);

        if (_ring)
        {
          DrawCircle(_line, Mathf.Lerp(0.15f, _size, t), 6);
          _line.startWidth = Mathf.Lerp(0.1f, 0.025f, t);
          _line.endWidth = _line.startWidth;
        }
        else
        {
          var width = Mathf.Lerp(_line.startWidth, 0.018f, t);
          _line.startWidth = width;
          _line.endWidth = width;
        }

        if (_age >= _lifetime)
          _release?.Invoke(this);
      }

      static Color WithAlpha(Color color, float alpha)
      {
        color.a = alpha;
        return color;
      }

      static void DrawCircle(LineRenderer line, float radius, int segments)
      {
        line.positionCount = segments;
        for (var i = 0; i < segments; i++)
        {
          var angle = i * Mathf.PI * 2f / segments;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
      }
    }

    sealed class SlashFx : MonoBehaviour
    {
      LineRenderer _line;
      float _age;
      float _duration;
      System.Action<SlashFx> _release;

      public static SlashFx Create(Transform root, Material material)
      {
        var go = new GameObject("DashSlashFx");
        go.transform.SetParent(root, false);
        var fx = go.AddComponent<SlashFx>();
        fx._line = go.AddComponent<LineRenderer>();
        fx._line.useWorldSpace = true;
        fx._line.material = material;
        fx._line.sortingOrder = 145;
        go.SetActive(false);
        return fx;
      }

      public void Play(Vector2 origin, Vector2 aimDir, float radius, System.Action<SlashFx> release)
      {
        _release = release;
        _age = 0f;
        _duration = 0.32f;
        gameObject.SetActive(true);
        var dir = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : Vector2.right;
        const int segments = 14;
        const float halfAngle = 55f;
        _line.positionCount = segments;
        for (var i = 0; i < segments; i++)
        {
          var t = segments <= 1 ? 0.5f : i / (float)(segments - 1);
          var angle = Mathf.Lerp(-halfAngle, halfAngle, t);
          var ray = Rotate2D(dir, angle) * radius;
          _line.SetPosition(i, GameplayPlane.ToWorld(origin + ray, 0f));
        }
      }

      public void ResetState()
      {
        _release = null;
        gameObject.SetActive(false);
      }

      public void ForceStop() => ResetState();

      void Update()
      {
        if (!gameObject.activeSelf)
          return;
        _age += Time.deltaTime;
        var alpha = 1f - Mathf.Clamp01(_age / _duration);
        _line.startColor = new Color(0.95f, 0.98f, 1f, 0.85f * alpha);
        _line.endColor = new Color(0.55f, 0.82f, 1f, 0.05f * alpha);
        if (_age >= _duration)
          _release?.Invoke(this);
      }

      static Vector2 Rotate2D(Vector2 v, float degrees)
      {
        var rad = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
      }
    }
  }
}
