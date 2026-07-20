using System.Collections.Generic;
using UnityEngine;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Projectile
{
  /// <summary>Short-lived conductive mark for lightning chain prioritization.</summary>
  public static class ProjectileConductMark
  {
    static readonly Dictionary<int, float> s_expiryByInstanceId = new();

    public static void Apply(Health target, float durationSeconds = 2f)
    {
      if (target == null)
        return;
      s_expiryByInstanceId[target.GetInstanceID()] = Time.time + durationSeconds;
    }

    public static bool IsActive(Health target)
    {
      if (target == null)
        return false;
      var id = target.GetInstanceID();
      if (!s_expiryByInstanceId.TryGetValue(id, out var expiry))
        return false;
      if (Time.time > expiry)
      {
        s_expiryByInstanceId.Remove(id);
        return false;
      }
      return true;
    }

    public static void Prune()
    {
      if (s_expiryByInstanceId.Count == 0)
        return;
      var now = Time.time;
      var remove = new List<int>();
      foreach (var pair in s_expiryByInstanceId)
      {
        if (pair.Value <= now)
          remove.Add(pair.Key);
      }
      for (var i = 0; i < remove.Count; i++)
        s_expiryByInstanceId.Remove(remove[i]);
    }
  }
}
