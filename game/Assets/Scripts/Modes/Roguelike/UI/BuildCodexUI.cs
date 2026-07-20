using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>C3: Build codex summary + next-run recommendation for lobby.</summary>
  public static class BuildCodexUI
  {
    public static void AppendToPanel(RectTransform panel, float yOffset, Transform overlayRoot)
    {
      var recommendation = ArenaMetaProgress.GetRecommendedBuildLabel();
      var codexLine = ArenaMetaProgress.GetCodexSummaryLine();

      var box = CreateImage(panel, "CodexBox", new Color(0.05f, 0.1f, 0.14f, 0.82f),
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yOffset), new Vector2(720f, 72f));

      var title = CreateText(box.transform, "CodexTitle", codexLine, 15, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, -16f), new Vector2(560f, 22f));
      title.color = new Color(0.62f, 0.82f, 0.92f, 1f);

      var rec = CreateText(box.transform, "CodexRec", recommendation, 17, FontStyle.Bold,
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60f, 18f), new Vector2(560f, 28f));
      rec.color = new Color(0.95f, 0.88f, 0.55f, 1f);

      CreateCodexButton(box.transform, overlayRoot);
    }

    static void CreateCodexButton(Transform parent, Transform overlayRoot)
    {
      var go = new GameObject("OpenCodexButton", typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(1f, 0.5f);
      rt.anchorMax = new Vector2(1f, 0.5f);
      rt.pivot = new Vector2(1f, 0.5f);
      rt.anchoredPosition = new Vector2(-12f, 0f);
      rt.sizeDelta = new Vector2(108f, 36f);

      var image = go.GetComponent<Image>();
      image.color = new Color(0.18f, 0.34f, 0.42f, 0.95f);

      var btn = go.GetComponent<Button>();
      btn.targetGraphic = image;
      btn.onClick.AddListener(() =>
      {
        if (overlayRoot != null)
          BuildCodexDetailUI.Open(overlayRoot);
      });

      var label = CreateText(go.transform, "Label", "查看图鉴", 15, FontStyle.Bold,
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      label.color = new Color(0.75f, 0.92f, 1f, 1f);
      label.raycastTarget = false;
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
