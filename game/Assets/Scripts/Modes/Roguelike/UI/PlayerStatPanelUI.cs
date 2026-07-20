using UnityEngine.UI;
using UnityEngine;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;

using Game.Modes.Roguelike.Combat;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike;
using Game.Shared.Core;
using Game.Shared.UI;

namespace Game.UI
{
  /// <summary>
  /// ESC 属性面板：玩家按 ESC 查看当前所有构筑属性。
  /// 仅在无其他 UI（背包/升级/键位设置/预览）时打开。
  /// 打开时暂停战斗。
  /// </summary>
  [DisallowMultipleComponent]
  public class PlayerStatPanelUI : MonoBehaviour
  {
    Canvas _canvas;
    GameObject _backdrop;
    GameObject _panel;
    GameObject _listContent;
    Text _titleText;
    bool _visible;

    static PlayerStatPanelUI s_instance;

    public static bool IsVisible => s_instance != null && s_instance._visible;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_PlayerStatPanel");
      go.AddComponent<PlayerStatPanelUI>();
    }

    public static void HideIfVisible()
    {
      if (s_instance != null && s_instance._visible)
        s_instance.Close();
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
      CreateUI();
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_visible)
            {
                Close();
                return;
            }

            if (LevelUpController.IsWaiting) return;
            if (KeyBindingsUI.IsOpen) return;
            if (MonsterPreviewUI.IsOpen) return;

            Open();
        }
    }

    void Open()
    {
      _visible = true;
      _backdrop.SetActive(true);
      _panel.SetActive(true);
      CombatTimePause.PushPause();
      Refresh();
    }

    void Close()
    {
      _visible = false;
      _backdrop.SetActive(false);
      _panel.SetActive(false);
      CombatTimePause.PopPause();
    }

    void CreateUI()
    {
        var canvasGo = new GameObject("StatPanelCanvas");
        canvasGo.transform.SetParent(transform);
        _canvas = canvasGo.AddComponent<Canvas>();
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFontHelper.ConfigureCanvas(_canvas, scaler, 280);
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasGo.transform, false);
        var backdropRt = backdrop.AddComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        var backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
        backdropImg.raycastTarget = true;
        _backdrop = backdrop;
        _backdrop.SetActive(false);

        _panel = new GameObject("StatPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720, 580);

        var panelBg = _panel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);
        var outline = _panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.75f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        _titleText = CreateLabel(_panel.transform, "角色属性 [ESC]", 24, FontStyle.Bold);
        var titleRt = _titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -10);
        titleRt.sizeDelta = new Vector2(-20, 32);

        CreateMainMenuButton(_panel.transform);

        var hint = CreateLabel(_panel.transform, "按 [ESC] 关闭  |  战斗中暂停", 13, FontStyle.Normal);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0, 52);
        hintRt.sizeDelta = new Vector2(-20, 22);
        hint.color = new Color(0.65f, 0.7f, 0.75f, 1f);

        _listContent = CreateScrollList(_panel.transform, new Vector2(0, -48), new Vector2(690, 438));
        _panel.SetActive(false);
    }

    GameObject CreateScrollList(Transform parent, Vector2 anchoredPos, Vector2 size)
    {
      var scrollGo = new GameObject("StatScroll");
      scrollGo.transform.SetParent(parent, false);
      var scrollRt = scrollGo.AddComponent<RectTransform>();
      scrollRt.anchorMin = new Vector2(0.5f, 1f);
      scrollRt.anchorMax = new Vector2(0.5f, 1f);
      scrollRt.pivot = new Vector2(0.5f, 1f);
      scrollRt.anchoredPosition = anchoredPos;
      scrollRt.sizeDelta = size;

      var scrollBg = scrollGo.AddComponent<Image>();
      scrollBg.color = new Color(0.03f, 0.04f, 0.07f, 0.6f);

      var scroll = scrollGo.AddComponent<ScrollRect>();
      scroll.horizontal = false;
      scroll.movementType = ScrollRect.MovementType.Clamped;

      var viewport = new GameObject("Viewport");
      viewport.transform.SetParent(scrollGo.transform, false);
      var viewportRt = viewport.AddComponent<RectTransform>();
      viewportRt.anchorMin = Vector2.zero;
      viewportRt.anchorMax = Vector2.one;
      viewportRt.offsetMin = Vector2.zero;
      viewportRt.offsetMax = Vector2.zero;
      viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
      viewport.AddComponent<Mask>().showMaskGraphic = false;

      var content = new GameObject("Content");
      content.transform.SetParent(viewport.transform, false);
      var contentRt = content.AddComponent<RectTransform>();
      contentRt.anchorMin = new Vector2(0f, 1f);
      contentRt.anchorMax = new Vector2(1f, 1f);
      contentRt.pivot = new Vector2(0.5f, 1f);
      contentRt.anchoredPosition = Vector2.zero;
      contentRt.sizeDelta = new Vector2(0f, 0f);

      var fitter = content.AddComponent<ContentSizeFitter>();
      fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

      var layout = content.AddComponent<VerticalLayoutGroup>();
      layout.spacing = 2f;
      layout.padding = new RectOffset(8, 8, 8, 8);
      layout.childAlignment = TextAnchor.UpperCenter;
      layout.childControlWidth = true;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;

      scroll.viewport = viewportRt;
      scroll.content = contentRt;

      return content;
    }

    void Refresh()
    {
      if (_listContent == null) return;
      ClearChildren(_listContent.transform);

      var health = GetPlayerHealth();
      var hpCur = health != null ? health.CurrentHp : 0f;
      var hpMax = health != null ? health.MaxHp : 0f;
      var lv = GetCurrentLevel();

      AddSection("基本信息");
      AddRow("武器主题", RunBuildState.WeaponTheme switch
      {
        "warrior" => "战士",
        "melee" => "近战",
        "ranged" => "射手",
        "mage" => "法师",
        _ => "未知"
      });
      AddRow("当前等级", $"Lv.{lv}");
      AddRow("生命值", $"{hpCur:F0} / {hpMax:F0}  ({(hpMax > 0f ? hpCur / hpMax * 100f : 0):F1}%)");
      AddRow("成长进度", $"技能T{RunBuildState.SkillTier}  属性T{RunBuildState.PlayerTier}");

      // ── 攻击 ──
      AddSection("攻击");
      AddRow("武器伤害", $"×{RunBuildState.GetWeaponDamageMult():F2}");
      AddRow("攻击速度", $"×{RunBuildState.GetWeaponAttackSpeedMult():F2}");
      AddRow("攻击范围", $"+{RunBuildState.GetWeaponRangeAdd():F2}");
      AddRow("额外弹体", $"{RunBuildState.GetWeaponExtraProjectiles()}");
      AddRowIf("暴击率", $"{RunBuildState.GetCritChance() * 100f:F1}%", RunBuildState.GetCritChance());
      AddRowIf("暴击伤害", $"×{RunBuildState.GetCritDamageMult():F2}", RunBuildState.GetStat(StatKeys.CritDamageMult));
      AddRowIf("全伤害", $"×{RunBuildState.GetAllDamageMult():F2}", RunBuildState.GetStat(StatKeys.AllDamageMult));

      // ── 技能 ──
      if (RunBuildState.SkillTier > 0 || RunBuildState.GetStat(StatKeys.SkillDamageMult) > 0f)
      {
        AddSection("技能");
        AddRowIf("技能伤害", $"×{RunBuildState.GetSkillDamageMult():F2}", RunBuildState.GetStat(StatKeys.SkillDamageMult));
        AddRowIf("冷却缩减", $"{RunBuildState.GetSkillCooldownReduce() * 100f:F0}%", RunBuildState.GetSkillCooldownReduce());
        AddRowIf("技能范围", $"×{RunBuildState.GetSkillRangeMult():F2}", RunBuildState.GetStat(StatKeys.SkillRangeMult));
        AddRowIf("技能弹道", $"{RunBuildState.GetSkillExtraProjectiles()}", RunBuildState.GetSkillExtraProjectiles());
        AddRowIf("技能暴击率", $"{RunBuildState.GetSkillCritChance() * 100f:F1}%", RunBuildState.GetSkillCritChance());
        AddRowIf("技能暴伤", $"+{RunBuildState.GetSkillCritDamage() * 100f:F0}%", RunBuildState.GetSkillCritDamage());
        AddRowIf("技能减速概率", $"{RunBuildState.GetSkillSlowChance() * 100f:F0}%", RunBuildState.GetSkillSlowChance());
        AddRowIf("技能减速量", $"{RunBuildState.GetSkillSlowAmount() * 100f:F0}%", RunBuildState.GetSkillSlowAmount());
        AddRowIf("技能灼烧", $"{RunBuildState.GetSkillBurnDps():F1}/秒×{RunBuildState.GetSkillBurnDuration():F1}秒", RunBuildState.GetSkillBurnDps());
        AddRowIf("技能连锁", $"{RunBuildState.GetSkillChainCount()}", RunBuildState.GetSkillChainCount());
        AddRowIf("技能穿透", $"{RunBuildState.GetSkillPierce()}", RunBuildState.GetSkillPierce());
        AddRowIf("技能爆炸半径", $"{RunBuildState.GetSkillExplosionRadius():F1}", RunBuildState.GetSkillExplosionRadius());
        AddRowIf("技能回响", "开", RunBuildState.GetSkillEcho());
        AddRowIf("技能黑洞", "开", RunBuildState.GetSkillVacuum());
      }

      // ── 生存 ──
      AddSection("生存");
      AddRowIf("最大生命倍率", $"×{RunBuildState.GetMaxHpMult():F2}", RunBuildState.GetStat(StatKeys.MaxHpMult));
      AddRowIf("生命固定加成", $"+{RunBuildState.GetMaxHpFlat():F0}", RunBuildState.GetMaxHpFlat());
      AddRowIf("生命回复", $"{RunBuildState.GetHpRegen():F2}/秒", RunBuildState.GetHpRegen());
      AddRowIf("吸血", $"{RunBuildState.GetLifesteal() * 100f:F1}%", RunBuildState.GetLifesteal());
      AddRowIf("减伤", $"{RunBuildState.GetDamageReduction() * 100f:F1}%", RunBuildState.GetDamageReduction());
      AddRowIf("全抗性", $"{RunBuildState.GetAllResist() * 100f:F0}%", RunBuildState.GetAllResist());
      AddRowIf("移动速度", $"×{RunBuildState.GetMoveSpeedMult():F2}", RunBuildState.GetStat(StatKeys.MoveSpeedMult));
      AddRowIf("击退抗性", $"{RunBuildState.GetKnockbackResist() * 100f:F0}%", RunBuildState.GetKnockbackResist());
      AddRowIf("溢出护盾", "开", RunBuildState.GetOverhealShield());
      AddRowIf("击杀回复", $"{RunBuildState.GetHealOnKillPct() * 100f:F1}% 最大生命", RunBuildState.GetHealOnKillPct());
      AddRowIf("受击回复", $"{RunBuildState.GetHealOnHitPct() * 100f:F1}% 伤害值", RunBuildState.GetHealOnHitPct());

      // ── 弹体效果 ──
      if (HasProjectileEffects())
      {
        AddSection("弹体效果");
        AddRowIf("弱追踪", $"{RunBuildState.GetProjectileHomingTurnRate():F0}°/秒", RunBuildState.GetProjectileHomingTurnRate() - 92f);
        AddRowIf("尾迹散弹", $"+{RunBuildState.GetProjectileTrailSpray()}/跳", RunBuildState.GetProjectileTrailSpray());
        AddRowIf("命中分裂", $"+{RunBuildState.GetProjectileSplitOnHit()}", RunBuildState.GetProjectileSplitOnHit());
        AddRowIf("重型弹", "开", RunBuildState.GetProjectileHeavyShot());
        AddRowIf("侧向抛射", "开", RunBuildState.GetProjectileSideShed());
        AddRowIf("爆炸半径", $"{RunBuildState.GetProjectileExplosionRadius():F1}", RunBuildState.GetProjectileExplosionRadius());
        AddRowIf("爆炸伤害比", $"{RunBuildState.GetProjectileExplosionRatio() * 100f:F0}%", Mathf.Abs(RunBuildState.GetProjectileExplosionRatio() - 0.5f));
        AddRowIf("连锁数", $"{Mathf.RoundToInt(RunBuildState.GetProjectileChainCount())}", RunBuildState.GetProjectileChainCount());
        AddRowIf("连锁伤害比", $"{RunBuildState.GetProjectileChainDamageRatio() * 100f:F0}%", Mathf.Abs(RunBuildState.GetProjectileChainDamageRatio() - 0.5f));
        AddRowIf("黑洞吸引", "开", RunBuildState.GetExplosionVacuum());
        AddRowIf("减速概率", $"{RunBuildState.GetProjectileSlowChance() * 100f:F0}%", RunBuildState.GetProjectileSlowChance());
        AddRowIf("减速量", $"{RunBuildState.GetProjectileSlowAmount() * 100f:F0}%", RunBuildState.GetProjectileSlowAmount());
        AddRowIf("灼烧", $"{RunBuildState.GetProjectileBurnDps():F1}/秒×{RunBuildState.GetProjectileBurnDuration():F1}秒", RunBuildState.GetProjectileBurnDps());
      }

      // ── 近战效果 ──
      if (HasMeleeEffects())
      {
        AddSection("近战效果");
        AddRowIf("爆炸半径", $"{RunBuildState.GetMeleeExplosionRadius():F1}", RunBuildState.GetMeleeExplosionRadius());
        AddRowIf("击退概率", $"{RunBuildState.GetMeleeKnockbackChance() * 100f:F0}%", RunBuildState.GetMeleeKnockbackChance());
        AddRowIf("减速概率", $"{RunBuildState.GetMeleeSlowChance() * 100f:F0}%", RunBuildState.GetMeleeSlowChance());
        AddRowIf("减速量", $"{RunBuildState.GetMeleeSlowAmount() * 100f:F0}%", RunBuildState.GetMeleeSlowAmount());
        AddRowIf("流血", $"{RunBuildState.GetMeleeBleedDps():F1}/秒×{RunBuildState.GetMeleeBleedDuration():F1}秒", RunBuildState.GetMeleeBleedDps());
        AddRowIf("灼烧", $"{RunBuildState.GetMeleeBurnDps():F1}/秒×{RunBuildState.GetMeleeBurnDuration():F1}秒", RunBuildState.GetMeleeBurnDps());
      }

      // ── 通用伤害 ──
      if (HasGeneralDamageEffects())
      {
        AddSection("通用伤害");
        AddRowIf("爆炸伤害", $"×{RunBuildState.GetExplosionDamageMult():F2}", RunBuildState.GetStat(StatKeys.ExplosionDamageMult));
        AddRowIf("精英伤害", $"×{RunBuildState.GetEliteDamageMult():F2}", RunBuildState.GetStat(StatKeys.EliteDamageMult));
        AddRowIf("Boss伤害", $"×{RunBuildState.GetBossDamageMult():F2}", RunBuildState.GetStat(StatKeys.BossDamageMult));
        AddRowIf("远程伤害", $"×{RunBuildState.GetLongRangeDamageMult():F2}", RunBuildState.GetStat(StatKeys.LongRangeDamageMult));
        AddRowIf("减速目标伤害", $"×{RunBuildState.GetSlowTargetDamageMult():F2}", RunBuildState.GetStat(StatKeys.SlowTargetDamageMult));
      }

      // ── 经济 ──
      if (HasEconomyEffects())
      {
        AddSection("经济");
        AddRowIf("经验获取", $"×{1f + RunBuildState.GetExpGainMult():F2}", RunBuildState.GetExpGainMult());
        AddRowIf("爆炸吸引", "开", RunBuildState.GetExplosionVacuum());
      }
    }

    bool HasProjectileEffects() => RunBuildState.GetProjectileExplosionRadius() > 0f || RunBuildState.GetExplosionVacuum() > 0f || RunBuildState.GetProjectileChainCount() > 0f || RunBuildState.GetProjectileWeakHoming() > 0f || RunBuildState.GetProjectileTrailSpray() > 0 || RunBuildState.GetProjectileSplitOnHit() > 0 || RunBuildState.GetProjectileHeavyShot() > 0f || RunBuildState.GetProjectileSideShed() > 0f || RunBuildState.GetProjectileSlowChance() > 0f || RunBuildState.GetProjectileBurnDps() > 0f;
    bool HasMeleeEffects() => RunBuildState.GetMeleeExplosionRadius() > 0f || RunBuildState.GetMeleeKnockbackChance() > 0f || RunBuildState.GetMeleeSlowChance() > 0f || RunBuildState.GetMeleeBleedDps() > 0f || RunBuildState.GetMeleeBurnDps() > 0f;
    bool HasGeneralDamageEffects() => RunBuildState.GetStat("explosion_damage_mult") > 0f || RunBuildState.GetStat("elite_damage_mult") > 0f || RunBuildState.GetStat("boss_damage_mult") > 0f || RunBuildState.GetStat("long_range_damage_mult") > 0f || RunBuildState.GetStat("slow_target_damage_mult") > 0f;
    bool HasEconomyEffects() => RunBuildState.GetExpGainMult() > 0f || RunBuildState.GetExplosionVacuum() > 0f;

    void AddSection(string text)
    {
      var go = new GameObject("Section"); go.transform.SetParent(_listContent.transform, false);
      var le = go.AddComponent<LayoutElement>(); le.minHeight = 26f; le.preferredHeight = 26f;
      var sepGo = new GameObject("Sep"); sepGo.transform.SetParent(go.transform, false);
      var sepRt = sepGo.AddComponent<RectTransform>(); sepRt.anchorMin = Vector2.zero; sepRt.anchorMax = Vector2.one; sepRt.offsetMin = new Vector2(4, 0); sepRt.offsetMax = new Vector2(-4, 0);
      var sepImg = sepGo.AddComponent<Image>(); sepImg.color = new Color(0.45f, 0.75f, 1f, 0.35f);
      var labelGo = new GameObject("Label"); labelGo.transform.SetParent(go.transform, false);
      var labelRt = labelGo.AddComponent<RectTransform>(); labelRt.anchorMin = new Vector2(0f, 0.5f); labelRt.anchorMax = new Vector2(1f, 0.5f); labelRt.anchoredPosition = Vector2.zero; labelRt.sizeDelta = new Vector2(-16, 22);
      var label = labelGo.AddComponent<Text>(); UiFontHelper.StyleText(label, 15, FontStyle.Bold); label.text = $"-- {text} --"; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(0.5f, 0.82f, 1f, 1f);
    }

    void AddRow(string label, string value)
    {
      var go = new GameObject("Row"); go.transform.SetParent(_listContent.transform, false);
      var le = go.AddComponent<LayoutElement>(); le.minHeight = 22f; le.preferredHeight = 22f;
      var labelGo = new GameObject("Label"); labelGo.transform.SetParent(go.transform, false);
      var labelRt = labelGo.AddComponent<RectTransform>(); labelRt.anchorMin = new Vector2(0f, 0.5f); labelRt.anchorMax = new Vector2(0.42f, 0.5f); labelRt.anchoredPosition = Vector2.zero; labelRt.sizeDelta = Vector2.zero;
      var labelTxt = labelGo.AddComponent<Text>(); UiFontHelper.StyleText(labelTxt, 14, FontStyle.Normal); labelTxt.text = label; labelTxt.alignment = TextAnchor.MiddleRight; labelTxt.color = new Color(0.7f, 0.75f, 0.82f, 1f);
      var valueGo = new GameObject("Value"); valueGo.transform.SetParent(go.transform, false);
      var valueRt = valueGo.AddComponent<RectTransform>(); valueRt.anchorMin = new Vector2(0.44f, 0.5f); valueRt.anchorMax = new Vector2(1f, 0.5f); valueRt.anchoredPosition = Vector2.zero; valueRt.sizeDelta = Vector2.zero;
      var valueTxt = valueGo.AddComponent<Text>(); UiFontHelper.StyleText(valueTxt, 14, FontStyle.Normal); valueTxt.text = value; valueTxt.alignment = TextAnchor.MiddleLeft; valueTxt.color = new Color(0.92f, 0.95f, 1f, 1f);
    }

    void AddRowIf(string label, string value, float condition) { if (condition > 0.001f) AddRow(label, value); }
    void AddRowIf(string label, string value, int condition) { if (condition > 0) AddRow(label, value); }

    void ReturnToMainMenu()
    {
      Close();
      ArenaRunRestart.ReturnToMainMenu();
    }

    void CreateMainMenuButton(Transform parent)
    {
      var go = new GameObject("MainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0f);
      rt.anchorMax = new Vector2(0.5f, 0f);
      rt.pivot = new Vector2(0.5f, 0f);
      rt.anchoredPosition = new Vector2(0f, 14f);
      rt.sizeDelta = new Vector2(220f, 36f);

      var image = go.GetComponent<Image>();
      image.color = new Color(0.14f, 0.22f, 0.30f, 0.95f);

      var btn = go.GetComponent<Button>();
      btn.targetGraphic = image;
      btn.onClick.AddListener(ReturnToMainMenu);

      var labelGo = new GameObject("Label");
      labelGo.transform.SetParent(go.transform, false);
      var labelRt = labelGo.AddComponent<RectTransform>();
      labelRt.anchorMin = Vector2.zero;
      labelRt.anchorMax = Vector2.one;
      labelRt.offsetMin = Vector2.zero;
      labelRt.offsetMax = Vector2.zero;
      var label = labelGo.AddComponent<Text>();
      label.text = "返回主菜单";
      label.alignment = TextAnchor.MiddleCenter;
      label.color = new Color(0.88f, 0.95f, 1f, 1f);
      UiFontHelper.StyleText(label, 15, FontStyle.Bold);
      label.raycastTarget = false;
    }

    static Text CreateLabel(Transform parent, string text, int size, FontStyle style)
    {
      var go = new GameObject("Label"); go.transform.SetParent(parent, false);
      var rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(200, size + 8);
      var label = go.AddComponent<Text>(); label.alignment = TextAnchor.MiddleCenter; label.text = text; UiFontHelper.StyleText(label, size, style); return label;
    }

    static void ClearChildren(Transform root) { for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject); }

    Health GetPlayerHealth() { var player = GameObject.FindGameObjectWithTag("Player"); if (player == null) player = GameObject.Find("Player"); return player != null ? player.GetComponent<Health>() : null; }
    int GetCurrentLevel() { return ExperienceSystem.Level; }
  }
}
