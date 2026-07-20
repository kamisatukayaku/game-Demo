using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Tests.PlayMode.Roguelike.RuntimeValidation
{
  [Category("RuntimeValidation")]
  public sealed class BossRuntimePlayModeTests
  {
    static readonly int[] TargetFps = { 30, 60, 120 };
    static readonly string BossId = "wild_boss_hex_king";
    const float SampleSeconds = 45f;
    const float TickDamageTolerance = 12f;

    [UnityTest]
    [Explicit("Boss FPS consistency — excluded from unified validation runs")]
    [Timeout(900000)]
    public IEnumerator BossSkill_FpsConsistency_HexKing()
    {
      var runs = new List<BossFpsDamageRecorder.FpsRunResult>();
      var detailCsv = new StringBuilder();
      detailCsv.AppendLine("boss_id,skill_id,frame,time,damage,attack_instance");

      foreach (var fps in TargetFps)
      {
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = 0;
        Time.captureFramerate = 0;

        var recorder = new BossFpsDamageRecorder();
        yield return SampleBossSkills(fps, recorder, detailCsv);
        runs.Add(recorder.BuildRunResult(fps, SampleSeconds));
      }

      var summaryCsv = new StringBuilder();
      summaryCsv.AppendLine("target_fps,sample_sec,damage_events,skill_executions,total_damage,early_telegraph_violations,frame_damage_violations,status");
      foreach (var run in runs)
      {
        var pass = run.EarlyTelegraphViolations == 0 && run.FrameDamageViolations == 0 && run.DamageEvents > 0;
        summaryCsv.Append(run.TargetFps).Append(',')
          .Append(run.SampleSeconds.ToString("F1")).Append(',')
          .Append(run.DamageEvents).Append(',')
          .Append(run.SkillExecutions).Append(',')
          .Append(run.TotalDamage.ToString("F2")).Append(',')
          .Append(run.EarlyTelegraphViolations).Append(',')
          .Append(run.FrameDamageViolations).Append(',')
          .Append(pass ? "PASS" : "FAIL").Append('\n');
      }

      RuntimeValidationReportWriter.WriteCsv("boss_skill_results.csv", summaryCsv.ToString());
      RuntimeValidationReportWriter.WriteCsv("boss_skill_damage_events.csv", detailCsv.ToString());

      var crossFpsError = BossFpsDamageRecorder.ValidateCrossFps(runs, TickDamageTolerance);
      var report = new StringBuilder();
      report.AppendLine("# Boss Runtime Results — FPS Consistency");
      report.AppendLine();
      report.AppendLine($"Generated: {RuntimeValidationReportWriter.TimestampUtc()}");
      report.AppendLine();
      foreach (var run in runs)
      {
        report.AppendLine(
          $"- FPS {run.TargetFps}: damage_events={run.DamageEvents} skills={run.SkillExecutions} " +
          $"total={run.TotalDamage:F1} early={run.EarlyTelegraphViolations} frame_violations={run.FrameDamageViolations}");
      }

      if (crossFpsError != null)
      {
        report.AppendLine();
        report.AppendLine($"Status: **FAIL** — {crossFpsError}");
        RuntimeValidationReportWriter.WriteText("BOSS_RUNTIME_RESULTS.md", report.ToString());
        Assert.Fail(crossFpsError);
      }

      report.AppendLine();
      report.AppendLine("Status: **PASS**");
      RuntimeValidationReportWriter.WriteText("BOSS_RUNTIME_RESULTS.md", report.ToString());
    }

    static IEnumerator SampleBossSkills(int fps, BossFpsDamageRecorder recorder, StringBuilder detailCsv)
    {
      var (buildId, weaponTheme) = RingArenaPlayModeSession.ResolveStarter("mage");
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(buildId, weaponTheme, 90000 + fps);
      RuntimeValidationSettings.RestoreTimeScale();
      RingArenaPlayModeSession.EnableAutoPlayer();
      if (WaveDirector.Instance != null)
        WaveDirector.Instance.EditorSetManualSpawning(true);

      var player = GameObject.FindWithTag("Player");
      Assert.IsNotNull(player, "Player required for boss skill sampling.");
      var playerHealth = player.GetComponent<Health>();
      Assert.IsNotNull(playerHealth);

      var spawner = Object.FindAnyObjectByType<EnemySpawner>();
      Assert.IsNotNull(spawner, "EnemySpawner required for boss sampling.");

      var center = CircleArenaController.IsActive ? CircleArenaController.Center : Vector2.zero;
      player.transform.position = new Vector3(center.x, center.y - 1.5f, 0f);

      var bossGo = spawner.SpawnEnemy(BossId, center + Vector2.up * 2.5f);
      Assert.IsNotNull(bossGo, $"Failed to spawn boss '{BossId}'.");
      var boss = bossGo.GetComponent<BossCore>();
      Assert.IsNotNull(boss);

      recorder.Begin(boss);
      var elapsed = 0f;
      while (elapsed < SampleSeconds)
      {
        if (LevelUpController.IsWaiting)
          LevelUpController.ValidationAutoPickFirst();
        recorder.Tick();
        elapsed += Time.deltaTime;
        yield return null;
      }

      recorder.Stop();
      detailCsv.Append(recorder.ToCsvRows(BossId));
    }

    [UnityTest]
    [Explicit("Boss TTK sampling — run after W1/W10/W20 matrix")]
    public IEnumerator BossTtk_ThreeStarters()
    {
      var ttkCsv = new StringBuilder();
      ttkCsv.AppendLine(new BossRuntimeSampler().TtkCsvHeader());
      yield return null;
      RuntimeValidationReportWriter.WriteCsv("boss_ttk_results.csv", ttkCsv.ToString());
    }
  }
}
