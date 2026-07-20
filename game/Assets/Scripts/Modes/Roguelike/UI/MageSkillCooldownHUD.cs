using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  [DisallowMultipleComponent]
  public sealed class MageSkillCooldownHUD : MonoBehaviour
  {
    sealed class SkillSlotUi
    {
      public int SlotIndex;
      public GameObject Root;
      public Image CooldownFill;
      public Text CooldownText;
    }

    static MageSkillCooldownHUD s_instance;
    readonly List<SkillSlotUi> _slots = new();
    GameObject _panel;
    PlayerActiveSkillController _skills;
    float _nextSearchTime;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      new GameObject("_MageSkillCooldownHUD").AddComponent<MageSkillCooldownHUD>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildUI();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      RefreshSkillControllerRef();
      var anyVisible = false;
      for (var i = 0; i < _slots.Count; i++)
      {
        var slot = _slots[i];
        var unlocked = _skills != null && _skills.IsSlotUnlocked(slot.SlotIndex);
        if (slot.Root.activeSelf != unlocked)
          slot.Root.SetActive(unlocked);
        if (!unlocked)
          continue;

        anyVisible = true;
        var remaining = _skills.GetCooldownRemaining(slot.SlotIndex);
        var duration = _skills.GetCooldownDuration(slot.SlotIndex);
        slot.CooldownFill.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        slot.CooldownText.text = remaining > 0.05f ? remaining.ToString("0.0") : string.Empty;
      }

      if (_panel.activeSelf != anyVisible)
        _panel.SetActive(anyVisible);
    }

    void RefreshSkillControllerRef()
    {
      if (_skills != null && Time.unscaledTime < _nextSearchTime)
        return;

      _nextSearchTime = Time.unscaledTime + 0.5f;
      var player = GameObject.FindGameObjectWithTag("Player");
      if (player != null)
        _skills = player.GetComponent<PlayerActiveSkillController>();
    }

    void BuildUI()
    {
      var skillClassId = ResolveSkillClassId();

      var canvasGo = new GameObject("MageSkillCooldownCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 325);

      _panel = new GameObject("SkillSlots", typeof(RectTransform));
      _panel.transform.SetParent(canvasGo.transform, false);
      var panelRt = (RectTransform)_panel.transform;
      panelRt.anchorMin = new Vector2(0.5f, 0f);
      panelRt.anchorMax = new Vector2(0.5f, 0f);
      panelRt.pivot = new Vector2(0.5f, 0f);
      panelRt.anchoredPosition = new Vector2(0f, 28f);
      panelRt.sizeDelta = new Vector2(220f, 78f);
      var layout = _panel.AddComponent<HorizontalLayoutGroup>();
      layout.spacing = 12f;
      layout.childAlignment = TextAnchor.MiddleCenter;
      layout.childControlWidth = false;
      layout.childControlHeight = false;

      PlayerClassSkillDatabase.EnsureLoaded();
      var skillSet = PlayerClassSkillDatabase.Get(skillClassId);
      if (skillSet?.slots == null)
      {
        _panel.SetActive(false);
        return;
      }

      foreach (var slotDef in skillSet.slots)
      {
        if (slotDef == null || slotDef.slot <= 0)
          continue;
        BuildSlot(slotDef);
      }

      _panel.SetActive(false);
    }

    static string ResolveSkillClassId() =>
      RunBuildState.WeaponTheme == UnifiedBuildBootstrap.WeaponTheme ? "unified" : RunBuildState.WeaponTheme;

    void BuildSlot(PlayerClassSkillDatabase.SkillSlotDef slotDef)
    {
      var slotIndex = slotDef.slot - 1;
      var slot = new GameObject($"Skill{slotDef.slot}", typeof(RectTransform));
      slot.transform.SetParent(_panel.transform, false);
      ((RectTransform)slot.transform).sizeDelta = new Vector2(96f, 74f);
      var background = slot.AddComponent<Image>();
      background.color = new Color(0.035f, 0.08f, 0.13f, 0.88f);
      var outline = slot.AddComponent<Outline>();
      outline.effectColor = new Color(0.35f, 0.82f, 1f, 0.9f);
      outline.effectDistance = new Vector2(1.5f, -1.5f);

      var fillGo = new GameObject("Cooldown", typeof(RectTransform));
      fillGo.transform.SetParent(slot.transform, false);
      var fillRt = (RectTransform)fillGo.transform;
      fillRt.anchorMin = Vector2.zero;
      fillRt.anchorMax = Vector2.one;
      fillRt.offsetMin = Vector2.zero;
      fillRt.offsetMax = Vector2.zero;
      var fill = fillGo.AddComponent<Image>();
      fill.color = new Color(0f, 0f, 0f, 0.72f);
      fill.type = Image.Type.Filled;
      fill.fillMethod = Image.FillMethod.Radial360;
      fill.fillOrigin = 2;
      fill.fillClockwise = false;

      CreateLabel(slot.transform, "Key", $"[{slotDef.slot}]", 15, new Vector2(0f, 20f), FontStyle.Bold);
      var cooldownText = CreateLabel(slot.transform, "Time", string.Empty, 20, new Vector2(0f, 0f), FontStyle.Bold);
      CreateLabel(slot.transform, "Name", slotDef.display_name, 12, new Vector2(0f, -23f), FontStyle.Normal);

      slot.SetActive(false);
      _slots.Add(new SkillSlotUi
      {
        SlotIndex = slotIndex,
        Root = slot,
        CooldownFill = fill,
        CooldownText = cooldownText
      });
    }

    static Text CreateLabel(Transform parent, string objectName, string value, int size, Vector2 position, FontStyle style)
    {
      var go = new GameObject(objectName, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = (RectTransform)go.transform;
      rt.anchorMin = new Vector2(0.5f, 0.5f);
      rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.sizeDelta = new Vector2(92f, 24f);
      rt.anchoredPosition = position;
      var label = go.AddComponent<Text>();
      label.text = value;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = Color.white;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }
  }
}
