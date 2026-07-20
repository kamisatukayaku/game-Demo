using System.Collections.Generic;

using Game.Shared.Combat.Events;
using UnityEngine;

namespace Game.DevTools.Sandbox
{
  /// <summary>沙盒 DPS / 伤害统计（监听真实 CombatEventBus）。</summary>
  public static class SandboxCombatMetrics
  {
    const float DpsWindowSeconds = 5f;
    const float AvgWindowSeconds = 10f;

    static bool s_active;
    static GameObject s_player;
    static float s_sessionStart;
    static float s_windowStart;
    static float s_windowDamage;
    static float s_totalDamage;
    static int s_hitCount;
    static int s_killCount;
    static float s_lastDps;
    static float s_avg10sDps;
    static readonly List<(float time, float damage)> s_recentHits = new();

    public static float TotalDamage => s_totalDamage;
    public static float CurrentDps => s_lastDps;
    public static float Average10sDps => s_avg10sDps;
    public static int HitCount => s_hitCount;
    public static int KillCount => s_killCount;
    public static float SessionSeconds => s_active ? Time.time - s_sessionStart : 0f;

    public static event System.Action Changed;

    public static void Begin(GameObject player)
    {
      End();
      s_player = player;
      s_active = player != null;
      if (!s_active)
        return;

      s_sessionStart = Time.time;
      ResetWindow();
      s_totalDamage = 0f;
      s_hitCount = 0;
      s_killCount = 0;
      s_recentHits.Clear();
      s_avg10sDps = 0f;

      CombatEventBus.PostDamage += OnPostDamage;
      CombatEventBus.OnKill += OnKill;
    }

    public static void End()
    {
      if (!s_active)
        return;

      CombatEventBus.PostDamage -= OnPostDamage;
      CombatEventBus.OnKill -= OnKill;
      s_active = false;
      s_player = null;
      s_recentHits.Clear();
    }

    public static void Reset()
    {
      if (!s_active)
        return;

      s_sessionStart = Time.time;
      ResetWindow();
      s_totalDamage = 0f;
      s_hitCount = 0;
      s_killCount = 0;
      s_recentHits.Clear();
      s_avg10sDps = 0f;
      Notify();
    }

    static void ResetWindow()
    {
      s_windowStart = Time.time;
      s_windowDamage = 0f;
      s_lastDps = 0f;
    }

    static void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (!s_active || s_player == null || args.Attacker != s_player)
        return;
      if (args.Result.FinalDamage <= 0f)
        return;

      var dmg = args.Result.FinalDamage;
      var now = Time.time;

      s_totalDamage += dmg;
      s_windowDamage += dmg;
      s_hitCount++;
      s_recentHits.Add((now, dmg));
      PruneRecentHits(now);
      s_avg10sDps = ComputeAverageDps(now);

      var elapsed = now - s_windowStart;
      if (elapsed >= DpsWindowSeconds)
      {
        s_lastDps = s_windowDamage / elapsed;
        ResetWindow();
      }
      else if (elapsed > 0.01f)
      {
        s_lastDps = s_windowDamage / elapsed;
      }

      Notify();
    }

    static void PruneRecentHits(float now)
    {
      var cutoff = now - AvgWindowSeconds;
      var removeCount = 0;
      while (removeCount < s_recentHits.Count && s_recentHits[removeCount].time < cutoff)
        removeCount++;

      if (removeCount > 0)
        s_recentHits.RemoveRange(0, removeCount);
    }

    static float ComputeAverageDps(float now)
    {
      if (s_recentHits.Count == 0)
        return 0f;

      var windowStart = now - AvgWindowSeconds;
      var total = 0f;
      var earliest = now;

      foreach (var hit in s_recentHits)
      {
        total += hit.damage;
        if (hit.time < earliest)
          earliest = hit.time;
      }

      var span = Mathf.Max(0.01f, now - Mathf.Max(windowStart, earliest));
      return total / span;
    }

    static void OnKill(CombatEventBus.KillArgs args)
    {
      if (!s_active || s_player == null || args.Killer != s_player)
        return;

      s_killCount++;
      Notify();
    }

    static void Notify() => Changed?.Invoke();
  }
}
