using System.Collections.Generic;



namespace Game.Modes.Roguelike.UI

{

  /// <summary>A15/B10: Records run timeline nodes and decision moments for victory/death UI.</summary>

  public static class RunTimelineRecorder

  {

    public enum MomentKind

    {

      None = 0,

      NearDeathEscape,

      BossClutchKill,

      LevelUpStreak

    }



    public readonly struct TimelineNode

    {

      public readonly string Category;

      public readonly string Detail;

      public readonly float Time;

      public readonly MomentKind Kind;

      public readonly int Priority;

      public readonly bool IsDecision;



      public TimelineNode(string category, string detail, float time, MomentKind kind = MomentKind.None, int priority = 0, bool isDecision = false)

      {

        Category = category;

        Detail = detail;

        Time = time;

        Kind = kind;

        Priority = priority;

        IsDecision = isDecision;

      }

    }



    static readonly List<TimelineNode> s_nodes = new();

    static readonly HashSet<MomentKind> s_recordedOnce = new();

    static float s_startTime;



    public static IReadOnlyList<TimelineNode> Nodes => s_nodes;



    public static void Reset()

    {

      s_nodes.Clear();

      s_recordedOnce.Clear();

      s_startTime = UnityEngine.Time.unscaledTime;

    }



    public static int GetPriority(MomentKind kind) => kind switch

    {

      MomentKind.BossClutchKill => 100,

      MomentKind.NearDeathEscape => 90,

      MomentKind.LevelUpStreak => 70,

      _ => 0

    };



    public static void Record(string category, string detail)

    {

      if (string.IsNullOrEmpty(category))

        return;



      s_nodes.Add(new TimelineNode(category, detail ?? "", UnityEngine.Time.unscaledTime - s_startTime));

    }



    public static bool TryRecordOnce(MomentKind kind, string category, string detail)

    {

      if (kind == MomentKind.None || string.IsNullOrEmpty(category) || s_recordedOnce.Contains(kind))

        return false;



      s_recordedOnce.Add(kind);

      var time = UnityEngine.Time.unscaledTime - s_startTime;

      var priority = GetPriority(kind);

      s_nodes.Add(new TimelineNode(category, detail ?? "", time, kind, priority, true));

      return true;

    }



    public static TimelineNode? GetBrightestMoment()

    {

      TimelineNode? best = null;

      foreach (var node in s_nodes)

      {

        if (!node.IsDecision)

          continue;



        if (best == null || node.Priority > best.Value.Priority

            || (node.Priority == best.Value.Priority && node.Time > best.Value.Time))

          best = node;

      }



      return best;

    }



    public static IReadOnlyList<TimelineNode> GetDecisionMoments()

    {

      var result = new List<TimelineNode>();

      foreach (var node in s_nodes)

      {

        if (node.IsDecision)

          result.Add(node);

      }



      return result;

    }

  }

}


