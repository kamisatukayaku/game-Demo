using System.Collections.Generic;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Death;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using UnityEngine;

namespace Game.Modes.Roguelike.BossRush
{
  public static class BossRushCombatService
  {
    static readonly List<StraightProjectile> s_projectileBuffer = new();

    public static EnemySpawner EnsureSpawner()
    {
      EnemySpawner.SpawningEnabled = true;
      var spawner = Object.FindAnyObjectByType<EnemySpawner>();
      if (spawner != null)
        return spawner;

      var go = new GameObject("_EnemySpawner");
      return go.AddComponent<EnemySpawner>();
    }

    public static GameObject SpawnBoss(
      EnemySpawner spawner,
      BossRushEncounterDef encounter,
      int encounterIndex,
      Vector2 position)
    {
      if (spawner == null || encounter == null || string.IsNullOrEmpty(encounter.boss_id))
        return null;

      var scaling = BossRushScaling.Build(encounter, encounterIndex);
      var boss = spawner.SpawnWaveEnemy(encounter.boss_id, position, scaling);
      if (boss == null)
        return null;

      if (!boss.activeSelf)
        boss.SetActive(true);

      ConfigureBoss(boss, encounter, scaling);
      EnsureBossVisible(boss);
      return boss;
    }

    public static void ConfigureBoss(
      GameObject boss,
      BossRushEncounterDef encounter,
      WaveSpawnScaling scaling)
    {
      if (boss == null || encounter == null)
        return;

      var bossId = encounter.boss_id;
      var health = boss.GetComponent<Health>();
      if (health != null)
        health.Died += ArenaQuadrantBlocker.Clear;

      var ctx = BossWaveContext.Ensure(boss, bossId, scaling.waveNumber);
      ctx?.ConfigureEncounterTuning(
        encounter.cooldown_mult,
        encounter.allow_minions,
        encounter.max_minions);

      if (boss.GetComponent<BossDamageMitigation>() == null)
        boss.AddComponent<BossDamageMitigation>();

      var deathHandler = boss.GetComponent<EnemyDeathHandler>();
      var def = EnemySpawner.SpawningEnabled
        ? Object.FindAnyObjectByType<EnemySpawner>()?.GetEnemyDef(bossId)
        : null;
      if (deathHandler != null && def != null)
        deathHandler.LootTableId = def.loot_table_id ?? "common_mob";

      var core = boss.GetComponent<BossCore>();
      if (core != null)
        core.SetSkillIntroGrace(encounter.ResolveIntroGrace());
    }

    static void EnsureBossVisible(GameObject boss)
    {
      if (boss == null)
        return;

      if (!boss.activeSelf)
        boss.SetActive(true);

      var ceremony = boss.GetComponent<EnemySpawnCeremonyRunner>();
      if (ceremony != null)
        Object.Destroy(ceremony);

      foreach (var renderer in boss.GetComponentsInChildren<SpriteRenderer>(true))
      {
        if (renderer == null)
          continue;

        if (!renderer.gameObject.activeSelf)
          renderer.gameObject.SetActive(true);

        renderer.enabled = true;
        var color = renderer.color;
        color.a = 1f;
        renderer.color = color;
      }

      var visual = boss.transform.Find("Visual");
      if (visual != null && visual.localScale.sqrMagnitude < 0.05f)
        visual.localScale = Vector3.one;
    }

    public static Vector2 PickSpawnPosition(float radius)
    {
      var settings = BossRushDatabase.Settings;
      var playerCenter = GetPlayerCenter();
      var layout = ArenaLayoutLocator.Layout;
      var arenaCenter = layout.IsActive ? layout.Center : Vector2.zero;
      var origin = playerCenter.sqrMagnitude > 0.01f ? playerCenter : arenaCenter;

      var minDist = settings.boss_spawn_distance_min > 0f ? settings.boss_spawn_distance_min : 5.5f;
      var maxDist = settings.boss_spawn_distance_max > minDist
        ? settings.boss_spawn_distance_max
        : minDist + 4f;
      if (radius > 0f)
        minDist = maxDist = radius;

      const int attempts = 12;
      for (var attempt = 0; attempt < attempts; attempt++)
      {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var distance = Random.Range(minDist, maxDist);
        var pos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        pos = ClampSpawnPosition(pos);
        if (Vector2.Distance(pos, origin) >= minDist * 0.85f)
          return pos;
      }

      var fallbackAngle = Random.Range(0f, Mathf.PI * 2f);
      return ClampSpawnPosition(origin + new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle)) * minDist);
    }

    static Vector2 GetPlayerCenter()
    {
      var player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
      return player != null
        ? new Vector2(player.transform.position.x, player.transform.position.y)
        : Vector2.zero;
    }

    static Vector2 ClampSpawnPosition(Vector2 pos)
    {
      if (CircleArenaController.IsActive)
        return CircleArenaController.ClampPosition(pos, 1.2f);

      return WaveDirector.SpawnPositionClamper != null
        ? WaveDirector.SpawnPositionClamper(pos)
        : pos;
    }

    public static void CleanupBattlefield(BossRushSettings settings)
    {
      if (settings == null)
        settings = BossRushSettings.Default;

      if (settings.clear_enemy_projectiles)
        ClearEnemyProjectiles();

      if (settings.clear_hazards)
        ArenaQuadrantBlocker.Clear();

      if (settings.clear_boss_summons)
        DestroyTaggedEnemies();

      MageZonePool.ResetAll();
    }

    public static void DestroyActiveBoss(GameObject boss)
    {
      if (boss == null)
        return;

      var core = boss.GetComponent<BossCore>();
      if (core != null)
        core.enabled = false;

      DestroyGameObject(boss);
    }

    static void DestroyGameObject(GameObject go)
    {
      if (go == null)
        return;

#if UNITY_EDITOR
      if (!Application.isPlaying)
      {
        Object.DestroyImmediate(go);
        return;
      }
#endif
      Object.Destroy(go);
    }

    static void ClearEnemyProjectiles()
    {
      s_projectileBuffer.Clear();
      ActiveProjectileRegistry.CopyActive(s_projectileBuffer);
      foreach (var projectile in s_projectileBuffer)
      {
        if (projectile != null)
          DestroyGameObject(projectile.gameObject);
      }

      ActiveProjectileRegistry.ResetAll();
    }

    static void DestroyTaggedEnemies()
    {
      var enemies = Object.FindObjectsOfType<EnemyCore>();
      foreach (var enemy in enemies)
      {
        if (enemy == null)
          continue;

        var go = enemy.gameObject;
        if (go.GetComponent<BossCore>() != null)
          continue;

        DestroyGameObject(go);
      }
    }

    public static void ApplyRecovery(float healPercent, float minimumHealPercent)
    {
      var player = Object.FindAnyObjectByType<Health>();
      if (player == null || !player.CompareTag("Player"))
      {
        foreach (var health in Object.FindObjectsOfType<Health>())
        {
          if (health != null && health.CompareTag("Player"))
          {
            player = health;
            break;
          }
        }
      }

      if (player == null)
        return;

      var max = player.MaxHp;
      var healAmount = max * healPercent;
      var minimumResult = max * minimumHealPercent;
      var next = player.CurrentHp + healAmount;
      if (next < minimumResult)
        next = minimumResult;
      next = Mathf.Min(next, max);
      player.Heal(Mathf.Max(0f, next - player.CurrentHp));
    }
  }
}
