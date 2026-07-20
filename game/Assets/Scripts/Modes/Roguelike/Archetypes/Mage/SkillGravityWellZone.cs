using UnityEngine;

using Game.Shared.Core;
using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  /// <summary>兼容层：?SkillGravityWellZone API?/summary>
  public static class SkillGravityWellZone
  {
    public static void Spawn(Transform caster, Vector2 aimDir, float baseRadius, float baseDuration, float baseDamage)
    {
      var ctx = MageSystemLocator.Context;
      if (caster == null)
        return;

      var rangeMult = ctx.SkillRangeMult;
      var radius = Mathf.Max(1.5f, (baseRadius + ctx.GravityRadiusBonus + ctx.SkillExplosionRadius * 0.35f) * rangeMult);
      var duration = baseDuration * (1f + ctx.SkillVacuumDuration);
      var center = GameplayPlane.Position2D(caster) + aimDir.normalized * 4.5f * rangeMult;
      var count = 1 + ctx.SkillVacuumSplit;
      var damage = baseDamage;

      for (var i = 0; i < count; i++)
      {
        var offset = count <= 1
          ? Vector2.zero
          : Rotate2D(Vector2.right, i * (360f / count)) * radius * 0.35f;
        MageZone.Spawn(caster, center + offset, radius * (count > 1 ? 0.75f : 1f), duration, damage);
      }

      if (ctx.SkillVacuumTrail > 0.5f)
      {
        var trailGo = new GameObject("SkillGravityTrail");
        trailGo.AddComponent<SkillGravityTrailFollower>().Init(caster, radius * 0.55f, duration * 0.65f, damage);
      }
    }

    public static void SpawnMini(Transform caster, Vector2 center, float radius, float duration, float baseDamage) =>
      MageZone.Spawn(caster, center, radius, duration, baseDamage);

    static Vector2 Rotate2D(Vector2 v, float degrees)
    {
      var rad = degrees * Mathf.Deg2Rad;
      var cos = Mathf.Cos(rad);
      var sin = Mathf.Sin(rad);
      return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
  }

  [DisallowMultipleComponent]
  public class SkillGravityTrailFollower : MonoBehaviour
  {
    Transform _caster;
    float _radius;
    float _remaining;
    float _damage;
    Vector2 _lastSpawn;

    public void Init(Transform caster, float radius, float duration, float damage)
    {
      _caster = caster;
      _radius = radius;
      _remaining = duration;
      _damage = damage;
      _lastSpawn = GameplayPlane.Position2D(caster);
    }

    void Update()
    {
      if (_caster == null)
      {
        Destroy(gameObject);
        return;
      }

      _remaining -= Time.deltaTime;
      if (_remaining <= 0f)
      {
        Destroy(gameObject);
        return;
      }

      var pos = GameplayPlane.Position2D(_caster);
      if (Vector2.Distance(pos, _lastSpawn) < 1.2f)
        return;

      _lastSpawn = pos;
      MageZone.Spawn(_caster, pos, _radius * 0.6f, 0.75f, _damage * 0.45f);
    }
  }
}
