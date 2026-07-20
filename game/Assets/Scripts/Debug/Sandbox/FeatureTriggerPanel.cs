using System;
using UnityEngine;
using UnityEngine.UI;

using Game.Shared.Core;

namespace Game.DevTools.Sandbox
{
  public class FeatureTriggerPanel
  {
    readonly Font _font;
    readonly Action<string> _onTrigger;
    RectTransform _content;

    static readonly (string id, string label)[] Triggers =
    {
      ("time_stop", "Time Stop"),
      ("element_burst", "Element Burst"),
      ("element_melt", "Element Melt"),
      ("element_overload", "Element Overload"),
      ("cooldown_reset", "Cooldown Reset"),
      ("gravity_well", "Gravity Well"),
      ("pierce", "Pierce"),
      ("split", "Split"),
      ("chain", "Chain"),
      ("homing", "Homing"),
      ("explosion", "Explosion"),
    };

    public FeatureTriggerPanel(Font font, Action<string> onTrigger)
    {
      _font = font;
      _onTrigger = onTrigger;
    }

    public void Build(Transform parent)
    {
      var scrollGo = new GameObject("TriggerScroll", typeof(RectTransform));
      scrollGo.transform.SetParent(parent, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>();
      scrollRt.anchorMin = new Vector2(0f, 0f);
      scrollRt.anchorMax = new Vector2(1f, 1f);
      scrollRt.offsetMin = new Vector2(0f, 0f);
      scrollRt.offsetMax = new Vector2(0f, 0f);

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false;
      scroll.vertical = true;
      scroll.movementType = ScrollRect.MovementType.Clamped;

      var viewport = CreatePanel(scrollGo.transform, "Viewport", new Color(0.06f, 0.08f, 0.10f, 0.01f),
        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.anchorMin = Vector2.zero;
      viewport.anchorMax = Vector2.one;
      viewport.offsetMin = Vector2.zero;
      viewport.offsetMax = Vector2.zero;
      var viewportImg = viewport.GetComponent<Image>();
      if (viewportImg == null)
        viewportImg = viewport.gameObject.AddComponent<Image>();
      viewportImg.raycastTarget = true;
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

      _content = CreatePanel(viewport, "Content", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f));
      _content.pivot = new Vector2(0.5f, 1f);
      scroll.viewport = viewport;
      scroll.content = _content;

      var y = -4f;
      foreach (var (id, label) in Triggers)
      {
        var captured = id;
        CreateButton(_content, $"Trigger_{id}", label,
          new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(-8f, 30f),
          new Color(0.22f, 0.32f, 0.42f, 1f),
          () => _onTrigger?.Invoke(captured));
        y -= 34f;
      }

      _content.sizeDelta = new Vector2(0f, Mathf.Abs(y) + 8f);
    }

    RectTransform CreatePanel(Transform parent, string name, Color bg,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = sizeDelta;

      if (bg.a > 0.001f)
      {
        var img = go.AddComponent<Image>();
        img.color = bg;
      }

      return rt;
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

    Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 anchoredPos, Vector2 sizeDelta, Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, name, bg, anchorMin, anchorMax, anchoredPos, sizeDelta);
      var btn = rt.gameObject.AddComponent<Button>();
      btn.targetGraphic = rt.GetComponent<Image>();

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(rt, false);
      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = new Vector2(6f, 2f);
      textRt.offsetMax = new Vector2(-6f, -2f);

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 12;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;
      text.text = label;
      text.raycastTarget = false;

      if (onClick != null)
        btn.onClick.AddListener(onClick);

      return btn;
    }
  }
}
