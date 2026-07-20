using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>C2: Compact achievement + daily contract panel for lobby and post-run.</summary>
  public static class ArenaAchievementUI
  {
    public static void AppendToPanel(RectTransform parent, float yOffset)
    {
      ArenaAchievementSystem.EnsureLoaded();

      var box = CreateImage(parent, "AchievementBox", new Color(0.06f, 0.12f, 0.16f, 0.88f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yOffset), new Vector2(480f, 120f));

      var title = CreateText(box.transform, "AchTitle",
        $"成就 {ArenaAchievementSystem.UnlockedCount}/{ArenaAchievementSystem.AllAchievements.Count}",
        16, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(440f, 24f));
      title.color = new Color(0.95f, 0.88f, 0.55f, 1f);

      var daily = ArenaAchievementSystem.ActiveDailyContract;
      var dailyLine = daily != null
        ? (ArenaAchievementSystem.IsDailyComplete()
          ? $"今日契约：{daily.title} ✓"
          : $"今日契约：{daily.title} ({daily.description})")
        : "今日契约：—";

      var body = CreateText(box.transform, "AchBody", BuildRecentUnlocks() + "\n" + dailyLine,
        14, FontStyle.Normal,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(440f, 72f));
      body.alignment = TextAnchor.UpperLeft;
      body.color = new Color(0.72f, 0.88f, 0.94f, 1f);
    }

    static string BuildRecentUnlocks()
    {
      var lines = string.Empty;
      var count = 0;
      foreach (var ach in ArenaAchievementSystem.AllAchievements)
      {
        if (ach == null || !ArenaAchievementSystem.IsUnlocked(ach.id))
          continue;
        lines += $"• {ach.title}\n";
        count++;
        if (count >= 3)
          break;
      }

      return string.IsNullOrEmpty(lines) ? "暂无已解锁成就" : lines.TrimEnd();
    }

    static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = size;
      var image = go.AddComponent<Image>();
      image.color = color;
      return image;
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 rectSize)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = anchoredPosition;
      rt.sizeDelta = rectSize;
      var label = go.AddComponent<Text>();
      label.text = text;
      label.fontSize = size;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = Color.white;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }
  }
}
