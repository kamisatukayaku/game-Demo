using System.IO;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

#if UNITY_EDITOR

namespace Game.Editor
{
  /// <summary>
  /// 从 URP Shader Graph 样本复制激光 Shader Graph，并生成 Resources/VFX/Laser 材质。
  /// 菜单：Game → Setup Laser VFX (Shader Graph)
  /// </summary>
  public static class LaserVfxShaderGraphSetup
  {
    const string GraphDir = "Assets/Shaders/VFX/Laser";
    const string ResourcesDir = "Assets/Resources/VFX/Laser";
    const string ParticleGraphFile = "LaserParticleAdditive.shadergraph";
    const string BeamGraphFile = "LaserBeamAdditive.shadergraph";

    [MenuItem("Game/Setup Laser VFX (Shader Graph)")]
    public static void SetupFromMenu()
    {
      SetupInternal(force: true);
      Debug.Log("[LaserVfx] Shader Graph + materials created. Open Assets/Shaders/VFX/Laser/*.shadergraph to tweak.");
    }

    [InitializeOnLoadMethod]
    static void AutoSetupOnLoad()
    {
      EditorApplication.delayCall += () =>
      {
        if (AssetDatabase.LoadAssetAtPath<Material>($"{ResourcesDir}/LaserParticle.mat") != null)
          return;

        SetupInternal(force: false);
      };
    }

    static void SetupInternal(bool force)
    {
      Directory.CreateDirectory(GraphDir);
      Directory.CreateDirectory(ResourcesDir);

      if (force || !File.Exists($"{GraphDir}/{ParticleGraphFile}"))
        CopyShaderGraphSample(
          "Samples~/ProductionReady/Common/Shaders/ParticleEffect.shadergraph",
          $"{GraphDir}/{ParticleGraphFile}");

      if (force || !File.Exists($"{GraphDir}/{BeamGraphFile}"))
        CopyShaderGraphSample(
          "Samples~/ProductionReady/SRPCommon/Materials/Shaders/SamplesEmissive.shadergraph",
          $"{GraphDir}/{BeamGraphFile}");

      AssetDatabase.Refresh();

      CreateOrUpdateMaterial(
        $"{ResourcesDir}/LaserParticle.mat",
        new[] { "Shader Graphs/LaserParticleAdditive", "Universal Render Pipeline/Particles/Unlit" },
        configureParticle: true,
        configureBeam: false);

      CreateOrUpdateMaterial(
        $"{ResourcesDir}/LaserBeam.mat",
        new[] { "Shader Graphs/LaserBeamAdditive", "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default" },
        configureParticle: false,
        configureBeam: true);

      AssetDatabase.SaveAssets();
    }

    static void CopyShaderGraphSample(string sampleRelative, string destAssetPath)
    {
      var packageRoot = ResolveShaderGraphPackageRoot();
      if (string.IsNullOrEmpty(packageRoot))
      {
        Debug.LogWarning("[LaserVfx] com.unity.shadergraph package not found; skip copying sample graph.");
        return;
      }

      var src = Path.Combine(packageRoot, sampleRelative.Replace('/', Path.DirectorySeparatorChar));
      if (!File.Exists(src))
      {
        Debug.LogWarning($"[LaserVfx] Sample shader graph not found: {src}");
        return;
      }

      File.Copy(src, Path.GetFullPath(destAssetPath), overwrite: true);
    }

    static string ResolveShaderGraphPackageRoot()
    {
      var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.shadergraph");
      return pkg?.resolvedPath;
    }

    static void CreateOrUpdateMaterial(
      string assetPath,
      string[] shaderNames,
      bool configureParticle,
      bool configureBeam)
    {
      var shader = FindFirstShader(shaderNames);
      if (shader == null)
      {
        Debug.LogWarning($"[LaserVfx] No shader found for {assetPath}");
        return;
      }

      var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
      if (mat == null)
      {
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, assetPath);
      }
      else
      {
        mat.shader = shader;
      }

      mat.name = Path.GetFileNameWithoutExtension(assetPath);

      if (configureParticle && shader.name.Contains("Particles/Unlit"))
        ConfigureUrpParticleAdditive(mat);

      if (configureBeam)
        ConfigureSpriteAdditive(mat);

      if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", Color.white);
      if (mat.HasProperty("_Color"))
        mat.color = Color.white;
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", new Color(2.5f, 1.1f, 0.35f, 1f));

      EditorUtility.SetDirty(mat);
    }

    static Shader FindFirstShader(string[] names)
    {
      foreach (var name in names)
      {
        var shader = Shader.Find(name);
        if (shader != null)
          return shader;
      }

      return null;
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
  }
}
#endif