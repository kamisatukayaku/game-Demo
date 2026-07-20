using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  public sealed class SandboxRangedRegressionPanel
  {
    readonly Font _font;
    readonly Func<SandboxSceneController> _sceneAccessor;
    GameObject _root;

    static readonly (string id, string label)[] Lines =
    {
      ("sp", "散射"),
      ("pc", "穿透"),
      ("ex", "爆破"),
      ("lt", "闪电")
    };

    public SandboxRangedRegressionPanel(Font font, Func<SandboxSceneController> sceneAccessor)
    {
      _font = font;
      _sceneAccessor = sceneAccessor;
    }

    public void Build(Transform parent)
    {
      _root = parent.gameObject;
      CreateLabel(parent, "RangedVfxHeader", "射手回归", 12, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -2f), new Vector2(-4f, 16f));

      var y = -22f;
      y = AddRow(parent, "基础射击", y, () => Apply("base"));
      foreach (var (id, label) in Lines)
      {
        var lineId = id;
        var lineLabel = label;
        for (var tier = 1; tier <= 5; tier++)
        {
          var capturedTier = tier;
          CreateButton(parent, $"Ranged_{lineId}_T{capturedTier}", $"{lineLabel}T{capturedTier}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, y), new Vector2(-4f, 20f),
            new Color(0.20f, 0.32f, 0.42f, 1f),
            () => Apply($"{lineId}{capturedTier}"));
          y -= 22f;
        }
        y -= 2f;
      }

      y = AddRow(parent, "散射+穿透", y, () => Apply("spread_pierce"));
      y = AddRow(parent, "爆破+闪电", y, () => Apply("explosion_lightning"));
      y = AddRow(parent, "全组合MAX", y, () => Apply("full_max"));
      y = AddRow(parent, "高攻速", y, () => Apply("high_as"));
      y = AddRow(parent, "怪物群", y, () => Apply("swarm"));
      AddRow(parent, "Reset VFX", y, SandboxRangedRegressionService.ResetVfxPools);
    }

    public void SetVisible(bool visible)
    {
      if (_root != null)
        _root.SetActive(visible);
    }

    void Apply(string preset) => SandboxRangedRegressionService.ApplyPreset(_sceneAccessor(), preset);

    float AddRow(Transform parent, string label, float y, Action action)
    {
      CreateButton(parent, $"Ranged_{label}", label,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, y), new Vector2(-4f, 22f),
        new Color(0.22f, 0.34f, 0.44f, 1f),
        () => action?.Invoke());
      return y - 26f;
    }

    Text CreateLabel(Transform parent, string name, string text, int fontSize, FontStyle style,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0f, 1f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;
      var label = go.AddComponent<Text>();
      label.font = _font;
      label.fontSize = fontSize;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleLeft;
      label.color = Color.white;
      label.text = text;
      label.raycastTarget = false;
      return label;
    }

    void CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 anchoredPos, Vector2 sizeDelta, Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(name, typeof(RectTransform), typeof(Image));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0f, 1f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;
      var img = go.GetComponent<Image>();
      img.color = bg;
      var btn = go.AddComponent<Button>();
      btn.targetGraphic = img;

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = new Vector2(2f, 1f);
      textRt.offsetMax = new Vector2(-2f, -1f);
      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 10;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      text.text = label;
      text.raycastTarget = false;
      if (onClick != null)
        btn.onClick.AddListener(onClick);
    }
  }
}
