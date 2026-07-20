using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
  /// <summary>为图鉴 UI 程序化生成小怪多边形 Sprite（不依赖外部贴图）。</summary>
  public static class MinionPreviewSpriteFactory
  {
    const int TextureSize = 128;
    const float OutlinePx = 2f;
    static readonly Dictionary<string, Sprite> s_cache = new();

    public static Sprite Get(string shapeId, Color fill, Color outline)
    {
      if (string.IsNullOrEmpty(shapeId)) shapeId = "square";
      var key = $"{shapeId}|{fill}|{outline}";
      if (s_cache.TryGetValue(key, out var cached)) return cached;
      var verts = BuildVertices(shapeId);
      var tex = Rasterize(verts, fill, outline);
      var sprite = Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
      sprite.name = $"preview_{shapeId}";
      s_cache[key] = sprite;
      return sprite;
    }

    static Vector2[] BuildVertices(string shapeId)
    {
      return shapeId switch
      {
        "tri" => RegularPolygon(3, 0f),
        "square" => RegularPolygon(4, 0f),
        "pent" => RegularPolygon(5, 0f),
        "hex" => RegularPolygon(6, 0f),
        "oct" => RegularPolygon(8, 0f),
        "diamond" => RegularPolygon(4, 45f),
        "star4" => Star(4, 0.42f),
        "star5" => Star(5, 0.45f),
        "star6" => Star(6, 0.48f),
        "hexagram" => Star(6, 0.55f),
        _ => RegularPolygon(4, 0f)
      };
    }

    static Vector2[] RegularPolygon(int sides, float rotationDeg)
    {
      var verts = new Vector2[sides];
      for (var i = 0; i < sides; i++)
      {
        var angle = (360f * i / sides + rotationDeg - 90f) * Mathf.Deg2Rad;
        verts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
      }
      return verts;
    }

    static Vector2[] Star(int points, float innerRatio)
    {
      var verts = new Vector2[points * 2];
      for (var i = 0; i < points * 2; i++)
      {
        var angle = (i * 180f / points - 90f) * Mathf.Deg2Rad;
        var radius = i % 2 == 0 ? 1f : innerRatio;
        verts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
      }
      return verts;
    }

    static Texture2D Rasterize(Vector2[] verts, Color fill, Color outline)
    {
      var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
      var pixels = new Color32[TextureSize * TextureSize];
      var center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
      var radius = TextureSize * 0.42f;
      for (var y = 0; y < TextureSize; y++)
        for (var x = 0; x < TextureSize; x++)
        {
          var uv = (new Vector2(x + 0.5f, y + 0.5f) - center) / radius;
          var inside = PointInPolygon(uv, verts);
          if (!inside) { pixels[y * TextureSize + x] = new Color32(0, 0, 0, 0); continue; }
          var edgeDist = DistanceToEdge(uv, verts);
          pixels[y * TextureSize + x] = edgeDist * radius <= OutlinePx ? (Color32)outline : (Color32)fill;
        }
      tex.SetPixels32(pixels);
      tex.Apply(false, true);
      return tex;
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
      var inside = false;
      for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
      {
        var pi = poly[i]; var pj = poly[j];
        if ((pi.y > p.y) != (pj.y > p.y) && p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 0.00001f) + pi.x) inside = !inside;
      }
      return inside;
    }

    static float DistanceToEdge(Vector2 p, Vector2[] poly)
    {
      var min = float.MaxValue;
      for (var i = 0; i < poly.Length; i++)
      {
        var a = poly[i]; var b = poly[(i + 1) % poly.Length];
        min = Mathf.Min(min, DistancePointToSegment(p, a, b));
      }
      return min;
    }

    static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
      var ab = b - a; var lenSq = ab.sqrMagnitude;
      if (lenSq < 0.00001f) return (p - a).magnitude;
      var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
      return (p - (a + ab * t)).magnitude;
    }
  }
}
