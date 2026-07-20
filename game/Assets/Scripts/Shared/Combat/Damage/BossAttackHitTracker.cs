using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Combat.Damage
{
  /// <summary>Central hit rules for boss attacks — prevents per-frame duplicate damage.</summary>
  public static class BossAttackHitTracker
  {
    static readonly Dictionary<long, float> s_instantHits = new();
    static readonly Dictionary<long, float> s_contactHits = new();
    static readonly Dictionary<long, float> s_tickHits = new();
    static int s_nextAttackInstanceId;

    public static int NewAttackInstanceId() => ++s_nextAttackInstanceId;

    public static void ClearAttackInstance(int attackInstanceId)
    {
      var high = (long)attackInstanceId << 32;
      var highEnd = high | 0xFFFFFFFFL;
      RemoveKeysInRange(s_instantHits, high, highEnd);
    }

    public static bool TryInstantHit(int attackInstanceId, int targetInstanceId)
    {
      if (attackInstanceId <= 0 || targetInstanceId == 0)
        return true;

      var key = Pack(attackInstanceId, targetInstanceId);
      if (s_instantHits.ContainsKey(key))
      {
        BossCombatDebugLog.ReportDuplicateHit("instant", attackInstanceId, targetInstanceId);
        return false;
      }

      s_instantHits[key] = Time.time;
      return true;
    }

    public static bool TryContactHit(int sourceInstanceId, int targetInstanceId, float intervalSec)
    {
      if (sourceInstanceId == 0 || targetInstanceId == 0)
        return true;

      var key = Pack(sourceInstanceId, targetInstanceId);
      var now = Time.time;
      if (s_contactHits.TryGetValue(key, out var last) && now - last < intervalSec)
        return false;

      s_contactHits[key] = now;
      return true;
    }

    public static bool TryTickHit(string tickGroup, int targetInstanceId, float intervalSec)
    {
      if (string.IsNullOrEmpty(tickGroup) || targetInstanceId == 0)
        return true;

      var key = Pack(tickGroup.GetHashCode(), targetInstanceId);
      var now = Time.time;
      if (s_tickHits.TryGetValue(key, out var last) && now - last < intervalSec)
        return false;

      s_tickHits[key] = now;
      return true;
    }

    public static void ResetRun()
    {
      s_instantHits.Clear();
      s_contactHits.Clear();
      s_tickHits.Clear();
      s_nextAttackInstanceId = 0;
      BossCombatDebugLog.ResetRun();
    }

    static long Pack(int a, int b) => ((long)a << 32) | (uint)b;

    static void RemoveKeysInRange(Dictionary<long, float> map, long min, long max)
    {
      var remove = new List<long>();
      foreach (var key in map.Keys)
      {
        if (key >= min && key <= max)
          remove.Add(key);
      }

      foreach (var key in remove)
        map.Remove(key);
    }
  }

  /// <summary>Dev-only boss combat diagnostics. No-op in release builds.</summary>
  public static class BossCombatDebugLog
  {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    static readonly Dictionary<int, float> s_lastHitTime = new();
    static float s_dpsWindowStart;
    static float s_dpsAccum;
    static string s_lastSkillId;
    static int s_lastAttackInstanceId;

    public static string LastSkillId => s_lastSkillId;
    public static int LastAttackInstanceId => s_lastAttackInstanceId;
    public static float DpsLastSecond => s_dpsAccum;

    public static void ResetRun()
    {
      s_lastHitTime.Clear();
      s_dpsWindowStart = Time.time;
      s_dpsAccum = 0f;
      s_lastSkillId = null;
      s_lastAttackInstanceId = 0;
    }

    public static void SetActiveSkill(string skillId, int attackInstanceId)
    {
      s_lastSkillId = skillId;
      s_lastAttackInstanceId = attackInstanceId;
    }

    public static void ReportPlayerHit(float damage, string sourceId)
    {
      if (Time.time - s_dpsWindowStart >= 1f)
      {
        s_dpsWindowStart = Time.time;
        s_dpsAccum = 0f;
      }

      s_dpsAccum += damage;
    }

    public static void ReportDuplicateHit(string kind, int attackInstanceId, int targetInstanceId)
    {
      var key = attackInstanceId * 997 + targetInstanceId;
      var now = Time.time;
      if (s_lastHitTime.TryGetValue(key, out var last) && now - last < 0.05f)
        return;

      s_lastHitTime[key] = now;
      Debug.LogWarning(
        $"[BossCombat] Duplicate {kind} hit blocked: attack={attackInstanceId} target={targetInstanceId} skill={s_lastSkillId}");
    }
#else
    public static string LastSkillId => null;
    public static int LastAttackInstanceId => 0;
    public static float DpsLastSecond => 0f;
    public static void ResetRun() { }
    public static void SetActiveSkill(string skillId, int attackInstanceId) { }
    public static void ReportPlayerHit(float damage, string sourceId) { }
    public static void ReportDuplicateHit(string kind, int attackInstanceId, int targetInstanceId) { }
#endif
  }
}
