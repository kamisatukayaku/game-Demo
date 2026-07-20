using System.Collections.Generic;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Gameplay.Events;
using Game.Shared.Combat;
using Game.Shared.Enemy.AI;
using Game.Shared.Laser;
using Game.Shared.UI;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class EnemyHitReactionVfx : MonoBehaviour
  {
    const int SparkPoolSize = 80;
    static EnemyHitReactionVfx s_instance;
    readonly HitSparkFx[] _sparks = new HitSparkFx[SparkPoolSize];

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_EnemyHitReactionVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<EnemyHitReactionVfx>();
    }

    public static void ResetAll()
    {
      if (s_instance == null)
        return;
      foreach (var spark in s_instance._sparks)
        spark?.Deactivate();
    }

    public static string GetDebugSummary()
    {
      if (s_instance == null)
        return "HitReaction inactive";
      var active = 0;
      foreach (var spark in s_instance._sparks)
      {
        if (spark != null && spark.Active)
          active++;
      }
      return $"HitReaction {active}/{s_instance._sparks.Length}";
    }

    public static void Play(DamageEvent evt)
    {
      if (evt.Target == null || evt.Amount <= 0f || IsExcluded(evt.Target))
        return;

      EnsureExists();
      var elite = IsElite(evt.Target);
      var color = ResolveColor(evt.DamageType, evt.DamageSource, evt.Attacker);
      var direction = ResolveDirection(evt.Attacker, evt.Target, evt.Position);
      var target = evt.Target.GetComponent<EnemyHitReactionTarget>();
      if (target == null)
        target = evt.Target.AddComponent<EnemyHitReactionTarget>();
      target.Play(elite);

      var scale = Mathf.Clamp(Mathf.Sqrt(evt.Amount) * 0.11f, 0.55f, elite ? 1.15f : 0.95f);
      s_instance.PlaySpark(evt.Position, direction, color, scale, elite);
      // Damage numbers are shown once by DamageDisplay on Health.Damaged.
    }

    static bool IsExcluded(GameObject target)
    {
      if (target.CompareTag("Player"))
        return true;
      if (target.GetComponentInParent<BossCore>() != null)
        return true;
      var name = target.name.ToLowerInvariant();
      return name.Contains("boss");
    }

    static bool IsElite(GameObject target)
    {
      var agent = target.GetComponent<MonsterEcosystemAgent>();
      if (agent != null && agent.IsElite)
        return true;
      return target.name.ToLowerInvariant().Contains("elite");
    }

    static Vector2 ResolveDirection(GameObject attacker, GameObject target, Vector3 hitPosition)
    {
      if (attacker != null)
      {
        var delta = (Vector2)(hitPosition - attacker.transform.position);
        if (delta.sqrMagnitude > 0.0001f)
          return delta.normalized;
      }

      if (target != null)
      {
        var delta = (Vector2)(hitPosition - target.transform.position);
        if (delta.sqrMagnitude > 0.0001f)
          return delta.normalized;
      }

      return Random.insideUnitCircle.normalized;
    }

    static Color ResolveColor(string damageType, string damageSource, GameObject attacker)
    {
      if (IsPlayerAttribution(attacker))
        return Color.Lerp(ArenaBuildBootstrap.GetKillFeedbackColor(), Color.white, 0.18f);

      var key = ((damageSource ?? string.Empty) + " " + (damageType ?? string.Empty)).ToLowerInvariant();
      if (key.Contains("fire") || key.Contains("flame") || key.Contains("burn") || key.Contains("missile"))
        return new Color(1f, 0.42f, 0.08f, 1f);
      if (key.Contains("shock") || key.Contains("lightning") || key.Contains("laser") || key.Contains("energy"))
        return new Color(0.45f, 0.92f, 1f, 1f);
      if (key.Contains("ice") || key.Contains("frost") || key.Contains("slow") || key.Contains("tidal"))
        return new Color(0.58f, 0.86f, 1f, 1f);
      if (key.Contains("poison") || key.Contains("trail"))
        return new Color(0.24f, 1f, 0.42f, 1f);
      if (key.Contains("explosion") || key.Contains("physical") || key.Contains("boomerang"))
        return new Color(1f, 0.82f, 0.22f, 1f);
      return Color.white;
    }

    static DamageNumberStyle ResolveNumberStyle(string damageType, string damageSource)
    {
      var key = ((damageSource ?? string.Empty) + " " + (damageType ?? string.Empty)).ToLowerInvariant();
      if (key.Contains("shock") || key.Contains("lightning") || key.Contains("laser") || key.Contains("energy"))
        return DamageNumberStyle.Tech;
      if (key.Contains("poison") || key.Contains("trail"))
        return DamageNumberStyle.Eco;
      return DamageNumberStyle.Normal;
    }

    static bool IsPlayerAttribution(GameObject attacker) =>
      attacker != null && (attacker.CompareTag("Player") || attacker.name == "Player"
        || attacker.transform.root.CompareTag("Player"));

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
      for (var i = 0; i < _sparks.Length; i++)
        _sparks[i] = HitSparkFx.Create(transform, i);
    }

    void Update()
    {
      var dt = Time.unscaledDeltaTime;
      foreach (var spark in _sparks)
        spark.Tick(dt);
    }

    void PlaySpark(Vector3 position, Vector2 direction, Color color, float scale, bool elite)
    {
      foreach (var spark in _sparks)
      {
        if (spark.Active)
          continue;
        spark.Play(position, direction, color, scale, elite);
        return;
      }
    }

    [DisallowMultipleComponent]
    sealed class EnemyHitReactionTarget : MonoBehaviour
    {
      readonly List<SpriteRenderer> _sprites = new();
      readonly List<Color> _spriteColors = new();
      readonly List<LineRenderer> _lines = new();
      readonly List<(Color start, Color end)> _lineColors = new();
      Vector3 _baseScale;
      float _age;
      float _duration;
      bool _active;

      void Awake()
      {
        _baseScale = transform.localScale;
        CacheRenderers();
      }

      void OnEnable()
      {
        _baseScale = transform.localScale;
        Restore();
      }

      void OnDisable() => Restore();

      public void Play(bool elite)
      {
        if (_sprites.Count == 0 && _lines.Count == 0)
          CacheRenderers();
        _duration = elite ? 0.09f : 0.115f;
        _age = 0f;
        _active = true;
      }

      void Update()
      {
        if (!_active)
          return;

        _age += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(_age / Mathf.Max(0.01f, _duration));
        var flash = Mathf.Pow(1f - t, 2.2f);
        var punch = Mathf.Sin(t * Mathf.PI);
        for (var i = 0; i < _sprites.Count; i++)
        {
          if (_sprites[i] == null)
            continue;
          _sprites[i].color = Color.Lerp(_spriteColors[i], Color.white, flash * 0.82f);
        }
        for (var i = 0; i < _lines.Count; i++)
        {
          if (_lines[i] == null)
            continue;
          _lines[i].startColor = Color.Lerp(_lineColors[i].start, Color.white, flash * 0.72f);
          _lines[i].endColor = Color.Lerp(_lineColors[i].end, Color.white, flash * 0.72f);
        }

        var squeeze = 1f - 0.08f * flash + 0.035f * punch;
        transform.localScale = new Vector3(_baseScale.x * squeeze, _baseScale.y * (1f + 0.04f * flash), _baseScale.z);
        if (t >= 1f)
        {
          _active = false;
          Restore();
        }
      }

      void CacheRenderers()
      {
        _sprites.Clear();
        _spriteColors.Clear();
        GetComponentsInChildren(true, _sprites);
        foreach (var sprite in _sprites)
          _spriteColors.Add(sprite != null ? sprite.color : Color.white);

        _lines.Clear();
        _lineColors.Clear();
        GetComponentsInChildren(true, _lines);
        foreach (var line in _lines)
          _lineColors.Add(line != null ? (line.startColor, line.endColor) : (Color.white, Color.white));
      }

      void Restore()
      {
        for (var i = 0; i < _sprites.Count; i++)
          if (_sprites[i] != null)
            _sprites[i].color = _spriteColors[i];
        for (var i = 0; i < _lines.Count; i++)
          if (_lines[i] != null)
          {
            _lines[i].startColor = _lineColors[i].start;
            _lines[i].endColor = _lineColors[i].end;
          }
        if (_baseScale != Vector3.zero)
          transform.localScale = _baseScale;
      }
    }

    sealed class HitSparkFx
    {
      readonly GameObject _root;
      readonly LineRenderer _spray;
      readonly LineRenderer _arc;
      readonly LineRenderer _ring;
      readonly SpriteRenderer[] _bits;
      Vector2 _direction;
      Color _color;
      float _age;
      float _duration;
      float _scale;
      bool _elite;

      public bool Active => _root.activeSelf;

      HitSparkFx(GameObject root, LineRenderer spray, LineRenderer arc, LineRenderer ring, SpriteRenderer[] bits)
      {
        _root = root;
        _spray = spray;
        _arc = arc;
        _ring = ring;
        _bits = bits;
        _root.SetActive(false);
      }

      public static HitSparkFx Create(Transform parent, int index)
      {
        var root = new GameObject($"EnemyHitSpark_{index + 1}");
        root.transform.SetParent(parent, false);
        var spray = CreateLine(root.transform, "DirectionalSpray", 82, 7, false, 0.045f);
        var arc = CreateLine(root.transform, "ImpactArc", 83, 8, false, 0.035f);
        var ring = CreateLine(root.transform, "ImpactRing", 81, 25, true, 0.035f);
        BuildCircle(ring, 1f);

        var bits = new SpriteRenderer[5];
        for (var i = 0; i < bits.Length; i++)
        {
          var go = new GameObject($"Bit_{i + 1}");
          go.transform.SetParent(root.transform, false);
          var sprite = go.AddComponent<SpriteRenderer>();
          sprite.sprite = LaserVfxShared.SoftGlowSprite;
          sprite.material = LaserVfxShared.CreateBeamMaterialInstance();
          sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
          sprite.sortingOrder = 84;
          bits[i] = sprite;
        }

        return new HitSparkFx(root, spray, arc, ring, bits);
      }

      static LineRenderer CreateLine(Transform parent, string name, int order, int count, bool loop, float width)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = count;
        line.material = LaserVfxShared.CreateBeamMaterialInstance();
        line.sortingLayerName = LaserVfxShared.SortingLayerName;
        line.sortingOrder = order;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 3;
        return line;
      }

      static void BuildCircle(LineRenderer line, float radius)
      {
        for (var i = 0; i < line.positionCount; i++)
        {
          var angle = i / (float)(line.positionCount - 1) * Mathf.PI * 2f;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
      }

      public void Play(Vector3 position, Vector2 direction, Color color, float scale, bool elite)
      {
        _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        _color = color;
        _scale = scale * (elite ? 0.82f : 1f);
        _elite = elite;
        _duration = elite ? 0.14f : 0.18f;
        _age = 0f;
        _root.transform.position = position;
        _root.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(_direction.x, _direction.y, 0f));
        _root.SetActive(true);
        BuildSpray(0f);
        BuildArc(0f);
        for (var i = 0; i < _bits.Length; i++)
        {
          var angle = Mathf.Lerp(-42f, 42f, i / (float)(_bits.Length - 1)) * Mathf.Deg2Rad;
          var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
          _bits[i].transform.localPosition = dir * Random.Range(0.08f, 0.2f) * _scale;
          _bits[i].transform.localScale = Vector3.one * Random.Range(0.035f, 0.075f) * _scale;
        }
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;

        _age += deltaTime;
        var t = Mathf.Clamp01(_age / _duration);
        var alpha = 1f - t;
        var color = new Color(_color.r, _color.g, _color.b, alpha * (_elite ? 0.55f : 0.82f));
        _spray.startColor = _spray.endColor = color;
        _arc.startColor = _arc.endColor = Color.Lerp(color, Color.white, 0.35f);
        _ring.startColor = _ring.endColor = new Color(color.r, color.g, color.b, alpha * 0.34f);
        _spray.startWidth = _spray.endWidth = 0.05f * alpha * _scale;
        _arc.startWidth = _arc.endWidth = 0.035f * alpha * _scale;
        _ring.startWidth = _ring.endWidth = 0.028f * alpha * _scale;
        _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.42f * _scale, t);
        BuildSpray(t);
        BuildArc(t);

        for (var i = 0; i < _bits.Length; i++)
        {
          var bit = _bits[i];
          var drift = (0.18f + i * 0.035f) * t * _scale;
          bit.transform.localPosition += new Vector3(drift * deltaTime * 16f, Mathf.Sin((t + i) * Mathf.PI) * 0.008f, 0f);
          LaserVfxShared.SetSpriteColor(bit, new Color(color.r, color.g, color.b, alpha * 0.7f));
        }

        if (t >= 1f)
          _root.SetActive(false);
      }

      public void Deactivate() => _root.SetActive(false);

      void BuildSpray(float t)
      {
        for (var i = 0; i < _spray.positionCount; i++)
        {
          var f = i / (float)(_spray.positionCount - 1);
          var x = Mathf.Lerp(0f, 0.8f * _scale, f) * (0.65f + t * 0.35f);
          var y = Mathf.Sin(f * Mathf.PI * 2f) * 0.08f * _scale * (1f - t);
          _spray.SetPosition(i, new Vector3(x, y, 0f));
        }
      }

      void BuildArc(float t)
      {
        for (var i = 0; i < _arc.positionCount; i++)
        {
          var f = i / (float)(_arc.positionCount - 1);
          var angle = Mathf.Lerp(-54f, 54f, f) * Mathf.Deg2Rad;
          var radius = Mathf.Lerp(0.12f, 0.38f * _scale, t);
          _arc.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
      }
    }
  }
}
