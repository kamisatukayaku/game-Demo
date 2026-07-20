using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Events;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [DisallowMultipleComponent]
  public sealed class RoguelikeEnemyDeathFeedbackSystem : MonoBehaviour
  {
    static RoguelikeEnemyDeathFeedbackSystem s_instance;

    [SerializeField] DeathEffectProfile defaultProfile;

    readonly Queue<DeathFeedbackFx> _pool = new();
    Material _lineMaterial;
    DeathEffectProfile _runtimeDefault;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_RoguelikeEnemyDeathFeedback");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<RoguelikeEnemyDeathFeedbackSystem>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
      s_instance = this;
      _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "RoguelikeDeathFeedbackLine_Runtime" };
      _runtimeDefault = ScriptableObject.CreateInstance<DeathEffectProfile>();
      CombatEventBus.OnKill += OnKill;
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
      CombatEventBus.OnKill -= OnKill;
    }

    void OnKill(CombatEventBus.KillArgs args)
    {
      if (args.IsPlayer || args.Victim == null)
        return;

      var profile = defaultProfile != null ? defaultProfile : _runtimeDefault;
      var fx = GetFx(profile);
      var pos = args.Victim.transform.position;
      var color = ResolveEnemyColor(args.Victim);
      var source = ResolveSource(args.Killer);
      var direction = ResolveDirection(args.Killer, pos);
      var elite = IsElite(args.Victim, args.VictimId);
      fx.Play(pos, color, source, direction, elite, profile, Release);
    }

    DeathFeedbackFx GetFx(DeathEffectProfile profile)
    {
      while (_pool.Count > 0)
      {
        var fx = _pool.Dequeue();
        if (fx != null)
          return fx;
      }

      var go = new GameObject("RoguelikeEnemyDeathFeedbackFx");
      go.transform.SetParent(transform, false);
      var created = go.AddComponent<DeathFeedbackFx>();
      created.Initialize(_lineMaterial, Mathf.Max(12, profile.fragmentCount));
      return created;
    }

    void Release(DeathFeedbackFx fx)
    {
      if (fx == null) return;
      fx.gameObject.SetActive(false);
      fx.transform.SetParent(transform, false);
      _pool.Enqueue(fx);
    }

    static Color ResolveEnemyColor(GameObject enemy)
    {
      var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
      if (renderer != null)
        return renderer.color;

      var mesh = enemy.GetComponentInChildren<MeshRenderer>();
      if (mesh != null && mesh.sharedMaterial != null)
        return mesh.sharedMaterial.color;

      return new Color(0.95f, 0.34f, 0.26f, 1f);
    }

    static DeathSource ResolveSource(GameObject killer)
    {
      if (killer == null) return DeathSource.Generic;
      var name = killer.name.ToLowerInvariant();
      if (name.Contains("warrior") || name.Contains("orbit") || name.Contains("blade"))
        return DeathSource.Warrior;
      if (name.Contains("ranged") || name.Contains("projectile") || name.Contains("bullet") || name.Contains("shooter"))
        return DeathSource.Ranged;
      if (name.Contains("mage") || name.Contains("arcane") || name.Contains("flame") || name.Contains("gravity") || name.Contains("tidal"))
        return DeathSource.Mage;
      return DeathSource.Generic;
    }

    static Vector2 ResolveDirection(GameObject killer, Vector3 victimPos)
    {
      if (killer == null)
        return Random.insideUnitCircle.normalized;

      var delta = (Vector2)(victimPos - killer.transform.position);
      return delta.sqrMagnitude > 0.0001f ? delta.normalized : Random.insideUnitCircle.normalized;
    }

    static bool IsElite(GameObject victim, string victimId)
    {
      if (!string.IsNullOrEmpty(victimId) && victimId.ToLowerInvariant().Contains("elite"))
        return true;
      return victim.name.ToLowerInvariant().Contains("elite");
    }

    enum DeathSource { Generic, Mage, Warrior, Ranged }

    sealed class DeathFeedbackFx : MonoBehaviour
    {
      const int CircleSegments = 36;
      readonly List<Fragment> _fragments = new();
      readonly List<LineRenderer> _cracks = new();

      LineRenderer _flashRing;
      LineRenderer _shockRing;
      ParticleSystem _particles;
      DeathEffectProfile _profile;
      System.Action<DeathFeedbackFx> _release;
      Color _baseColor;
      Color _accentColor;
      Vector2 _impactDirection;
      float _age;
      bool _elite;
      DeathSource _source;

      public void Initialize(Material material, int maxFragments)
      {
        _flashRing = CreateLine("DeathFlashRing", material, 0.11f, 90);
        _flashRing.loop = true;
        DrawCircle(_flashRing, 0.45f);

        _shockRing = CreateLine("DeathShockRing", material, 0.08f, 91);
        _shockRing.loop = true;
        DrawCircle(_shockRing, 0.3f);

        for (var i = 0; i < 4; i++)
        {
          var crack = CreateLine($"EnergyCrack_{i + 1}", material, 0.04f, 92);
          crack.loop = false;
          crack.positionCount = 2;
          _cracks.Add(crack);
        }

        for (var i = 0; i < maxFragments; i++)
        {
          var line = CreateLine($"DeathFragment_{i + 1}", material, 0.035f, 93);
          line.loop = true;
          DrawFragment(line, i);
          _fragments.Add(new Fragment { Line = line });
        }

        _particles = CreateParticles();
        gameObject.SetActive(false);
      }

      public void Play(
        Vector3 position,
        Color baseColor,
        DeathSource source,
        Vector2 direction,
        bool elite,
        DeathEffectProfile profile,
        System.Action<DeathFeedbackFx> release)
      {
        transform.position = position;
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);

        _age = 0f;
        _baseColor = baseColor;
        _source = source;
        _impactDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _elite = elite;
        _profile = profile;
        _release = release;
        _accentColor = source switch
        {
          DeathSource.Mage => new Color(0.35f, 0.82f, 1f, 1f),
          DeathSource.Warrior => new Color(1f, 0.68f, 0.22f, 1f),
          DeathSource.Ranged => new Color(1f, 0.95f, 0.58f, 1f),
          _ => new Color(0.55f, 0.95f, 1f, 1f)
        };

        SetupFragments(Mathf.Clamp(profile.fragmentCount + (elite ? 5 : 0), 4, _fragments.Count));
        SetupCracks();
        EmitParticles(profile.particleCount + (elite ? 10 : 0));
        UpdateVisuals(0f);
      }

      void Update()
      {
        if (_profile == null) return;
        _age += Time.deltaTime;
        UpdateVisuals(_age);
        if (_age >= TotalDuration)
          _release?.Invoke(this);
      }

      float TotalDuration => Mathf.Max(0.18f, _profile.flashDuration + _profile.crackDuration + _profile.dissolveTime + (_elite ? 0.18f : 0f));

      void SetupFragments(int activeCount)
      {
        for (var i = 0; i < _fragments.Count; i++)
        {
          var fragment = _fragments[i];
          var active = i < activeCount;
          fragment.Line.enabled = active;
          if (!active)
          {
            _fragments[i] = fragment;
            continue;
          }

          var random = Random.insideUnitCircle.normalized;
          var dir = _source switch
          {
            DeathSource.Warrior => (_impactDirection * 0.72f + random * 0.28f).normalized,
            DeathSource.Ranged => (_impactDirection * 0.62f + random * 0.38f).normalized,
            _ => random
          };
          fragment.LocalPosition = Vector2.zero;
          fragment.Velocity = dir * Random.Range(_profile.fragmentSpeed * 0.55f, _profile.fragmentSpeed * (_elite ? 1.55f : 1.15f));
          fragment.Rotation = Random.Range(0f, 360f);
          fragment.RotationSpeed = Random.Range(-540f, 540f);
          fragment.Scale = Random.Range(0.13f, _elite ? 0.32f : 0.25f);
          _fragments[i] = fragment;
        }
      }

      void SetupCracks()
      {
        for (var i = 0; i < _cracks.Count; i++)
        {
          var angle = i * Mathf.PI * 2f / _cracks.Count + Random.Range(-0.25f, 0.25f);
          var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
          _cracks[i].SetPosition(0, Vector3.zero);
          _cracks[i].SetPosition(1, dir * Random.Range(0.38f, _elite ? 0.92f : 0.68f));
          _cracks[i].enabled = true;
        }
      }

      void UpdateVisuals(float age)
      {
        var flashT = Mathf.Clamp01(age / Mathf.Max(0.01f, _profile.flashDuration));
        var crackT = Mathf.Clamp01((age - _profile.flashDuration * 0.35f) / Mathf.Max(0.01f, _profile.crackDuration));
        var dissolveT = Mathf.Clamp01((age - _profile.flashDuration - _profile.crackDuration) / Mathf.Max(0.01f, _profile.dissolveTime + (_elite ? 0.18f : 0f)));

        _flashRing.enabled = flashT < 1f;
        if (_flashRing.enabled)
        {
          DrawCircle(_flashRing, Mathf.Lerp(0.22f, _elite ? 0.95f : 0.68f, flashT));
          var a = (1f - flashT) * _profile.flashIntensity;
          _flashRing.startColor = DeathAlpha(Color.white, a);
          _flashRing.endColor = DeathAlpha(_accentColor, a * 0.75f);
          _flashRing.startWidth = Mathf.Lerp(0.16f, 0.035f, flashT);
          _flashRing.endWidth = _flashRing.startWidth;
        }

        _shockRing.enabled = _elite || _source == DeathSource.Warrior;
        if (_shockRing.enabled)
        {
          var t = Mathf.Clamp01(age / (_elite ? 0.48f : 0.22f));
          DrawCircle(_shockRing, Mathf.Lerp(0.35f, _elite ? 1.45f : 0.9f, 1f - Mathf.Pow(1f - t, 2f)));
          var a = Mathf.Sin(t * Mathf.PI) * (_elite ? 0.52f : 0.35f);
          _shockRing.startColor = DeathAlpha(_accentColor, a);
          _shockRing.endColor = DeathAlpha(Color.white, a * 0.5f);
        }

        foreach (var crack in _cracks)
        {
          crack.enabled = crackT < 1f;
          if (crack.enabled)
          {
            var a = Mathf.Sin(crackT * Mathf.PI) * 0.85f;
            crack.startColor = DeathAlpha(_accentColor, a);
            crack.endColor = DeathAlpha(Color.white, a * 0.35f);
          }
        }

        for (var i = 0; i < _fragments.Count; i++)
        {
          var fragment = _fragments[i];
          if (!fragment.Line.enabled) continue;
          fragment.LocalPosition += fragment.Velocity * Time.deltaTime;
          fragment.Velocity *= 1f - Time.deltaTime * 1.8f;
          fragment.Rotation += fragment.RotationSpeed * Time.deltaTime;

          var alpha = 1f - dissolveT;
          var color = DeathAlpha(Color.Lerp(_baseColor, _accentColor, 0.28f), alpha * 0.9f);
          fragment.Line.startColor = color;
          fragment.Line.endColor = DeathAlpha(_accentColor, alpha * 0.45f);
          DrawFragment(fragment.Line, i);
          ApplyFragmentTransform(fragment.Line, fragment.LocalPosition, fragment.Rotation, fragment.Scale * Mathf.Lerp(1f, 0.2f, dissolveT));
          _fragments[i] = fragment;
        }
      }

      void EmitParticles(int count)
      {
        if (_particles == null) return;
        _particles.Clear(true);
        for (var i = 0; i < count; i++)
        {
          var dir = Random.insideUnitCircle.normalized;
          if (_source is DeathSource.Warrior or DeathSource.Ranged)
            dir = (_impactDirection * 0.55f + dir * 0.45f).normalized;

          var emit = new ParticleSystem.EmitParams
          {
            position = Vector3.zero,
            velocity = new Vector3(dir.x, dir.y, 0f) * Random.Range(1.2f, _elite ? 5.8f : 3.8f),
            startLifetime = Random.Range(0.18f, _elite ? 0.55f : 0.38f),
            startSize = Random.Range(0.025f, _elite ? 0.08f : 0.06f),
            startColor = DeathAlpha(Color.Lerp(_accentColor, Color.white, Random.Range(0.1f, 0.55f)), Random.Range(0.45f, 0.9f)),
            rotation = Random.Range(0f, 360f)
          };
          _particles.Emit(emit, 1);
        }
      }

      LineRenderer CreateLine(string name, Material material, float width, int sort)
      {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = material;
        line.startWidth = width;
        line.endWidth = width;
        line.sortingOrder = sort;
        line.numCapVertices = 2;
        return line;
      }

      ParticleSystem CreateParticles()
      {
        var go = new GameObject("DeathEnergyDissolveParticles");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f);
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.enabled = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
          new Keyframe(0f, 1f),
          new Keyframe(1f, 0.05f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
          new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.45f, 0.9f, 1f), 1f) },
          new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default")) { name = "DeathFeedbackParticles_Runtime" };
        renderer.sortingOrder = 94;
        return ps;
      }

      static void DrawCircle(LineRenderer line, float radius)
      {
        line.positionCount = CircleSegments;
        for (var i = 0; i < CircleSegments; i++)
        {
          var angle = i * Mathf.PI * 2f / CircleSegments;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
      }

      static void DrawFragment(LineRenderer line, int index)
      {
        if (index % 3 == 0)
        {
          line.positionCount = 3;
          line.SetPosition(0, new Vector3(0f, 0.55f, 0f));
          line.SetPosition(1, new Vector3(0.48f, -0.28f, 0f));
          line.SetPosition(2, new Vector3(-0.48f, -0.28f, 0f));
          return;
        }

        line.positionCount = 4;
        line.SetPosition(0, new Vector3(0f, 0.55f, 0f));
        line.SetPosition(1, new Vector3(0.55f, 0f, 0f));
        line.SetPosition(2, new Vector3(0f, -0.55f, 0f));
        line.SetPosition(3, new Vector3(-0.55f, 0f, 0f));
      }

      static void ApplyFragmentTransform(LineRenderer line, Vector2 pos, float rotation, float scale)
      {
        var rad = rotation * Mathf.Deg2Rad;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        for (var i = 0; i < line.positionCount; i++)
        {
          var basePoint = line.GetPosition(i);
          var x = basePoint.x * scale;
          var y = basePoint.y * scale;
          line.SetPosition(i, new Vector3(pos.x + x * cos - y * sin, pos.y + x * sin + y * cos, 0f));
        }
      }

      struct Fragment
      {
        public LineRenderer Line;
        public Vector2 LocalPosition;
        public Vector2 Velocity;
        public float Rotation;
        public float RotationSpeed;
        public float Scale;
      }
    }

    static Color DeathAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }
}
