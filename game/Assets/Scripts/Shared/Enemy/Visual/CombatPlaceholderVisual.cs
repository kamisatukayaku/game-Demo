using UnityEngine;

using Game.Shared.Laser;
using Game.Shared.Vfx;
namespace Game.Shared.Enemy.Visual
{
  /// <summary>
  /// 极简占位外观：Visual 子物体上的颜?网格，供 HitFlash 与运行时生成使用?
  /// </summary>
  [DisallowMultipleComponent]
  public class EntityPlaceholderVisual : MonoBehaviour
  {
    public const string DefaultChildName = "Visual";

    [SerializeField] Color baseColor = Color.white;

    Renderer _meshRenderer;
    SpriteRenderer _spriteRenderer;

    public Color BaseColor => baseColor;

    void Awake()
    {
      CacheRenderers();
      ApplyBaseColor(baseColor);
    }

    void CacheRenderers()
    {
      _meshRenderer = GetComponent<Renderer>();
      _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void ApplyBaseColor(Color color)
    {
      baseColor = color;
      CacheRenderers();

      if (_spriteRenderer != null)
        LaserVfxShared.SetSpriteColor(_spriteRenderer, color);

      if (_meshRenderer != null && Application.isPlaying)
        LaserVfxShared.SetMaterialColor(_meshRenderer.material, color);
    }
  }

  /// <summary>
  /// 运行时怪物/实体程序化外观（球体 mesh 占位，待替换?2D 多边?Sprite）?
  /// </summary>
  public static class CombatPlaceholderVisual
  {
    public const float MinionDisplayScaleMultiplier = 1.5f;
    /// <summary>Gameplay hitbox inset vs sprite edge (0.92 = 8% smaller than visual).</summary>
    public const float CollisionRadiusInset = 0.92f;

    public static float VisualRadiusFromScale(float visualScale) =>
      Mathf.Max(0.25f, visualScale * 0.5f);

    public static float CollisionRadiusFromVisualScale(float visualScale) =>
      VisualRadiusFromScale(visualScale) * CollisionRadiusInset;

    public static float ResolveScale(string enemyId, float dataScale)
    {
      if (dataScale > 0f)
        return dataScale;

      return enemyId switch
      {
        "mob_pent_01" => 1.85f,
        "mob_hex_03" => 1.5f,
        "mob_square_01" => 1.45f,
        "mob_hex_01" => 1.4f,
        "mob_star5_01" => 1.42f,
        "mob_star4_01" => 1.28f,
        "mob_square_02" => 1.22f,
        "mob_star4_02" => 1.18f,
        "mob_tri_01" => 1.2f,
        "mob_tri_05" => 1.35f,
        "wild_boss_hex_king" => 2.6f,
        "wild_boss_pent_colossus" => 2.75f,
        "wild_boss_star_hive" => 2.5f,
        "final_boss_prism_nexus" => 3.5f,
        "mini_boss_hex_sentinel" => 2.0f,
        "mini_boss_star_chorus" => 1.95f,
        "mini_boss_square_jailer" => 2.05f,
        _ => 1.4f
      };
    }

    public static void ApplySphere(GameObject root, float visualScale, Color color)
    {
      if (root == null)
        return;

      var visual = root.transform.Find(EntityPlaceholderVisual.DefaultChildName);
      GameObject visualGo;

      if (visual == null)
      {
        visualGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualGo.name = EntityPlaceholderVisual.DefaultChildName;
        visualGo.transform.SetParent(root.transform, false);
        var col = visualGo.GetComponent<Collider>();
        if (col != null)
          Object.Destroy(col);
      }
      else
      {
        visualGo = visual.gameObject;
      }

      visualGo.transform.localPosition = Vector3.zero;
      visualGo.transform.localRotation = Quaternion.identity;
      visualGo.transform.localScale = Vector3.one * Mathf.Max(0.3f, visualScale);

      var entityVisual = visualGo.GetComponent<EntityPlaceholderVisual>();
      if (entityVisual == null)
        entityVisual = visualGo.AddComponent<EntityPlaceholderVisual>();

      var renderer = visualGo.GetComponent<Renderer>();
      if (renderer != null)
      {
        var mat = renderer.sharedMaterial;
        if (mat == null || mat.shader == null)
        {
          mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
          if (mat.shader == null)
            mat = new Material(Shader.Find("Standard"));
          renderer.sharedMaterial = mat;
        }

        LaserVfxShared.SetMaterialColor(mat, color);
      }

      entityVisual.ApplyBaseColor(color);
    }
  }
}