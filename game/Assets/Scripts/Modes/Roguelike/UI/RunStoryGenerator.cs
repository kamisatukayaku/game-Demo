using System.Collections.Generic;



using Game.Modes.Roguelike.Progression;



namespace Game.Modes.Roguelike.UI

{

  /// <summary>S10/A16/B10/B11: Run narrative lines, title, and share keywords.</summary>

  public static class RunStoryGenerator

  {

    public readonly struct RunStoryLines

    {

      public readonly string Highlight;

      public readonly string Crisis;

      public readonly string Turn;

      public readonly string Title;



      public RunStoryLines(string highlight, string crisis, string turn, string title)

      {

        Highlight = highlight;

        Crisis = crisis;

        Turn = turn;

        Title = title;

      }



      public string FormatBlock() =>

        string.IsNullOrEmpty(Title)

          ? $"▸ {Highlight}\n▸ {Crisis}\n▸ {Turn}"

          : $"「{Title}」\n▸ {Highlight}\n▸ {Crisis}\n▸ {Turn}";

    }



    public static RunStoryLines Generate(bool victory)

    {

      var build = ArenaBuildBootstrap.GetDisplayName(RunDeathSummary.BuildDirection);

      var wave = RunDeathSummary.WaveReached;

      var kills = RunDeathSummary.TotalKills;

      var level = RunDeathSummary.PlayerLevel;

      var time = RunDeathSummary.FormatSurviveTime(RunDeathSummary.SurviveSeconds);

      var brightest = RunTimelineRecorder.GetBrightestMoment();



      string highlight;

      string crisis;

      string turn;



      if (brightest.HasValue)

      {

        var moment = brightest.Value;

        highlight = DescribeBrightestHighlight(moment, build, wave, time, kills);

        crisis = DescribeBrightestCrisis(moment, wave);

        turn = DescribeBrightestTurn(moment, victory, wave, time);

      }

      else

      {

        highlight = kills >= 120

          ? $"以 {build} 身份在 {time} 内斩落 {kills} 敌，波次 {wave} 形成压制高潮。"

          : $"以 {build} 起步，Lv.{level} 时在波次 {wave} 打出 {kills} 次击杀。";



        crisis = wave >= 10

          ? "中段多波高压围攻，血量多次跌入危险区。"

          : "早期遭遇精英/Boss 压迫，走位与回复被反复考验。";



        turn = victory

          ? "最终撑过全波次，Arena 在最后一击后归于寂静。"

          : $"在第 {wave} 波防线崩溃，Run 在 {time} 处戛然而止。";

      }



      if (victory)

        RunTimelineRecorder.Record("胜利", $"W{wave} 通关");



      return new RunStoryLines(highlight, crisis, turn, GenerateRunTitle(victory));

    }



    public static string[] GenerateKeywords(bool victory)

    {

      var keywords = new List<string>(3);

      keywords.Add(BuildKeyword(RunDeathSummary.BuildDirection));



      var brightest = RunTimelineRecorder.GetBrightestMoment();

      if (brightest.HasValue)

        keywords.Add(MomentKeyword(brightest.Value.Kind));

      else if (RunDeathSummary.WaveReached >= 10)

        keywords.Add("深波");

      else if (RunDeathSummary.TotalKills >= 100)

        keywords.Add("屠戮");

      else

        keywords.Add("缠斗");



      keywords.Add(victory ? "凯旋" : "折戟");



      while (keywords.Count < 3)

        keywords.Add("Arena");



      return new[] { keywords[0], keywords[1], keywords[2] };

    }



    public static string GenerateRunTitle(bool victory)

    {

      var buildTag = RunDeathSummary.BuildDirection switch

      {

        ArenaBuildBootstrap.Shooter => "弹幕织网者",

        ArenaBuildBootstrap.Contact => "星环裁决者",

        ArenaBuildBootstrap.Support => "潮汐治愈者",

        _ => "脉冲织网者"

      };



      var suffix = victory ? "险死翻盘" : "折戟 Arena";

      if (RunDeathSummary.WaveReached >= 15 && victory)

        suffix = "全波征服";

      else if (RunDeathSummary.TotalKills >= 150)

        suffix = "斩尽群敌";



      var brightest = RunTimelineRecorder.GetBrightestMoment();

      if (brightest.HasValue)

      {

        suffix = brightest.Value.Kind switch

        {

          RunTimelineRecorder.MomentKind.BossClutchKill => victory ? "极限反杀" : "反杀未竟",

          RunTimelineRecorder.MomentKind.NearDeathEscape => victory ? "险死翻盘" : "血线溃败",

          RunTimelineRecorder.MomentKind.LevelUpStreak => "连升破局",

          _ => suffix

        };

      }

      else

      {

        foreach (var node in RunTimelineRecorder.Nodes)

        {

          if (node.Category.Contains("濒死"))

          {

            suffix = victory ? "险死翻盘" : "血线溃败";

            break;

          }

        }

      }



      return $"{buildTag} · {suffix}";

    }



    static string DescribeBrightestHighlight(RunTimelineRecorder.TimelineNode moment, string build, int wave, string time, int kills) =>

      moment.Kind switch

      {

        RunTimelineRecorder.MomentKind.BossClutchKill =>

          $"W{wave} 以 {build} 在 {moment.Detail}，{time} 内累计 {kills} 杀完成极限反杀。",

        RunTimelineRecorder.MomentKind.NearDeathEscape =>

          $"W{wave} {build} 于 {moment.Detail}，{time} 内以 {kills} 杀续命推进。",

        RunTimelineRecorder.MomentKind.LevelUpStreak =>

          $"W{wave} {build} 触发 {moment.Detail}，Build 在 {time} 内成型并打出 {kills} 杀。",

        _ => $"W{wave} 决定性瞬间：{moment.Category} — {moment.Detail}。"

      };



    static string DescribeBrightestCrisis(RunTimelineRecorder.TimelineNode moment, int wave) =>

      moment.Kind switch

      {

        RunTimelineRecorder.MomentKind.BossClutchKill => "Boss 战将血线压至 1% 以下，全场进入读秒式生死局。",

        RunTimelineRecorder.MomentKind.NearDeathEscape => "多次跌入濒死区，走位与回复被反复推到极限。",

        RunTimelineRecorder.MomentKind.LevelUpStreak => $"W{wave} 前后遭遇高压，升级窗口与敌潮正面冲突。",

        _ => wave >= 10 ? "中段多波高压围攻，血量多次跌入危险区。" : "早期遭遇精英/Boss 压迫，走位与回复被反复考验。"

      };



    static string DescribeBrightestTurn(RunTimelineRecorder.TimelineNode moment, bool victory, int wave, string time) =>

      moment.Kind switch

      {

        RunTimelineRecorder.MomentKind.BossClutchKill => victory

          ? "反杀 Boss 后士气逆转，最终撑过全波次。"

          : $"反杀后仍未能守住第 {wave} 波，Run 在 {time} 处终止。",

        RunTimelineRecorder.MomentKind.NearDeathEscape => victory

          ? "死里逃生后稳住节奏，Arena 在最后一击后归于寂静。"

          : $"濒死脱身后仍倒在第 {wave} 波，Run 在 {time} 处戛然而止。",

        RunTimelineRecorder.MomentKind.LevelUpStreak => victory

          ? "连升后战力跳变，最终撑过全波次。"

          : $"连升后仍未能守住第 {wave} 波，Run 在 {time} 处终止。",

        _ => victory

          ? "最终撑过全波次，Arena 在最后一击后归于寂静。"

          : $"在第 {wave} 波防线崩溃，Run 在 {time} 处戛然而止。"

      };



    static string BuildKeyword(string buildId) => buildId switch

    {

      ArenaBuildBootstrap.Shooter => "弹幕",

      ArenaBuildBootstrap.Contact => "近战",

      ArenaBuildBootstrap.Support => "治愈",

      _ => "脉冲"

    };



    static string MomentKeyword(RunTimelineRecorder.MomentKind kind) => kind switch

    {

      RunTimelineRecorder.MomentKind.BossClutchKill => "反杀",

      RunTimelineRecorder.MomentKind.NearDeathEscape => "濒死",

      RunTimelineRecorder.MomentKind.LevelUpStreak => "连升",

      _ => "瞬间"

    };

  }

}


