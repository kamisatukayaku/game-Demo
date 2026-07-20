using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>A15: 5-node run timeline on victory/death screens.</summary>
  public static class RunTimelineUI
  {
    const float NodeWidth = 520f;

    public static void AppendToPanel(RectTransform parent, float yOffset = -360f)
    {
      if (parent == null)
        return;

      var hostGo = new GameObject("RunTimeline", typeof(RectTransform));
      hostGo.transform.SetParent(parent, false);
      var host = hostGo.GetComponent<RectTransform>();
      host.anchorMin = new Vector2(0.5f, 1f);
      host.anchorMax = new Vector2(0.5f, 1f);
      host.pivot = new Vector2(0.5f, 1f);
      host.anchoredPosition = new Vector2(0f, yOffset);
      host.sizeDelta = new Vector2(NodeWidth, 120f);
      BuildTimelineContent(host);
    }

    public static void AppendToHost(RectTransform host)
    {
      if (host == null)
        return;

      BuildTimelineContent(host);
    }

    static void BuildTimelineContent(RectTransform host)
    {
      var nodes = BuildDisplayNodes();
      if (nodes.Count == 0)
      {
        var empty = CreateText(host, "TimelineEmpty", "暂无关键节点", 13, FontStyle.Italic,
          Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        empty.alignment = TextAnchor.UpperLeft;
        empty.color = new Color(0.62f, 0.78f, 0.86f, 0.75f);
        return;
      }

      var bodyHeight = Mathf.Max(72f, nodes.Count * 18f + 8f);
      host.sizeDelta = new Vector2(0f, bodyHeight);

      var body = CreateText(host, "TimelineBody", FormatNodes(nodes), 13, FontStyle.Normal,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      var bodyRt = body.rectTransform;
      bodyRt.anchorMin = Vector2.zero;
      bodyRt.anchorMax = Vector2.one;
      bodyRt.offsetMin = Vector2.zero;
      bodyRt.offsetMax = Vector2.zero;
      body.alignment = TextAnchor.UpperLeft;
      body.color = new Color(0.78f, 0.9f, 0.96f, 0.92f);
      body.supportRichText = true;
      body.horizontalOverflow = HorizontalWrapMode.Wrap;
      body.verticalOverflow = VerticalWrapMode.Overflow;
      body.lineSpacing = 1.08f;
    }

    static System.Collections.Generic.List<RunTimelineRecorder.TimelineNode> BuildDisplayNodes()
    {
      var all = RunTimelineRecorder.Nodes;
      var result = new System.Collections.Generic.List<RunTimelineRecorder.TimelineNode>();

      string[] categories = { "首进化", "Boss", "濒死", "Capstone", "胜利" };
      foreach (var cat in categories)
      {
        foreach (var node in all)
        {
          if (node.Category.Contains(cat) || MatchesCategory(node, cat))
          {
            result.Add(node);
            break;
          }
        }
      }

      if (result.Count == 0 && all.Count > 0)
      {
        var take = Mathf.Min(5, all.Count);
        for (var i = 0; i < take; i++)
          result.Add(all[i]);
      }

      return result;
    }

    static bool MatchesCategory(RunTimelineRecorder.TimelineNode node, string cat) => cat switch
    {
      "首进化" => node.Category.Contains("Evolution") || node.Category.Contains("进化"),
      "Boss" => node.Category.Contains("Boss"),
      "濒死" => node.Category.Contains("濒死") || node.Category.Contains("NearDeath"),
      "Capstone" => node.Category.Contains("Capstone"),
      "胜利" => node.Category.Contains("Victory") || node.Category.Contains("胜利"),
      _ => false
    };

    static string FormatNodes(System.Collections.Generic.List<RunTimelineRecorder.TimelineNode> nodes)
    {
      var sb = new StringBuilder();
      foreach (var node in nodes)
      {
        var mins = Mathf.FloorToInt(node.Time / 60f);
        var secs = Mathf.FloorToInt(node.Time % 60f);
        var stamp = mins > 0 ? $"{mins}:{secs:00}" : $"{secs}s";
        var line = $"[{stamp}] {node.Category}: {node.Detail}";
        if (node.IsDecision)
          sb.AppendLine($"<color=#FFD966>★ {line}</color>");
        else
          sb.AppendLine(line);
      }
      return sb.ToString().TrimEnd();
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 rectSize)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = pos;
      rt.sizeDelta = rectSize;
      var label = go.AddComponent<Text>();
      label.text = text;
      label.fontSize = size;
      label.fontStyle = style;
      label.font = UiFontHelper.GetFont();
      label.alignment = TextAnchor.MiddleCenter;
      return label;
    }
  }
}
