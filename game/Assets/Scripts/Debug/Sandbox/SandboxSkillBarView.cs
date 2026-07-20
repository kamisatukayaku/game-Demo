using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;

namespace Game.DevTools.Sandbox
{
  /// <summary>右侧法师技能释放面板（数据来自 player_class_skills.json）。</summary>
  public class SandboxSkillBarView
  {
    const float HeaderH = 22f;
    const float ToolbarH = 24f;
    const float HintH = 32f;
    const float ContentTop = HeaderH + ToolbarH + HintH + 6f;

    static readonly Color ActiveBg = new(0.18f, 0.38f, 0.32f, 1f);
    static readonly Color IdleBg = new(0.20f, 0.32f, 0.44f, 1f);
    static readonly Color TextDim = new(0.62f, 0.72f, 0.78f, 1f);

    readonly Font _font;
    readonly RectTransform _host;
    readonly Action<int> _onToggle;
    readonly Func<int, bool> _isActive;
    readonly Action _onAutoCastToggle;
    readonly Func<bool> _isAutoCastOn;
    readonly Action _onResetCooldowns;

    GameObject _panel;
    RectTransform _contentRoot;
    Text _autoCastLabel;
    readonly List<Button> _buttons = new();
    readonly List<Image> _buttonImages = new();
    readonly List<int> _buttonSlots = new();
    string _currentTheme;

    public SandboxSkillBarView(
      Font font,
      RectTransform host,
      Action<int> onToggle,
      Func<int, bool> isActive,
      Action onAutoCastToggle = null,
      Func<bool> isAutoCastOn = null,
      Action onResetCooldowns = null)
    {
      _font = font;
      _host = host;
      _onToggle = onToggle;
      _isActive = isActive;
      _onAutoCastToggle = onAutoCastToggle;
      _isAutoCastOn = isAutoCastOn;
      _onResetCooldowns = onResetCooldowns;
    }

    public void Refresh(string weaponTheme)
    {
      if (_currentTheme != weaponTheme || _panel == null)
        Rebuild(weaponTheme);
      else
        RefreshActiveStates();
    }

    void Rebuild(string weaponTheme)
    {
      _currentTheme = weaponTheme;
      _buttons.Clear();
      _buttonImages.Clear();
      _buttonSlots.Clear();

      if (_panel != null)
        UnityEngine.Object.Destroy(_panel);

      _panel = new GameObject("SkillCastPanel", typeof(RectTransform));
      _panel.transform.SetParent(_host, false);
      Stretch(_panel.GetComponent<RectTransform>());

      var bg = _panel.AddComponent<Image>();
      bg.color = new Color(0.10f, 0.14f, 0.18f, 0.95f);

      var hasSkills = weaponTheme == "mage";

      CreateLabel(_panel.transform, "SkillHeader", "技能释放",
        new Vector2(0f, 1f), new Vector2(1f, 1f),
        new Vector2(8f, -4f), new Vector2(-8f, HeaderH),
        13, Color.white, FontStyle.Bold);

      if (hasSkills)
      {
        var autoOn = _isAutoCastOn != null && _isAutoCastOn();
        _autoCastLabel = CreateLabel(_panel.transform, "AutoCastLabel",
          autoOn ? "自动: 开" : "自动: 关",
          new Vector2(1f, 1f), new Vector2(1f, 1f),
          new Vector2(-52f, -(HeaderH + 2f)), new Vector2(52f, ToolbarH - 4f),
          10, TextDim, FontStyle.Normal);
        _autoCastLabel.alignment = TextAnchor.MiddleRight;

        CreateButton(_panel.transform, "AutoCastBtn", "自动",
          new Vector2(1f, 1f), new Vector2(1f, 1f),
          new Vector2(-98f, -(HeaderH + 2f)), new Vector2(40f, ToolbarH - 4f),
          autoOn ? ActiveBg : IdleBg,
          () =>
          {
            _onAutoCastToggle?.Invoke();
            if (_autoCastLabel != null && _isAutoCastOn != null)
            {
              var on = _isAutoCastOn();
              _autoCastLabel.text = on ? "自动: 开" : "自动: 关";
            }
          });

        CreateButton(_panel.transform, "ResetCdBtn", "清CD",
          new Vector2(1f, 1f), new Vector2(1f, 1f),
          new Vector2(-142f, -(HeaderH + 2f)), new Vector2(40f, ToolbarH - 4f),
          IdleBg,
          () => _onResetCooldowns?.Invoke());
      }

      CreateLabel(_panel.transform, "SkillHint",
        hasSkills
          ? "键盘 1-4  |  护体/引力井：开关  |  飞弹/新星：即时"
          : "法师 可使用主动技能",
        new Vector2(0f, 1f), new Vector2(1f, 1f),
        new Vector2(8f, -(HeaderH + ToolbarH + 2f)), new Vector2(-8f, HintH),
        9, TextDim, FontStyle.Normal);

      var contentGo = new GameObject("Content", typeof(RectTransform));
      contentGo.transform.SetParent(_panel.transform, false);
      _contentRoot = contentGo.GetComponent<RectTransform>();
      _contentRoot.anchorMin = Vector2.zero;
      _contentRoot.anchorMax = Vector2.one;
      _contentRoot.offsetMin = new Vector2(6f, 6f);
      _contentRoot.offsetMax = new Vector2(-6f, -ContentTop);

      if (!hasSkills)
      {
        CreateLabel(_contentRoot, "SkillPlaceholder", "当前职业无主动技能槽位",
          Vector2.zero, Vector2.one,
          Vector2.zero, Vector2.zero,
          11, TextDim, FontStyle.Normal);
        return;
      }

      PlayerClassSkillDatabase.EnsureLoaded();
      var set = PlayerClassSkillDatabase.Get(weaponTheme);
      if (set?.slots == null || set.slots.Length == 0)
      {
        CreateLabel(_contentRoot, "SkillPlaceholder", "未找到 player_class_skills 配置",
          Vector2.zero, Vector2.one,
          Vector2.zero, Vector2.zero,
          11, TextDim, FontStyle.Normal);
        return;
      }

      BuildSkillList(set);
      RefreshActiveStates();
    }

    void BuildSkillList(PlayerClassSkillDatabase.ClassSkillSet set)
    {
      var scrollGo = new GameObject("SkillScroll", typeof(RectTransform));
      scrollGo.transform.SetParent(_contentRoot, false);
      Stretch(scrollGo.GetComponent<RectTransform>());

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false;
      scroll.vertical = true;

      var viewport = new GameObject("Viewport", typeof(RectTransform));
      viewport.transform.SetParent(scrollGo.transform, false);
      var viewportRt = viewport.GetComponent<RectTransform>();
      Stretch(viewportRt);
      viewport.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.35f);
      viewport.AddComponent<Mask>().showMaskGraphic = false;

      var content = new GameObject("Content", typeof(RectTransform));
      content.transform.SetParent(viewport.transform, false);
      var contentRt = content.GetComponent<RectTransform>();
      contentRt.anchorMin = new Vector2(0f, 1f);
      contentRt.anchorMax = new Vector2(1f, 1f);
      contentRt.pivot = new Vector2(0.5f, 1f);
      contentRt.anchoredPosition = Vector2.zero;

      scroll.viewport = viewportRt;
      scroll.content = contentRt;

      var y = 0f;
      const float btnH = 36f;
      const float gap = 4f;

      foreach (var slot in set.slots)
      {
        if (slot == null || slot.slot < 1 || slot.slot > 4)
          continue;

        var index = slot.slot - 1;
        var name = !string.IsNullOrEmpty(slot.display_name) ? slot.display_name : slot.id;
        var meta = BuildSlotMeta(slot);
        var label = $"{slot.slot}. {name}  ·  {meta}";
        var captured = index;

        var btn = CreateButton(content.transform, $"Skill_{slot.slot}", label,
          new Vector2(0f, 1f), new Vector2(1f, 1f),
          new Vector2(0f, y), new Vector2(0f, btnH),
          IdleBg,
          () => _onToggle?.Invoke(captured));

        _buttons.Add(btn);
        _buttonImages.Add(btn.GetComponent<Image>());
        _buttonSlots.Add(captured);
        y -= btnH + gap;
      }

      contentRt.sizeDelta = new Vector2(0f, Mathf.Max(40f, -y));
    }

    static string BuildSlotMeta(PlayerClassSkillDatabase.SkillSlotDef slot)
    {
      var parts = new List<string>();
      if (slot.cooldown > 0f)
        parts.Add($"CD {slot.cooldown:0.#}s");
      if (slot.base_radius > 0f)
        parts.Add($"R {slot.base_radius:0.#}m");
      if (slot.duration > 0f)
        parts.Add($"{slot.duration:0.#}s");

      var mode = slot.kind switch
      {
        "ground_aura" => "开关",
        "gravity_well" or "tidal_pulse" or "frost_ward" => "开关",
        _ => "即时"
      };
      parts.Add(mode);
      return string.Join(" ", parts);
    }

    public void RefreshActiveStates()
    {
      for (var i = 0; i < _buttonImages.Count; i++)
      {
        var active = _isActive != null && _isActive(_buttonSlots[i]);
        _buttonImages[i].color = active ? ActiveBg : IdleBg;
      }

      if (_autoCastLabel != null && _isAutoCastOn != null)
      {
        var on = _isAutoCastOn();
        _autoCastLabel.text = on ? "自动: 开" : "自动: 关";
      }
    }

    static void Stretch(RectTransform rt)
    {
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
    }

    Text CreateLabel(Transform parent, string name, string text,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
      int fontSize, Color color, FontStyle style)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0f, 1f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;

      var label = go.AddComponent<Text>();
      label.font = _font;
      label.fontSize = fontSize;
      label.fontStyle = style;
      label.alignment = TextAnchor.MiddleLeft;
      label.color = color;
      label.text = text;
      label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap;
      label.verticalOverflow = VerticalWrapMode.Overflow;
      return label;
    }

    Button CreateButton(Transform parent, string name, string label,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color bg,
      UnityEngine.Events.UnityAction onClick)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0f, 1f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;

      var img = go.AddComponent<Image>();
      img.color = bg;

      var btn = go.AddComponent<Button>();
      btn.targetGraphic = img;

      var textGo = new GameObject("Label", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      Stretch(textGo.GetComponent<RectTransform>());

      var text = textGo.AddComponent<Text>();
      text.font = _font;
      text.fontSize = 10;
      text.alignment = TextAnchor.MiddleLeft;
      text.color = Color.white;
      text.text = label;
      text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Truncate;

      if (onClick != null)
        btn.onClick.AddListener(onClick);

      return btn;
    }
  }
}
