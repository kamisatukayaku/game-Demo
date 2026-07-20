using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>运行时 Sprite 统一走 Unlit，避免 Global Light 2D 未配置时 Lit 精灵不可见。</summary>
  public static class SpriteMaterialUtility
  {
    static Material s_unlit;

    public static Material UnlitSpriteMaterial
    {
      get
      {
        if (s_unlit != null)
          return s_unlit;

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
          shader = Shader.Find("Sprites/Default");

        s_unlit = new Material(shader) { name = "RuntimeSpriteUnlit" };
        return s_unlit;
      }
    }

    public static void ApplyUnlit(SpriteRenderer renderer)
    {
      if (renderer == null)
        return;

      renderer.sharedMaterial = UnlitSpriteMaterial;
    }
  }
}
