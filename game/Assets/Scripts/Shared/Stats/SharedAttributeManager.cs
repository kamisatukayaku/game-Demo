using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Stats
{
  [Serializable]
  public class SharedAttributeDef
  {
    public string attr_id;
    public float base_value;
    public float min;
    public float max;
  }

  /// <summary>
  /// 通用属性管理器 — Shared 层核心框架，统一 World 和 Roguelike 模式的数值处理。
  ///
  /// 公式：clamp( (base + Σadd) × (1 + Σmult) × (1 + ΣfinalMult) + ΣfinalAdd , min, max )
  ///
  /// 设计原则：
  ///   1. 属性定义由调用方注入（World/Roguelike 各自注册具体属性名）
  ///   2. 来源追踪：每个修饰符标记 sourceId，移除来源时精确回退
  ///   3. 脏标记缓存：仅在修饰符变化时重算
  /// </summary>
  public class SharedAttributeManager
  {
    // ══════════════════════════════════════════════════════
    //  公开配置
    // ══════════════════════════════════════════════════════

    public bool DebugLog { get; set; }

    /// <summary>任意属性值发生变化时触发（参数为变化的 attrId 集合，null 表示批量更新）</summary>
    public event Action<IReadOnlyCollection<string>> OnAttributesChanged;

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    readonly Dictionary<string, SharedAttributeEntry> _entries = new();
    readonly Dictionary<string, List<ModifierEntry>> _sourceModifiers = new();
    bool _initialized;

    // ══════════════════════════════════════════════════════
    //  初始化 — 由调用方注入属性定义
    // ══════════════════════════════════════════════════════

    /// <summary>注入属性定义并初始化。</summary>
    public void Initialize(IReadOnlyDictionary<string, SharedAttributeDef> defs)
    {
      if (_initialized) return;

      _entries.Clear();
      _sourceModifiers.Clear();

      if (defs != null)
      {
        foreach (var kv in defs)
        {
          var def = kv.Value;
          if (def != null && !string.IsNullOrEmpty(def.attr_id))
            _entries[def.attr_id] = new SharedAttributeEntry(def);
        }
      }

      _initialized = true;

      if (DebugLog)
        Debug.Log($"[SharedAttributeManager] Initialized with {_entries.Count} attributes.");
    }

    public void Initialize(IReadOnlyDictionary<string, global::Game.Shared.Stats.SharedAttributeDef> defs)
    {
      if (_initialized) return;

      var converted = new Dictionary<string, SharedAttributeManager.SharedAttributeDef>();
      if (defs != null)
      {
        foreach (var kv in defs)
        {
          var def = kv.Value;
          if (def == null || string.IsNullOrEmpty(def.attr_id))
            continue;

          converted[def.attr_id] = new SharedAttributeManager.SharedAttributeDef
          {
            attr_id = def.attr_id,
            base_value = def.base_value,
            min = def.min,
            max = def.max
          };
        }
      }

      Initialize(converted);
    }

    public void Shutdown()
    {
      _entries.Clear();
      _sourceModifiers.Clear();
      _initialized = false;
    }

    public bool IsInitialized => _initialized;

    // ══════════════════════════════════════════════════════
    //  API — 修饰符管理
    // ══════════════════════════════════════════════════════

    /// <summary>注册/更新一个来源的全部修饰符。会先清除该来源的旧修饰符，再应用新列表。</summary>
    public void ApplyModifiers(string sourceId, List<ModifierEntry> entries)
    {
      if (!_initialized || string.IsNullOrEmpty(sourceId)) return;

      RemoveSourceModifiers(sourceId);

      if (entries != null)
      {
        foreach (var entry in entries)
        {
          if (entry == null || string.IsNullOrEmpty(entry.attrId)) continue;
          AddSourceModifier(sourceId, entry.attrId, entry.op, entry.value);
        }
      }

      if (DebugLog)
        Debug.Log($"[SharedAttributeManager] Applied modifiers from '{sourceId}': {entries?.Count ?? 0} entries.");
    }

    /// <summary>移除一个来源的所有修饰符。</summary>
    public void RemoveSource(string sourceId)
    {
      if (!_initialized || string.IsNullOrEmpty(sourceId)) return;
      RemoveSourceModifiers(sourceId);
    }

    /// <summary>单次添加修饰符（不先清除）。</summary>
    public void AddModifier(string sourceId, string attrId, ModifierOp op, float value)
    {
      if (!_initialized) return;
      AddSourceModifier(sourceId, attrId, op, value);
    }

    // ══════════════════════════════════════════════════════
    //  API — 值查询
    // ══════════════════════════════════════════════════════

    public float GetValue(string attrId, float defaultValue = 0f)
    {
      if (!_initialized || string.IsNullOrEmpty(attrId)) return defaultValue;
      if (_entries.TryGetValue(attrId, out var entry))
        return entry.ComputedValue;
      return defaultValue;
    }

    public bool TryGetValue(string attrId, out float value)
    {
      value = 0f;
      if (!_initialized || string.IsNullOrEmpty(attrId)) return false;
      if (_entries.TryGetValue(attrId, out var entry))
      { value = entry.ComputedValue; return true; }
      return false;
    }

    public float GetBaseValue(string attrId, float defaultValue = 0f)
    {
      if (!_initialized || string.IsNullOrEmpty(attrId)) return defaultValue;
      if (_entries.TryGetValue(attrId, out var entry))
        return entry.Def.base_value;
      return defaultValue;
    }

    public bool HasAttribute(string attrId)
    {
      return !string.IsNullOrEmpty(attrId) && _entries.ContainsKey(attrId);
    }

    public IReadOnlyDictionary<string, float> GetAllValues()
    {
      var snapshot = new Dictionary<string, float>();
      foreach (var kv in _entries)
        snapshot[kv.Key] = kv.Value.ComputedValue;
      return snapshot;
    }

    // ══════════════════════════════════════════════════════
    //  内部 — 修饰符来源追踪
    // ══════════════════════════════════════════════════════

    void AddSourceModifier(string sourceId, string attrId, ModifierOp op, float value)
    {
      if (!_entries.TryGetValue(attrId, out var entry)) return;

      if (!_sourceModifiers.TryGetValue(sourceId, out var list))
      {
        list = new List<ModifierEntry>();
        _sourceModifiers[sourceId] = list;
      }
      list.Add(new ModifierEntry { attrId = attrId, op = op, value = value });

      switch (op)
      {
        case ModifierOp.Add:      entry.AddSum += value; break;
        case ModifierOp.Mult:     entry.MultSum += value; break;
        case ModifierOp.FinalAdd: entry.FinalAddSum += value; break;
        case ModifierOp.FinalMult: entry.FinalMultSum += value; break;
      }
      entry.MarkDirty();
    }

    void RemoveSourceModifiers(string sourceId)
    {
      if (!_sourceModifiers.TryGetValue(sourceId, out var mods) || mods.Count == 0) return;

      var changedAttrs = new HashSet<string>();
      foreach (var mod in mods)
      {
        if (!_entries.TryGetValue(mod.attrId, out var entry)) continue;
        switch (mod.op)
        {
          case ModifierOp.Add:      entry.AddSum -= mod.value; break;
          case ModifierOp.Mult:     entry.MultSum -= mod.value; break;
          case ModifierOp.FinalAdd: entry.FinalAddSum -= mod.value; break;
          case ModifierOp.FinalMult: entry.FinalMultSum -= mod.value; break;
        }
        entry.MarkDirty();
        changedAttrs.Add(mod.attrId);
      }
      _sourceModifiers.Remove(sourceId);

      if (changedAttrs.Count > 0)
        OnAttributesChanged?.Invoke(changedAttrs);
    }

    // ══════════════════════════════════════════════════════
    //  内部数据类
    // ══════════════════════════════════════════════════════

    public class SharedAttributeEntry
    {
      public readonly SharedAttributeDef Def;
      public float AddSum;
      public float MultSum;
      public float FinalAddSum;
      public float FinalMultSum;

      float _cachedResult;
      bool _isDirty = true;

      public SharedAttributeEntry(SharedAttributeDef def)
      {
        Def = def;
        Recompute();
      }

      public float ComputedValue
      {
        get { if (_isDirty) Recompute(); return _cachedResult; }
      }

      public void MarkDirty() { _isDirty = true; }

      void Recompute()
      {
        var b = Def.base_value;
        var step1 = b + AddSum;
        var step2 = step1 * (1f + MultSum);
        var step3 = step2 * (1f + FinalMultSum) + FinalAddSum;
        _cachedResult = Mathf.Clamp(step3, Def.min, Def.max);
        _isDirty = false;
      }
    }

    /// <summary>通用的属性定义（不含 UI 字段）。</summary>
    [Serializable]
    public class SharedAttributeDef
    {
      public string attr_id;
      public float base_value;
      public float min;
      public float max;
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
      {
        this.attrId = attrId; this.op = op; this.value = value;
      }
    }

    static class Mathf
    {
      public static float Clamp(float v, float min, float max)
      {
        if (v < min) return min;
        if (v > max) return max;
        return v;
      }
    }
  }
}
