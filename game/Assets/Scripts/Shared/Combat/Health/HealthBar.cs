using System.Collections;
using UnityEngine.UI;
using UnityEngine;

using Game.Shared.Combat;
namespace Game.Shared.Combat.Health
{
  /// <summary>
  /// 敌人血条。Canvas ScreenSpaceOverlay 下渲染，跟随世界实体?
  /// 使用 left-anchor + sizeDelta 宽度控制，比 Image.Type.Filled 更可靠?
  /// 一段时间无伤害后渐隐。由 CombatFeedbackManager 管理?
  /// </summary>
  public class HealthBar : MonoBehaviour
  {
    [Header("Size")]
    [SerializeField] float barWidth = 120f;
    [SerializeField] float barHeight = 8f;
    [SerializeField] float bgPadding = 4f;
    [SerializeField] float borderRadius = 3f;

    CanvasGroup _canvasGroup;
    Image _bgImage;
    Image _shieldBorderImage;
    Image _fillImage;
    Image _shieldFillImage;
    RectTransform _fillRt;
    RectTransform _shieldFillRt;
    RectTransform _rt;

    float _hideDelay;
    float _fadeDuration;
    float _hideTimer;
    Coroutine _fadeRoutine;
    bool _visible;

    // Current fill width in pixels (for width-based fill)
    float _fullBarWidth;

    void Awake()
    {
      _fullBarWidth = barWidth;
      BuildUI();
    }

    void BuildUI()
    {
      // ── Root RectTransform ────────────────────────
      _rt = gameObject.AddComponent<RectTransform>();
      var totalW = barWidth + bgPadding * 2f + borderRadius * 2f;
      var totalH = barHeight + bgPadding * 2f;
      _rt.sizeDelta = new Vector2(totalW, totalH);

      _canvasGroup = gameObject.AddComponent<CanvasGroup>();
      _canvasGroup.alpha = 0f;
      _canvasGroup.blocksRaycasts = false;

      // ── Background ────────────────────────────────
      _bgImage = gameObject.AddComponent<Image>();
      _bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.78f);
      _bgImage.raycastTarget = false;
      // Rounded corners via sprite? Not needed for prototype.

      var borderGo = new GameObject("ShieldBorder");
      borderGo.transform.SetParent(transform, false);
      var borderRt = borderGo.AddComponent<RectTransform>();
      borderRt.anchorMin = Vector2.zero;
      borderRt.anchorMax = Vector2.one;
      borderRt.offsetMin = new Vector2(-2f, -2f);
      borderRt.offsetMax = new Vector2(2f, 2f);
      _shieldBorderImage = borderGo.AddComponent<Image>();
      _shieldBorderImage.color = new Color(0.94f, 0.96f, 1f, 0.92f);
      _shieldBorderImage.raycastTarget = false;
      _shieldBorderImage.enabled = false;
      borderGo.transform.SetAsFirstSibling();

      // ── Fill (left-anchored, width = barWidth * percent) ──
      var fillGo = new GameObject("Fill");
      fillGo.transform.SetParent(transform, false);
      _fillRt = fillGo.AddComponent<RectTransform>();

      // Anchor to LEFT edge, full height
      _fillRt.anchorMin = new Vector2(0f, 0f);
      _fillRt.anchorMax = new Vector2(0f, 1f);
      _fillRt.pivot = new Vector2(0f, 0.5f);
      // Offset from left/top/bottom for padding
      _fillRt.anchoredPosition = new Vector2(bgPadding + borderRadius, 0f);
      _fillRt.sizeDelta = new Vector2(_fullBarWidth, -(bgPadding * 2f));

      _fillImage = fillGo.AddComponent<Image>();
      _fillImage.color = new Color(0.2f, 0.85f, 0.25f, 0.95f);
      _fillImage.raycastTarget = false;
      // PLAIN Image ?no Filled type needed, width controls the fill

      var shieldFillGo = new GameObject("ShieldFill");
      shieldFillGo.transform.SetParent(transform, false);
      _shieldFillRt = shieldFillGo.AddComponent<RectTransform>();
      _shieldFillRt.anchorMin = new Vector2(0f, 0f);
      _shieldFillRt.anchorMax = new Vector2(0f, 1f);
      _shieldFillRt.pivot = new Vector2(0f, 0.5f);
      _shieldFillRt.anchoredPosition = new Vector2(bgPadding + borderRadius, 0f);
      _shieldFillRt.sizeDelta = new Vector2(0f, -(bgPadding * 2f));
      _shieldFillImage = shieldFillGo.AddComponent<Image>();
      _shieldFillImage.color = new Color(0.62f, 0.66f, 0.72f, 0.88f);
      _shieldFillImage.raycastTarget = false;
      _shieldFillImage.enabled = false;

      _visible = false;
    }

    public void Setup(float hideDelay, float fadeDuration)
    {
      _hideDelay = hideDelay;
      _fadeDuration = fadeDuration;
    }

    /// <summary>宽度驱动填充?~1。比 Image.fillAmount 可靠得多?/summary>
    public void UpdateFill(float percent)
    {
      if (_fillRt == null) return;

      var clamped = Mathf.Clamp01(percent);

      // Width-based fill: resize the rect
      _fillRt.sizeDelta = new Vector2(_fullBarWidth * clamped, _fillRt.sizeDelta.y);

      // Color gradient: green ?yellow ?red
      if (clamped > 0.55f)
        _fillImage.color = new Color(0.2f, 0.85f, 0.25f, 0.95f);
      else if (clamped > 0.25f)
        _fillImage.color = new Color(0.95f, 0.72f, 0.12f, 0.95f);
      else
        _fillImage.color = new Color(0.88f, 0.18f, 0.18f, 0.95f);
    }

    /// <summary>护盾以血条前方的灰色段 + 白色描边呈现，不再单独显示第二根血条。</summary>
    public void UpdateShieldOverlay(float bossHpPercent, float shieldHp, float bossMaxHp)
    {
      if (_shieldFillImage == null || _shieldBorderImage == null)
        return;

      if (shieldHp <= 0.01f || bossMaxHp <= 0.01f)
      {
        _shieldFillImage.enabled = false;
        _shieldBorderImage.enabled = false;
        return;
      }

      var hpEnd = _fullBarWidth * Mathf.Clamp01(bossHpPercent);
      var shieldWidth = _fullBarWidth * Mathf.Clamp(shieldHp / bossMaxHp, 0f, 1f - bossHpPercent);
      if (shieldWidth <= 0.01f)
      {
        _shieldFillImage.enabled = false;
        _shieldBorderImage.enabled = false;
        return;
      }

      _shieldBorderImage.enabled = true;
      _shieldFillImage.enabled = true;
      _shieldFillRt.anchoredPosition = new Vector2(bgPadding + borderRadius + hpEnd, 0f);
      _shieldFillRt.sizeDelta = new Vector2(shieldWidth, -(bgPadding * 2f));
    }

    public void UpdatePosition(Vector3 worldPos)
    {
      var cam = Camera.main;
      if (cam == null) return;

      var screenPos = cam.WorldToScreenPoint(worldPos);

      // Hide when behind camera
      if (screenPos.z <= 0f)
      {
        if (_canvasGroup.alpha > 0f)
          _canvasGroup.alpha = 0f;
        return;
      }

      _rt.position = screenPos;
    }

    public void Show()
    {
      if (_fadeRoutine != null)
      {
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
      }

      _visible = true;
      _canvasGroup.alpha = 1f;
      _hideTimer = _hideDelay;
    }

    public void Hide(float duration)
    {
      if (!_visible) return;
      _visible = false;

      if (_fadeRoutine != null)
        StopCoroutine(_fadeRoutine);

      if (duration > 0f)
        _fadeRoutine = StartCoroutine(FadeOut(duration));
      else
        _canvasGroup.alpha = 0f;
    }

    public void ResetHideTimer(float delay)
    {
      _hideTimer = delay;
      if (!_visible)
        Show();
    }

    void Update()
    {
      if (!_visible) return;

      _hideTimer -= Time.deltaTime;
      if (_hideTimer <= 0f)
      {
        Hide(_fadeDuration);
      }
    }

    IEnumerator FadeOut(float duration)
    {
      var startAlpha = _canvasGroup.alpha;
      var elapsed = 0f;

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / Mathf.Max(0.01f, duration));
        yield return null;
      }

      _canvasGroup.alpha = 0f;
      _fadeRoutine = null;
    }
  }
}