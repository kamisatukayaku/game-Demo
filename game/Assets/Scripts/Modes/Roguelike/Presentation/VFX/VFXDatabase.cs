using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [CreateAssetMenu(fileName = "RoguelikeVFXDatabase", menuName = "Game/Roguelike/VFX Database")]
  public sealed class VFXDatabase : ScriptableObject
  {
    [Serializable]
    public sealed class EffectEntry
    {
      public string effectId;
      public GameObject prefab;
      [Min(0)] public int prewarmCount;
      [Min(0.05f)] public float autoReleaseSeconds = 2f;
    }

    [Serializable]
    public sealed class EventMapping
    {
      public string eventId;
      public string effectId;
    }

    [SerializeField] List<EffectEntry> effects = new();
    [SerializeField] List<EventMapping> mappings = new();

    readonly Dictionary<string, EffectEntry> _effectsById = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _effectByEvent = new(StringComparer.OrdinalIgnoreCase);
    bool _indexed;

    public string ResolveEffectId(string eventId)
    {
      EnsureIndex();
      return !string.IsNullOrEmpty(eventId) && _effectByEvent.TryGetValue(eventId, out var effectId)
        ? effectId
        : eventId;
    }

    public bool TryGetEffect(string effectId, out EffectEntry entry)
    {
      EnsureIndex();
      return _effectsById.TryGetValue(effectId ?? string.Empty, out entry);
    }

    public static VFXDatabase CreateRuntimeDefaults()
    {
      var database = CreateInstance<VFXDatabase>();
      database.mappings.AddRange(new[]
      {
        new EventMapping { eventId = "Damage", effectId = "HitSpark" },
        new EventMapping { eventId = "EnemyDeath", effectId = "EnemyExplode" },
        new EventMapping { eventId = "Mage", effectId = "MagicCircle" },
        new EventMapping { eventId = "Explosion", effectId = "EnemyExplode" },
        new EventMapping { eventId = "XpPickup", effectId = "XpNumber" },
        new EventMapping { eventId = "PlayerDamaged", effectId = "PlayerDamageNumber" }
      });
      database.EnsureIndex();
      return database;
    }

    void OnEnable()
    {
      _indexed = false;
    }

    void EnsureIndex()
    {
      if (_indexed)
        return;

      _effectsById.Clear();
      _effectByEvent.Clear();

      foreach (var entry in effects)
      {
        if (entry != null && !string.IsNullOrEmpty(entry.effectId))
          _effectsById[entry.effectId] = entry;
      }

      foreach (var mapping in mappings)
      {
        if (mapping != null
            && !string.IsNullOrEmpty(mapping.eventId)
            && !string.IsNullOrEmpty(mapping.effectId))
          _effectByEvent[mapping.eventId] = mapping.effectId;
      }

      _indexed = true;
    }
  }
}
