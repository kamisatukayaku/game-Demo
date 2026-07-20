using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Progression.UpgradeRules;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  [DisallowMultipleComponent]
  public sealed class LevelUpCeremonyUI : MonoBehaviour
  {
    const int SortOrder = 1250;
    const float CeremonyIntroSeconds = 0.68f;
    const float PanelIntroSeconds = 0.25f;
    const float CardStagger = 0.08f;

    static LevelUpCeremonyUI s_instance;

    Canvas _canvas;
    CanvasGroup _rootGroup;
    Image _dim;
    RectTransform _panel;
    Text _title;
    Text _hint;
    readonly List<CardView> _cards = new();
    Action<int> _onPick;
    bool _acceptingInput;
    bool _selecting;
    Coroutine _routine;
    LevelUpBurstWorldVfx _burst;

    public static bool IsAcceptingInput => s_instance != null && s_instance._acceptingInput;

    public static void LockInput()
    {
      if (s_instance != null)
        s_instance._acceptingInput = false;
    }

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_LevelUpCeremonyUI");
      DontDestroyOnLoad(go);
      go.AddComponent<LevelUpCeremonyUI>();
    }

    public static void Show(
      int fromLevel,
      int toLevel,
      LevelUpChoiceDatabase.LevelUpOffer offer,
      Action<int> onPick)
    {
      EnsureExists();
      s_instance.ShowInternal(fromLevel, toLevel, offer, onPick);
    }

    public static void Select(int index)
    {
      if (s_instance == null || !s_instance._acceptingInput || s_instance._selecting)
        return;
      s_instance.SelectInternal(index);
    }

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
    public static void ValidationDismiss()
    {
      if (s_instance == null)
        return;

      if (s_instance._routine != null)
      {
        s_instance.StopCoroutine(s_instance._routine);
        s_instance._routine = null;
      }

      s_instance._acceptingInput = false;
      s_instance._selecting = false;
      s_instance._onPick = null;
      s_instance.gameObject.SetActive(false);
      if (s_instance._rootGroup != null)
      {
        s_instance._rootGroup.alpha = 1f;
        s_instance._rootGroup.blocksRaycasts = false;
      }
    }
#endif

    void Awake()
    {
      if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildUI();
      EnsureEventSystem();
      gameObject.SetActive(false);
    }

    void OnDestroy()
    {
      if (s_instance == this) s_instance = null;
    }

    void ShowInternal(
      int fromLevel,
      int toLevel,
      LevelUpChoiceDatabase.LevelUpOffer offer,
      Action<int> onPick)
    {
      if (_routine != null)
        StopCoroutine(_routine);

      _onPick = onPick;
      _acceptingInput = false;
      _selecting = false;
      gameObject.SetActive(true);
      _routine = StartCoroutine(ShowRoutine(fromLevel, toLevel, offer));
    }

    IEnumerator ShowRoutine(int fromLevel, int toLevel, LevelUpChoiceDatabase.LevelUpOffer offer)
    {
      _rootGroup.alpha = 1f;
      _rootGroup.blocksRaycasts = true;
      _dim.color = new Color(0f, 0f, 0f, 0f);
      _panel.localScale = Vector3.zero;
      _panel.gameObject.SetActive(false);
      _title.text = toLevel > fromLevel ? $"升级  Lv.{toLevel}" : "升级";
      ClearCards();

      PlayPlayerBurst();
      StartCoroutine(CameraPulseRoutine());

      var intro = 0f;
      while (intro < CeremonyIntroSeconds)
      {
        intro += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(intro / CeremonyIntroSeconds);
        _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.7f, Mathf.SmoothStep(0f, 1f, t)));
        yield return null;
      }

      BuildCards(offer);
      _panel.gameObject.SetActive(true);
      yield return AnimatePanelIn();

      for (var i = 0; i < _cards.Count; i++)
        StartCoroutine(_cards[i].FlyIn(i * CardStagger));

      yield return new WaitForSecondsRealtime(CardStagger * Mathf.Max(0, _cards.Count - 1) + 0.32f);
      _acceptingInput = true;
      _routine = null;
    }

    IEnumerator AnimatePanelIn()
    {
      var elapsed = 0f;
      while (elapsed < PanelIntroSeconds)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / PanelIntroSeconds);
        _panel.localScale = Vector3.one * EaseOutBack(t);
        yield return null;
      }
      _panel.localScale = Vector3.one;
    }

    void SelectInternal(int index)
    {
      if (index < 0 || index >= _cards.Count)
        return;

      _selecting = true;
      _acceptingInput = false;
      StartCoroutine(SelectRoutine(index));
    }

    IEnumerator SelectRoutine(int index)
    {
      var selected = _cards[index];
      for (var i = 0; i < _cards.Count; i++)
        if (i != index)
          _cards[i].FadeTo(0.22f);

      yield return selected.PlayConfirm();
      PlayPlayerInfusion(selected.AccentColor);

      var elapsed = 0f;
      while (elapsed < 0.18f)
      {
        elapsed += Time.unscaledDeltaTime;
        _rootGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.18f);
        yield return null;
      }

      gameObject.SetActive(false);
      _rootGroup.alpha = 1f;
      _rootGroup.blocksRaycasts = false;
      _onPick?.Invoke(index);
    }

    void BuildUI()
    {
      var canvasGo = new GameObject("LevelUpCeremonyCanvas");
      canvasGo.transform.SetParent(transform, false);
      _canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, SortOrder);
      canvasGo.AddComponent<GraphicRaycaster>();

      _rootGroup = canvasGo.AddComponent<CanvasGroup>();
      _rootGroup.blocksRaycasts = false;

      var dimGo = new GameObject("Dim", typeof(RectTransform));
      dimGo.transform.SetParent(canvasGo.transform, false);
      var dimRt = dimGo.GetComponent<RectTransform>();
      dimRt.anchorMin = Vector2.zero;
      dimRt.anchorMax = Vector2.one;
      dimRt.offsetMin = Vector2.zero;
      dimRt.offsetMax = Vector2.zero;
      _dim = dimGo.AddComponent<Image>();
      _dim.color = Color.clear;

      _panel = CreateRect(canvasGo.transform, "UpgradePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 560f));
      var panelImage = _panel.gameObject.AddComponent<Image>();
      panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.92f);
      var panelOutline = _panel.gameObject.AddComponent<Outline>();
      panelOutline.effectColor = new Color(0.45f, 0.9f, 1f, 0.5f);
      panelOutline.effectDistance = new Vector2(2f, -2f);

      _title = CreateText(_panel, "Title", "升级", 42, FontStyle.Bold, TextAnchor.MiddleCenter,
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(820f, 64f));
      _title.color = Color.white;

      _hint = CreateText(_panel, "Hint", "1 / 2 / 3", 18, FontStyle.Normal, TextAnchor.MiddleCenter,
        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(360f, 32f));
      _hint.color = new Color(0.72f, 0.86f, 0.96f, 0.85f);
    }

    void BuildCards(LevelUpChoiceDatabase.LevelUpOffer offer)
    {
      ClearCards();
      if (offer.choices == null)
        return;

      var count = offer.choices.Length;
      var spacing = 282f;
      var startX = -(count - 1) * spacing * 0.5f;
      for (var i = 0; i < count; i++)
      {
        var card = CardView.Create(_panel, offer.choices[i], i, this);
        card.Rect.anchoredPosition = new Vector2(startX + i * spacing, 8f);
        _cards.Add(card);
      }
    }

    void ClearCards()
    {
      foreach (var card in _cards)
        if (card != null)
          Destroy(card.gameObject);
      _cards.Clear();
    }

    void SetHover(CardView hovered)
    {
      // Hover feedback is owned by the hovered card itself. Other cards keep their
      // original rarity/background treatment so the panel does not flash white.
    }

    void PlayPlayerBurst()
    {
      if (_burst == null)
      {
        var go = new GameObject("LevelUpBurstWorldVfx");
        DontDestroyOnLoad(go);
        _burst = go.AddComponent<LevelUpBurstWorldVfx>();
      }

      var player = GameObject.FindWithTag("Player");
      if (player != null)
        _burst.PlayBurst(player.transform.position, new Color(0.8f, 0.95f, 1f, 1f));
    }

    void PlayPlayerInfusion(Color color)
    {
      if (_burst == null)
        return;

      var player = GameObject.FindWithTag("Player");
      if (player != null)
        _burst.PlayBurst(player.transform.position, color);
    }

    static IEnumerator CameraPulseRoutine()
    {
      var camera = Camera.main;
      if (camera == null)
        yield break;

      var elapsed = 0f;
      var duration = 0.3f;
      var ortho = camera.orthographic;
      var startSize = camera.orthographicSize;
      var startFov = camera.fieldOfView;
      while (elapsed < duration)
      {
        elapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(elapsed / duration);
        var pulse = Mathf.Sin(t * Mathf.PI);
        if (ortho)
          camera.orthographicSize = Mathf.Lerp(startSize, startSize * 0.92f, pulse);
        else
          camera.fieldOfView = Mathf.Lerp(startFov, startFov - 5f, pulse);
        yield return null;
      }

      if (ortho) camera.orthographicSize = startSize;
      else camera.fieldOfView = startFov;
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return rt;
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 rectSize)
    {
      var rt = CreateRect(parent, name, anchorMin, anchorMax, pos, rectSize);
      var label = rt.gameObject.AddComponent<Text>();
      label.font = UiFontHelper.GetFont();
      label.fontSize = size;
      label.fontStyle = style;
      label.alignment = align;
      label.text = text;
      label.raycastTarget = false;
      return label;
    }

    static void EnsureEventSystem()
    {
      if (FindObjectOfType<EventSystem>() != null)
        return;

      var go = new GameObject("EventSystem");
      DontDestroyOnLoad(go);
      go.AddComponent<EventSystem>();
      go.AddComponent<StandaloneInputModule>();
    }

    static float EaseOutBack(float t)
    {
      const float c1 = 1.70158f;
      const float c3 = c1 + 1f;
      t -= 1f;
      return 1f + c3 * t * t * t + c1 * t * t;
    }

    static Color RarityColor(int tier)
    {
      if (tier >= 5) return new Color(1f, 0.76f, 0.18f, 1f);
      if (tier >= 4) return new Color(0.76f, 0.3f, 1f, 1f);
      if (tier >= 2) return new Color(0.28f, 0.68f, 1f, 1f);
      return new Color(0.95f, 0.98f, 1f, 1f);
    }

    static string RarityName(int tier)
    {
      if (tier >= 5) return "传说";
      if (tier >= 4) return "史诗";
      if (tier >= 2) return "稀有";
      return "普通";
    }

    static Color TagAccent(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def?.tags != null)
      {
        foreach (var tag in def.tags)
        {
          if (tag is "fire" or "explosion") return new Color(1f, 0.22f, 0.12f, 1f);
          if (tag is "ice" or "tidal" or "frost") return new Color(0.25f, 0.78f, 1f, 1f);
          if (tag is "lightning" or "chain") return new Color(0.55f, 0.92f, 1f, 1f);
          if (tag is "spin" or "power" or "collision") return new Color(1f, 0.58f, 0.16f, 1f);
          if (tag is "arcane" or "gravity") return new Color(0.62f, 0.34f, 1f, 1f);
        }
      }
      return RarityColor(def != null ? def.tier : 1);
    }

    static string OfferGroupLabel(LevelUpChoiceDatabase.UpgradeDef def)
    {
      if (def == null)
        return "通用";

      return UpgradeOfferGroupPolicy.GetDisplayLabel(def.offer_group);
    }

    static string TagsLabel(string[] tags)
    {
      if (tags == null || tags.Length == 0)
        return "GENERAL";

      var result = "";
      foreach (var tag in tags)
      {
        if (string.IsNullOrEmpty(tag) || tag.EndsWith("_mastery"))
          continue;
        if (result.Length > 0)
          result += " / ";
        result += tag.ToUpperInvariant();
      }
      return result.Length > 0 ? result : "GENERAL";
    }

    sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
      LevelUpCeremonyUI _owner;
      CanvasGroup _group;
      Image _bg;
      Outline _outline;
      Text _rarity;
      Text _key;
      Vector2 _basePos;
      bool _hover;
      float _breathe;

      public RectTransform Rect { get; private set; }
      public Color AccentColor { get; private set; }
      public int Index { get; private set; }

      public static CardView Create(RectTransform parent, LevelUpChoiceDatabase.UpgradeDef def, int index, LevelUpCeremonyUI owner)
      {
        var go = new GameObject($"UpgradeCard_{index + 1}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<CardView>();
        view.Build(def, index, owner);
        return view;
      }

      void Build(LevelUpChoiceDatabase.UpgradeDef def, int index, LevelUpCeremonyUI owner)
      {
        _owner = owner;
        Index = index;
        Rect = GetComponent<RectTransform>();
        Rect.sizeDelta = new Vector2(248f, 360f);
        AccentColor = TagAccent(def);

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        _bg = gameObject.AddComponent<Image>();
        _bg.color = new Color(0.055f, 0.07f, 0.09f, 0.98f);
        _outline = gameObject.AddComponent<Outline>();
        _outline.effectColor = RarityColor(def != null ? def.tier : 1);
        _outline.effectDistance = new Vector2(3f, -3f);

        _key = CreateText(Rect, "Key", $"{index + 1}", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
          new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -25f), new Vector2(38f, 30f));
        _key.color = new Color(0.85f, 0.94f, 1f, 0.95f);

        var title = CreateText(Rect, "Name", def?.display_name ?? "-", 24, FontStyle.Bold, TextAnchor.MiddleCenter,
          new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(210f, 58f));
        title.color = Color.white;

        var icon = CreateText(Rect, "Icon", ResolveIcon(def), 68, FontStyle.Bold, TextAnchor.MiddleCenter,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(160f, 100f));
        icon.color = AccentColor;

        var desc = CreateText(Rect, "Description", def?.description ?? string.Empty, 17, FontStyle.Normal, TextAnchor.UpperCenter,
          new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -68f), new Vector2(210f, 96f));
        desc.color = new Color(0.84f, 0.9f, 0.95f, 1f);

        _rarity = CreateText(Rect, "Rarity", $"{RarityName(def != null ? def.tier : 1)}  {OfferGroupLabel(def)}", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
          new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(218f, 28f));
        _rarity.color = RarityColor(def != null ? def.tier : 1);
      }

      void Update()
      {
        _breathe += Time.unscaledDeltaTime;
        if (_outline != null && _rarity != null)
        {
          var pulse = 0.72f + Mathf.Sin(_breathe * 4f) * 0.18f;
          var c = _rarity.color;
          _outline.effectColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(pulse));
        }
      }

      public IEnumerator FlyIn(float delay)
      {
        yield return new WaitForSecondsRealtime(delay);
        _basePos = Rect.anchoredPosition;
        Rect.anchoredPosition = _basePos + new Vector2(0f, 170f);
        Rect.localScale = Vector3.one * 0.8f;
        var elapsed = 0f;
        while (elapsed < 0.28f)
        {
          elapsed += Time.unscaledDeltaTime;
          var t = Mathf.Clamp01(elapsed / 0.28f);
          var s = Mathf.SmoothStep(0f, 1f, t);
          Rect.anchoredPosition = Vector2.Lerp(_basePos + new Vector2(0f, 170f), _basePos, s);
          Rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, s);
          _group.alpha = s;
          yield return null;
        }
        Rect.anchoredPosition = _basePos;
        Rect.localScale = Vector3.one;
        _group.alpha = 1f;
      }

      public void FadeTo(float alpha)
      {
        _group.alpha = alpha;
      }

      public void SetDimmed(bool dim)
      {
        if (_hover) return;
        _group.alpha = dim ? 0.55f : 1f;
      }

      public IEnumerator PlayConfirm()
      {
        var start = Rect.anchoredPosition;
        var elapsed = 0f;
        while (elapsed < 0.16f)
        {
          elapsed += Time.unscaledDeltaTime;
          var t = Mathf.Clamp01(elapsed / 0.16f);
          Rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, Mathf.Sin(t * Mathf.PI * 0.5f));
          yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.24f)
        {
          elapsed += Time.unscaledDeltaTime;
          var t = Mathf.Clamp01(elapsed / 0.24f);
          Rect.anchoredPosition = Vector2.Lerp(start, Vector2.zero, Mathf.SmoothStep(0f, 1f, t));
          Rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.25f, t);
          _group.alpha = Mathf.Lerp(1f, 0.05f, t);
          yield return null;
        }
      }

      public void OnPointerEnter(PointerEventData eventData)
      {
        if (!_owner._acceptingInput || _owner._selecting) return;
        _hover = true;
        _owner.SetHover(this);
        StopAllCoroutines();
        StartCoroutine(HoverRoutine(true));
      }

      public void OnPointerExit(PointerEventData eventData)
      {
        if (!_owner._acceptingInput || _owner._selecting) return;
        _hover = false;
        _owner.SetHover(null);
        StopAllCoroutines();
        StartCoroutine(HoverRoutine(false));
      }

      public void OnPointerClick(PointerEventData eventData)
      {
        Select(Index);
      }

      IEnumerator HoverRoutine(bool enter)
      {
        var startScale = Rect.localScale.x;
        var targetScale = enter ? 1.05f : 1f;
        var startPos = Rect.anchoredPosition;
        var targetPos = _basePos + (enter ? new Vector2(0f, 9f) : Vector2.zero);
        var elapsed = 0f;
        while (elapsed < 0.1f)
        {
          elapsed += Time.unscaledDeltaTime;
          var t = Mathf.Clamp01(elapsed / 0.1f);
          Rect.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);
          Rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
          _group.alpha = 1f;
          yield return null;
        }
      }

      static string ResolveIcon(LevelUpChoiceDatabase.UpgradeDef def)
      {
        if (def?.tags == null) return "◇";
        foreach (var tag in def.tags)
        {
          if (tag is "fire" or "explosion") return "◎";
          if (tag is "ice" or "tidal" or "frost") return "◌";
          if (tag is "lightning" or "chain") return "✦";
          if (tag is "spin" or "power" or "collision") return "⬡";
          if (tag is "arcane" or "gravity") return "◇";
        }
        return "◆";
      }
    }

    sealed class LevelUpBurstWorldVfx : MonoBehaviour
    {
      LineRenderer _ringA;
      LineRenderer _ringB;
      ParticleSystem _particles;
      Material _lineMaterial;

      public void PlayBurst(Vector3 position, Color color)
      {
        if (_ringA == null) Build();
        transform.position = position;
        StopAllCoroutines();
        StartCoroutine(BurstRoutine(color));
      }

      void Build()
      {
        _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "LevelUpBurstLine_Runtime" };
        _ringA = CreateRing("LevelRingA", 0.12f, 140);
        _ringB = CreateRing("LevelRingHex", 0.055f, 141);
        _particles = CreateParticles();
      }

      IEnumerator BurstRoutine(Color color)
      {
        _ringA.enabled = true;
        _ringB.enabled = true;
        Emit(color);
        var elapsed = 0f;
        while (elapsed < 0.62f)
        {
          elapsed += Time.unscaledDeltaTime;
          var t = Mathf.Clamp01(elapsed / 0.62f);
          DrawCircle(_ringA, Mathf.Lerp(0.3f, 3.8f, 1f - Mathf.Pow(1f - t, 2f)), 72);
          DrawCircle(_ringB, Mathf.Lerp(0.2f, 2.6f, Mathf.SmoothStep(0f, 1f, t)), 6);
          var a = Mathf.Sin(t * Mathf.PI);
          _ringA.startColor = Color.white.WithAlpha(a * 0.85f);
          _ringA.endColor = color.WithAlpha(a * 0.45f);
          _ringB.startColor = color.WithAlpha(a * 0.75f);
          _ringB.endColor = Color.white.WithAlpha(a * 0.35f);
          yield return null;
        }
        _ringA.enabled = false;
        _ringB.enabled = false;
      }

      void Emit(Color color)
      {
        _particles.Clear(true);
        for (var i = 0; i < 46; i++)
        {
          var dir = UnityEngine.Random.insideUnitCircle.normalized;
          var emit = new ParticleSystem.EmitParams
          {
            position = Vector3.zero,
            velocity = new Vector3(dir.x, dir.y, 0f) * UnityEngine.Random.Range(2.5f, 6.5f),
            startLifetime = UnityEngine.Random.Range(0.25f, 0.55f),
            startSize = UnityEngine.Random.Range(0.04f, 0.1f),
            startColor = Color.Lerp(color, Color.white, UnityEngine.Random.Range(0.2f, 0.7f)).WithAlpha(0.9f),
            rotation = UnityEngine.Random.Range(0f, 360f)
          };
          _particles.Emit(emit, 1);
        }
      }

      LineRenderer CreateRing(string name, float width, int sort)
      {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.material = _lineMaterial;
        line.startWidth = width;
        line.endWidth = width;
        line.sortingOrder = sort;
        line.enabled = false;
        return line;
      }

      ParticleSystem CreateParticles()
      {
        var go = new GameObject("LevelUpParticles");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f);
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default")) { name = "LevelUpParticles_Runtime" };
        renderer.sortingOrder = 142;
        return ps;
      }

      static void DrawCircle(LineRenderer line, float radius, int segments)
      {
        line.positionCount = segments;
        for (var i = 0; i < segments; i++)
        {
          var angle = i * Mathf.PI * 2f / segments;
          line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
      }
    }
  }

  static class LevelUpCeremonyColorExtensions
  {
    public static Color WithAlpha(this Color color, float alpha)
    {
      color.a = alpha;
      return color;
    }
  }
}
