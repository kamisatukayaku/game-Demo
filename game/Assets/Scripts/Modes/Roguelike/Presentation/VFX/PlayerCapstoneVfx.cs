using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.UI;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  /// <summary>A1: Capstone silhouette VFX when tier-5 evolution is reached.</summary>
  [DisallowMultipleComponent]
  public sealed class PlayerCapstoneVfx : MonoBehaviour
  {
    static PlayerCapstoneVfx s_instance;
    static readonly Dictionary<string, Color> BuildColors = new()
    {
      { ArenaBuildBootstrap.Mage, new Color(0.35f, 0.72f, 1f, 0.85f) },
      { ArenaBuildBootstrap.Shooter, new Color(1f, 0.55f, 0.12f, 0.85f) },
      { ArenaBuildBootstrap.Contact, new Color(0.95f, 0.98f, 1f, 0.9f) },
      { ArenaBuildBootstrap.Support, new Color(0.35f, 0.95f, 0.55f, 0.85f) }
    };

    EventListenerHandle _upgradeHandle;
    Transform _player;
    LineRenderer _outline;
    TrailRenderer _trail;
    float _pulseAge;
    bool _active;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_PlayerCapstoneVfx");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<PlayerCapstoneVfx>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      _upgradeHandle = GameEventBus.Subscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
    }

    void OnDestroy()
    {
      if (_upgradeHandle.Valid)
        GameEventBus.Unsubscribe(_upgradeHandle);
      if (s_instance == this)
        s_instance = null;
    }

    void OnUpgradeApplied(UpgradeAppliedEvent evt)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      var def = LevelUpChoiceDatabase.FindById(evt.UpgradeId);
      if (def == null || def.tier < 5)
        return;

      ActivateCapstone();
      RunTimelineRecorder.Record("Capstone", def.display_name);
      var accent = BuildColors.TryGetValue(ArenaBuildBootstrap.SelectedBuildId, out var c)
        ? c
        : BuildColors[ArenaBuildBootstrap.Mage];
      ArenaMomentUI.ShowBanner("CAPSTONE", accent);
      ArenaMomentUI.PlaySignatureFlash(new Color(accent.r, accent.g, accent.b, 0.9f));
    }

    void ActivateCapstone()
    {
      _active = true;
      EnsureVisuals();
    }

    void EnsureVisuals()
    {
      if (_player == null)
      {
        var go = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        _player = go != null ? go.transform : null;
      }

      if (_player == null)
        return;

      if (_outline == null)
      {
        var outlineGo = new GameObject("CapstoneOutline");
        outlineGo.transform.SetParent(_player, false);
        _outline = outlineGo.AddComponent<LineRenderer>();
        _outline.useWorldSpace = false;
        _outline.loop = true;
        _outline.material = new Material(Shader.Find("Sprites/Default"));
        _outline.startWidth = 0.12f;
        _outline.endWidth = 0.12f;
        _outline.sortingOrder = 200;
        DrawCircle(_outline, 0.72f, 24);
      }

      if (_trail == null)
      {
        _trail = _player.gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.35f;
        _trail.startWidth = 0.22f;
        _trail.endWidth = 0.02f;
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.sortingOrder = 199;
      }

      var color = BuildColors.TryGetValue(ArenaBuildBootstrap.SelectedBuildId, out var c)
        ? c
        : BuildColors[ArenaBuildBootstrap.Mage];
      _outline.startColor = _outline.endColor = color;
      _trail.startColor = color;
      _trail.endColor = CapstoneAlpha(color, 0.05f);
    }

    void Update()
    {
      if (!_active)
        return;

      EnsureVisuals();
      _pulseAge += Time.deltaTime * 2.5f;
      if (_outline != null)
      {
        var pulse = 0.65f + 0.35f * Mathf.Sin(_pulseAge);
        var c = _outline.startColor;
        c.a = pulse;
        _outline.startColor = _outline.endColor = c;
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
    static Color CapstoneAlpha(Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }
}
