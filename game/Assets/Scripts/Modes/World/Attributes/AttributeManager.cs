using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Stats;
using SharedModOp = Game.Shared.Stats.SharedAttributeManager.ModifierOp;
using SharedModEntry = Game.Shared.Stats.SharedAttributeManager.ModifierEntry;

namespace Game.World
{
  /// <summary>
  /// World 模式属性管理器 — 包装 SharedAttributeManager，注册 World 模式特有属性定义。
  ///
  /// 核心数值管理框架现已提取到 Shared/Stats/SharedAttributeManager，统一 World 和 Roguelike 模式。
  /// 本类仅负责：
  ///   1. 从 WorldDatabase 加载属性定义并注入 SharedAttributeManager
  ///   2. 实现 IWorldSystem 和 IBuildStatWriter 接口
  ///   3. 转发修饰符管理和值查询到 SharedAttributeManager
  /// </summary>
  public class AttributeManager : IWorldSystem, IBuildStatWriter
  {
    public bool DebugLog { get; set; }

    public event Action<IReadOnlyCollection<string>> OnAttributesChanged;

    readonly SharedAttributeManager _core = new();
    bool _initialized;

    const string MetaProgressionSourceId = "meta_progression";

    // ══════════════════════════════════════════════════════
    //  IWorldSystem
    // ══════════════════════════════════════════════════════

    public void Initialize()
    {
      if (_initialized) return;

      WorldDatabase.EnsureLoaded();
      var worldDefs = WorldDatabase.AttributeDefs;

      // 转换为 Shared 层通用属性定义
      var sharedDefs = new Dictionary<string, SharedAttributeDef>();
      if (worldDefs != null)
      {
        foreach (var kv in worldDefs)
        {
          var def = kv.Value;
          if (def == null || string.IsNullOrEmpty(def.attr_id)) continue;
          sharedDefs[def.attr_id] = new SharedAttributeDef
          {
            attr_id = def.attr_id,
            base_value = def.base_value,
            min = def.min,
            max = def.max
          };
        }
      }

      _core.DebugLog = DebugLog;
      _core.Initialize(sharedDefs);
      _core.OnAttributesChanged += attrs => OnAttributesChanged?.Invoke(attrs);

      BuildStatWriterLocator.Register(this);
      _initialized = true;

      if (DebugLog)
        Debug.Log($"[AttributeManager] Initialized with {sharedDefs.Count} World attributes.");
    }

    public void Tick(float deltaTime) { }
    public void OnPause() { }
    public void OnResume() { }

    public void Shutdown()
    {
      _core.Shutdown();
      BuildStatWriterLocator.Clear();
      _initialized = false;
    }

    // ══════════════════════════════════════════════════════
    //  IBuildStatWriter
    // ══════════════════════════════════════════════════════

    void IBuildStatWriter.AddStat(string key, float value)
    {
      if (!_initialized || string.IsNullOrEmpty(key)) return;
      _core.AddModifier(MetaProgressionSourceId, key, SharedModOp.Add, value);
    }

    // ══════════════════════════════════════════════════════
    //  修饰符管理 — 转发到 core
    // ══════════════════════════════════════════════════════

    public void ApplyModifiers(string sourceId, List<ModifierEntry> entries)
    {
      if (!_initialized || string.IsNullOrEmpty(sourceId)) return;

      var sharedEntries = new List<SharedModEntry>();
      if (entries != null)
      {
        foreach (var e in entries)
        {
          if (e == null) continue;
          sharedEntries.Add(new SharedModEntry(e.attrId, (SharedModOp)(int)e.op, e.value));
        }
      }
      _core.ApplyModifiers(sourceId, sharedEntries);
    }

    public void RemoveSource(string sourceId)
    {
      _core.RemoveSource(sourceId);
    }

    // ══════════════════════════════════════════════════════
    //  值查询 — 转发到 core
    // ══════════════════════════════════════════════════════

    public float GetValue(string attrId, float defaultValue = 0f)
      => _core.GetValue(attrId, defaultValue);

    public bool TryGetValue(string attrId, out float value)
      => _core.TryGetValue(attrId, out value);

    public float GetBaseValue(string attrId, float defaultValue = 0f)
      => _core.GetBaseValue(attrId, defaultValue);

    public WorldDatabase.AttributeDef GetDef(string attrId)
    {
      if (!_initialized || string.IsNullOrEmpty(attrId)) return null;
      WorldDatabase.AttributeDefs.TryGetValue(attrId, out var def);
      return def;
    }

    public bool HasAttribute(string attrId) => _core.HasAttribute(attrId);

    public IReadOnlyDictionary<string, float> GetAllValues() => _core.GetAllValues();

    public IReadOnlyDictionary<string, AttributeEntry> AllEntries
    {
      get
      {
        // 兼容旧 API：返回包装后的 AttributeEntry
        var result = new Dictionary<string, AttributeEntry>();
        var worldDefs = WorldDatabase.AttributeDefs;
        var allValues = _core.GetAllValues();
        foreach (var kv in allValues)
        {
          var attrId = kv.Key;
          worldDefs.TryGetValue(attrId, out var wDef);
          result[attrId] = new AttributeEntry(wDef) { _value = kv.Value };
        }
        return result;
      }
    }

    // ══════════════════════════════════════════════════════
    //  兼容旧 AttributeEntry 的简化包装
    // ══════════════════════════════════════════════════════

    public class AttributeEntry
    {
      public WorldDatabase.AttributeDef Def { get; }
      public float ComputedValue => _value;
      internal float _value;

      public AttributeEntry(WorldDatabase.AttributeDef def) { Def = def; }
    }

    public enum ModifierOp { Add, Mult, FinalAdd, FinalMult }

    [Serializable]
    public class ModifierEntry
    {
      public string attrId;
      public ModifierOp op;
      public float value;

      public ModifierEntry() { }
      public ModifierEntry(string attrId, ModifierOp op, float value)
      { this.attrId = attrId; this.op = op; this.value = value; }
    }
  }
}
