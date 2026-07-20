using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Events;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>Tracks recent player damage sources for death summary.</summary>
  public sealed class ArenaDamageLog : MonoBehaviour
  {
    struct Entry
    {
      public float Time;
      public string Label;
      public float Amount;
    }

    static ArenaDamageLog s_instance;
    readonly List<Entry> _entries = new();

    public static ArenaDamageLog Instance => s_instance;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaDamageLog");
      DontDestroyOnLoad(go);
      go.AddComponent<ArenaDamageLog>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      CombatEventBus.PostDamage += OnPostDamage;
    }

    void OnDestroy()
    {
      CombatEventBus.PostDamage -= OnPostDamage;
      if (s_instance == this)
        s_instance = null;
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (args.Target == null || !args.Target.CompareTag("Player"))
        return;

      if (args.Result.FinalDamage <= 0f)
        return;

      _entries.Add(new Entry
      {
        Time = Time.unscaledTime,
        Label = FormatSource(args.Request.DamageSourceId, args.Attacker),
        Amount = args.Result.FinalDamage
      });

      Prune();
    }

    void Prune()
    {
      var cutoff = Time.unscaledTime - 5f;
      for (var i = _entries.Count - 1; i >= 0; i--)
      {
        if (_entries[i].Time < cutoff)
          _entries.RemoveAt(i);
      }
    }

    public static IReadOnlyList<string> GetRecentLines(int maxLines = 5)
    {
      if (s_instance == null)
        return System.Array.Empty<string>();

      s_instance.Prune();
      var lines = new List<string>();
      var start = Mathf.Max(0, s_instance._entries.Count - maxLines);
      for (var i = start; i < s_instance._entries.Count; i++)
      {
        var entry = s_instance._entries[i];
        lines.Add($"{entry.Label}  -{entry.Amount:F0}");
      }

      return lines;
    }

    static string FormatSource(string sourceId, GameObject attacker)
    {
      if (!string.IsNullOrEmpty(sourceId))
      {
        if (sourceId.Contains("support"))
          return "支援治疗场";
        if (sourceId.Contains("disruptor"))
          return "干扰减速";
        if (sourceId.Contains("bomber") || sourceId.Contains("explosion"))
          return "爆炸";
        if (sourceId.Contains("laser") || sourceId.Contains("beam"))
          return "激光";
        if (sourceId.Contains("projectile") || sourceId.Contains("bullet"))
          return "远程子弹";
        if (sourceId.Contains("contact") || sourceId.Contains("melee"))
          return "接触伤害";
        if (sourceId.Contains("arena_edge") || sourceId.Contains("hazard"))
          return "边界危险区";
        if (sourceId.Contains("enemy") || sourceId.Contains("mob") || sourceId.Contains("minion"))
          return "敌人攻击";
        if (sourceId.Contains("detached_pulse"))
          return "脉冲波";
      }

      if (attacker != null)
      {
        var id = attacker.name.ToLowerInvariant();
        if (id.Contains("support"))
          return "支援单位";
        if (id.Contains("disruptor"))
          return "干扰者";
        if (id.Contains("bomber"))
          return "自爆怪";
        if (id.Contains("star") || id.Contains("hex") || id.Contains("pent") || id.Contains("tri") || id.Contains("square") || id.Contains("mob") || id.Contains("enemy"))
          return "敌人攻击";
        if (id.Contains("boss"))
          return "Boss 攻击";
      }

      return "其他伤害";
    }
  }
}
