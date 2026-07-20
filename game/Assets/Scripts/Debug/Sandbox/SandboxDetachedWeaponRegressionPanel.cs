using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DevTools.Sandbox
{
  /// <summary>Sandbox UI for detached weapon visual regression (not shown in production HUD).</summary>
  public sealed class SandboxDetachedWeaponRegressionPanel
  {
    readonly Font _font;
    readonly Func<SandboxSceneController> _sceneAccessor;
    Text _statusText;

    static readonly (string key, string label) ContactWeapon = ("contact", "Contact");

    static readonly (string group, string key, string label)[] StarterEvolutions =
    {
      ("法师外置", "laser", "Laser"),
      ("法师外置", "pulse", "Pulse"),
      ("射手外置", "missile", "Missile"),
      ("射手外置", "explosion", "Explosion"),
      ("接触外置", "boomerang", "Boomerang"),
      ("接触外置", "trail", "Trail")
    };

    public SandboxDetachedWeaponRegressionPanel(Font font, Func<SandboxSceneController> sceneAccessor)
    {
      _font = font;
      _sceneAccessor = sceneAccessor;
    }

    public void Build(Transform parent)
    {
      CreateLabel(parent, "DetachedVfxHeader", "外置武器回归 (Sandbox 全路线)", 12, FontStyle.Bold,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -2f), new Vector2(-4f, 16f));

      var y = -22f;
      y = AddSection(parent, "基础", y);
      y = AddWeaponRow(parent, ContactWeapon.key, ContactWeapon.label, y);

      var currentGroup = string.Empty;
      foreach (var (group, key, label) in StarterEvolutions)
      {
        if (group != currentGroup)
        {
          currentGroup = group;
          y = AddSection(parent, group, y);
        }

        y = AddWeaponRow(parent, key, label, y);
      }

      y -= 4f;
      y = AddRow(parent, "全路线 x6", y, () => SandboxDetachedWeaponRegressionService.SpawnSixWeapons(_sceneAccessor()));
      y = AddRow(parent, "跨路线 T5", y, () => SandboxDetachedWeaponRegressionService.SpawnHighTierMix(_sceneAccessor()));
      y = AddRow(parent, "Attack", y, () => SandboxDetachedWeaponRegressionService.ForceAttack(_sceneAccessor()));
      y = AddRow(parent, "Kill Target", y, () => SandboxDetachedWeaponRegressionService.KillCurrentTarget(_sceneAccessor()));
      y = AddRow(parent, "3 Targets", y, () => SandboxDetachedWeaponRegressionService.SpawnTargets(_sceneAccessor(), 3));
      y = AddRow(parent, "Recycle Test", y, () => SandboxDetachedWeaponRegressionService.RecycleSpawnTest(_sceneAccessor()));
      y = AddRow(parent, "Clear Weapons", y, () => SandboxDetachedWeaponRegressionService.ClearAllWeapons(_sceneAccessor()));
      y = AddRow(parent, "Reset Pools", y, SandboxDetachedWeaponRegressionService.ResetPresentationPools);
      y = AddRow(parent, "Route Checks", y, RunRouteChecks);
      y = AddRow(parent, "Refresh Stats", y, RefreshStatus);

      var statusGo = new GameObject("DetachedVfxStatus", typeof(RectTransform));
      statusGo.transform.SetParent(parent, false);
      var statusRt = statusGo.GetComponent<RectTransform>();
      statusRt.anchorMin = new Vector2(0f, 0f);
      statusRt.anchorMax = new Vector2(1f, 0f);
      statusRt.pivot = new Vector2(0.5f, 0f);
      statusRt.anchoredPosition = new Vector2(0f, 4f);
      statusRt.sizeDelta = new Vector2(-8f, 52f);
      _statusText = statusGo.AddComponent<Text>();
      _statusText.font = _font;
      _statusText.fontSize = 10;
      _statusText.alignment = TextAnchor.UpperLeft;
      _statusText.color = new Color(0.62f, 0.72f, 0.78f, 1f);
      _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
      _statusText.verticalOverflow = VerticalWrapMode.Overflow;
      _statusText.raycastTarget = false;
      RefreshStatus();
    }

    float AddSection(Transform parent, string title, float y)
    {
      CreateLabel(parent, $"Section_{title}", title, 10, FontStyle.Italic,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, y), new Vector2(-4f, 14f));
      return y - 16f;
    }

    float AddWeaponRow(Transform parent, string key, string label, float y)
    {
      var capturedKey = key;
      CreateButton(parent, $"Spawn_{key}", label,
        new Vector2(0f, 1f), new Vector2(0.48f, 1f), new Vector2(4f, y), new Vector2(-2f, 22f),
        new Color(0.18f, 0.30f, 0.42f, 1f),
        () => Spawn(capturedKey, 1));
      CreateButton(parent, $"Tier5_{key}", "T5",
        new Vector2(0.52f, 1f), new Vector2(1f, 1f), new Vector2(2f, y), new Vector2(-4f, 22f),
        new Color(0.24f, 0.36f, 0.28f, 1f),
        () => Spawn(capturedKey, 5));
      return y - 26f;
    }

    float AddRow(Transform parent, string label, float y, Action action)
    {
      CreateButton(parent, $"Detached_{label}", label,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, y), new Vector2(-4f, 22f),
        new Color(0.22f, 0.32f, 0.42f, 1f),
        () => action?.Invoke());
      return y - 26f;
    }

    void Spawn(string key, int tier)
    {
      SandboxDetachedWeaponRegressionService.ClearAllWeapons(_sceneAccessor());
      SandboxDetachedWeaponRegressionService.SpawnWeapon(_sceneAccessor(), key, tier);
      SandboxDetachedWeaponRegressionService.SpawnTargets(_sceneAccessor(), 2);
      RefreshStatus();
    }

    void RunRouteChecks()
    {
      if (_statusText != null)
        _statusText.text = SandboxDetachedWeaponRegressionService.RunRouteChecks();
    }

    void RefreshStatus()
    {
      if (_statusText == null)
        return;
      var scene = _sceneAccessor();
      _statusText.text =
        $"Route Filter: bypassed in Sandbox\n" +
        $"Lock: {SandboxDetachedWeaponRegressionService.GetLockTargetLabel(scene)}\n" +
        $"{SandboxDetachedWeaponRegressionService.GetIntroStatusLabel()}\n" +
        $"Visuals: {SandboxDetachedWeaponRegressionService.CountDetachedWeaponVisuals()}\n" +
        SandboxDetachedWeaponRegressionService.GetPoolStats();
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
      return btn;
    }
  }
}
