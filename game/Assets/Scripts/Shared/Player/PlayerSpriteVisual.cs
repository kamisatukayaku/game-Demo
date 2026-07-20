using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.Visual;
namespace Game.Shared.Player
{
  /// <summary>
  /// 玩家 2D 精灵：Resources/Sprites/Player/player，加粗白边 + 更大体型。
  /// 资源缺失时使用程序生成的占位圆盘。
  /// </summary>
  [DisallowMultipleComponent]
  public class PlayerSpriteVisual : MonoBehaviour
  {
    const string ResourcePath = "Sprites/Player/player";
    const string VisualName = EntityPlaceholderVisual.DefaultChildName;
    const string SpriteChildName = "PlayerSprite";
    const string OutlineChildName = "Outline";
    const float PixelsPerUnit = 128f;
    const float VisualDiameterTiles = 2.565f;
    const float OutlineScaleMultiplier = 1.05f;

    static Sprite s_sprite;
    static Sprite s_placeholderSprite;

    public static void EnsureOnPlayer(GameObject playerRoot)
    {
      if (playerRoot == null)
        return;

      if (playerRoot.GetComponent<PlayerSpriteVisual>() != null)
        return;

      playerRoot.AddComponent<PlayerSpriteVisual>();
    }

    void Awake()
    {
      ApplySpriteVisual();
    }

    void ApplySpriteVisual()
    {
      var sprite = LoadSprite();
      if (sprite == null)
      {
        Debug.LogWarning("[PlayerSpriteVisual] player.png not found in Resources; using placeholder disc.");
        sprite = GetPlaceholderSprite();
      }

      var visual = EnsureVisualRoot();
      var spriteWidth = sprite.bounds.size.x > 0f ? sprite.bounds.size.x : 1f;
      var baseScale = WorldGridConstants.TileSize * VisualDiameterTiles / spriteWidth;
      visual.localScale = Vector3.one * baseScale;

      var spriteRoot = EnsureSpriteRoot(visual);
      EnsureOutlineChild(spriteRoot, sprite);

      var sr = spriteRoot.GetComponent<SpriteRenderer>();
      if (sr == null)
        sr = spriteRoot.gameObject.AddComponent<SpriteRenderer>();

      if (sr == null)
      {
        Debug.LogError("[PlayerSpriteVisual] Failed to add SpriteRenderer on Visual child.");
        return;
      }

      sr.sprite = sprite;
      sr.color = Color.white;
      sr.sortingOrder = 10;
      SpriteMaterialUtility.ApplyUnlit(sr);

      var entityVisual = spriteRoot.gameObject.GetComponent<EntityPlaceholderVisual>();
      if (entityVisual == null)
        entityVisual = spriteRoot.gameObject.AddComponent<EntityPlaceholderVisual>();

      entityVisual.ApplyBaseColor(Color.white);
    }

    Transform EnsureVisualRoot()
    {
      var existing = transform.Find(VisualName);
      if (existing != null)
        return existing;

      var visualGo = new GameObject(VisualName);
      visualGo.transform.SetParent(transform, false);
      visualGo.transform.localPosition = Vector3.zero;
      visualGo.transform.localRotation = Quaternion.identity;
      visualGo.transform.localScale = Vector3.one;
      return visualGo.transform;
    }

    static Transform EnsureSpriteRoot(Transform visual)
    {
      if (visual.GetComponent<SpriteRenderer>() != null)
        return visual;

      var existingRenderer = visual.GetComponent<Renderer>();
      if (existingRenderer == null)
        return visual;

      existingRenderer.enabled = false;
      var legacyOutline = visual.Find(OutlineChildName);
      if (legacyOutline != null)
        legacyOutline.gameObject.SetActive(false);

      var spriteRoot = visual.Find(SpriteChildName);
      if (spriteRoot == null)
      {
        var spriteGo = new GameObject(SpriteChildName);
        spriteGo.transform.SetParent(visual, false);
        spriteRoot = spriteGo.transform;
      }

      spriteRoot.localPosition = Vector3.zero;
      spriteRoot.localRotation = Quaternion.identity;
      spriteRoot.localScale = Vector3.one;
      return spriteRoot;
    }

    static void EnsureOutlineChild(Transform visual, Sprite sprite)
    {
      var outline = visual.Find(OutlineChildName);
      if (outline == null)
      {
        var outlineGo = new GameObject(OutlineChildName);
        outlineGo.transform.SetParent(visual, false);
        outline = outlineGo.transform;
      }

      outline.localPosition = Vector3.zero;
      outline.localRotation = Quaternion.identity;
      outline.localScale = Vector3.one * OutlineScaleMultiplier;

      var outlineSr = outline.GetComponent<SpriteRenderer>();
      if (outlineSr == null)
        outlineSr = outline.gameObject.AddComponent<SpriteRenderer>();

      outlineSr.sprite = sprite;
      outlineSr.color = Color.white;
      outlineSr.sortingOrder = 9;
      SpriteMaterialUtility.ApplyUnlit(outlineSr);
    }

    static Sprite LoadSprite()
    {
      if (s_sprite != null)
        return s_sprite;

      var tex = Resources.Load<Texture2D>(ResourcePath);
      if (tex == null)
        return null;

      s_sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
      s_sprite.name = "player_runtime";
      return s_sprite;
    }

    static Sprite GetPlaceholderSprite()
    {
      if (s_placeholderSprite != null)
        return s_placeholderSprite;

      const int size = 64;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };

      var center = (size - 1) * 0.5f;
      var radius = size * 0.38f;
      var pixels = new Color32[size * size];
      for (var y = 0; y < size; y++)
      {
        for (var x = 0; x < size; x++)
        {
          var dx = x - center;
          var dy = y - center;
          var dist = Mathf.Sqrt(dx * dx + dy * dy);
          var alpha = dist <= radius ? (byte)255 : (byte)0;
          var edge = Mathf.Clamp01(1f - (dist - radius + 1.5f) / 2f);
          pixels[y * size + x] = new Color32(120, 220, 255, (byte)(alpha * edge));
        }
      }

      tex.SetPixels32(pixels);
      tex.Apply(false, true);
      s_placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
      s_placeholderSprite.name = "player_placeholder";
      return s_placeholderSprite;
    }
  }
}
