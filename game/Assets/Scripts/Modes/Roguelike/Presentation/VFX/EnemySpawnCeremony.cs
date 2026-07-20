using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public static class EnemySpawnCeremony
  {
    static SpawnFxPool s_pool;

    public static void Play(GameObject enemy, string enemyId)
    {
      if (enemy == null)
        return;

      if (s_pool == null)
        s_pool = new SpawnFxPool();

      var runner = enemy.GetComponent<EnemySpawnCeremonyRunner>();
      if (runner == null)
        runner = enemy.AddComponent<EnemySpawnCeremonyRunner>();

      var elite = IsElite(enemy, enemyId);
      var delay = Random.Range(0f, 0.3f);
      runner.Begin(s_pool, elite, delay);
    }

    static bool IsElite(GameObject enemy, string enemyId)
    {
      if (!string.IsNullOrEmpty(enemyId) && enemyId.ToLowerInvariant().Contains("elite"))
        return true;
      if (!string.IsNullOrEmpty(enemyId) && enemyId.ToLowerInvariant().Contains("boss"))
        return true;
      var lowerName = enemy.name.ToLowerInvariant();
      return lowerName.Contains("elite") || lowerName.Contains("boss");
    }
  }

  [DisallowMultipleComponent]
  public sealed class EnemySpawnCeremonyRunner : MonoBehaviour
  {
    const float WarningDuration = 0.4f;
    const float RiftDuration = 0.3f;
    const float MaterializeDuration = 0.3f;

    readonly List<Behaviour> _disabledBehaviours = new();
    readonly List<Collider2D> _disabledColliders2D = new();
    readonly List<SpriteRendererState> _spriteStates = new();
    readonly List<LineRendererState> _lineStates = new();
    readonly List<MeshRendererState> _meshStates = new();

    SpawnFxPool _pool;
    Vector3 _originalScale;
    bool _running;

    public void Begin(SpawnFxPool pool, bool elite, float delay)
    {
      _pool = pool;
      if (_running)
        StopAllCoroutines();
      StartCoroutine(Routine(elite, delay));
    }

    IEnumerator Routine(bool elite, float delay)
    {
      _running = true;
      _originalScale = transform.localScale;
      CaptureRenderers();
      FreezeGameplay();
      ApplyVisualProgress(0f, true);

      if (delay > 0f)
        yield return new WaitForSeconds(delay);

      var position = transform.position;
      var radius = EstimateRadius();
      _pool.PlayWarning(position, radius, elite);
      yield return new WaitForSeconds(WarningDuration);

      _pool.PlayRift(position, radius, elite);
      yield return new WaitForSeconds(RiftDuration);

      var elapsed = 0f;
      while (elapsed < MaterializeDuration)
      {
        elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(elapsed / MaterializeDuration);
        ApplyVisualProgress(1f - Mathf.Pow(1f - t, 2.2f), false);
        var lift = Mathf.Sin(t * Mathf.PI) * 0.2f;
        transform.position = position + Vector3.up * lift;
        yield return null;
      }

      transform.position = position;
      ApplyVisualProgress(1f, false);
      _pool.PlayImpact(position, radius, elite);
      RestoreGameplay();
      _running = false;
      Destroy(this);
    }

    void FreezeGameplay()
    {
      var health = GetComponent<Health>();
      if (health != null)
        health.GrantInvulnerability(WarningDuration + RiftDuration + MaterializeDuration + 0.45f);

      _disabledBehaviours.Clear();
      var behaviours = GetComponents<Behaviour>();
      foreach (var behaviour in behaviours)
      {
        if (behaviour == null || behaviour == this || !behaviour.enabled)
          continue;

        if (behaviour is EnemyCore or EnemyAttack or EnemyMovement or BossCore)
        {
          behaviour.enabled = false;
          _disabledBehaviours.Add(behaviour);
        }
      }

      _disabledColliders2D.Clear();
      var colliders = GetComponentsInChildren<Collider2D>(true);
      foreach (var collider in colliders)
      {
        if (collider == null || !collider.enabled)
          continue;

        collider.enabled = false;
        _disabledColliders2D.Add(collider);
      }
    }

    void RestoreGameplay()
    {
      foreach (var collider in _disabledColliders2D)
        if (collider != null)
          collider.enabled = true;
      _disabledColliders2D.Clear();

      foreach (var behaviour in _disabledBehaviours)
        if (behaviour != null)
          behaviour.enabled = true;
      _disabledBehaviours.Clear();
    }

    void CaptureRenderers()
    {
      _spriteStates.Clear();
      foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
        if (sprite != null)
          _spriteStates.Add(new SpriteRendererState(sprite, sprite.color));

      _lineStates.Clear();
      foreach (var line in GetComponentsInChildren<LineRenderer>(true))
        if (line != null)
          _lineStates.Add(new LineRendererState(line, line.startColor, line.endColor));

      _meshStates.Clear();
      foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true))
        if (mesh != null)
          _meshStates.Add(new MeshRendererState(mesh));
    }

    void ApplyVisualProgress(float progress, bool hidden)
    {
      progress = hidden ? 0f : Mathf.Clamp01(progress);
      transform.localScale = _originalScale * Mathf.Lerp(0.08f, 1f, progress);

      foreach (var state in _spriteStates)
      {
        if (state.Renderer == null) continue;
        var color = state.OriginalColor;
        color.a *= progress;
        state.Renderer.color = color;
      }

      foreach (var state in _lineStates)
      {
        if (state.Renderer == null) continue;
        state.Renderer.startColor = WithAlpha(state.StartColor, state.StartColor.a * progress);
        state.Renderer.endColor = WithAlpha(state.EndColor, state.EndColor.a * progress);
      }

      foreach (var state in _meshStates)
        state.Apply(progress);
    }

    float EstimateRadius()
    {
      var bounds = new Bounds(transform.position, Vector3.one * 0.8f);
      var found = false;
      foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
      {
        if (sprite == null) continue;
        bounds = found ? Encapsulate(bounds, sprite.bounds) : sprite.bounds;
        found = true;
      }

      if (!found)
        return 0.65f;

      return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y), 0.35f, 2.2f);
    }

    static Bounds Encapsulate(Bounds bounds, Bounds other)
    {
      bounds.Encapsulate(other.min);
      bounds.Encapsulate(other.max);
      return bounds;
    }

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }

    readonly struct SpriteRendererState
    {
      public readonly SpriteRenderer Renderer;
      public readonly Color OriginalColor;

      public SpriteRendererState(SpriteRenderer renderer, Color originalColor)
      {
        Renderer = renderer;
        OriginalColor = originalColor;
      }
    }

    readonly struct LineRendererState
    {
      public readonly LineRenderer Renderer;
      public readonly Color StartColor;
      public readonly Color EndColor;

      public LineRendererState(LineRenderer renderer, Color startColor, Color endColor)
      {
        Renderer = renderer;
        StartColor = startColor;
        EndColor = endColor;
      }
    }

    sealed class MeshRendererState
    {
      readonly MeshRenderer _renderer;
      readonly MaterialPropertyBlock _block = new();
      readonly Color _color;
      readonly bool _hasColor;

      public MeshRendererState(MeshRenderer renderer)
      {
        _renderer = renderer;
        var mat = renderer.sharedMaterial;
        _hasColor = mat != null && mat.HasProperty("_Color");
        _color = _hasColor ? mat.color : Color.white;
      }

      public void Apply(float progress)
      {
        if (_renderer == null || !_hasColor)
          return;

        _renderer.GetPropertyBlock(_block);
        var color = _color;
        color.a *= progress;
        _block.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_block);
      }
    }
  }

  public sealed class SpawnFxPool
  {
    const int PrewarmCount = 48;

    readonly Queue<SpawnFx> _pool = new();
    readonly Transform _root;
    readonly Material _lineMaterial;

    public SpawnFxPool()
    {
      _root = new GameObject("_RoguelikeEnemySpawnFxPool").transform;
      if (Application.isPlaying)
        Object.DontDestroyOnLoad(_root.gameObject);
      _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EnemySpawnFxLine_Runtime" };

      for (var i = 0; i < PrewarmCount; i++)
        Release(SpawnFx.Create(_root, _lineMaterial));
    }

    public void PlayWarning(Vector3 position, float enemyRadius, bool elite)
    {
      var fx = Get();
      fx.PlayWarning(position, enemyRadius, elite, Release);
    }

    public void PlayRift(Vector3 position, float enemyRadius, bool elite)
    {
      var fx = Get();
      fx.PlayRift(position, enemyRadius, elite, Release);
    }

    public void PlayImpact(Vector3 position, float enemyRadius, bool elite)
    {
      var fx = Get();
      fx.PlayImpact(position, enemyRadius, elite, Release);
    }

    SpawnFx Get()
    {
      while (_pool.Count > 0)
      {
        var fx = _pool.Dequeue();
        if (fx != null)
          return fx;
      }

      return SpawnFx.Create(_root, _lineMaterial);
    }

    void Release(SpawnFx fx)
    {
      if (fx == null) return;
      fx.gameObject.SetActive(false);
      fx.transform.SetParent(_root, false);
      _pool.Enqueue(fx);
    }
  }

  sealed class SpawnFx : MonoBehaviour
  {
    const int CircleSegments = 72;

    LineRenderer _outer;
    LineRenderer _inner;
    LineRenderer _hex;
    ParticleSystem _particles;
    System.Action<SpawnFx> _release;
    Color _color;
    float _radius;
    float _duration;
    float _age;
    Mode _mode;
    bool _elite;

    enum Mode { Warning, Rift, Impact }

    public static SpawnFx Create(Transform root, Material material)
    {
      var go = new GameObject("EnemySpawnFx");
      go.transform.SetParent(root, false);
      var fx = go.AddComponent<SpawnFx>();
      fx.Build(material);
      go.SetActive(false);
      return fx;
    }

    public void PlayWarning(Vector3 position, float enemyRadius, bool elite, System.Action<SpawnFx> release)
    {
      Begin(position, enemyRadius, elite, release, Mode.Warning, 0.4f);
    }

    public void PlayRift(Vector3 position, float enemyRadius, bool elite, System.Action<SpawnFx> release)
    {
      Begin(position, enemyRadius, elite, release, Mode.Rift, 0.3f);
      EmitInwardParticles(elite ? 28 : 16);
    }

    public void PlayImpact(Vector3 position, float enemyRadius, bool elite, System.Action<SpawnFx> release)
    {
      Begin(position, enemyRadius, elite, release, Mode.Impact, 0.22f);
      EmitOutwardParticles(elite ? 22 : 10);
    }

    void Begin(Vector3 position, float enemyRadius, bool elite, System.Action<SpawnFx> release, Mode mode, float duration)
    {
      transform.position = position;
      _radius = Mathf.Max(0.45f, enemyRadius) * (elite ? 3f : 2f);
      _elite = elite;
      _release = release;
      _mode = mode;
      _duration = duration;
      _age = 0f;
      _color = elite ? new Color(1f, 0.42f, 0.1f, 1f) : new Color(1f, 0.18f, 0.08f, 1f);
      gameObject.SetActive(true);

      _outer.enabled = true;
      _inner.enabled = true;
      _hex.enabled = true;
      _particles.Clear(true);
      UpdateVisual(0f);
    }

    void Build(Material material)
    {
      _outer = CreateLine("SpawnOuterRing", material, 0.08f, 86);
      _inner = CreateLine("SpawnInnerRing", material, 0.045f, 87);
      _hex = CreateLine("SpawnHexGate", material, 0.075f, 88);
      _particles = CreateParticles();
    }

    void Update()
    {
      _age += Time.deltaTime;
      var t = Mathf.Clamp01(_age / Mathf.Max(0.01f, _duration));
      UpdateVisual(t);
      if (_age >= _duration)
        _release?.Invoke(this);
    }

    void UpdateVisual(float t)
    {
      switch (_mode)
      {
        case Mode.Warning:
          DrawCircle(_outer, Mathf.Lerp(_radius * 0.6f, _radius, t), CircleSegments);
          DrawCircle(_inner, Mathf.Lerp(_radius * 0.25f, _radius * 0.58f, t), 6);
          DrawCircle(_hex, _radius * 0.7f, 6);
          SetLineColor(_outer, _color, Mathf.SmoothStep(0f, 1f, t));
          SetLineColor(_inner, Color.white, 0.42f * Mathf.SmoothStep(0f, 1f, t));
          SetLineColor(_hex, _color, 0.62f * Mathf.SmoothStep(0f, 1f, t));
          _hex.transform.localRotation = Quaternion.Euler(0f, 0f, _age * 120f);
          break;

        case Mode.Rift:
          var collapse = 1f - t;
          DrawCircle(_outer, Mathf.Lerp(_radius, _radius * 0.62f, t), 6);
          DrawCircle(_inner, Mathf.Lerp(_radius * 0.45f, _radius * 0.12f, t), CircleSegments);
          DrawCircle(_hex, Mathf.Lerp(_radius * 0.9f, _radius * 0.55f, t), 6);
          SetLineColor(_outer, _color, 0.9f * collapse);
          SetLineColor(_inner, Color.black, 0.85f * collapse);
          SetLineColor(_hex, Color.white, 0.55f * collapse);
          _outer.transform.localRotation = Quaternion.Euler(0f, 0f, _age * -180f);
          _hex.transform.localRotation = Quaternion.Euler(0f, 0f, _age * 220f);
          break;

        case Mode.Impact:
          DrawCircle(_outer, Mathf.Lerp(_radius * 0.3f, _radius * (_elite ? 1.45f : 1.15f), t), CircleSegments);
          DrawCircle(_inner, Mathf.Lerp(_radius * 0.2f, _radius * 0.82f, t), 6);
          DrawCircle(_hex, Mathf.Lerp(_radius * 0.25f, _radius, t), 6);
          var alpha = Mathf.Sin(t * Mathf.PI);
          SetLineColor(_outer, _color, alpha * 0.8f);
          SetLineColor(_inner, Color.white, alpha * 0.42f);
          SetLineColor(_hex, _color, alpha * 0.55f);
          break;
      }
    }

    void EmitInwardParticles(int count)
    {
      for (var i = 0; i < count; i++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var pos = dir * Random.Range(_radius * 0.55f, _radius * 1.1f);
        var emit = new ParticleSystem.EmitParams
        {
          position = new Vector3(pos.x, pos.y, 0f),
          velocity = new Vector3(-dir.x, -dir.y, 0f) * Random.Range(3.5f, _elite ? 8f : 5.5f),
          startLifetime = Random.Range(0.18f, 0.32f),
          startSize = Random.Range(0.035f, _elite ? 0.09f : 0.065f),
          startColor = WithAlpha(Color.Lerp(_color, Color.white, Random.Range(0.1f, 0.5f)), 0.9f)
        };
        _particles.Emit(emit, 1);
      }
    }

    void EmitOutwardParticles(int count)
    {
      for (var i = 0; i < count; i++)
      {
        var dir = Random.insideUnitCircle.normalized;
        var emit = new ParticleSystem.EmitParams
        {
          position = Vector3.zero,
          velocity = new Vector3(dir.x, dir.y, 0f) * Random.Range(1.4f, _elite ? 5.2f : 3.2f),
          startLifetime = Random.Range(0.14f, 0.28f),
          startSize = Random.Range(0.03f, _elite ? 0.08f : 0.055f),
          startColor = WithAlpha(Color.Lerp(_color, Color.white, Random.Range(0.05f, 0.55f)), 0.85f)
        };
        _particles.Emit(emit, 1);
      }
    }

    LineRenderer CreateLine(string name, Material material, float width, int sortOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = false;
      line.loop = true;
      line.material = material;
      line.startWidth = width;
      line.endWidth = width;
      line.sortingOrder = sortOrder;
      line.numCapVertices = 2;
      return line;
    }

    ParticleSystem CreateParticles()
    {
      var go = new GameObject("SpawnFragments");
      go.transform.SetParent(transform, false);
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.loop = false;
      main.playOnAwake = false;
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f);
      main.maxParticles = 80;
      var emission = ps.emission;
      emission.enabled = false;
      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      renderer.renderMode = ParticleSystemRenderMode.Billboard;
      renderer.material = new Material(Shader.Find("Sprites/Default")) { name = "EnemySpawnParticles_Runtime" };
      renderer.sortingOrder = 89;
      return ps;
    }

    void SetLineColor(LineRenderer line, Color color, float alpha)
    {
      line.startColor = WithAlpha(color, alpha);
      line.endColor = WithAlpha(Color.white, alpha * 0.35f);
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

    static Color WithAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }
}
