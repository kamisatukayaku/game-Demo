using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;

namespace Game.Modes.Roguelike.Tutorial
{
  /// <summary>World-space ground zone labels with pointer lines (presentation only).</summary>
  public sealed class GroundZoneInfoPresenter : MonoBehaviour
  {
    const int SortOrder = 935;

    static GroundZoneInfoPresenter s_instance;

    readonly List<CalloutView> _active = new();

    Canvas _canvas;
    RectTransform _canvasRt;
    Camera _camera;

    public static GroundZoneInfoPresenter Instance => s_instance;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_GroundZoneInfoPresenter");
      DontDestroyOnLoad(go);
      go.AddComponent<GroundZoneInfoPresenter>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildCanvas();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void BuildCanvas()
    {
      _canvas = gameObject.AddComponent<Canvas>();
      _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      _canvas.sortingOrder = SortOrder;
      var scaler = gameObject.AddComponent<CanvasScaler>();
      gameObject.AddComponent<GraphicRaycaster>();
      UiFontHelper.ConfigureCanvas(_canvas, scaler, SortOrder);
      _canvasRt = _canvas.GetComponent<RectTransform>();
      _camera = Camera.main;
    }

    void LateUpdate()
    {
      for (var i = _active.Count - 1; i >= 0; i--)
      {
        if (_active[i].Tick(_camera, _canvasRt))
          continue;
        _active[i].Destroy();
        _active.RemoveAt(i);
      }
    }

    public void ShowFirstEncounter(string zoneId, Vector2 center, float radius)
    {
      EnsureExists();
      var def = GroundZoneDefinitionDatabase.Get(zoneId);
      var color = GroundZoneDefinitionDatabase.ParseColor(def.color, new Color(0.45f, 0.9f, 0.85f, 1f));
      var duration = def.isHazardous ? 2.5f : 3.2f;
      _active.Add(new CalloutView(transform, def.displayName, def.description, center, color, duration));
    }

    sealed class CalloutView
    {
      readonly GameObject _root;
      readonly RectTransform _labelRt;
      readonly RectTransform _lineRt;
      readonly Image _lineImage;
      readonly Text _title;
      readonly Text _body;
      readonly Vector2 _worldCenter;
      readonly float _duration;
      float _age;

      public CalloutView(
        Transform parent,
        string title,
        string body,
        Vector2 worldCenter,
        Color accent,
        float duration)
      {
        _worldCenter = worldCenter;
        _duration = duration;

        _root = new GameObject("ZoneCallout", typeof(RectTransform));
        _root.transform.SetParent(parent, false);

        _lineRt = CreateRect(_root.transform, "Line", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 40f));
        _lineImage = _lineRt.gameObject.AddComponent<Image>();
        _lineImage.color = new Color(accent.r, accent.g, accent.b, 0.75f);
        _lineImage.raycastTarget = false;

        var label = CreateRect(_root.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 72f));
        _labelRt = label;
        var bg = label.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.08f, 0.12f, 0.82f);
        bg.raycastTarget = false;

        _title = CreateText(label, "Title", title, 16, FontStyle.Bold, new Vector2(0f, 0.52f), new Vector2(1f, 1f), accent);
        _body = CreateText(label, "Body", body, 14, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Color(0.82f, 0.96f, 1f, 1f));
      }

      public bool Tick(Camera camera, RectTransform canvasRt)
      {
        _age += Time.unscaledDeltaTime;
        if (_age >= _duration)
          return false;

        if (camera == null)
          camera = Camera.main;
        if (camera == null || canvasRt == null)
          return true;

        var world = new Vector3(_worldCenter.x, _worldCenter.y, 0f);
        var screen = camera.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
          _root.SetActive(false);
          return true;
        }

        _root.SetActive(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local);
        _labelRt.anchoredPosition = local + new Vector2(0f, 72f);
        _lineRt.anchoredPosition = local + new Vector2(0f, 36f);
        var alpha = _age < 0.25f
          ? _age / 0.25f
          : _age > _duration - 0.35f
            ? (_duration - _age) / 0.35f
            : 1f;
        SetAlpha(alpha);
        return true;
      }

      void SetAlpha(float alpha)
      {
        var cTitle = _title.color;
        cTitle.a = alpha;
        _title.color = cTitle;
        var cBody = _body.color;
        cBody.a = alpha;
        _body.color = cBody;
        var cLine = _lineImage.color;
        cLine.a = alpha * 0.75f;
        _lineImage.color = cLine;
      }

      public void Destroy()
      {
        if (_root != null)
          Object.Destroy(_root);
      }

      static RectTransform CreateRect(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size)
      {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
      }

      static Text CreateText(
        RectTransform parent,
        string name,
        string value,
        int size,
        FontStyle style,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
      {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(8f, 4f);
        rt.offsetMax = new Vector2(-8f, -4f);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        UiFontHelper.StyleText(text, size);
        return text;
      }
    }
  }
}
