using System.Collections;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Roguelike.RuntimeValidation
{
  [Category("RuntimeValidation")]
  public sealed class MonsterAiSoakPlayModeTests
  {
    static readonly (string enemyId, string role)[] Roles =
    {
      ("mob_square_01", "chaser"),
      ("mob_hex_01", "runner"),
      ("mob_hex_03", "tank"),
      ("mob_splitter_eco_01", "splitter"),
      ("mob_star4_01", "shooter"),
      ("mob_support_eco_01", "supporter"),
      ("mob_bomber_eco_01", "bomber"),
      ("mob_disruptor_eco_01", "disruptor"),
      ("mob_wisp_fast_01", "runner_wisp")
    };

    [UnityTest]
    public IEnumerator Chaser_10SecondSmoke()
    {
      var csv = new StringBuilder();
      csv.AppendLine("enemy_id,role,duration_sec,move_distance,longest_idle,attacks,avg_attack_interval,state_changes,sprint_count,sprint_recoveries,boundary_stuck,result");
      yield return RunSingleSoak("mob_square_01", "chaser", csv, 10f);
      RuntimeValidationReportWriter.WriteCsv("ai_soak_results.csv", csv.ToString());
    }

    [UnityTest]
    [Explicit("Full 60s soak for all roles (~10 min)")]
    public IEnumerator AllRoles_60SecondSoak()
    {
      var csv = new StringBuilder();
      csv.AppendLine("enemy_id,role,duration_sec,move_distance,longest_idle,attacks,avg_attack_interval,state_changes,sprint_count,sprint_recoveries,boundary_stuck,result");

      foreach (var (enemyId, role) in Roles)
      {
        yield return RunSingleSoak(enemyId, role, csv, RuntimeValidationSettings.AiSoakDurationSeconds);
      }

      RuntimeValidationReportWriter.WriteCsv("ai_soak_results.csv", csv.ToString());
      RuntimeValidationReportWriter.WriteText("AI_SOAK_RESULTS.md", "# AI Soak Results\n\nStatus: PASS\n\nSee ai_soak_results.csv\n");
    }

    static IEnumerator RunSingleSoak(string enemyId, string role, StringBuilder csv, float durationSeconds)
    {
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter("mage");
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, 51000 + enemyId.GetHashCode());
      RuntimeValidationSettings.SetAccelerated();
      RingArenaPlayModeSession.ApplyValidationPlayerSurvival();

      var spawner = Object.FindAnyObjectByType<EnemySpawner>();
      Assert.IsNotNull(spawner, "EnemySpawner required for AI soak.");
      EnemySpawner.SpawningEnabled = true;

      var spawnPos = CircleArenaController.IsActive
        ? CircleArenaController.Center + Vector2.right * 4f
        : Vector2.right * 4f;
      var enemy = spawner.SpawnEnemy(enemyId, spawnPos);
      Assert.IsNotNull(enemy, $"Failed to spawn {enemyId}");

      var monitor = MonsterAiSoakMonitor.Attach(enemy, role);
      var elapsed = 0f;
      while (elapsed < durationSeconds)
      {
        elapsed += Time.deltaTime;
        yield return null;
      }

      var result = monitor.BuildResult(false);
      csv.Append(monitor.ToCsvRow());
      if (durationSeconds >= RuntimeValidationSettings.AiSoakDurationSeconds - 1f)
        Assert.IsTrue(result.Pass, $"{enemyId}/{role} soak failed: attacks={result.AttackCount} move={result.MoveDistance:F1} idle={result.LongestIdleSec:F1}");
      else
        Assert.Greater(result.MoveDistance, 0.5f, $"{enemyId}/{role} smoke: expected movement");
    }
  }
}
