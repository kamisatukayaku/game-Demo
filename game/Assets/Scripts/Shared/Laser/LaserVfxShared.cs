using UnityEngine.Rendering;
using UnityEngine;

using Game.Shared.Enemy.Visual;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 激?VFX 材质与贴图。优?Resources/VFX/Laser/*.mat（Shader Graph），否则 URP Particles/Unlit Additive?
  /// 首次打开项目请运?Game ?Setup Laser VFX (Shader Graph)?
  /// </summary>
  public static class LaserVfxShared
  {
    public const string SortingLayerName = "Default";
    public const float VfxDepthZ = -0.2f;

    public static readonly Color BeamCoreColor = Color.white;
    public static readonly Color BeamGlowColor = new(1f, 0.36f, 0.08f, 0.85f);
    public static readonly Color BeamTailColor = new(1f, 0.48f, 0.12f, 0.45f);
    public static readonly Color ChargeSparkColor = new(1f, 0.98f, 0.88f, 1f);
    public static readonly Color ChargeSquareBright = new(1f, 1f, 1f, 1f);
    public static readonly Color ChargeSquareWarm = new(1f, 0.96f, 0.55f, 0.95f);

    /// <summary>激光发射锚点：优先 SpriteRenderer.bounds 几何中心?/summary>
    public static Vector3 GetOwnerEmissionPoint(Transform owner)
    {
      if (owner == null)
        return Vector3.zero;

      var visual = owner.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual != null)
      {
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
          return sr.bounds.center;

        return visual.position;
      }

      return owner.position;
    }

    public static float GetOwnerVisualRadius(Transform owner)
    {
      if (owner == null)
        return 0.55f;

      var visual = owner.Find(EntityPlaceholderVisual.DefaultChildName);
      if (visual == null)
        return 0.55f;

      var sr = visual.GetComponent<SpriteRenderer>();
      if (sr != null && sr.sprite != null)
      {
        var ext = sr.bounds.extents;
        return Mathf.Max(0.25f, Mathf.Max(ext.x, ext.y));
      }

      var scale = visual.lossyScale;
      return Mathf.Max(0.25f, Mathf.Max(scale.x, scale.y) * 0.5f);
    }

    static Material s_particleTemplate;
    static Material s_beamTemplate;
    static Material s_flatUnlitTemplate;
    static Texture2D s_squareTex;
    static Texture2D s_beamGradientTex;
    static Texture2D s_softGlowTex;
    static Sprite s_beamSprite;
    static Sprite s_softGlowSprite;

    public static Texture2D SquareParticleTexture
    {
      get
      {
        if (s_squareTex != null)
          return s_squareTex;

        var fromResources = Resources.Load<Texture2D>("VFX/Laser/square_particle");
        if (fromResources != null)
        {
          s_squareTex = fromResources;
          return s_squareTex;
        }

        const int size = 8;
        s_squareTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
          filterMode = FilterMode.Point,
          wrapMode = TextureWrapMode.Clamp,
          name = "LaserSquareParticle_Runtime"
        };

        var pixels = new Color32[size * size];
        for (var i = 0; i < pixels.Length; i++)
          pixels[i] = new Color32(255, 255, 255, 255);

        s_squareTex.SetPixels32(pixels);
        s_squareTex.Apply(false, true);
        return s_squareTex;
      }
    }

    public static Sprite SoftGlowSprite
    {
      get
      {
        if (s_softGlowSprite != null)
          return s_softGlowSprite;

        const int size = 32;
        if (s_softGlowTex == null)
        {
          s_softGlowTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
          {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "LaserSoftGlow_Runtime"
          };

          var pixels = new Color32[size * size];
          var center = (size - 1) * 0.5f;
          var maxR = center;
          for (var y = 0; y < size; y++)
          {
            for (var x = 0; x < size; x++)
            {
              var dx = (x - center) / maxR;
              var dy = (y - center) / maxR;
              var r = Mathf.Sqrt(dx * dx + dy * dy);
              var alpha = Mathf.Clamp01(1f - r);
              alpha = alpha * alpha * alpha;
              pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
          }

          s_softGlowTex.SetPixels32(pixels);
          s_softGlowTex.Apply(false, true);
        }

        s_softGlowSprite = Sprite.Create(
          s_softGlowTex,
          new Rect(0, 0, size, size),
          new Vector2(0.5f, 0.5f),
          100f);
        s_softGlowSprite.name = "LaserSoftGlow";
        return s_softGlowSprite;
      }
    }

    public static Sprite BeamGradientSprite
    {
      get
      {
        if (s_beamSprite != null)
          return s_beamSprite;

        var tex = BeamGradientTexture;
        s_beamSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0.5f), 100f);
        s_beamSprite.name = "LaserBeamGradient";
        return s_beamSprite;
      }
    }

    public static Texture2D BeamGradientTexture
    {
      get
      {
        if (s_beamGradientTex != null)
          return s_beamGradientTex;

        var fromResources = Resources.Load<Texture2D>("VFX/Laser/beam_gradient");
        if (fromResources != null)
        {
          s_beamGradientTex = fromResources;
          return s_beamGradientTex;
        }

        const int w = 64;
        const int h = 8;
        s_beamGradientTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
          filterMode = FilterMode.Bilinear,
          wrapMode = TextureWrapMode.Clamp,
          name = "LaserBeamGradient_Runtime"
        };

        var pixels = new Color32[w * h];
        for (var y = 0; y < h; y++)
        {
          var ny = (y / (float)(h - 1) - 0.5f) * 2f;
          for (var x = 0; x < w; x++)
          {
            var nx = x / (float)(w - 1);
            var edge = Mathf.Clamp01(1f - Mathf.Abs(ny));
            edge = edge * edge;
            var along = Mathf.Lerp(0.65f, 1f, nx);
            var core = Mathf.Clamp01(1f - Mathf.Abs(ny) * 4.5f);
            var alpha = Mathf.Clamp01((edge * 0.55f + core * 1f) * along);
            var g = (byte)Mathf.Lerp(220f, 255f, core);
            var b = (byte)Mathf.Lerp(220f, 255f, core);
            pixels[y * w + x] = new Color32(255, g, b, (byte)(alpha * 255f));
          }
        }

        s_beamGradientTex.SetPixels32(pixels);
        s_beamGradientTex.Apply(false, true);
        return s_beamGradientTex;
      }
    }

    public static Material CreateParticleMaterialInstance()
    {
      var mat = new Material(GetParticleTemplate());
      ApplyParticleTexture(mat);
      return mat;
    }

    public static Material CreateBeamMaterialInstance()
    {
      var mat = new Material(GetBeamTemplate());
      if (mat.HasProperty("_BaseMap"))
        mat.SetTexture("_BaseMap", BeamGradientTexture);
      if (mat.HasProperty("_MainTex"))
        mat.SetTexture("_MainTex", BeamGradientTexture);
      return mat;
    }

    /// <summary>白底 VFX 材质：使用 Unlit/Sprites 着色，供外置武器等非激光特效使用。</summary>
    public static Material CreateFlatBeamMaterialInstance()
    {
      var mat = new Material(GetFlatUnlitTemplate());
      ApplyVfxTint(mat, Color.white);
      return mat;
    }

    static Material GetFlatUnlitTemplate()
    {
      if (s_flatUnlitTemplate != null)
        return s_flatUnlitTemplate;

      var shader = FindShader(
        "Universal Render Pipeline/Unlit",
        "Sprites/Default",
        "Universal Render Pipeline/Particles/Unlit");

      s_flatUnlitTemplate = new Material(shader) { name = "DetachedVfxFlat" };
      ConfigureSpriteAlphaBlend(s_flatUnlitTemplate);

      var white = SquareParticleTexture;
      if (s_flatUnlitTemplate.HasProperty("_BaseMap"))
        s_flatUnlitTemplate.SetTexture("_BaseMap", white);
      if (s_flatUnlitTemplate.HasProperty("_MainTex"))
        s_flatUnlitTemplate.SetTexture("_MainTex", white);
      if (s_flatUnlitTemplate.HasProperty("_Color"))
        s_flatUnlitTemplate.SetColor("_Color", Color.white);
      else if (s_flatUnlitTemplate.HasProperty("_BaseColor"))
        s_flatUnlitTemplate.SetColor("_BaseColor", Color.white);
      return s_flatUnlitTemplate;
    }

    static Color EmissionFromTint(Color color, float strength = 1.35f)
    {
      return new Color(
        color.r * strength,
        color.g * strength,
        color.b * strength * 0.95f,
        1f);
    }

    /// <summary>同步 Base/Emission，让 Shader Graph 光束正确吃 tint。</summary>
    public static void ApplyVfxTint(Material mat, Color color)
    {
      SetMaterialColor(mat, color);
      if (mat == null)
        return;
      var emission = EmissionFromTint(color);
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", emission);
      if (mat.HasProperty("_Emission_Color"))
        mat.SetColor("_Emission_Color", emission);
    }

    public static void SetLineColor(LineRenderer line, Color start, Color end)
    {
      if (line == null)
        return;
      line.startColor = start;
      line.endColor = end;
      if (line.material != null && line.material.shader != null && !UsesSpriteTintShader(line.material.shader))
        ApplyVfxTint(line.material, start);
    }

    public static void ApplySquareParticleRenderer(ParticleSystemRenderer renderer, int sortingOrder)
    {
      if (renderer == null)
        return;

      renderer.renderMode = ParticleSystemRenderMode.Billboard;
      renderer.sortingLayerName = SortingLayerName;
      renderer.sortingOrder = sortingOrder;
      renderer.material = GetParticleTemplate();
      ApplyParticleTexture(renderer.material);
    }

    /// <summary>共享材质版本，避免每实例 new Material?0+ 激光友好）?/summary>
    public static void ApplySharedParticleRenderer(ParticleSystemRenderer renderer, int sortingOrder) =>
      ApplySquareParticleRenderer(renderer, sortingOrder);

    public static SpriteRenderer CreateBeamSprite(
      Transform parent,
      string name,
      Color color,
      float length,
      float thickness,
      int sortingOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      go.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = BeamGradientSprite;
      sr.material = CreateBeamMaterialInstance();
      SetSpriteColor(sr, color);
      sr.sortingLayerName = SortingLayerName;
      sr.sortingOrder = sortingOrder;
      sr.drawMode = SpriteDrawMode.Simple;

      var spriteWidth = Mathf.Max(0.01f, sr.sprite.bounds.size.x);
      var spriteHeight = Mathf.Max(0.01f, sr.sprite.bounds.size.y);
      go.transform.localScale = new Vector3(length / spriteWidth, thickness / spriteHeight, 1f);
      return sr;
    }

    static Material GetParticleTemplate()
    {
      if (s_particleTemplate != null)
        return s_particleTemplate;

      s_particleTemplate = Resources.Load<Material>("VFX/Laser/LaserParticle");
      if (s_particleTemplate != null)
      {
        ConfigureParticleTemplate(s_particleTemplate);
        return s_particleTemplate;
      }

      s_particleTemplate = CreateUrpParticleAdditiveMaterial("LaserParticle_Runtime");
      ConfigureParticleTemplate(s_particleTemplate);
      return s_particleTemplate;
    }

    static void ConfigureParticleTemplate(Material mat)
    {
      ApplyParticleTexture(mat);
      if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", Color.white);
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", new Color(3.5f, 3.5f, 3.2f, 1f));
    }

    static Material GetBeamTemplate()
    {
      if (s_beamTemplate != null)
        return s_beamTemplate;

      var loaded = Resources.Load<Material>("VFX/Laser/LaserBeam");
      if (loaded != null)
      {
        // 不从模板材质克隆（会复制序列化的 _Color 等不兼容属性触发 Shader Graph 警告）
        // 而是从 shader 直接新建材质
        var shader = loaded.shader != null ? loaded.shader : Shader.Find("Sprites/Default");
        s_beamTemplate = new Material(shader) { name = "LaserBeam" };
        return s_beamTemplate;
      }

      s_beamTemplate = CreateUrpSpriteAdditiveMaterial("LaserBeam_Runtime");
      return s_beamTemplate;
    }

    /// <summary>安全读取 Material 颜色（兼容 Shader Graph，避免 material.color 访问 _Color）</summary>
    public static Color GetMaterialColor(Material mat)
    {
      if (mat == null)
        return Color.white;
      if (mat.HasProperty("_Color"))
        return mat.GetColor("_Color");
      if (mat.HasProperty("_BaseColor"))
        return mat.GetColor("_BaseColor");
      return Color.white;
    }

    /// <summary>安全设置 Material 颜色</summary>
    public static void SetMaterialColor(Material mat, Color color)
    {
      if (mat == null)
        return;
      if (mat.HasProperty("_Color"))
        mat.SetColor("_Color", color);
      else if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", color);
    }

    /// <summary>安全设置 SpriteRenderer 颜色：绕过 Unity 内部对 _Color 属性的直接访问，兼容 Shader Graph</summary>
    public static void SetSpriteColor(SpriteRenderer sr, Color color)
    {
      if (sr == null) return;
      var mat = sr.material;
      if (mat != null && mat.shader != null && UsesSpriteTintShader(mat.shader))
      {
        sr.color = color;
        ApplyVfxTint(mat, Color.white);
        return;
      }

      sr.color = Color.white;
      ApplyVfxTint(mat, color);
    }

    static bool UsesSpriteTintShader(Shader shader)
    {
      var name = shader.name;
      return name.Contains("Sprites/Default")
             || name.Contains("Particles/Unlit")
             || name.Contains("Universal Render Pipeline/Unlit");
    }

    /// <summary>安全读取 SpriteRenderer 颜色</summary>
    public static Color GetSpriteColor(SpriteRenderer sr)
    {
      if (sr == null) return Color.white;
      var mat = sr.material;
      if (mat.HasProperty("_Color"))
        return mat.GetColor("_Color");
      if (mat.HasProperty("_BaseColor"))
        return mat.GetColor("_BaseColor");
      return Color.white;
    }

    static void ApplyParticleTexture(Material mat)
    {
      if (mat == null)
        return;

      var tex = SquareParticleTexture;
      if (mat.HasProperty("_BaseMap"))
        mat.SetTexture("_BaseMap", tex);
      if (mat.HasProperty("_MainTex"))
        mat.SetTexture("_MainTex", tex);
    }

    static Material CreateUrpParticleAdditiveMaterial(string matName)
    {
      var shader = FindShader(
        "Shader Graphs/LaserParticleAdditive",
        "Universal Render Pipeline/Particles/Unlit",
        "Sprites/Default");

      var mat = new Material(shader) { name = matName };
      if (shader.name.Contains("Particles/Unlit"))
        ConfigureUrpParticleAdditive(mat);
      else if (shader.name.Contains("Sprites/Default"))
        ConfigureSpriteAdditive(mat);

      if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", Color.white);
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", new Color(3f, 3f, 2.8f, 1f));

      return mat;
    }

    static Material CreateUrpSpriteAdditiveMaterial(string matName)
    {
      var shader = FindShader(
        "Shader Graphs/LaserBeamAdditive",
        "Universal Render Pipeline/2D/Sprite-Unlit-Default",
        "Sprites/Default");

      var mat = new Material(shader) { name = matName };
      ConfigureSpriteAdditive(mat);
      if (mat.HasProperty("_Color"))
        mat.SetColor("_Color", Color.white);
      else if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", Color.white);
      return mat;
    }

    static void ConfigureUrpParticleAdditive(Material mat)
    {
      mat.SetFloat("_Surface", 1f);
      mat.SetFloat("_Blend", 2f);
      mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
      mat.SetFloat("_DstBlend", (float)BlendMode.One);
      mat.SetFloat("_ZWrite", 0f);
      mat.renderQueue = (int)RenderQueue.Transparent;
    }

    static void ConfigureSpriteAlphaBlend(Material mat)
    {
      if (mat.HasProperty("_Surface"))
        mat.SetFloat("_Surface", 1f);
      if (mat.HasProperty("_Blend"))
        mat.SetFloat("_Blend", 0f);
      if (mat.HasProperty("_SrcBlend"))
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
      if (mat.HasProperty("_DstBlend"))
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
      if (mat.HasProperty("_ZWrite"))
        mat.SetFloat("_ZWrite", 0f);
      mat.renderQueue = (int)RenderQueue.Transparent;
    }

    static void ConfigureSpriteAdditive(Material mat)
    {
      if (mat.HasProperty("_Surface"))
        mat.SetFloat("_Surface", 1f);
      if (mat.HasProperty("_Blend"))
        mat.SetFloat("_Blend", 2f);
      if (mat.HasProperty("_SrcBlend"))
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
      if (mat.HasProperty("_DstBlend"))
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
      if (mat.HasProperty("_ZWrite"))
        mat.SetFloat("_ZWrite", 0f);
      mat.renderQueue = (int)RenderQueue.Transparent;
    }

    static Shader FindShader(params string[] names)
    {
      foreach (var name in names)
      {
        var shader = Shader.Find(name);
        if (shader != null)
          return shader;
      }

      Debug.LogWarning("[LaserVfx] Falling back to Sprites/Default; run Game ?Setup Laser VFX (Shader Graph).");
      return Shader.Find("Sprites/Default");
    }
  }
}