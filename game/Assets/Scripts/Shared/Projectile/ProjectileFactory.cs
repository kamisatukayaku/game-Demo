using System.Collections.Generic;
using UnityEngine;

using Game.Shared.Combat.Damage;
using Game.Shared.Vfx;
namespace Game.Shared.Projectile
{
  public static class ProjectileFactory
  {
    const int MaxPoolSizePerVariant = 256;
    static readonly Stack<StraightProjectile> s_plainStraightPool = new();
    static readonly Stack<StraightProjectile> s_rangedStraightPool = new();
    static readonly Stack<StraightProjectile> s_auxiliaryStraightPool = new();
    static Material s_sharedProjectileMaterial;
    static MaterialPropertyBlock s_colorBlock;

    /// <summary>玩家：沿指定方向飞行，命中路径上最近敌人?/summary>
    public static StraightProjectile SpawnDirectional(
      Vector3 position,
      Vector3 direction,
      in DamageRequest request,
      float speed,
      float scale,
      Color color,
      float maxRange,
      string name = "PlayerProjectile",
      float hitRadius = 0f,
      float lifetime = 4f,
      Player.PlayerAttackDirector.ProjectileBuildModifiers buildMods = default,
      RangedProjectileVisualKind visualKind = RangedProjectileVisualKind.Primary)
    {
      var rangedVisual = request.DamageSourceId == "weapon";
      var projectile = AcquireStraight(name, position, scale, color, rangedVisual, visualKind);
      projectile.LaunchDirectional(position, direction, request, speed, maxRange, lifetime, hitRadius);
      projectile.SetBuildModifiers(buildMods);
      return projectile;
    }

    /// <summary>玩家：索敌后直线，无追踪?/summary>
    public static StraightProjectile SpawnStraight(
      Vector3 position,
      Transform target,
      in DamageRequest request,
      float speed,
      float scale,
      Color color,
      string name = "PlayerProjectile",
      float hitRadius = 0f)
    {
      var projectile = AcquireStraight(name, position, scale, color, request.DamageSourceId == "weapon");
      projectile.Launch(position, target, request, speed, hitRadius: hitRadius);
      return projectile;
    }

    /// <summary>怪物：弱追踪弹?/summary>
    public static WeakHomingProjectile SpawnWeakHoming(
      Vector3 position,
      Transform target,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float scale,
      Color color,
      string name = "EnemyProjectile",
      float hitRadius = 0f)
    {
      var go = CreateProjectileObject(name, position, scale, color);
      var projectile = go.AddComponent<WeakHomingProjectile>();
      projectile.Launch(position, target, request, speed, turnRateDeg, hitRadius: hitRadius);
      return projectile;
    }

    /// <summary>反弹弹：原?+ 弱追踪?/summary>
    public static WeakHomingProjectile SpawnReflectedWeakHoming(
      Vector3 position,
      Vector3 direction,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float scale,
      Color color,
      float lifetime = 5f,
      string name = "ReflectedProjectile",
      float hitRadius = 0f)
    {
      var go = CreateProjectileObject(name, position, scale, color);
      var projectile = go.AddComponent<WeakHomingProjectile>();
      projectile.LaunchReflect(position, direction, request, speed, turnRateDeg, lifetime, hitRadius);
      return projectile;
    }

    /// <summary>怪物：直线弹（无追踪，发射方向锁定）?/summary>
    public static StraightProjectile SpawnEnemyStraight(
      Vector3 position,
      Transform target,
      in DamageRequest request,
      float speed,
      float scale,
      Color color,
      string name = "EnemyStraightProjectile",
      float hitRadius = 0f)
    {
      var projectile = AcquireStraight(name, position, scale, color, false);
      projectile.Launch(position, target, request, speed, hitRadius: hitRadius);
      return projectile;
    }

    /// <summary>
    /// 怪物：失锁追踪弹 ?初始弱追踪，当玩家移动方向与子弹方向夹角超过阈值时
    /// 切断追踪，切换为直线飞行?
    /// </summary>
    public static LockLossHomingProjectile SpawnLockLossHoming(
      Vector3 position,
      Transform target,
      in DamageRequest request,
      float speed,
      float turnRateDeg,
      float lockLossAngleDeg,
      float scale,
      Color color,
      string name = "EnemyLockLossProjectile",
      float hitRadius = 0f)
    {
      var go = CreateProjectileObject(name, position, scale, color);
      var projectile = go.AddComponent<LockLossHomingProjectile>();
      projectile.Launch(position, target, request, speed, turnRateDeg, lockLossAngleDeg, hitRadius: hitRadius);
      return projectile;
    }

    static GameObject CreateProjectileObject(string name, Vector3 position, float scale, Color color)
    {
      var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      go.name = name;
      go.transform.position = position;
      go.transform.localScale = Vector3.one * scale;

      var col = go.GetComponent<Collider>();
      if (col != null)
        Object.Destroy(col);

      ApplyVisual(go, scale, color);

      return go;
    }

    static StraightProjectile AcquireStraight(
      string name,
      Vector3 position,
      float scale,
      Color color,
      bool rangedVisual,
      RangedProjectileVisualKind visualKind = RangedProjectileVisualKind.Primary)
    {
      var isAuxiliary = name != null && name.IndexOf("Auxiliary", System.StringComparison.Ordinal) >= 0;
      var pool = isAuxiliary
        ? s_auxiliaryStraightPool
        : rangedVisual ? s_rangedStraightPool : s_plainStraightPool;
      StraightProjectile projectile = null;
      while (pool.Count > 0 && projectile == null)
        projectile = pool.Pop();

      if (projectile == null)
      {
        var created = CreateProjectileObject(name, position, scale, color);
        projectile = created.AddComponent<StraightProjectile>();
        projectile.ConfigurePooling(rangedVisual, isAuxiliary);
        if (rangedVisual)
          RangedProjectileVfx.Attach(created, visualKind);
      }
      else
      {
        var go = projectile.gameObject;
        go.name = name;
        go.transform.SetPositionAndRotation(position, Quaternion.identity);
        ApplyVisual(go, scale, color);
        go.SetActive(true);
      }

      projectile.PrepareForLaunch();
      if (rangedVisual)
      {
        var vfx = projectile.GetComponent<RangedProjectileVfx>();
        vfx?.ResetForReuse();
        vfx?.ApplyVisualKind(visualKind);
      }
      return projectile;
    }

    internal static void Release(StraightProjectile projectile, bool rangedVisual, bool isAuxiliary = false)
    {
      if (projectile == null)
        return;

      var pool = isAuxiliary
        ? s_auxiliaryStraightPool
        : rangedVisual ? s_rangedStraightPool : s_plainStraightPool;
      if (pool.Count >= MaxPoolSizePerVariant)
      {
        Object.Destroy(projectile.gameObject);
        return;
      }

      projectile.gameObject.SetActive(false);
      projectile.transform.SetParent(null, false);
      pool.Push(projectile);
    }

    static void ApplyVisual(GameObject go, float scale, Color color)
    {
      go.transform.localScale = Vector3.one * scale;
      var renderer = go.GetComponent<Renderer>();
      if (renderer == null)
        return;

      renderer.sharedMaterial = SharedProjectileMaterial;
      s_colorBlock ??= new MaterialPropertyBlock();
      s_colorBlock.Clear();
      s_colorBlock.SetColor("_BaseColor", color);
      s_colorBlock.SetColor("_Color", color);
      renderer.SetPropertyBlock(s_colorBlock);
    }

    static Material SharedProjectileMaterial
    {
      get
      {
        if (s_sharedProjectileMaterial == null)
        {
          var shader = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                       ?? Shader.Find("Sprites/Default");
          s_sharedProjectileMaterial = new Material(shader) { name = "ProjectileShared_Runtime" };
        }

        return s_sharedProjectileMaterial;
      }
    }
  }
}
