using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  [DisallowMultipleComponent]
  public sealed class DetachedWeaponController : MonoBehaviour
  {
    readonly List<IDetachedWeaponBehavior> _behaviors = new();
    DetachedWeaponRuntimeContext _context;
    bool _configured;

    public string WeaponId => _context.Definition?.id;

    public bool Configure(GameObject owner, string weaponId)
    {
      ShutdownBehaviors();
      var definition = DetachedWeaponDatabase.Get(weaponId);
      if (owner == null || definition == null)
        return false;

      _context = new DetachedWeaponRuntimeContext(owner, transform, definition);
      if (definition.attack_modes != null)
      {
        foreach (var modeId in definition.attack_modes)
        {
          if (!DetachedWeaponDatabase.TryParseMode(modeId, out var mode) ||
              !DetachedWeaponBehaviorRegistry.TryCreate(mode, out var behavior))
            continue;
          behavior.Initialize(_context);
          _behaviors.Add(behavior);
        }
      }

      _configured = true;
      return true;
    }

    void Update()
    {
      if (!_configured)
        return;

      var state = GetComponent<DetachedWeaponVisualState>();
      if (state != null && state.IntroActive)
        return;

      var deltaTime = Time.deltaTime;
      foreach (var behavior in _behaviors)
        behavior.Tick(deltaTime);
    }

    void OnDisable() => ShutdownBehaviors();

    void ShutdownBehaviors()
    {
      foreach (var behavior in _behaviors)
        behavior.Shutdown();
      _behaviors.Clear();
      _configured = false;
    }
  }
}
