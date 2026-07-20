using UnityEngine.Rendering;
using UnityEngine;

using Game.Shared.Core;
namespace Game.Shared.Laser
{
  /// <summary>
  /// 高能激光主光束：高亮白?+ 窄红/橙能量外壳（LineRenderer 双层）?
  /// </summary>
  [DisallowMultipleComponent]
  public class LaserBeamView : MonoBehaviour
  {
    const int ShellSortingOrder = 30;
    const int CoreSortingOrder = 31;
    const float BeamDepthZ = -0.12f;

    static Material s_coreMaterial;
    static Material s_shellMaterial;

    public static Material SharedGlowMaterial => GetShellMaterial();

    LineRenderer _coreLine;
    LineRenderer _shellLine;
    LaserBeamSettings _settings;
    Color _coreBase;
    Color _shellBase;

    public void EnsureBuilt()
    {
      if (_coreLine != null)
        return;

      _shellLine = CreateLine(transform, "EnergyShell", GetShellMaterial(), ShellSortingOrder);
      _coreLine = CreateLine(transform, "WhiteCore", GetCoreMaterial(), CoreSortingOrder);
    }

    public void ApplySettings(LaserBeamSettings settings)
    {
      _settings = settings;
      _coreBase = settings.CoreColor;
      _shellBase = settings.GlowColor;
      ApplyWidth(settings.CoreWidth, settings.GlowWidth);

      if (_coreLine != null)
        _coreLine.startColor = _coreLine.endColor = settings.CoreColor;
      if (_shellLine != null)
        _shellLine.startColor = _shellLine.endColor = settings.GlowColor;
    }

    public void SetEndpoints(Vector3 start, Vector3 end)
    {
      if (_coreLine == null)
        return;

      start.z = end.z = BeamDepthZ;

      _coreLine.SetPosition(0, start);
      _coreLine.SetPosition(1, end);
      _shellLine.SetPosition(0, start);
      _shellLine.SetPosition(1, end);
    }

    public void UpdatePulse(float time)
    {
      if (_coreLine == null)
        return;

      var pulse = 1f + _settings.PulseAmount * Mathf.Sin(time * _settings.PulseSpeed);
      var shellPulse = 1f + _settings.PulseAmount * 0.35f * Mathf.Sin(time * _settings.PulseSpeed + 0.6f);

      // 白芯宽度稳定；外壳极窄且脉冲微弱，保持直线高能束观感?
      ApplyWidth(_settings.CoreWidth, _settings.GlowWidth * shellPulse);

      var core = _coreBase;
      core.a *= 0.94f + 0.06f * pulse;
      _coreLine.startColor = _coreLine.endColor = core;

      var shell = _shellBase;
      shell.a *= 0.75f + 0.12f * shellPulse;
      _shellLine.startColor = _shellLine.endColor = shell;
    }

    public void SetVisible(bool visible)
    {
      if (_coreLine != null)
        _coreLine.enabled = visible;
      if (_shellLine != null)
        _shellLine.enabled = visible;
    }

    void ApplyWidth(float coreWidth, float shellWidth)
    {
      _coreLine.startWidth = _coreLine.endWidth = coreWidth;
      _shellLine.startWidth = _shellLine.endWidth = shellWidth;
    }

    static LineRenderer CreateLine(Transform parent, string name, Material material, int sortingOrder)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);

      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.alignment = LineAlignment.TransformZ;
      line.loop = false;
      line.numCornerVertices = 4;
      line.numCapVertices = 4;
      line.textureMode = LineTextureMode.Stretch;
      line.shadowCastingMode = ShadowCastingMode.Off;
      line.receiveShadows = false;
      line.positionCount = 2;
      line.material = material;
      line.sortingLayerName = LaserVfxShared.SortingLayerName;
      line.sortingOrder = sortingOrder;
      line.enabled = false;
      return line;
    }

    static Material GetCoreMaterial()
    {
      if (s_coreMaterial != null)
        return s_coreMaterial;

      s_coreMaterial = Resources.Load<Material>("VFX/Laser/LaserLineCore");
      if (s_coreMaterial == null)
        s_coreMaterial = CreateRuntimeLineMaterial("LaserLineCore_Runtime", new Color(1f, 1f, 1f, 1f), 3.5f);

      return s_coreMaterial;
    }

    static Material GetShellMaterial()
    {
      if (s_shellMaterial != null)
        return s_shellMaterial;

      s_shellMaterial = Resources.Load<Material>("VFX/Laser/LaserLineGlow");
      if (s_shellMaterial == null)
        s_shellMaterial = CreateRuntimeLineMaterial(
          "LaserLineShell_Runtime",
          new Color(1f, 0.22f, 0.06f, 0.55f),
          1.8f);

      return s_shellMaterial;
    }

    static Material CreateRuntimeLineMaterial(string name, Color color, float emission = 2.5f)
    {
      var shader = Shader.Find("Universal Render Pipeline/Unlit")
        ?? Shader.Find("Sprites/Default");

      var mat = new Material(shader) { name = name };
      if (mat.HasProperty("_Color"))
        mat.SetColor("_Color", color);
      else if (mat.HasProperty("_BaseColor"))
        mat.SetColor("_BaseColor", color);
      mat.SetFloat("_Surface", 1f);
      mat.SetFloat("_Blend", 2f);
      mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
      mat.SetFloat("_DstBlend", (float)BlendMode.One);
      mat.SetFloat("_ZWrite", 0f);
      mat.renderQueue = (int)RenderQueue.Transparent;
      if (mat.HasProperty("_EmissionColor"))
        mat.SetColor("_EmissionColor", new Color(emission, emission * 0.95f, emission * 0.9f, 1f));
      return mat;
    }
  }
}