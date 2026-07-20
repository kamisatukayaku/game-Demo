using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Core;
using Game.Shared.Enemy.Database;
namespace Game.Shared.Enemy.Visual
{
  /// <summary>
  /// ?Resources/Sprites/Enemies/Minions/ 加载战斗精灵（源美术?Assets/Sprites/Enemies/Minions/，需同步?Resources）?
  /// </summary>
  public static class CombatSpriteVisual
  {
    const string MinionResourcePrefix = "Sprites/Enemies/Minions/";
    const string BossResourcePrefix = "Sprites/Enemies/Bosses/";
    const float FallbackPixelsPerUnit = 100f;

    static readonly Dictionary<string, Sprite> s_spriteCache = new();

    /// <summary>源美术目?Assets/Sprites/Enemies/Minions/，运行时镜像?Resources?/summary>
    public static string MinionResourcePath(string enemyId) => MinionResourcePrefix + enemyId;

    public static Sprite LoadMinion(string enemyId)
    {
      if (string.IsNullOrEmpty(enemyId))
        return null;

      var cacheKey = $"{enemyId}@sprite";
      if (s_spriteCache.TryGetValue(cacheKey, out var cached))
        return cached;

      var path = MinionResourcePath(enemyId);

      // 贴图导入类型?Sprite 时必?Load<Sprite>，Load<Texture2D> 会返?null?
      var sprite = Resources.Load<Sprite>(path);
      if (sprite != null)
      {
        s_spriteCache[cacheKey] = sprite;
        return sprite;
      }

      var tex = Resources.Load<Texture2D>(path);
      if (tex == null)
      {
        sprite = CreateConfiguredShapeSprite(enemyId);
        if (sprite != null)
        {
          s_spriteCache[cacheKey] = sprite;
          return sprite;
        }

        Debug.LogWarning($"[CombatSpriteVisual] Missing minion sprite and visual definition for '{enemyId}'.");
        return null;
      }

      sprite = Sprite.Create(
        tex,
        new Rect(0, 0, tex.width, tex.height),
        new Vector2(0.5f, 0.5f),
        FallbackPixelsPerUnit);
      sprite.name = enemyId;
      s_spriteCache[cacheKey] = sprite;
      return sprite;
    }

    public static bool ApplyBoss(GameObject root, string bossId, float visualScale)
    {
      if (root == null || string.IsNullOrEmpty(bossId))
        return false;

      var sprite = Resources.Load<Sprite>(BossResourcePrefix + bossId)
                   ?? LoadShapeSpriteForId(bossId);
      if (sprite == null)
      {
        Debug.LogWarning($"[CombatSpriteVisual] Missing boss sprite: {BossResourcePrefix}{bossId}");
        return false;
      }

      var visual = PrepareVisualForSprite(root);
      if (visual == null)
        return false;
      visual.localRotation = Quaternion.identity;

      var diameter = Mathf.Max(0.8f, visualScale);
      var spriteExtent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
      visual.localScale = Vector3.one * (diameter / Mathf.Max(0.01f, spriteExtent));

      var sr = visual.GetComponent<SpriteRenderer>();
      if (sr == null)
        sr = visual.gameObject.AddComponent<SpriteRenderer>();
      if (sr == null)
        return false;

      sr.sprite = sprite;
      sr.color = Color.white;
      sr.sortingLayerName = "Default";
      sr.sortingOrder = 8;

      var entityVisual = visual.GetComponent<EntityPlaceholderVisual>();
      if (entityVisual == null)
        entityVisual = visual.gameObject.AddComponent<EntityPlaceholderVisual>();
      entityVisual.ApplyBaseColor(Color.white);

      var rootPlaceholder = root.GetComponent<EntityPlaceholderVisual>();
      if (rootPlaceholder != null)
      {
        var mr = rootPlaceholder.GetComponent<MeshRenderer>();
        if (mr != null)
          mr.enabled = false;
      }

      return true;
    }

    public static bool ApplyMinion(GameObject root, string enemyId, float visualScale)
    {
      if (root == null || string.IsNullOrEmpty(enemyId))
        return false;

      var sprite = LoadMinion(enemyId);
      if (sprite == null)
        return false;

      var visual = PrepareVisualForSprite(root);
      if (visual == null)
        return false;

      visual.localRotation = Quaternion.identity;

      var diameter = Mathf.Max(0.5f, visualScale);
      var spriteExtent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
      var scale = diameter / Mathf.Max(0.01f, spriteExtent);
      visual.localScale = Vector3.one * scale;

      var sr = visual.GetComponent<SpriteRenderer>();
      if (sr == null)
        sr = visual.gameObject.AddComponent<SpriteRenderer>();

      sr.sprite = sprite;
      sr.color = Color.white;
      sr.flipX = false;
      sr.flipY = false;
      sr.sortingLayerName = "Default";
      sr.sortingOrder = 6;

      var entityVisual = visual.GetComponent<EntityPlaceholderVisual>();
      if (entityVisual == null)
        entityVisual = visual.gameObject.AddComponent<EntityPlaceholderVisual>();

      entityVisual.ApplyBaseColor(Color.white);
      return true;
    }

    static Sprite LoadShapeSpriteForId(string enemyId)
    {
      var cacheKey = $"{enemyId}@shape";
      if (s_spriteCache.TryGetValue(cacheKey, out var cached))
        return cached;

      var sprite = CreateConfiguredShapeSprite(enemyId);
      if (sprite != null)
        s_spriteCache[cacheKey] = sprite;
      return sprite;
    }

    static Sprite CreateConfiguredShapeSprite(string enemyId)
    {
      var visual = EnemyVisualDatabase.GetMinion(enemyId);
      if (visual == null || string.IsNullOrEmpty(visual.shape_id))
      {
        if (!TryResolveShapeFallback(enemyId, out var shapeId, out var palette))
          return null;

        visual = new EnemyVisualDatabase.MinionVisualDef
        {
          enemy_id = enemyId,
          shape_id = shapeId,
          palette = palette
        };
      }

      const int resolution = 128;
      var outer = BuildShapeVertices(visual.shape_id, 0.88f);
      var inner = BuildShapeVertices(visual.shape_id, 0.78f);
      if (outer == null || inner == null)
        return null;

      var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
      {
        name = $"{enemyId}_Generated",
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };
      var pixels = new Color[resolution * resolution];
      var fill = EnemyVisualDatabase.GetFillColor(visual.palette);

      for (var y = 0; y < resolution; y++)
      {
        for (var x = 0; x < resolution; x++)
        {
          var point = new Vector2(
            (x + 0.5f) / resolution * 2f - 1f,
            (y + 0.5f) / resolution * 2f - 1f);
          if (!ContainsPoint(outer, point))
            continue;

          pixels[y * resolution + x] = ContainsPoint(inner, point) ? fill : Color.white;
        }
      }

      texture.SetPixels(pixels);
      texture.Apply();
      var sprite = Sprite.Create(
        texture,
        new Rect(0f, 0f, resolution, resolution),
        new Vector2(0.5f, 0.5f),
        FallbackPixelsPerUnit);
      sprite.name = enemyId;
      return sprite;
    }

    static Vector2[] BuildShapeVertices(string shapeId, float radius)
    {
      var sides = shapeId switch
      {
        "tri" => 3,
        "square" or "diamond" => 4,
        "pent" => 5,
        "hex" => 6,
        "oct" => 8,
        _ => 0
      };
      var starPoints = shapeId switch
      {
        "star4" => 4,
        "star5" => 5,
        "star6" or "hexagram" => 6,
        "star8" => 8,
        _ => 0
      };
      var vertexCount = starPoints > 0 ? starPoints * 2 : sides;
      if (vertexCount <= 0)
        return null;

      var vertices = new Vector2[vertexCount];
      var rotation = shapeId == "diamond" ? 0f : Mathf.PI * 0.5f;
      for (var i = 0; i < vertexCount; i++)
      {
        var vertexRadius = starPoints > 0 && (i & 1) == 1 ? radius * 0.5f : radius;
        var angle = rotation + i * Mathf.PI * 2f / vertexCount;
        vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * vertexRadius;
      }
      return vertices;
    }

    static bool ContainsPoint(Vector2[] polygon, Vector2 point)
    {
      var inside = false;
      for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
      {
        var a = polygon[i];
        var b = polygon[j];
        if ((a.y > point.y) != (b.y > point.y)
            && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
          inside = !inside;
      }
      return inside;
    }

    static bool TryResolveShapeFallback(string enemyId, out string shapeId, out string palette)
    {
      shapeId = null;
      palette = "wild_boss";
      if (string.IsNullOrEmpty(enemyId))
        return false;

      if (enemyId.EndsWith("_part", System.StringComparison.Ordinal))
      {
        shapeId = enemyId switch
        {
          "hex_king_part" => "oct",
          "pent_colossus_part" => "square",
          _ => "hex"
        };
        return true;
      }

      if (enemyId.EndsWith("_phantom", System.StringComparison.Ordinal))
      {
        shapeId = "hex";
        return true;
      }

      shapeId = enemyId switch
      {
        "wild_boss_hex_king" or "mini_boss_hex_sentinel" => "hex",
        "wild_boss_star_hive" or "mini_boss_star_chorus" => "star5",
        "wild_boss_pent_colossus" => "pent",
        "mini_boss_square_jailer" => "square",
        "final_boss_prism_nexus" => "hex",
        _ => null
      };
      return !string.IsNullOrEmpty(shapeId);
    }

    static Transform PrepareVisualForSprite(GameObject root)
    {
      var existing = root.transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (existing != null)
      {
        var go = existing.gameObject;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
          Object.DestroyImmediate(collider);

        DestroyMeshComponents(go);
        return existing;
      }

      return EnsureVisualTransform(root);
    }

    static Transform EnsureVisualTransform(GameObject root)
    {
      var visual = root.transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual != null)
        return visual;

      var visualGo = new GameObject(EntityPlaceholderVisual.DefaultChildName);
      visualGo.transform.SetParent(root.transform, false);
      return visualGo.transform;
    }

    static void DestroyMeshComponents(GameObject visualGo)
    {
      var meshRenderer = visualGo.GetComponent<MeshRenderer>();
      if (meshRenderer != null)
        Object.DestroyImmediate(meshRenderer);

      var meshFilter = visualGo.GetComponent<MeshFilter>();
      if (meshFilter != null)
        Object.DestroyImmediate(meshFilter);
    }
  }
}
