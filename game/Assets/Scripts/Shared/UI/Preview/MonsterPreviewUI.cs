using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

using Game.Shared.Combat.Damage;
using UnityEngine.UI;
using Game.Shared.Core;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Visual;

namespace Game.UI
{
  /// <summary>开局界面怪物图鉴：当前仅展示小怪攻击方式。</summary>
  public class MonsterPreviewUI : MonoBehaviour
  {
    static MonsterPreviewUI s_instance;
    static readonly Color PanelBg = new(0.08f, 0.12f, 0.16f, 0.98f);
    static readonly Color Accent = new(0.36f, 0.72f, 0.82f, 1f);
    static readonly Color NormalBg = new(0.14f, 0.20f, 0.26f, 1f);
    static readonly Color SelectedBg = new(0.22f, 0.50f, 0.60f, 1f);
    static readonly Color HoverBg = new(0.18f, 0.28f, 0.34f, 1f);
    static readonly Color DetailBg = new(0.06f, 0.09f, 0.12f, 0.90f);
    static readonly Color DisabledBg = new(0.10f, 0.12f, 0.14f, 0.60f);
    static readonly Color TextDim = new(0.65f, 0.75f, 0.82f, 1f);
    const int PreviewCanvasSortOrder = 600;

    Font _font; GameObject _panel; RectTransform _listContent; ScrollRect _listScroll;
    Text _detailTitle; Text _detailAttack; Text _detailDesc; Text _detailStats;
    RectTransform _previewPanel; RawImage _previewView; Image _previewSpriteImage;
    RectTransform _previewSpriteRt; Vector2 _spriteHomeAnchoredPos; MonsterPreviewStage _stage;
    Coroutine _idleRoutine; Coroutine _attackRoutine;
    readonly List<EnemyDatabase.EnemyDef> _minions = new(); string _selectedId;

    public static bool IsOpen => s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;
    public static void Open(Transform parent) { EnsureExists(parent); s_instance.Show(); }
    static void EnsureExists(Transform parent) { if (s_instance != null) return; var go = new GameObject("_MonsterPreviewUI"); go.transform.SetParent(parent, false); go.AddComponent<MonsterPreviewUI>(); }
    void Awake() { if (s_instance != null) { Destroy(gameObject); return; } s_instance = this; _font = UiFontHelper.GetFont(); BuildUI(); Close(); }
    void CleanupPreview() { StopPreviewAnimations(); _stage?.SetActive(false); }
    void OnDestroy() { if (s_instance == this) s_instance = null; StopPreviewAnimations(); _stage?.DisposeStage(); }
    void Update() { if (!IsOpen) return; if (Input.GetKeyDown(KeyCode.Escape)) Close(); }
    void Show() { UiBootstrap.EnsureEventSystem(); LoadData(); RebuildList(); if (_minions.Count > 0) SelectMinion(_minions[0].id); else ClearDetail(); _panel.SetActive(true); _stage?.EnsureBuilt(); _stage?.SetActive(true); }
    void Close() { if (_panel != null) _panel.SetActive(false); CleanupPreview(); }
    void LoadData() { EnemyDatabase.EnsureLoaded(); EnemyVisualDatabase.EnsureLoaded(); AttackProfileDatabase.EnsureLoaded(); EnemyAiProfileDatabase.EnsureLoaded(); _minions.Clear(); _minions.AddRange(EnemyDatabase.GetMinions()); }

    // ==================== UI Building ====================

    void BuildUI()
    {
      var canvasGo = new GameObject("MonsterPreviewCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.sortingOrder = PreviewCanvasSortOrder;
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, PreviewCanvasSortOrder);
      canvasGo.AddComponent<GraphicRaycaster>();

      // Main panel
      _panel = CreatePanel(canvasGo.transform, "MonsterPreviewPanel", PanelBg,
        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 600f)).gameObject;

      // Title
      CreateLabel(_panel.transform, "Title", "怪物预览", 30, FontStyle.Bold,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 40f));

      // Subtitle
      CreateLabel(_panel.transform, "Hint", "查看各小怪的攻击方式与基础数值 · Esc 返回", 14, FontStyle.Normal,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(500f, 22f)).color = TextDim;

      // Tabs
      BuildTabs(_panel.transform);

      // Content area
      var content = CreatePanel(_panel.transform, "Content", Color.clear,
        new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      content.anchorMin = new Vector2(0f, 0f);
      content.anchorMax = new Vector2(1f, 1f);
      content.offsetMin = new Vector2(20f, 60f);
      content.offsetMax = new Vector2(-20f, -80f);

      BuildListColumn(content);
      BuildDetailColumn(content);

      // Back button
      CreateButton(_panel.transform, "BackButton", "返回",
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(160f, 40f),
        NormalBg, Close);
    }

    void BuildTabs(Transform parent)
    {
      var tabRow = CreatePanel(parent, "Tabs", Color.clear,
        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -78f), new Vector2(-40f, 36f));

      CreateButton(tabRow, "TabMinions", "小怪",
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(100f, 32f),
        SelectedBg, null);

      var bossBtn = CreateButton(tabRow, "TabBosses", "Boss（敬请期待）",
        new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 32f),
        DisabledBg, null);
      bossBtn.interactable = false;
    }

    void BuildListColumn(Transform parent)
    {
      var listHost = CreatePanel(parent, "ListHost", NormalBg, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(220f, 0f));
      listHost.anchorMin = new Vector2(0f, 0f); listHost.anchorMax = new Vector2(0f, 1f); listHost.pivot = new Vector2(0f, 0.5f);
      var scrollGo = new GameObject("Scroll", typeof(RectTransform)); scrollGo.transform.SetParent(listHost, false);
      var scrollRt = scrollGo.GetComponent<RectTransform>(); scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
      scrollRt.offsetMin = new Vector2(4f, 4f); scrollRt.offsetMax = new Vector2(-4f, -4f);
      var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
      var viewport = CreatePanel(scrollGo.transform, "Viewport", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false; scroll.viewport = viewport;
      var content = CreatePanel(viewport, "Content", Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f));
      content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
      var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 4f; layout.padding = new RectOffset(4, 4, 4, 4);
      layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
      content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      scroll.content = content; _listContent = content; _listScroll = scroll;
    }

    void BuildDetailColumn(Transform parent)
    {
      var detailHost = CreatePanel(parent, "DetailHost", DetailBg, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      detailHost.anchorMin = new Vector2(0f, 0f); detailHost.anchorMax = new Vector2(1f, 1f);
      detailHost.offsetMin = new Vector2(232f, 0f); detailHost.offsetMax = new Vector2(0f, 0f);
      _previewPanel = CreatePanel(detailHost, "PreviewPanel", new Color(0.04f, 0.06f, 0.08f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -16f), new Vector2(-32f, 180f));
      _previewPanel.anchorMin = new Vector2(0f, 1f); _previewPanel.anchorMax = new Vector2(1f, 1f); _previewPanel.pivot = new Vector2(0.5f, 1f);
      var previewViewGo = new GameObject("PreviewView", typeof(RectTransform)); previewViewGo.transform.SetParent(_previewPanel, false);
      var previewViewRt = previewViewGo.GetComponent<RectTransform>(); previewViewRt.anchorMin = Vector2.zero; previewViewRt.anchorMax = Vector2.one; previewViewRt.offsetMin = Vector2.zero; previewViewRt.offsetMax = Vector2.zero;
      _previewView = previewViewGo.AddComponent<RawImage>(); _previewView.color = Color.white; _previewView.raycastTarget = false;
      var spriteGo = new GameObject("PreviewSprite", typeof(RectTransform)); spriteGo.transform.SetParent(_previewPanel, false);
      _previewSpriteRt = spriteGo.GetComponent<RectTransform>(); _previewSpriteRt.anchorMin = new Vector2(0.5f, 0.5f); _previewSpriteRt.anchorMax = new Vector2(0.5f, 0.5f);
      _previewSpriteRt.pivot = new Vector2(0.5f, 0.5f); _previewSpriteRt.anchoredPosition = Vector2.zero; _spriteHomeAnchoredPos = Vector2.zero;
      _previewSpriteImage = spriteGo.AddComponent<Image>(); _previewSpriteImage.raycastTarget = false; _previewSpriteImage.preserveAspect = true; _previewSpriteImage.color = Color.white;
      _stage = gameObject.AddComponent<MonsterPreviewStage>();
      CreateButton(detailHost, "PlayAttackButton", "播放攻击动画", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(160f, 32f), SelectedBg, OnPlayAttackClicked);
      _detailTitle = CreateLabel(detailHost, "DetailTitle", "", 22, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -252f), new Vector2(-16f, 32f)); _detailTitle.alignment = TextAnchor.MiddleLeft;
      _detailAttack = CreateLabel(detailHost, "DetailAttack", "", 16, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -288f), new Vector2(-16f, 24f)); _detailAttack.alignment = TextAnchor.MiddleLeft; _detailAttack.color = Accent;
      _detailDesc = CreateLabel(detailHost, "DetailDesc", "", 14, FontStyle.Normal, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -320f), new Vector2(-16f, 60f)); _detailDesc.alignment = TextAnchor.UpperLeft; _detailDesc.color = new Color(0.88f, 0.92f, 0.95f, 1f);
      var statsHost = CreatePanel(detailHost, "StatsHost", new Color(0f, 0f, 0f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
      statsHost.offsetMin = new Vector2(16f, 16f); statsHost.offsetMax = new Vector2(-16f, -390f);
      _detailStats = CreateLabel(statsHost, "DetailStats", "", 14, FontStyle.Normal, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f)); _detailStats.alignment = TextAnchor.UpperLeft; _detailStats.color = new Color(0.82f, 0.88f, 0.92f, 1f);
    }

    void RebuildList()
    {
      if (_listContent == null) return;
      for (var i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);
      foreach (var def in _minions)
      {
        var visual = EnemyVisualDatabase.GetMinion(def.id);
        var shapeName = EnemyVisualDatabase.GetShapeDisplayName(visual?.shape_id);
        var attackLabel = GetAttackModeLabel(def.attack_mode);
        var capturedId = def.id;
        var btn = CreateButton(_listContent, $"Minion_{def.id}", $"{shapeName} . {attackLabel}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 48f), NormalBg, () => SelectMinion(capturedId));
        btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
        btn.gameObject.AddComponent<MinionListTag>().EnemyId = def.id;
      }
      var bottomPad = new GameObject("BottomPad", typeof(RectTransform)); bottomPad.transform.SetParent(_listContent, false); bottomPad.AddComponent<LayoutElement>().preferredHeight = 12f;
    }

    void SelectMinion(string enemyId)
    {
      _selectedId = enemyId; RefreshListSelection(); ScrollListToSelected(enemyId);
      var def = EnemyDatabase.Get(enemyId); if (def == null) { ClearDetail(); return; }
      var visual = EnemyVisualDatabase.GetMinion(enemyId);
      var shapeName = EnemyVisualDatabase.GetShapeDisplayName(visual?.shape_id);
      UpdatePreviewVisual(def, visual);
      if (_detailTitle != null) _detailTitle.text = shapeName + FormatTags(def.tags);
      if (_detailAttack != null) _detailAttack.text = $"攻击方式：{GetAttackModeLabel(def.attack_mode)}";
      if (_detailDesc != null) _detailDesc.text = DescribeAttackBehavior(def);
      if (_detailStats != null) _detailStats.text = BuildStatsText(def);
    }

    void RefreshListSelection()
    {
      if (_listContent == null) return;
      foreach (var tag in _listContent.GetComponentsInChildren<MinionListTag>(true))
      { var img = tag.GetComponent<Image>(); if (img != null) img.color = tag.EnemyId == _selectedId ? SelectedBg : NormalBg; }
    }

    void ScrollListToSelected(string enemyId)
    {
      if (_listScroll == null || _listContent == null || string.IsNullOrEmpty(enemyId)) return;
      MinionListTag targetTag = null;
      foreach (var tag in _listContent.GetComponentsInChildren<MinionListTag>(true)) { if (tag.EnemyId == enemyId) { targetTag = tag; break; } }
      if (targetTag == null) return;
      Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
      var item = targetTag.GetComponent<RectTransform>(); var viewport = _listScroll.viewport;
      if (item == null || viewport == null) return;
      var contentHeight = _listContent.rect.height; var viewportHeight = viewport.rect.height;
      if (contentHeight <= viewportHeight + 1f) return;
      var scrollRange = contentHeight - viewportHeight;
      var targetOffset = Mathf.Clamp(-item.anchoredPosition.y - viewportHeight * 0.5f, 0f, scrollRange);
      _listScroll.verticalNormalizedPosition = 1f - targetOffset / scrollRange;
    }

    void ClearDetail()
    {
      if (_detailTitle != null) _detailTitle.text = "暂无数据";
      if (_detailAttack != null) _detailAttack.text = string.Empty; if (_detailDesc != null) _detailDesc.text = string.Empty; if (_detailStats != null) _detailStats.text = string.Empty;
      StopPreviewAnimations(); if (_previewView != null) _previewView.texture = null;
      if (_previewSpriteImage != null) { _previewSpriteImage.sprite = null; _previewSpriteImage.enabled = false; }
    }

    void UpdatePreviewVisual(EnemyDatabase.EnemyDef def, EnemyVisualDatabase.MinionVisualDef visual)
    {
      StopPreviewAnimations(); if (_stage == null) return;
      ApplyPreviewSprite(def); _stage.EnsureBuilt(); _stage.SetEnemy(def);
      if (_previewView != null && _stage.Texture != null) _previewView.texture = _stage.Texture;
      _idleRoutine = StartCoroutine(_stage.PlayIdleMotion(visual?.motion, CreateUiMirror()));
    }

    void ApplyPreviewSprite(EnemyDatabase.EnemyDef def)
    {
      if (_previewSpriteImage == null || _previewSpriteRt == null)
        return;

      var sprite = CombatSpriteVisual.LoadMinion(def.id);
      _previewSpriteImage.sprite = sprite;
      _previewSpriteImage.enabled = sprite != null;

      if (sprite == null)
      {
        Debug.LogWarning(
          $"[MonsterPreviewUI] Sprite missing for '{def.id}'. Sync Assets/Sprites/Enemies/Minions → Resources.");
        return;
      }

      var scale = CombatPlaceholderVisual.ResolveScale(def.id, def.visual_scale)
        * CombatPlaceholderVisual.MinionDisplayScaleMultiplier;
      var size = 64f + scale * 32f;
      _previewSpriteRt.sizeDelta = new Vector2(size, size);
      ResetPreviewSpriteTransform();
    }

    MonsterPreviewStage.PreviewUiMirror CreateUiMirror() => new() { Sprite = _previewSpriteRt, HomeAnchoredPos = _spriteHomeAnchoredPos, PixelsPerWorldUnit = MonsterPreviewStage.DefaultUiPixelsPerWorldUnit };
    void ResetPreviewSpriteTransform() { if (_previewSpriteRt == null) return; _previewSpriteRt.anchoredPosition = _spriteHomeAnchoredPos; _previewSpriteRt.localRotation = Quaternion.identity; _previewSpriteRt.localScale = Vector3.one; }

    void OnPlayAttackClicked()
    {
      if (_stage == null || string.IsNullOrEmpty(_selectedId)) return;
      var def = EnemyDatabase.Get(_selectedId); if (def == null) return;
      if (_attackRoutine != null) StopCoroutine(_attackRoutine); if (_idleRoutine != null) { StopCoroutine(_idleRoutine); _idleRoutine = null; }
      _stage.StopAllEffects();
      var ai = EnemyAiProfileDatabase.Get(def.ai_profile); var profile = AttackProfileDatabase.Get(def.attack_profile_id);
      var windup = ai?.windup_base ?? profile?.windup ?? 0.3f;
      _attackRoutine = def.attack_mode switch
      {
        "charge" => StartCoroutine(PlayChargeAndResume(def, windup, profile?.dash_speed_mult ?? 2.2f)),
        "barrage" => StartCoroutine(PlayBarrageAndResume(def, windup)),
        "laser" => StartCoroutine(PlayLaserAndResume(def, windup)),
        _ => StartCoroutine(PlayGenericAndResume(windup))
      };
    }

    IEnumerator PlayChargeAndResume(EnemyDatabase.EnemyDef def, float windup, float dashMult) { yield return _stage.PlayChargeAttack(windup, dashMult, CreateUiMirror()); ResumeIdleAfterAttack(); }
    IEnumerator PlayBarrageAndResume(EnemyDatabase.EnemyDef def, float windup) { yield return _stage.PlayBarrageAttack(def, windup, CreateUiMirror()); ResumeIdleAfterAttack(); }
    IEnumerator PlayLaserAndResume(EnemyDatabase.EnemyDef def, float windup) { yield return _stage.PlayLaserAttack(windup, def.attack_profile_id, CreateUiMirror()); ResumeIdleAfterAttack(); }
    IEnumerator PlayGenericAndResume(float windup) { yield return new WaitForSeconds(windup); ResumeIdleAfterAttack(); }
    void ResumeIdleAfterAttack() { _attackRoutine = null; if (string.IsNullOrEmpty(_selectedId) || _stage == null) return; var visual = EnemyVisualDatabase.GetMinion(_selectedId); _idleRoutine = StartCoroutine(_stage.PlayIdleMotion(visual?.motion, CreateUiMirror())); }
    void StopPreviewAnimations() { if (_idleRoutine != null) { StopCoroutine(_idleRoutine); _idleRoutine = null; } if (_attackRoutine != null) { StopCoroutine(_attackRoutine); _attackRoutine = null; } _stage?.StopAllEffects(); ResetPreviewSpriteTransform(); }

    static string FormatTags(string[] tags)
    {
      if (tags == null || tags.Length == 0) return string.Empty;
      var sb = new StringBuilder("  ");
      foreach (var tag in tags) { var label = GetTagLabel(tag); if (!string.IsNullOrEmpty(label)) sb.Append('[').Append(label).Append("] "); }
      return sb.ToString().TrimEnd();
    }
    static string GetTagLabel(string tag) => tag switch { "fast" => "敏捷", "heavy" => "厚重", "elite" => "精英", "ranged" => "远程", "mutant" => "变异", _ => tag };

    static string GetAttackModeLabel(string attackMode) =>
      attackMode switch
      {
        "charge" => "冲撞",
        "melee" => "接触",
        "barrage" => "弹幕",
        "laser" => "激光",
        _ => attackMode ?? "未知"
      };

    static string DescribeAttackBehavior(EnemyDatabase.EnemyDef def)
    {
      var profile = AttackProfileDatabase.Get(def.attack_profile_id);
      return def.attack_mode switch
      {
        "charge" => BuildChargeDescription(profile),
        "melee" => "缓慢追踪玩家，进入接触范围后造成碰撞伤害",
        "barrage" => BuildBarrageDescription(profile),
        "laser" => BuildLaserDescription(profile),
        _ => "暂无攻击说明"
      };
    }

    static string BuildChargeDescription(AttackProfileDatabase.AttackProfile profile)
    {
      if (profile == null)
        return "前摇后向玩家冲刺，接触时造成冲击伤害";

      var sb = new StringBuilder("前摇后向玩家方向冲刺");
      sb.Append(profile.dash_speed_mult > 0f ? $"速度 x{profile.dash_speed_mult:0.#}" : "快速位移");
      sb.Append("），接触时造成冲击伤害");

      if (profile.aoe_radius > 0f)
        sb.Append($" 落地附带 {profile.aoe_radius:0.#} 范围冲击");

      return sb.ToString();
    }

    static string BuildBarrageDescription(AttackProfileDatabase.AttackProfile profile)
    {
      if (profile == null)
        return "向玩家方向齐射多枚弹体";

      var count = profile.projectile_count > 0 ? profile.projectile_count : 1;
      var spread = profile.spread_deg > 0f ? $"，散射角 {profile.spread_deg:0.#}°" : string.Empty;
      return $"向玩家方向一次齐射 {count} 枚弹体{spread}";
    }

    static string BuildLaserDescription(AttackProfileDatabase.AttackProfile profile)
    {
      if (profile == null)
        return "发射直线能量激光";

      var pierce = profile.beam_pierce ? "可穿透目标" : "命中首个目标后停止";
      return $"发射直线能量激光（{pierce}）";
    }

    static string BuildStatsText(EnemyDatabase.EnemyDef def)
    {
      var profile = AttackProfileDatabase.Get(def.attack_profile_id);
      var ai = EnemyAiProfileDatabase.Get(def.ai_profile);
      var sb = new StringBuilder();

      sb.AppendLine("【基础属性】");
      sb.AppendLine($"生命：{def.base_hp:0.#}");
      sb.AppendLine($"移速：{def.move_speed:0.#}");
      sb.AppendLine($"接触/基础伤害：{def.base_damage:0.#}");

      if (profile != null)
      {
        sb.AppendLine();
        sb.AppendLine("【攻击参数】");
        sb.AppendLine($"单次伤害：{profile.base_damage:0.#}（{profile.damage_type}）");
        sb.AppendLine($"攻击距离：{profile.range:0.#}");
        sb.AppendLine($"攻击间隔：{profile.cooldown:0.##} 秒");
      }

      if (ai != null)
      {
        sb.AppendLine($"前摇：{ai.windup_base:0.##} 秒");
        sb.AppendLine($"索敌范围：{ai.aggro_range_base:0.#}");
      }

      if (profile != null)
        AppendAttackExtras(sb, def.attack_mode, profile);

      return sb.ToString().TrimEnd();
    }

    static void AppendAttackExtras(StringBuilder sb, string attackMode, AttackProfileDatabase.AttackProfile profile)
    {
      switch (attackMode)
      {
        case "charge": if (profile.dash_speed_mult > 0f) sb.AppendLine($"冲刺倍速：x{profile.dash_speed_mult:0.#}"); if (profile.dash_distance > 0f) sb.AppendLine($"冲刺距离：{profile.dash_distance:0.#}"); if (profile.aoe_radius > 0f) sb.AppendLine($"冲击范围：{profile.aoe_radius:0.#}"); break;
        case "barrage": if (profile.projectile_count > 0) sb.AppendLine($"弹体数量：{profile.projectile_count}"); if (profile.spread_deg > 0f) sb.AppendLine($"散射角：{profile.spread_deg:0.#}°"); if (profile.projectile_speed > 0f) sb.AppendLine($"弹速：{profile.projectile_speed:0.#}"); break;
        case "laser": sb.AppendLine($"穿透：{(profile.beam_pierce ? "是" : "否")}"); if (profile.beam_half_width > 0f) sb.AppendLine($"束宽：{profile.beam_half_width * 2f:0.##}"); break;
      }
    }

    RectTransform CreatePanel(Transform parent, string name, Color bg, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
      rt.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f); rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
      if (bg.a > 0.001f) { var img = go.AddComponent<Image>(); img.color = bg; img.raycastTarget = bg.a > 0.01f; }
      return rt;
    }

    Text CreateLabel(Transform parent, string name, string text, int fontSize, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
      var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(anchorMin.x, anchorMin.y == anchorMax.y ? 0.5f : 1f); rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
      var label = go.AddComponent<Text>(); label.font = _font; label.fontSize = fontSize; label.fontStyle = style;
      label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.text = text; label.raycastTarget = false;
      label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
    }

    Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color bg, UnityEngine.Events.UnityAction onClick)
    {
      var rt = CreatePanel(parent, name, bg, anchorMin, anchorMax, anchoredPos, sizeDelta); var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = rt.GetComponent<Image>();
      var colors = btn.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f); colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f); colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f); btn.colors = colors;
      var textGo = new GameObject("Label", typeof(RectTransform)); textGo.transform.SetParent(rt, false);
      var textRt = textGo.GetComponent<RectTransform>(); textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = new Vector2(8f, 4f); textRt.offsetMax = new Vector2(-8f, -4f);
      var text = textGo.AddComponent<Text>(); text.font = _font; text.fontSize = 14; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = label; text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
      if (onClick != null) btn.onClick.AddListener(onClick); return btn;
    }

    sealed class MinionListTag : MonoBehaviour { public string EnemyId; }
  }
}
