using Game.Modes.Roguelike.Gameplay.DetachedWeapons;
using Game.Shared.Laser;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  public sealed class DetachedWeaponEvolutionVfx : MonoBehaviour
  {
    const int PoolSize = 8;
    static DetachedWeaponEvolutionVfx s_instance;
    EvolutionFx[] _pool;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_DetachedWeaponEvolutionVfx");
      if (!Application.isPlaying)
        go.hideFlags = HideFlags.HideAndDontSave;
      go.AddComponent<DetachedWeaponEvolutionVfx>();
    }

    public static void Play(GameObject player, string upgradeId)
    {
      if (!TryParseRoute(upgradeId, out var route))
        return;

      EnsureExists();
      var target = FindTargetWeapon(player, route);
      var position = target != null ? target.position : player != null ? player.transform.position : Vector3.zero;
      foreach (var fx in s_instance._pool)
      {
        if (fx.Active)
          continue;
        fx.Play(target, position, route);
        return;
      }
    }

    public static void ResetAll()
    {
      EnsureExists();
      if (s_instance?._pool == null)
        return;
      foreach (var fx in s_instance._pool)
        fx.ForceStop();
    }

    public static string GetDebugSummary()
    {
      if (s_instance?._pool == null)
        return "Evolution inactive";
      var active = 0;
      foreach (var fx in s_instance._pool)
        if (fx.Active)
          active++;
      return $"Evolution {active}/{s_instance._pool.Length}";
    }

    static bool TryParseRoute(string upgradeId, out string route)
    {
      route = null;
      if (string.IsNullOrEmpty(upgradeId) || !upgradeId.StartsWith("evo_"))
        return false;

      var rest = upgradeId.Substring(4);
      var separator = rest.IndexOf('_');
      if (separator <= 0)
        return false;

      route = rest.Substring(0, separator);
      switch (route)
      {
        case "laser":
        case "missile":
        case "explosion":
        case "pulse":
        case "trail":
        case "boomerang":
          return true;
        default:
          route = null;
          return false;
      }
    }

    static Transform FindTargetWeapon(GameObject player, string route)
    {
      var desired = $"{route}_weapon";
      var weapons = FindObjectsOfType<DetachedWeaponController>();
      Transform fallback = null;
      var bestDistance = float.MaxValue;
      var origin = player != null ? player.transform.position : Vector3.zero;
      foreach (var weapon in weapons)
      {
        if (weapon == null)
          continue;
        if (weapon.WeaponId == desired)
          return weapon.transform;
        var distance = (weapon.transform.position - origin).sqrMagnitude;
        if (distance >= bestDistance)
          continue;
        bestDistance = distance;
        fallback = weapon.transform;
      }
      return fallback;
    }

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
      _pool = new EvolutionFx[PoolSize];
      for (var i = 0; i < _pool.Length; i++)
        _pool[i] = EvolutionFx.Create(transform, i);
    }

    void Update()
    {
      var dt = Time.unscaledDeltaTime;
      foreach (var fx in _pool)
        fx.Tick(dt);
    }

    sealed class EvolutionFx
    {
      readonly GameObject _root;
      readonly SpriteRenderer _core;
      readonly SpriteRenderer _halo;
      readonly SpriteRenderer[] _fragments;
      readonly LineRenderer _outerRing;
      readonly LineRenderer _signature;
      readonly LineRenderer _secondary;
      readonly LineRenderer _confirmRing;
      Transform _follow;
      RoutePalette _palette;
      string _route;
      float _age;
      const float Duration = 1.15f;

      public bool Active => _root.activeSelf;

      EvolutionFx(
        GameObject root,
        SpriteRenderer core,
        SpriteRenderer halo,
        SpriteRenderer[] fragments,
        LineRenderer outerRing,
        LineRenderer signature,
        LineRenderer secondary,
        LineRenderer confirmRing)
      {
        _root = root;
        _core = core;
        _halo = halo;
        _fragments = fragments;
        _outerRing = outerRing;
        _signature = signature;
        _secondary = secondary;
        _confirmRing = confirmRing;
        _root.SetActive(false);
      }

      public static EvolutionFx Create(Transform parent, int index)
      {
        var root = new GameObject($"DetachedEvolutionFx_{index + 1}");
        root.transform.SetParent(parent, false);

        var halo = CreateSprite(root.transform, "Halo", 68, 1.9f);
        var core = CreateSprite(root.transform, "Core", 71, 0.5f);
        var fragments = new SpriteRenderer[8];
        for (var i = 0; i < fragments.Length; i++)
          fragments[i] = CreateSprite(root.transform, $"Fragment_{i + 1}", 72, 0.14f);

        var outerRing = CreateLine(root.transform, "OuterRing", 69, 33, true, 0.052f);
        BuildCircle(outerRing, 0.78f);
        var signature = CreateLine(root.transform, "RouteSignature", 73, 12, false, 0.075f);
        var secondary = CreateLine(root.transform, "RouteSignatureSecondary", 72, 18, false, 0.045f);
        var confirmRing = CreateLine(root.transform, "ConfirmRing", 67, 41, true, 0.07f);
        BuildCircle(confirmRing, 1f);

        return new EvolutionFx(root, core, halo, fragments, outerRing, signature, secondary, confirmRing);
      }

      static SpriteRenderer CreateSprite(Transform parent, string name, int order, float scale)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = LaserVfxShared.SoftGlowSprite;
        sprite.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
        sprite.sortingLayerName = LaserVfxShared.SortingLayerName;
        sprite.sortingOrder = order;
        return sprite;
      }

      static LineRenderer CreateLine(Transform parent, string name, int order, int count, bool loop, float width)
      {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = count;
        line.material = LaserVfxShared.CreateFlatBeamMaterialInstance();
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

      public void Play(Transform follow, Vector3 position, string route)
      {
        _follow = follow;
        _route = route;
        _palette = RoutePalette.For(route);
        _age = 0f;
        _root.transform.position = position;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;
        BuildSignature(route);
        _root.SetActive(true);
      }

      void BuildSignature(string route)
      {
        _signature.loop = false;
        _secondary.loop = false;
        switch (route)
        {
          case "laser":
            SetLine(_signature, new[]
            {
              new Vector3(-1.05f, 0f), new Vector3(-0.42f, 0f),
              new Vector3(0.42f, 0f), new Vector3(1.05f, 0f)
            });
            SetLine(_secondary, new[]
            {
              new Vector3(-0.55f, 0.16f), new Vector3(0.55f, 0.16f),
              new Vector3(-0.55f, -0.16f), new Vector3(0.55f, -0.16f)
            });
            break;
          case "missile":
            SetLine(_signature, new[]
            {
              new Vector3(-0.75f, -0.35f), new Vector3(0.82f, 0f),
              new Vector3(-0.75f, 0.35f), new Vector3(-0.45f, 0f),
              new Vector3(-0.75f, -0.35f)
            });
            SetLine(_secondary, new[]
            {
              new Vector3(-0.95f, 0f), new Vector3(-0.42f, 0f),
              new Vector3(-0.72f, 0.18f), new Vector3(-0.42f, 0f),
              new Vector3(-0.72f, -0.18f)
            });
            break;
          case "explosion":
            _signature.loop = true;
            BuildCircle(_signature, 0.62f);
            _secondary.loop = true;
            BuildStar(_secondary, 8, 0.82f, 0.35f);
            break;
          case "pulse":
            _signature.loop = true;
            BuildCircle(_signature, 0.54f);
            _secondary.loop = true;
            BuildCircle(_secondary, 0.92f);
            break;
          case "trail":
            SetLine(_signature, new[]
            {
              new Vector3(-0.92f, -0.35f), new Vector3(-0.28f, 0.18f),
              new Vector3(0.2f, -0.06f), new Vector3(0.92f, 0.32f)
            });
            SetLine(_secondary, new[]
            {
              new Vector3(-0.28f, 0.18f), new Vector3(-0.05f, 0.62f),
              new Vector3(0.2f, -0.06f), new Vector3(0.45f, -0.55f),
              new Vector3(0.2f, -0.06f), new Vector3(0.62f, 0.1f)
            });
            break;
          case "boomerang":
            _signature.loop = true;
            BuildStar(_signature, 6, 0.82f, 0.48f);
            SetLine(_secondary, new[]
            {
              new Vector3(-0.82f, -0.08f), new Vector3(-0.35f, 0.4f),
              new Vector3(0.35f, 0.4f), new Vector3(0.82f, -0.08f)
            });
            break;
        }
      }

      static void SetLine(LineRenderer line, Vector3[] points)
      {
        line.positionCount = points.Length;
        for (var i = 0; i < points.Length; i++)
          line.SetPosition(i, points[i]);
      }

      static void BuildStar(LineRenderer line, int points, float outer, float inner)
      {
        line.positionCount = points * 2 + 1;
        for (var i = 0; i < line.positionCount; i++)
        {
          var radius = i % 2 == 0 ? outer : inner;
          var angle = i / (points * 2f) * Mathf.PI * 2f;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
      }

      public void Tick(float deltaTime)
      {
        if (!Active)
          return;

        _age += deltaTime;
        var t = Mathf.Clamp01(_age / Duration);
        if (_follow != null)
          _root.transform.position = _follow.position;

        var collapse = Mathf.Clamp01(t / 0.32f);
        var form = Mathf.Clamp01((t - 0.22f) / 0.38f);
        var burst = Mathf.Clamp01((t - 0.58f) / 0.16f);
        var reveal = Mathf.Clamp01((t - 0.68f) / 0.35f);
        var fade = Mathf.Clamp01((t - 0.88f) / 0.24f);
        var routeColor = Color.Lerp(RoutePalette.Neutral, _palette.Core, Mathf.SmoothStep(0f, 1f, form));
        var alpha = 1f - fade;
        var flicker = 0.8f + Mathf.Sin(Time.unscaledTime * 70f) * 0.2f * (1f - collapse);
        var burstStrength = Mathf.Sin(burst * Mathf.PI);

        _root.transform.rotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * Mathf.Lerp(80f, 280f, form));
        _core.transform.localScale = Vector3.one * Mathf.Lerp(0.58f, 0.22f, collapse) * (1f + burstStrength * 3.1f);
        _halo.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 0.55f, collapse) * (1f + burstStrength * 1.8f);
        LaserVfxShared.SetSpriteColor(_core, WithAlpha(Color.Lerp(RoutePalette.Neutral, Color.white, burstStrength) * flicker, Mathf.Lerp(0.65f, 0.18f, fade)));
        LaserVfxShared.SetSpriteColor(_halo, WithAlpha(routeColor, Mathf.Lerp(0.18f, 0.02f, fade) + burstStrength * 0.24f));

        var ringAlpha = (1f - Mathf.Abs(collapse - 0.55f)) * alpha;
        LaserVfxShared.SetLineColor(_outerRing, WithAlpha(Color.Lerp(RoutePalette.Neutral, _palette.Edge, form), ringAlpha * 0.82f), WithAlpha(Color.Lerp(RoutePalette.Neutral, _palette.Edge, form), ringAlpha * 0.82f));
        _outerRing.startWidth = _outerRing.endWidth = Mathf.Lerp(0.07f, 0.02f, collapse) + burstStrength * 0.045f;
        _outerRing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.36f, collapse);

        var signatureAlpha = Mathf.SmoothStep(0f, 1f, form) * (1f - fade);
        LaserVfxShared.SetLineColor(_signature, WithAlpha(_palette.Core, signatureAlpha), WithAlpha(_palette.Core, signatureAlpha));
        LaserVfxShared.SetLineColor(_secondary, WithAlpha(_palette.Edge, signatureAlpha * 0.64f), WithAlpha(_palette.Edge, signatureAlpha * 0.64f));
        _signature.startWidth = _signature.endWidth = Mathf.Lerp(0.035f, 0.09f, reveal) * alpha;
        _secondary.startWidth = _secondary.endWidth = Mathf.Lerp(0.018f, 0.05f, reveal) * alpha;
        _signature.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.1f, reveal);
        _secondary.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.05f, reveal);

        LaserVfxShared.SetLineColor(_confirmRing, WithAlpha(_palette.Core, Mathf.Sin(reveal * Mathf.PI) * 0.48f * alpha), WithAlpha(_palette.Core, Mathf.Sin(reveal * Mathf.PI) * 0.48f * alpha));
        _confirmRing.startWidth = _confirmRing.endWidth = 0.08f * (1f - reveal) * alpha;
        _confirmRing.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.6f, reveal);

        TickFragments(form, burstStrength, alpha);

        if (t >= 1f)
        {
          _root.SetActive(false);
          _follow = null;
        }
      }

      public void ForceStop()
      {
        _root.SetActive(false);
        _follow = null;
      }

      void TickFragments(float form, float burstStrength, float alpha)
      {
        for (var i = 0; i < _fragments.Length; i++)
        {
          var angle = Time.unscaledTime * Mathf.Lerp(360f, 780f, form) + i * (360f / _fragments.Length);
          var radius = Mathf.Lerp(0.18f, 0.92f, Mathf.Sin(form * Mathf.PI)) + burstStrength * 0.55f;
          var local = Quaternion.Euler(0f, 0f, angle) * Vector3.right * radius;
          var fragment = _fragments[i];
          fragment.transform.localPosition = local;
          fragment.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.18f, form) * (1f + burstStrength);
          LaserVfxShared.SetSpriteColor(fragment, WithAlpha(_palette.Core, Mathf.Sin(form * Mathf.PI) * 0.68f * alpha));
        }
      }

      static Color WithAlpha(Color color, float alpha)
      {
        color.a = Mathf.Clamp01(alpha);
        return color;
      }
    }

    readonly struct RoutePalette
    {
      public static readonly Color Neutral = new(0.78f, 0.96f, 1f, 1f);
      public readonly Color Core;
      public readonly Color Edge;

      RoutePalette(Color core, Color edge)
      {
        Core = core;
        Edge = edge;
      }

      public static RoutePalette For(string route)
      {
        return route switch
        {
          // Mage: arcane beam — white core, blue-purple body, deep purple edge
          "laser" => new RoutePalette(new Color(0.62f, 0.48f, 1f, 1f), new Color(0.28f, 0.08f, 0.72f, 0.85f)),
          // Mage: resonance pulse — white-blue core, electric blue, purple edge
          "pulse" => new RoutePalette(new Color(0.55f, 0.82f, 1f, 1f), new Color(0.42f, 0.12f, 0.88f, 0.88f)),
          // Shooter: missile platform — white core, cyan-blue body, deep blue edge
          "missile" => new RoutePalette(new Color(0.42f, 0.88f, 1f, 1f), new Color(0.06f, 0.28f, 0.82f, 0.92f)),
          // Shooter: explosion — platinum core, orange-red body, deep red edge
          "explosion" => new RoutePalette(new Color(1f, 0.58f, 0.22f, 1f), new Color(0.72f, 0.08f, 0.06f, 0.88f)),
          // Contact: boomerang — white core, golden body, orange-red edge
          "boomerang" => new RoutePalette(new Color(1f, 0.82f, 0.28f, 1f), new Color(1f, 0.42f, 0.12f, 0.9f)),
          // Contact: trail — white-green core, cyan-green body, deep green edge
          "trail" => new RoutePalette(new Color(0.48f, 1f, 0.72f, 1f), new Color(0.06f, 0.62f, 0.28f, 0.85f)),
          _ => new RoutePalette(Neutral, Neutral)
        };
      }
    }
  }
}
