using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Laser;
namespace Game.Shared.Vfx
{
  /// <summary>
  /// 冲锋预警箭头：LineRenderer 箭杆 + Sprite 尖端，对象池复用?
  /// Update 中仅同步位置/方向/长度与闪烁，?Instantiate?
  /// </summary>
  [DisallowMultipleComponent]
  public class ChargeWarningIndicator : MonoBehaviour
  {
    const float UrgentFlashWindow = 0.3f;
    const float GrowInDuration = 0.12f;
    const float ShaftSortingOrder = 31;
    const float EdgeSortingOrder = 32;
    const float TipSortingOrder = 33;
    const float DepthZ = -0.04f;

    static readonly Color ShaftRed = new(1f, 0.12f, 0.08f, 0.92f);
    static readonly Color EdgeWhite = new(1f, 1f, 1f, 0.55f);
    static readonly Color TipRed = new(1f, 0.15f, 0.1f, 1f);
    static readonly Color TipEdge = new(1f, 1f, 1f, 0.85f);

    static Material s_shaftMaterial;
    static Material s_edgeMaterial;
    static Sprite s_tipSprite;

    LineRenderer _shaft;
    LineRenderer _edge;
    SpriteRenderer _tip;
    SpriteRenderer _tipEdge;

    float _windupDuration = 0.3f;
    float _elapsed;
    float _fullLength;
    float _currentLength;
    float _appearElapsed;
    Vector3 _origin;
    Vector3 _direction = Vector3.right;
    bool _active;

    public bool IsActive => _active;

    public void Begin(Vector3 origin, Vector3 direction, float fullLength, float windupDuration)
    {
      EnsureBuilt();
      _active = true;
      _elapsed = 0f;
      _appearElapsed = 0f;
      _windupDuration = Mathf.Max(0.05f, windupDuration);
      _fullLength = Mathf.Max(0.35f, fullLength);
      _currentLength = _fullLength * 0.08f;
      _origin = origin;
      _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;

      gameObject.SetActive(true);
      SetRenderersEnabled(true);
      ApplyGeometry();
      ApplyVisualState(0f, 0.35f);
    }

    public void Sync(Vector3 origin, Vector3 direction, float fullLength)
    {
      if (!_active)
        return;

      _origin = origin;
      _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : _direction;
      _fullLength = Mathf.Max(0.35f, fullLength);
    }

    public void Tick(float deltaTime)
    {
      if (!_active)
        return;

      _elapsed += deltaTime;
      _appearElapsed += deltaTime;

      var progress = Mathf.Clamp01(_elapsed / _windupDuration);
      var growT = Mathf.Clamp01((_elapsed - GrowInDuration * 0.15f) / Mathf.Max(0.01f, _windupDuration * 0.72f));
      growT = 1f - (1f - growT) * (1f - growT);
      _currentLength = Mathf.Lerp(_fullLength * 0.08f, _fullLength, growT);

      ApplyGeometry();
      ApplyVisualState(progress, ComputeFlashAlpha(progress));
    }

    public void HideImmediate()
    {
      _active = false;
      SetRenderersEnabled(false);
      gameObject.SetActive(false);
      ChargeWarningIndicatorPool.Release(this);
    }

    void EnsureBuilt()
    {
      if (_shaft != null)
        return;

      _shaft = CreateLine("Shaft", GetShaftMaterial(), ShaftSortingOrder, 0.22f);
      _edge = CreateLine("Edge", GetEdgeMaterial(), EdgeSortingOrder, 0.28f);

      var tipEdgeGo = new GameObject("TipEdge");
      tipEdgeGo.transform.SetParent(transform, false);
      _tipEdge = tipEdgeGo.AddComponent<SpriteRenderer>();
      _tipEdge.sprite = GetTipSprite();
      _tipEdge.color = TipEdge;
      _tipEdge.sortingLayerName = LaserVfxShared.SortingLayerName;
      _tipEdge.sortingOrder = (int)TipSortingOrder - 1;
      _tipEdge.transform.localScale = new Vector3(0.52f, 0.52f, 1f);

      var tipGo = new GameObject("Tip");
      tipGo.transform.SetParent(transform, false);
      _tip = tipGo.AddComponent<SpriteRenderer>();
      _tip.sprite = GetTipSprite();
      _tip.color = TipRed;
      _tip.sortingLayerName = LaserVfxShared.SortingLayerName;
      _tip.sortingOrder = (int)TipSortingOrder;
      _tip.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

      SetRenderersEnabled(false);
    }

    void ApplyGeometry()
    {
      var dir = _direction;
      var origin = _origin;
      origin.z = DepthZ;

      var tipWorldSize = 0.38f;
      var shaftLen = Mathf.Max(0.05f, _currentLength - tipWorldSize * 0.55f);
      var shaftEnd = origin + dir * shaftLen;
      var tipPos = origin + dir * _currentLength;

      _shaft.SetPosition(0, origin);
      _shaft.SetPosition(1, shaftEnd);
      _edge.SetPosition(0, origin);
      _edge.SetPosition(1, shaftEnd);

      var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
      var tipRot = Quaternion.Euler(0f, 0f, angle);
      _tip.transform.SetPositionAndRotation(tipPos, tipRot);
      _tipEdge.transform.SetPositionAndRotation(tipPos, tipRot);
    }

    void ApplyVisualState(float progress, float flashAlpha)
    {
      var appear = Mathf.Clamp01(_appearElapsed / GrowInDuration);
      appear = appear * appear;
      var brightness = Mathf.Lerp(0.55f, 1.15f, progress);

      var shaft = ShaftRed;
      shaft.a *= flashAlpha * appear * brightness;
      _shaft.startColor = _shaft.endColor = shaft;

      var edge = EdgeWhite;
      edge.a *= flashAlpha * appear * brightness * 0.95f;
      _edge.startColor = _edge.endColor = edge;

      var tip = TipRed;
      tip.a *= flashAlpha * appear * brightness;
      _tip.color = tip;

      var tipE = TipEdge;
      tipE.a *= flashAlpha * appear * brightness;
      _tipEdge.color = tipE;
    }

    float ComputeFlashAlpha(float progress)
    {
      var urgent = _windupDuration - _elapsed <= UrgentFlashWindow;
      var flashSpeed = urgent ? 28f : 9f;
      var flashMin = urgent ? 0.35f : 0.58f;
      var flashMax = urgent ? 1f : 0.92f;
      var wave = 0.5f + 0.5f * Mathf.Sin(_elapsed * flashSpeed);
      return Mathf.Lerp(flashMin, flashMax, wave);
    }

    void SetRenderersEnabled(bool enabled)
    {
      if (_shaft != null)
        _shaft.enabled = enabled;
      if (_edge != null)
        _edge.enabled = enabled;
      if (_tip != null)
        _tip.enabled = enabled;
      if (_tipEdge != null)
        _tipEdge.enabled = enabled;
    }

    LineRenderer CreateLine(string name, Material material, float sortingOrder, float width)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);

      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.alignment = LineAlignment.TransformZ;
      line.loop = false;
      line.numCornerVertices = 3;
      line.numCapVertices = 3;
      line.textureMode = LineTextureMode.Stretch;
      line.shadowCastingMode = ShadowCastingMode.Off;
      line.receiveShadows = false;
      line.positionCount = 2;
      line.material = material;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = (int)sortingOrder;
      line.startWidth = line.endWidth = width;
      line.enabled = false;
      return line;
    }

    static Material GetShaftMaterial()
    {
      if (s_shaftMaterial != null)
        return s_shaftMaterial;

      s_shaftMaterial = CreateLineMaterial("ChargeWarningShaft", ShaftRed, 2.4f);
      return s_shaftMaterial;
    }

    static Material GetEdgeMaterial()
    {
      if (s_edgeMaterial != null)
        return s_edgeMaterial;

      s_edgeMaterial = CreateLineMaterial("ChargeWarningEdge", EdgeWhite, 2.8f);
      return s_edgeMaterial;
    }

    static Material CreateLineMaterial(string name, Color color, float emission)
    {
      var shader = Shader.Find("Universal Render Pipeline/Unlit")
        ?? Shader.Find("Sprites/Default");

      var mat = new Material(shader) { name = name, color = color };
      mat.SetFloat("_Surface", 1f);
      mat.SetFloat("_Blend", 2f);
      mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
      mat.SetFloat("_DstBlend", (float)BlendMode.One);
      mat.SetFloat("_ZWrite", 0f);
      mat.renderQueue = (int)RenderQueue.Transparent;
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", new Color(emission, emission * 0.85f, emission * 0.75f, 1f));
      return mat;
    }

    static Sprite GetTipSprite()
    {
      if (s_tipSprite != null)
        return s_tipSprite;

      const int w = 16;
      const int h = 20;
      var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
        name = "ChargeWarningTip"
      };

      var pixels = new Color32[w * h];
      for (var y = 0; y < h; y++)
      for (var x = 0; x < w; x++)
      {
        var nx = x / (float)(w - 1);
        var ny = (y / (float)(h - 1) - 0.5f) * 2f;
        var inTip = nx >= 0.08f && Mathf.Abs(ny) <= (1f - nx) * 1.05f;
        pixels[y * w + x] = inTip
          ? new Color32(255, 255, 255, 255)
          : new Color32(0, 0, 0, 0);
      }

      tex.SetPixels32(pixels);
      tex.Apply(false, true);
      s_tipSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.08f, 0.5f), 32f);
      s_tipSprite.name = "ChargeWarningTip";
      return s_tipSprite;
    }

    public void SetLayer(int layer)
    {
      SetLayerRecursive(gameObject, layer);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
      if (go == null || layer < 0)
        return;

      go.layer = layer;
      foreach (Transform child in go.transform)
        SetLayerRecursive(child.gameObject, layer);
    }
  }

  /// <summary>预警箭头对象池?/summary>
  public static class ChargeWarningIndicatorPool
  {
    const int InitialCapacity = 48;

    static readonly Stack<ChargeWarningIndicator> s_pool = new();
    static Transform s_root;
    static bool s_ready;

    public static ChargeWarningIndicator Acquire()
    {
      EnsurePool();
      return s_pool.Count > 0 ? s_pool.Pop() : CreateInstance();
    }

    public static void Release(ChargeWarningIndicator indicator)
    {
      if (indicator == null)
        return;

      EnsurePool();
      indicator.transform.SetParent(s_root, false);
      indicator.gameObject.SetActive(false);
      s_pool.Push(indicator);
    }

    static void EnsurePool()
    {
      if (s_ready)
        return;

      s_ready = true;
      var rootGo = new GameObject("ChargeWarningPool");
      Object.DontDestroyOnLoad(rootGo);
      s_root = rootGo.transform;

      for (var i = 0; i < InitialCapacity; i++)
        s_pool.Push(CreateInstance());
    }

    static ChargeWarningIndicator CreateInstance()
    {
      var go = new GameObject("ChargeWarningIndicator");
      go.transform.SetParent(s_root, false);
      go.SetActive(false);
      return go.AddComponent<ChargeWarningIndicator>();
    }
  }
}