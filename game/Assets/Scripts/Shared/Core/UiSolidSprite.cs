using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>1×1 白块 Sprite，供 UI Image 着色（避免无 Sprite 时渲染异常）。</summary>
  public static class UiSolidSprite
  {
    static Sprite s_sprite;

    public static Sprite White => s_sprite ??= Create();

    static Sprite Create()
    {
      var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };
      tex.SetPixel(0, 0, Color.white);
      tex.Apply(false, true);
      return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
  }
}
