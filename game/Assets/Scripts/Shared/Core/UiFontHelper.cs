using UnityEngine.UI;
using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>
  /// UI 字体?Canvas 配置。LegacyRuntime.ttf 不含中文且缩放后易糊，改?OS 中文字体?
  /// </summary>
  public static class UiFontHelper
  {
    static Font s_uiFont;

    public static Font GetFont()
    {
      if (s_uiFont != null)
        return s_uiFont;

      s_uiFont = Font.CreateDynamicFontFromOSFont(
        new[]
        {
          "Microsoft YaHei UI",
          "Microsoft YaHei",
          "PingFang SC",
          "Noto Sans CJK SC",
          "Source Han Sans SC",
          "SimHei",
          "Arial Unicode MS",
          "Arial"
        },
        32);

      return s_uiFont;
    }

    public static void ConfigureCanvas(Canvas canvas, CanvasScaler scaler, int sortingOrder)
    {
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = sortingOrder;
      canvas.pixelPerfect = false;

      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920, 1080);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;
      scaler.referencePixelsPerUnit = 100f;
    }

    public static void StyleText(Text text, int fontSize, FontStyle fontStyle = FontStyle.Normal)
    {
      text.font = GetFont();
      text.fontSize = fontSize;
      text.fontStyle = fontStyle;
      text.horizontalOverflow = HorizontalWrapMode.Overflow;
      text.verticalOverflow = VerticalWrapMode.Overflow;

      if (text.GetComponent<Outline>() == null)
      {
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
      }
    }
  }
}