using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>S4: 开局 2s Build Identity Card — 名称、tagline、主题色。</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaBuildIdentityUI : MonoBehaviour
  {
    const int SortOrder = 340;
    const float DisplayDuration = 1.2f;

    static ArenaBuildIdentityUI s_instance;
    static Sprite s_panelSprite;

    CanvasGroup _group;
    Image _accentBarTop;
    Image _accentBarBottom;
    Text _titleText;
    Text _taglineText;
    Coroutine _showRoutine;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_ArenaBuildIdentityUI");
      go.AddComponent<ArenaBuildIdentityUI>();
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

    public static void ShowForCurrentBuild()
    {
      EnsureExists();
      s_instance?.PlayShow(ArenaBuildBootstrap.SelectedBuildId);
    }

    void PlayShow(string buildId)
    {
      if (_showRoutine != null)
        StopCoroutine(_showRoutine);

      _showRoutine = StartCoroutine(ShowRoutine(buildId));
    }

    IEnumerator ShowRoutine(string buildId)
    {
      var color = ArenaBuildBootstrap.GetIdentityColor(buildId);
      _titleText.text = ArenaBuildBootstrap.GetDisplayName(buildId);
      _taglineText.text = ArenaBuildBootstrap.GetTagline(buildId);
      _titleText.color = Color.Lerp(color, Color.white, 0.35f);
      _taglineText.color = new Color(0.82f, 0.9f, 0.96f, 0.92f);
      _accentBarTop.color = color;
      _accentBarBottom.color = color;
      _group.alpha = 0f;

      const float fadeIn = 0.22f;
      var elapsed = 0f;
      while (elapsed < fadeIn)
      {
        elapsed += Time.unscaledDeltaTime;
        _group.alpha = Mathf.Clamp01(elapsed / fadeIn);
        yield return null;
      }

      _group.alpha = 1f;
      yield return new WaitForSecondsRealtime(DisplayDuration);

      elapsed = 0f;
      const float fadeOut = 0.35f;
      while (elapsed < fadeOut)
      {
        elapsed += Time.unscaledDeltaTime;
        _group.alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);
        yield return null;
      }

      _group.alpha = 0f;
      _showRoutine = null;
    }

    void BuildUI()
    {
      EnsurePanelSprite();

      var canvasGo = new GameObject("BuildIdentityCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      canvasGo.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, SortOrder);

      var root = CreateRect(canvasGo.transform, "IdentityCard", new Vector2(0.5f, 0f), new Vector2(0f, 148f), new Vector2(480f, 132f));
      _group = root.gameObject.AddComponent<CanvasGroup>();
      _group.alpha = 0f;
      _group.blocksRaycasts = false;

      var bg = root.gameObject.AddComponent<Image>();
      bg.sprite = s_panelSprite;
      bg.type = Image.Type.Sliced;
      bg.color = new Color(0.04f, 0.07f, 0.11f, 0.88f);
      bg.raycastTarget = false;

      _accentBarTop = CreateAccentBar(root, "AccentTop", true);
      _accentBarBottom = CreateAccentBar(root, "AccentBottom", false);

      _titleText = CreateLabel(root, "Title", "构筑", 48, FontStyle.Bold, new Vector2(0f, -22f), new Vector2(440f, 64f));
      _taglineText = CreateLabel(root, "Tagline", "", 22, FontStyle.Italic, new Vector2(0f, -86f), new Vector2(440f, 36f));
    }

    static Image CreateAccentBar(RectTransform parent, string name, bool top)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
      rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
      rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
      rt.anchoredPosition = new Vector2(0f, top ? -5f : 5f);
      rt.sizeDelta = new Vector2(-28f, 5f);
      var image = go.AddComponent<Image>();
      image.raycastTarget = false;
      return image;
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchor;
      rt.anchorMax = anchor;
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return rt;
    }

    static Image CreateImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = anchorMin;
      rt.anchorMax = anchorMax;
      rt.pivot = new Vector2(0.5f, 1f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      var image = go.AddComponent<Image>();
      image.raycastTarget = false;
      return image;
    }

    static Text CreateLabel(RectTransform parent, string name, string text, int size, FontStyle style, Vector2 pos, Vector2 dim)
    {
      var rt = CreateRect(parent, name, new Vector2(0.5f, 1f), pos, dim);
      var label = rt.gameObject.AddComponent<Text>();
      label.text = text;
      label.alignment = TextAnchor.MiddleCenter;
      label.raycastTarget = false;
      UiFontHelper.StyleText(label, size, style);
      return label;
    }

    static void EnsurePanelSprite()
    {
      if (s_panelSprite != null)
        return;

      var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
      for (var i = 0; i < 16; i++)
        tex.SetPixel(i % 4, i / 4, Color.white);
      tex.Apply();
      s_panelSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
    }
  }
}
