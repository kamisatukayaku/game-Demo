using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Health = global::Game.Shared.Combat.Health.Health;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.BossRush;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Runtime;
using Game.Modes.Roguelike.UI;

namespace Game.Modes.Roguelike.Gameplay.Player
{
  [DisallowMultipleComponent]
  public sealed class PlayerStateMachine : MonoBehaviour
  {
    public enum PlayerLifeState
    {
      AliveState,
      DeadState
    }

    readonly List<Behaviour> _disabledBehaviours = new();
    readonly List<Collider2D> _disabledColliders = new();
    readonly List<ColorSnapshot> _colorSnapshots = new();

    Health _health;
    Transform _visualRoot;
    Vector3 _originalScale;
    Quaternion _originalRotation;
    PlayerLifeState _state = PlayerLifeState.AliveState;
    bool _deathRoutineStarted;

    public PlayerLifeState CurrentState => _state;
    public bool IsDead => _state == PlayerLifeState.DeadState;

    public static PlayerStateMachine Ensure(GameObject playerGo)
    {
      if (playerGo == null)
        return null;

      var stateMachine = playerGo.GetComponent<PlayerStateMachine>();
      if (stateMachine == null)
        stateMachine = playerGo.AddComponent<PlayerStateMachine>();
      return stateMachine;
    }

    void Awake()
    {
      _health = GetComponent<Health>();
      ResolveVisualRoot();
    }

    void OnEnable()
    {
      if (_health == null)
        _health = GetComponent<Health>();

      if (_health != null)
        _health.Died += OnHealthDied;
    }

    void OnDisable()
    {
      if (_health != null)
        _health.Died -= OnHealthDied;
    }

    void Update()
    {
      if (_state == PlayerLifeState.AliveState && _health != null && _health.IsDead)
        SwitchState(PlayerLifeState.DeadState, null);
    }

    void OnHealthDied()
    {
      SwitchState(PlayerLifeState.DeadState, null);
    }

    public void SwitchState(PlayerLifeState nextState, GameObject context)
    {
      if (_state == nextState)
        return;

      if (_state == PlayerLifeState.DeadState)
        ExitDeadState();

      _state = nextState;

      if (_state == PlayerLifeState.DeadState)
        EnterDeadState(context);
    }

    void EnterDeadState(GameObject killer)
    {
      if (_deathRoutineStarted)
        return;

      _deathRoutineStarted = true;
      RunDeathSummary.CaptureAtDeath();
      DisableGameplayComponents();
      StopWaveGameplay();
      ArenaMetaProgress.AwardRun(
        false,
        RunDeathSummary.WaveReached,
        RunDeathSummary.TotalKills,
        RunDeathSummary.PlayerLevel,
        RunDeathSummary.SurviveSeconds);
      ArenaAchievementSystem.EvaluateRun(
        false,
        RunDeathSummary.WaveReached,
        RunDeathSummary.TotalKills,
        RunDeathSummary.PlayerLevel,
        RunDeathSummary.BuildDirection,
        ArenaDifficultyRuntime.DifficultyId);
      PlayerDeathFailureUI.EnsureExists();
      StartCoroutine(DeadStateRoutine());
    }

    void ExitDeadState()
    {
      foreach (var behaviour in _disabledBehaviours)
        if (behaviour != null)
          behaviour.enabled = true;
      _disabledBehaviours.Clear();

      foreach (var collider in _disabledColliders)
        if (collider != null)
          collider.enabled = true;
      _disabledColliders.Clear();

      foreach (var snapshot in _colorSnapshots)
        snapshot.Restore();
      _colorSnapshots.Clear();

      ResolveVisualRoot();
      if (_visualRoot != null)
      {
        _visualRoot.localScale = _originalScale;
        _visualRoot.localRotation = _originalRotation;
      }
      Time.timeScale = 1f;
      _deathRoutineStarted = false;
    }

    void DisableGameplayComponents()
    {
      foreach (var behaviour in GetComponents<Behaviour>())
      {
        if (behaviour == null || behaviour == this || !behaviour.enabled)
          continue;

        var typeName = behaviour.GetType().Name;
        if (!IsPlayerGameplayBehaviour(typeName))
          continue;

        behaviour.enabled = false;
        _disabledBehaviours.Add(behaviour);
      }

      foreach (var collider in GetComponentsInChildren<Collider2D>())
      {
        if (collider == null || !collider.enabled)
          continue;

        collider.enabled = false;
        _disabledColliders.Add(collider);
      }
    }

    static bool IsPlayerGameplayBehaviour(string typeName)
    {
      return typeName == "PlayerSphereController"
        || typeName == "PlayerAttackDirector"
        || typeName == "PlayerAutoAttack"
        || typeName == "PlayerActiveSkillController"
        || typeName == "PlayerDashController"
        || typeName == "PlayerAimController";
    }

    void StopWaveGameplay()
    {
      if (WaveDirector.Instance != null)
        WaveDirector.Instance.enabled = false;
    }

    IEnumerator DeadStateRoutine()
    {
      ResolveVisualRoot();
      CaptureAndDimRenderers();
      SpawnDeathVfx();
      StartCoroutine(CameraDeathFeedback());

      var collapseDuration = 0.55f;
      var slowDuration = 0.42f;
      var wobbleAmplitude = 14f;
      var elapsed = 0f;
      while (elapsed < collapseDuration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / collapseDuration);
        var eased = 1f - Mathf.Pow(1f - t, 2.6f);

        if (_visualRoot != null)
        {
          var scalePulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
          _visualRoot.localScale = Vector3.Lerp(_originalScale * 1.1f, _originalScale * 0.08f, eased) * scalePulse;
          var wobble = Mathf.Sin(t * Mathf.PI * 3.2f) * wobbleAmplitude * (1f - t);
          _visualRoot.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, wobble);
        }

        ApplyDeathFade(t);
        AnimateDeathRings(t);

        var timeT = Mathf.Clamp01(elapsed / slowDuration);
        Time.timeScale = Mathf.Lerp(1f, 0.06f, timeT);
        yield return null;
      }

      ApplyDeathFade(1f);
      Time.timeScale = 0f;
      yield return new WaitForSecondsRealtime(0.65f);
      if (GameSessionConfig.IsBossRush)
        yield break;

      PlayerDeathFailureUI.Show();
    }

    void ApplyDeathFade(float t)
    {
      var alpha = Mathf.Lerp(1f, 0.18f, Mathf.SmoothStep(0f, 1f, t));
      foreach (var snapshot in _colorSnapshots)
        snapshot.SetAlpha(alpha);
    }

    GameObject _deathVfxRoot;
    readonly List<LineRenderer> _deathRings = new();

    void ResolveVisualRoot()
    {
      var visual = transform.Find("Visual");
      _visualRoot = visual != null ? visual : transform;
      _originalScale = _visualRoot.localScale;
      _originalRotation = _visualRoot.localRotation;
    }

    void CaptureAndDimRenderers()
    {
      _colorSnapshots.Clear();

      foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
      {
        if (renderer == null)
          continue;

        _colorSnapshots.Add(new ColorSnapshot(renderer));
        renderer.color = Color.Lerp(renderer.color, new Color(0.38f, 0.42f, 0.46f, renderer.color.a), 0.72f);
      }
    }

    static Material s_deathLineMaterial;

    void SpawnDeathVfx()
    {
      _deathVfxRoot = new GameObject("PlayerDeathVFX");
      _deathVfxRoot.transform.position = transform.position;
      _deathRings.Clear();

      var lineMaterial = GetDeathLineMaterial();

      for (var i = 0; i < 3; i++)
      {
        var ringGo = new GameObject($"DeathRing_{i + 1}");
        ringGo.transform.SetParent(_deathVfxRoot.transform, false);
        var ring = ringGo.AddComponent<LineRenderer>();
        if (lineMaterial != null)
          ring.material = lineMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.sortingOrder = 50 + i;
        ring.positionCount = 72;
        ring.startWidth = 0.06f - i * 0.012f;
        ring.endWidth = ring.startWidth;
        ring.startColor = new Color(0.72f, 0.94f, 1f, 0.42f - i * 0.1f);
        ring.endColor = new Color(0.3f, 0.62f, 1f, 0.16f);
        DrawLocalCircle(ring, 0.75f + i * 0.32f);
        _deathRings.Add(ring);
      }

      var particlesGo = new GameObject("DeathParticles");
      particlesGo.transform.SetParent(_deathVfxRoot.transform, false);
      var particles = particlesGo.AddComponent<ParticleSystem>();
      particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = particles.main;
      main.playOnAwake = false;
      main.loop = false;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
      main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.6f);
      main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
      main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.96f, 1f, 0.9f), new Color(0.2f, 0.52f, 1f, 0.6f));
      main.maxParticles = 42;
      var emission = particles.emission;
      emission.enabled = true;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
      var shape = particles.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.35f;
      var renderer = particles.GetComponent<ParticleSystemRenderer>();
      renderer.sortingOrder = 52;
      particles.Play(true);

      Destroy(_deathVfxRoot, 1.4f);
    }

    static Material GetDeathLineMaterial()
    {
      if (s_deathLineMaterial != null)
        return s_deathLineMaterial;

      var shader = Shader.Find("Sprites/Default");
      if (shader == null)
        shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
      if (shader == null)
        return null;

      s_deathLineMaterial = new Material(shader) { name = "PlayerDeathLine_Runtime" };
      return s_deathLineMaterial;
    }

    void AnimateDeathRings(float t)
    {
      if (_deathRings.Count == 0)
        return;

      for (var i = 0; i < _deathRings.Count; i++)
      {
        var ring = _deathRings[i];
        if (ring == null)
          continue;

        var baseRadius = 0.75f + i * 0.32f;
        var expand = 1f + t * (0.55f + i * 0.18f);
        DrawLocalCircle(ring, baseRadius * expand);

        var alpha = Mathf.Lerp(0.42f - i * 0.1f, 0f, t);
        ring.startColor = new Color(0.72f, 0.94f, 1f, alpha);
        ring.endColor = new Color(0.3f, 0.62f, 1f, alpha * 0.35f);
      }
    }

    IEnumerator CameraDeathFeedback()
    {
      var camera = Camera.main;
      if (camera == null)
        yield break;

      var original = camera.transform.position;
      var originalSize = camera.orthographicSize;
      var elapsed = 0f;
      while (elapsed < 0.35f)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / 0.35f);
        var shake = (1f - t) * 0.12f;
        camera.transform.position = original + (Vector3)(Random.insideUnitCircle * shake);
        camera.orthographicSize = Mathf.Lerp(originalSize * 0.94f, originalSize, t);
        yield return null;
      }

      camera.transform.position = original;
      camera.orthographicSize = originalSize;
    }

    static void DrawLocalCircle(LineRenderer line, float radius)
    {
      for (var i = 0; i < line.positionCount; i++)
      {
        var angle = i * Mathf.PI * 2f / line.positionCount;
        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
      }
    }

    readonly struct ColorSnapshot
    {
      readonly SpriteRenderer _renderer;
      readonly Color _color;

      public ColorSnapshot(SpriteRenderer renderer)
      {
        _renderer = renderer;
        _color = renderer != null ? renderer.color : Color.white;
      }

      public void Restore()
      {
        if (_renderer != null)
          _renderer.color = _color;
      }

      public void SetAlpha(float alpha)
      {
        if (_renderer == null)
          return;

        var c = _renderer.color;
        c.a = _color.a * alpha;
        _renderer.color = c;
      }
    }
  }
}
