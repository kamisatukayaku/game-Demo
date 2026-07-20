using UnityEngine.UI;
using UnityEngine;

using Game.Shared.UI;

namespace Game.Shared.Combat
{
  /// <summary>
  /// 浮动伤害数字。由 CombatFeedbackManager 对象池管理?
  /// Canvas ScreenSpaceOverlay 下运行，使用 WorldToScreenPoint 转换坐标?
  /// </summary>
  public class DamageNumber : MonoBehaviour
  {
    const float RandomOffsetX = 0.3f;

    Text _text;
    Outline _outline;
    RectTransform _rt;

    float _lifetime;
    float _elapsed;
    float _floatSpeed;
    float _fadeStart;
    Vector3 _worldPos;
    Color _color;
    bool _initialized;

    public bool IsFinished { get; private set; }

    void Awake()
    {
      Initialize();
    }

    void Initialize()
    {
      if (_initialized) return;
      _initialized = true;

      _rt = gameObject.AddComponent<RectTransform>();
      _rt.sizeDelta = new Vector2(140, 40);

      _text = gameObject.AddComponent<Text>();
      _text.font = GetOrCreateFont();
      _text.fontSize = Mathf.RoundToInt(20 * StreamModeSettings.DamageFontScale);
      _text.fontStyle = StreamModeSettings.Enabled ? FontStyle.Bold : FontStyle.Normal;
      _text.resizeTextForBestFit = false;
      _text.alignment = TextAnchor.MiddleCenter;
      _text.raycastTarget = false;

      _rt.sizeDelta = StreamModeSettings.Enabled
        ? new Vector2(200f, 56f)
        : new Vector2(140f, 40f);

      _outline = gameObject.AddComponent<Outline>();
      var outlineStrength = StreamModeSettings.Enabled ? 2.4f : 1.5f;
      _outline.effectColor = StreamModeSettings.Enabled
        ? new Color(0f, 0f, 0f, 0.95f)
        : new Color(0f, 0f, 0f, 0.7f);
      _outline.effectDistance = new Vector2(outlineStrength, -outlineStrength);
    }

    static Font GetOrCreateFont()
    {
      // Strategy 1: Dynamic OS font (most reliable on Windows)
      var font = Font.CreateDynamicFontFromOSFont("Arial", 16);
      if (font != null)
      {
        font.hideFlags = HideFlags.DontSave;
        return font;
      }

      // Strategy 2: Try built-in legacy runtime font
      font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      if (font != null) return font;

      // Strategy 3: Try built-in Arial for compatibility
      font = Resources.GetBuiltinResource<Font>("Arial.ttf");
      if (font != null) return font;

      // Strategy 4: Generic sans-serif fallback on Windows
      font = Font.CreateDynamicFontFromOSFont("Arial", 16);
      if (font != null) font.hideFlags = HideFlags.DontSave;
      return font;
    }

    public void Setup(
      Vector3 worldPos,
      float amount,
      Color color,
      float lifetime,
      float floatSpeed,
      float fadeStart)
    {
      Initialize(); // ensure initialized even if pooled & reactivated

      IsFinished = false;
      _elapsed = 0f;
      _lifetime = lifetime;
      _floatSpeed = floatSpeed;
      _fadeStart = fadeStart;

      _worldPos = worldPos + new Vector3(Random.Range(-RandomOffsetX, RandomOffsetX), 0.4f, 0f);
      _color = StreamModeSettings.BoostColor(color);

      var displayAmount = Mathf.RoundToInt(amount);
      _text.text = displayAmount > 0 ? displayAmount.ToString() : amount.ToString("F1");
      _text.fontSize = Mathf.RoundToInt(20 * StreamModeSettings.DamageFontScale);
      _text.fontStyle = StreamModeSettings.Enabled ? FontStyle.Bold : FontStyle.Normal;
      _text.color = _color;
      _rt.sizeDelta = StreamModeSettings.Enabled
        ? new Vector2(200f, 56f)
        : new Vector2(140f, 40f);

      UpdateScreenPosition();

      // Initial pop scale
      _rt.localScale = Vector3.one * 1.3f;
    }

    void Update()
    {
      if (IsFinished) return;

      _elapsed += Time.deltaTime;

      // Float upward in world space
      _worldPos += Vector3.up * (_floatSpeed * Time.deltaTime);
      UpdateScreenPosition();

      // Scale down from pop
      var t = Mathf.Clamp01(_elapsed / 0.15f);
      var scale = Mathf.Lerp(1.3f, 1f, t);
      _rt.localScale = Vector3.one * scale;

      // Fade near end
      if (_elapsed > _fadeStart)
      {
        var fadeT = (_elapsed - _fadeStart) / Mathf.Max(0.01f, _lifetime - _fadeStart);
        var c = _color;
        c.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(fadeT));
        _text.color = c;
      }

      if (_elapsed >= _lifetime)
      {
        IsFinished = true;
      }
    }

    void UpdateScreenPosition()
    {
      var cam = Camera.main;
      if (cam == null) return;

      // Screen Space Overlay canvas: use WorldToScreenPoint
      var screenPos = cam.WorldToScreenPoint(_worldPos);

      // Only show if in front of camera
      if (screenPos.z <= 0f)
      {
        _text.enabled = false;
        return;
      }

      _text.enabled = true;
      _rt.position = screenPos;
    }
  }
}