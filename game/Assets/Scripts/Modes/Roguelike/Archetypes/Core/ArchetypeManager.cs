using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Modes.Roguelike.Archetypes.Warrior;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Archetypes.Core
{
  /// <summary>Activates and updates the current Roguelike archetype.</summary>
  [DisallowMultipleComponent]
  public class ArchetypeManager : MonoBehaviour
  {
    readonly List<IArchetype> _archetypes = new();
    ArchetypeContext _context;
    EventListenerHandle _levelUpHandle;
    EventListenerHandle _enemyKilledHandle;

    void Awake()
    {
      _context = new ArchetypeContext(gameObject);
      _archetypes.Add(new MageArchetype());
      _archetypes.Add(new RangedArchetype());
      _archetypes.Add(new WarriorArchetype());
    }

    void OnEnable()
    {
      RunBuildState.Changed += RefreshArchetypes;
      _levelUpHandle = GameEventBus.Subscribe<LevelUpEvent>(OnLevelUpEvent);
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilledEvent);
      RefreshArchetypes();
    }

    void OnDisable()
    {
      RunBuildState.Changed -= RefreshArchetypes;
      if (_levelUpHandle.Valid)
        GameEventBus.Unsubscribe(_levelUpHandle);
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);

      foreach (var archetype in _archetypes)
      {
        if (archetype.IsActive)
          archetype.Shutdown();
      }
    }

    void Update()
    {
      var deltaTime = Time.deltaTime;
      foreach (var archetype in _archetypes)
      {
        if (archetype.IsActive)
          archetype.Tick(deltaTime);
      }
    }

    void RefreshArchetypes()
    {
      var theme = RunBuildState.WeaponTheme;
      var unified = theme == UnifiedBuildBootstrap.WeaponTheme;
      foreach (var archetype in _archetypes)
      {
        var shouldActivate = ShouldActivate(archetype.Id)
          && (unified || MatchesPrimaryTheme(archetype.Id, theme));
        if (shouldActivate && !archetype.IsActive)
          archetype.Initialize(_context.Player);
        else if (!shouldActivate && archetype.IsActive)
          archetype.Shutdown();
      }
    }

    static bool MatchesPrimaryTheme(string archetypeId, string weaponTheme) => weaponTheme switch
    {
      UnifiedBuildBootstrap.WeaponTheme => false,
      "ranged" => archetypeId == "ranged",
      "mage" => archetypeId == "mage",
      "warrior" => archetypeId == "warrior",
      _ => false
    };

    static bool ShouldActivate(string id) => id switch
    {
      "mage" => MageArchetype.ShouldActivate(),
      "ranged" => RangedArchetype.ShouldActivate(),
      "warrior" => WarriorArchetype.ShouldActivate(),
      _ => false
    };

    void OnLevelUpEvent(LevelUpEvent evt)
    {
      foreach (var archetype in _archetypes)
      {
        if (archetype.IsActive)
          archetype.OnLevelUp();
      }
    }

    void OnEnemyKilledEvent(EnemyKilledEvent evt)
    {
      if (!Game.Shared.Gameplay.PlayerCombatAttribution.IsPlayerOrOwned(evt.Killer))
        return;

      foreach (var archetype in _archetypes)
      {
        if (archetype.IsActive)
          archetype.OnEnemyKilled();
      }
    }
  }
}
