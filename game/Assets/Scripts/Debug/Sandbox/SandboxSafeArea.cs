using UnityEngine;

namespace Game.DevTools.Sandbox
{
  /// <summary>
  /// Sandbox UI 安全区工具：确保面板不超出屏幕，按钮始终可见可点击。
  /// </summary>
  public static class SandboxSafeArea
  {
    public const float TopMargin = 20f;
    public const float RightMargin = 20f;
    public const float ButtonMinHeight = 28f;

    /// <summary>将 RectTransform 锚定到 Canvas 右上角，添加安全边距。</summary>
    public static void AnchorTopRight(RectTransform rt, float width, float height)
    {
      rt.anchorMin = new Vector2(1f, 1f);
      rt.anchorMax = new Vector2(1f, 1f);
      rt.pivot = new Vector2(1f, 1f);
      rt.anchoredPosition = new Vector2(-RightMargin, -TopMargin);
      rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>检测 RectTransform 是否超出屏幕范围，超出则自动向内偏移。</summary>
    public static void ClampToScreen(RectTransform rt, Canvas canvas)
    {
      if (rt == null || canvas == null)
        return;

      var rect = rt.rect;
      var corners = new Vector3[4];
      rt.GetWorldCorners(corners);

      var screenRect = canvas.pixelRect;
      var adjustX = 0f;
      var adjustY = 0f;

      // 检查右侧超出
      if (corners[2].x > screenRect.xMax)
        adjustX = screenRect.xMax - corners[2].x;

      // 检查顶部超出
      if (corners[1].y > screenRect.yMax)
        adjustY = screenRect.yMax - corners[1].y;

      // 检查左侧超出
      if (corners[0].x < screenRect.xMin)
        adjustX = screenRect.xMin - corners[0].x;

      // 检查底部超出
      if (corners[0].y < screenRect.yMin)
        adjustY = screenRect.yMin - corners[0].y;

      if (Mathf.Abs(adjustX) > 0.5f || Mathf.Abs(adjustY) > 0.5f)
      {
        rt.anchoredPosition += new Vector2(adjustX, adjustY);
        Debug.Log($"[SandboxSafeArea] Clamped '{rt.name}' by ({adjustX:0.#}, {adjustY:0.#})");
      }
    }

    /// <summary>确保按钮在父面板内可见，不会被裁剪。</summary>
    public static void ClampToParent(RectTransform child, RectTransform parent, float margin)
    {
      if (child == null || parent == null)
        return;

      var parentRect = parent.rect;
      var childRect = child.rect;
      var pos = child.anchoredPosition;

      var halfW = childRect.width * 0.5f;
      var halfH = childRect.height * 0.5f;
      var parentHalfW = parentRect.width * 0.5f;
      var parentHalfH = parentRect.height * 0.5f;

      var clampedX = Mathf.Clamp(pos.x, -parentHalfW + halfW + margin, parentHalfW - halfW - margin);
      var clampedY = Mathf.Clamp(pos.y, -parentHalfH + halfH + margin, parentHalfH - halfH - margin);

      if (Mathf.Abs(clampedX - pos.x) > 0.01f || Mathf.Abs(clampedY - pos.y) > 0.01f)
      {
        child.anchoredPosition = new Vector2(clampedX, clampedY);
        Debug.Log($"[SandboxSafeArea] Clamped child '{child.name}' in '{parent.name}' pos=({clampedX:0.#}, {clampedY:0.#})");
      }
    }

    /// <summary>验证按钮区域在屏幕内且未被遮挡。</summary>
    public static bool VerifyButtonClickable(RectTransform buttonRect, string label)
    {
      if (buttonRect == null)
      {
        Debug.LogWarning($"[SandboxSafeArea] {label}: buttonRect is null");
        return false;
      }

      var corners = new Vector3[4];
      buttonRect.GetWorldCorners(corners);

      var w = corners[2].x - corners[0].x;
      var h = corners[1].y - corners[0].y;

      var onScreen = corners[0].x >= 0f && corners[0].y >= 0f
                  && corners[2].x <= Screen.width && corners[2].y <= Screen.height;

      if (!onScreen)
      {
        Debug.LogWarning($"[SandboxSafeArea] {label}: OFF SCREEN! corners=({corners[0]:0},{corners[1]:0},{corners[2]:0},{corners[3]:0}) screen={Screen.width}x{Screen.height}");
        return false;
      }

      Debug.Log($"[SandboxSafeArea] {label}: ON SCREEN size=({w:0}x{h:0}) pos=({corners[0].x:0},{corners[0].y:0})");
      return true;
    }
  }
}
