using UnityEngine;

namespace Game.Shared.Combat
{
  /// <summary>
  /// 奔跑风粒子特效：敌人奔跑时在移动方向背后产生微弱的风追尾粒子。
  /// 纯代码 ParticleSystem，无 Prefab；每怪物一份，随怪物销毁。
  /// </summary>
  [DisallowMultipleComponent]
  public class SprintWindEffect : MonoBehaviour
  {
    const int SortOrder = 14;
    const float DepthZ = -0.08f;

    static readonly Color WindColor1 = new(0.7f, 0.75f, 0.8f, 0.25f);
    static readonly Color WindColor2 = new(0.6f, 0.65f, 0.7f, 0.12f);

    ParticleSystem _wind;
    Transform _anchor;
    Vector2 _currentDir = Vector2.right;

    public static SprintWindEffect Ensure(GameObject owner)
    {
      if (owner == null) return null;
      var existing = owner.GetComponent<SprintWindEffect>();
      if (existing != null) return existing;
      return owner.AddComponent<SprintWindEffect>();
    }

    void Awake()
    {
      _anchor = new GameObject("SprintWindAnchor").transform;
      _anchor.SetParent(transform, false);
      _anchor.localPosition = Vector3.zero;

      var go = new GameObject("SprintWindParticles");
      go.transform.SetParent(_anchor, false);
      go.transform.localPosition = Vector3.zero;

      var ps = go.AddComponent<ParticleSystem>();
      var main = ps.main;
      main.startLifetime = 0.35f;
      main.startSpeed = 1.8f;
      main.startSize = 0.12f;
      main.startColor = WindColor1;
      main.simulationSpace = ParticleSystemSimulationSpace.Local;
      main.maxParticles = 12;
      main.loop = true;
      main.playOnAwake = false;

      var emission = ps.emission;
      emission.rateOverTime = 6f;

      var shape = ps.shape;
      shape.shapeType = ParticleSystemShapeType.Cone;
      shape.angle = 5f;
      shape.radius = 0.15f;
      shape.arc = 120f;

      var velocity = ps.velocityOverLifetime;
      velocity.enabled = true;
      velocity.speedModifier = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);

      var colorOverLifetime = ps.colorOverLifetime;
      colorOverLifetime.enabled = true;
      var gradient = new Gradient();
      gradient.SetKeys(
        new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.75f, 0.8f), 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.7f), 1f) },
        new GradientAlphaKey[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.05f, 1f) });
      colorOverLifetime.color = gradient;

      var sizeOverLifetime = ps.sizeOverLifetime;
      sizeOverLifetime.enabled = true;
      sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(0.3f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.1f)));

      var renderer = ps.GetComponent<ParticleSystemRenderer>();
      renderer.renderMode = ParticleSystemRenderMode.Billboard;
      renderer.sortingOrder = SortOrder;
      renderer.material = Resources.Load<Material>("Materials/DefaultParticle") ?? CreateDefaultParticleMaterial();

      _wind = ps;
      go.SetActive(false);
    }

    /// <summary>开始播放风粒子（方向默认为向右）</summary>
    public void Begin(Vector2 moveDirection)
    {
      if (_wind == null) return;

      UpdateDirection(moveDirection);
      _wind.gameObject.SetActive(true);
      _wind.Play();
    }

    /// <summary>更新粒子发射方向和位置</summary>
    public void UpdateDirection(Vector2 moveDirection)
    {
      if (_anchor == null || moveDirection.sqrMagnitude < 0.0001f)
        return;

      _currentDir = moveDirection.normalized;

      // anchor 放在敌人后方并朝向移动方向的反方向
      _anchor.localRotation = Quaternion.LookRotation(
        Vector3.forward,
        new Vector3(-_currentDir.x, -_currentDir.y, 0f));

      // 将 anchor 向后偏移一小段距离，使粒子出现在敌人身后
      _anchor.localPosition = new Vector3(-_currentDir.x * 0.3f, -_currentDir.y * 0.3f, DepthZ);
    }

    /// <summary>停止播放风粒子</summary>
    public void End()
    {
      if (_wind == null) return;

      _wind.Stop();
      _wind.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
      if (_wind != null)
      {
        _wind.Stop();
        Destroy(_wind.gameObject);
      }
      if (_anchor != null)
        Destroy(_anchor.gameObject);
    }

    static Material CreateDefaultParticleMaterial()
    {
      var mat = new Material(Shader.Find("Particles/Standard Unlit"));
      mat.name = "DefaultParticle_Generated";
      return mat;
    }
  }
}
