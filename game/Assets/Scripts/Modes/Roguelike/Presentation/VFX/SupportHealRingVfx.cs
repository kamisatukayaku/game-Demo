using UnityEngine;

using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Tutorial;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>A6: Green pulsing heal ring for Support build; marks enemies inside with slow debuff tint.</summary>
  [DisallowMultipleComponent]
  public sealed class SupportHealRingVfx : MonoBehaviour
  {
    const float BaseRadius = 5.5f;
    const float PulseSpeed = 2.4f;
    const float MarkInterval = 0.35f;

    static SupportHealRingVfx s_instance;

    LineRenderer _ring;
    Material _lineMaterial;
    Transform _player;
    float _pulseAge;
    float _markTimer;
    bool _zonePublished;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_SupportHealRingVfx");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<SupportHealRingVfx>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "SupportHealRing_Runtime" };
      BuildRing();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildRing()
    {
      _ring = gameObject.AddComponent<LineRenderer>();
      _ring.useWorldSpace = false;
      _ring.loop = true;
      _ring.material = _lineMaterial;
      _ring.startWidth = 0.1f;
      _ring.endWidth = 0.1f;
      _ring.sortingOrder = 12;
      DrawCircle(_ring, BaseRadius, 48);
    }

    void Update()
    {
      if (ArenaBuildBootstrap.SelectedBuildId != ArenaBuildBootstrap.Support)
      {
        if (_ring != null)
          _ring.enabled = false;
        _zonePublished = false;
        return;
      }

      if (_player == null)
      {
        var playerGo = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        _player = playerGo != null ? playerGo.transform : null;
      }

      if (_player == null)
        return;

      _ring.enabled = true;
      transform.position = _player.position;

      if (!_zonePublished)
      {
        _zonePublished = true;
        var center = GameplayPlane.Position2D(_player);
        GameEventBus.Publish(new GroundZoneSpawnedEvent("support_heal_ring", center, BaseRadius, 999f));
      }
      _pulseAge += Time.deltaTime * PulseSpeed;
      var pulse = 0.5f + 0.5f * Mathf.Sin(_pulseAge * Mathf.PI * 2f);
      var radius = BaseRadius * (0.92f + pulse * 0.1f);
      DrawCircle(_ring, radius, 48);
      var alpha = 0.35f + pulse * 0.35f;
      var color = new Color(0.35f, 0.95f, 0.55f, alpha);
      _ring.startColor = _ring.endColor = color;

      _markTimer -= Time.deltaTime;
      if (_markTimer <= 0f)
      {
        _markTimer = MarkInterval;
        MarkEnemiesInRing(radius);
      }
    }

    static void MarkEnemiesInRing(float radius)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null || s_instance?._player == null)
        return;

      var center = GameplayPlane.Position2D(s_instance._player);
      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;
        var sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
          sr.color = new Color(0.75f, 1f, 0.78f, 1f);
      }
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
}
