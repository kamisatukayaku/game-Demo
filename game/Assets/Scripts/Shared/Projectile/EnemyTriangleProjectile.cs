using UnityEngine;

using Game.Shared.Combat.Damage;
namespace Game.Shared.Projectile
{
  /// <summary>程序生成的尖锐三角弹（怪物远程）?/summary>
  public static class EnemyTriangleProjectile
  {
    static Sprite s_triangleSprite;

    public static StraightProjectile Spawn(
      Vector3 origin,
      Transform target,
      in DamageRequest request,
      float speed,
      float scale,
      Color color,
      float hitRadius = 0f,
      string name = "EnemyTriangleProjectile")
    {
      var go = new GameObject(name);
      go.transform.position = origin;

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = GetTriangleSprite();
      sr.color = color;
      sr.sortingOrder = 7;
      go.transform.localScale = Vector3.one * Mathf.Max(0.12f, scale * 2.4f);

      Vector3 dir;
      if (target != null)
      {
        dir = target.position - origin;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
      }
      else
      {
        dir = Vector3.right;
      }

      var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
      go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

      var projectile = go.AddComponent<StraightProjectile>();
      projectile.Launch(origin, target, request, speed, hitRadius: hitRadius, playerOnly: true);
      return projectile;
    }

    public static StraightProjectile SpawnDirectional(
      Vector3 origin,
      Vector3 direction,
      in DamageRequest request,
      float speed,
      float scale,
      Color color,
      float maxRange,
      float hitRadius = 0f,
      string name = "EnemyTriangleProjectile")
    {
      var go = new GameObject(name);
      go.transform.position = origin;

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = GetTriangleSprite();
      sr.color = color;
      sr.sortingOrder = 7;
      go.transform.localScale = Vector3.one * Mathf.Max(0.12f, scale * 2.4f);

      direction.z = 0f;
      if (direction.sqrMagnitude < 0.0001f)
        direction = Vector3.right;
      else
        direction.Normalize();

      var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
      go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

      var projectile = go.AddComponent<StraightProjectile>();
      projectile.LaunchDirectional(origin, direction, request, speed, maxRange, hitRadius: hitRadius, playerOnly: true);
      // Enemy shots commonly enter from outside the camera and must travel into view.
      projectile.SetOffscreenCulling(false);
      return projectile;
    }

    static Sprite GetTriangleSprite()
    {
      if (s_triangleSprite != null)
        return s_triangleSprite;

      const int size = 24;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };

      var pixels = new Color32[size * size];
      var cx = size * 0.5f;

      for (int y = 0; y < size; y++)
      for (int x = 0; x < size; x++)
      {
        var nx = (x - cx) / cx;
        var ny = y / (float)(size - 1);
        var halfWidth = ny * 0.95f;
        var inside = ny > 0.02f && Mathf.Abs(nx) <= halfWidth;
        pixels[y * size + x] = inside
          ? new Color32(255, 255, 255, 255)
          : new Color32(0, 0, 0, 0);
      }

      tex.SetPixels32(pixels);
      tex.Apply();
      s_triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.08f), size);
      return s_triangleSprite;
    }
  }
}
