#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using System.Collections;

using System.Text;

using Game.Modes.Roguelike.Combat;

using Game.Modes.Roguelike.Progression;

using UnityEngine;



namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation

{

  public sealed class CombatChainGateFailedException : System.Exception

  {

    public string FailedStage { get; }



    public CombatChainGateFailedException(string failedStage, string message) : base(message)

    {

      FailedStage = failedStage;

    }

  }

  /// <summary>Early-fail gates for wave-1 combat chain validation.</summary>

  public static class CombatChainGatekeeper

  {

    const float SpawnDeadlineSec = 5f;

    const float AfterSpawnTargetSec = 2f;

    const float AfterTargetAttackSec = 2f;

    const float AfterAttackProjectileSec = 1f;

    const float DamageDeadlineSec = 10f;

    const float KillDeadlineSec = 20f;



    enum GateStage

    {

      WaitingWaveActive,

      WaitingSpawn,

      WaitingTarget,

      WaitingAttack,

      WaitingProjectile,

      WaitingDamage,

      WaitingKill,

      Complete

    }



    public static IEnumerator MonitorWave1Combat(string contextLabel)

    {

      var stage = GateStage.WaitingWaveActive;

      var stageEnteredAt = Time.unscaledTime;

      var waveActiveAt = -1f;



      while (stage != GateStage.Complete)

      {

        if (LevelUpController.IsWaiting)

          ValidationBlockingUiAutoResponder.Tick();



        var now = Time.unscaledTime;

        var director = WaveDirector.Instance;



        if (stage == GateStage.WaitingWaveActive)

        {

          if (director != null && director.CurrentPhase == WaveDirector.Phase.WaveActive)

          {

            CombatChainTelemetry.MarkWaveActiveStart();

            waveActiveAt = CombatChainTelemetry.WaveActiveStartUnscaledTime;

            stage = GateStage.WaitingSpawn;

            stageEnteredAt = now;

          }

        }

        else if (stage == GateStage.WaitingSpawn)

        {

          if (CombatChainTelemetry.SpawnCount > 0)

          {

            stage = GateStage.WaitingTarget;

            stageEnteredAt = now;

          }

          else if (now - waveActiveAt > SpawnDeadlineSec)

            Fail(contextLabel, "spawn", $"No enemy spawn within {SpawnDeadlineSec:F0}s of wave active.");

        }

        else if (stage == GateStage.WaitingTarget)

        {

          if (CombatChainTelemetry.TargetAcquireCount > 0)

          {

            stage = GateStage.WaitingAttack;

            stageEnteredAt = now;

          }

          else if (CombatChainTelemetry.FirstSpawnUnscaledTime > 0f

                   && now - CombatChainTelemetry.FirstSpawnUnscaledTime > AfterSpawnTargetSec)

          {

            Fail(contextLabel, "target_acquire",

              $"No auto-aim target within {AfterSpawnTargetSec:F0}s of first spawn.");

          }

        }

        else if (stage == GateStage.WaitingAttack)

        {

          if (CombatChainTelemetry.AttackAttemptCount > 0)

          {

            stage = GateStage.WaitingProjectile;

            stageEnteredAt = now;

          }

          else if (CombatChainTelemetry.FirstTargetAcquireUnscaledTime > 0f

                   && now - CombatChainTelemetry.FirstTargetAcquireUnscaledTime > AfterTargetAttackSec)

          {

            Fail(contextLabel, "attack_attempt",

              $"No attack attempt within {AfterTargetAttackSec:F0}s of first target acquire.");

          }

        }

        else if (stage == GateStage.WaitingProjectile)

        {

          if (CombatChainTelemetry.ProjectileSpawnCount > 0)

          {

            stage = GateStage.WaitingDamage;

            stageEnteredAt = now;

          }

          else if (CombatChainTelemetry.FirstAttackAttemptUnscaledTime > 0f

                   && now - CombatChainTelemetry.FirstAttackAttemptUnscaledTime > AfterAttackProjectileSec)

          {

            Fail(contextLabel, "projectile_spawn",

              $"No projectile within {AfterAttackProjectileSec:F0}s of first attack attempt.");

          }

        }

        else if (stage == GateStage.WaitingDamage)

        {

          if (CombatChainTelemetry.DamageEventCount > 0)

          {

            stage = GateStage.WaitingKill;

            stageEnteredAt = now;

          }

          else if (waveActiveAt > 0f && now - waveActiveAt > DamageDeadlineSec)

          {

            Fail(contextLabel, "damage",

              $"No player damage event within {DamageDeadlineSec:F0}s of wave active.");

          }

        }

        else if (stage == GateStage.WaitingKill)

        {

          if (CombatChainTelemetry.KillCount > 0)

          {

            stage = GateStage.Complete;

            yield break;

          }



          if (waveActiveAt > 0f && now - waveActiveAt > KillDeadlineSec)

          {

            Fail(contextLabel, "kill",

              $"No enemy kill within {KillDeadlineSec:F0}s of wave active.");

          }

        }



        if (director != null

            && director.CurrentPhase == WaveDirector.Phase.BuildPhase

            && director.CurrentWave > 1)

        {

          Fail(contextLabel, "wave_skipped",

            "Wave advanced past W1 before combat chain gates completed — likely harness time-skip without real kills.");

        }



        yield return null;

      }

    }



    static void Fail(string contextLabel, string stage, string message)

    {

      var sb = new StringBuilder();

      sb.Append('[').Append(contextLabel).Append("] ").Append(message).Append('\n');

      sb.Append(CombatChainTelemetry.FormatSnapshot(stage));

      throw new CombatChainGateFailedException(stage, sb.ToString());

    }

  }

}

#endif

