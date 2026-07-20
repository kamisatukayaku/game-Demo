#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD

using System.Text;

using Game.Modes.Roguelike.Combat;

using Game.Modes.Roguelike.Progression;

using Game.Shared.Core;

using Game.Shared.Enemy.Spawn;

using Game.Shared.Player;

using UnityEngine;



namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation

{

  /// <summary>Step-by-step combat chain counters for runtime validation.</summary>

  public static class CombatChainTelemetry

  {

    public static int SpawnCount { get; private set; }

    public static int RegistryRegisterCount { get; private set; }

    public static int TargetAcquireCount { get; private set; }

    public static int AttackAttemptCount { get; private set; }

    public static int ProjectileSpawnCount { get; private set; }

    public static int DamageEventCount { get; private set; }

    public static int KillCount { get; private set; }

    public static int XpPickupSpawnCount { get; private set; }

    public static int XpPickupCollectCount { get; private set; }



    public static float FirstSpawnUnscaledTime { get; private set; } = -1f;

    public static float FirstTargetAcquireUnscaledTime { get; private set; } = -1f;

    public static float FirstAttackAttemptUnscaledTime { get; private set; } = -1f;

    public static float FirstProjectileSpawnUnscaledTime { get; private set; } = -1f;

    public static float FirstDamageUnscaledTime { get; private set; } = -1f;

    public static float FirstKillUnscaledTime { get; private set; } = -1f;

    public static float WaveActiveStartUnscaledTime { get; private set; } = -1f;



    public static string LastAttackBlockReason { get; private set; } = "none";



    static readonly System.Collections.Generic.Dictionary<string, int> _attackBlockCounts = new();



    public static void Reset()

    {

      SpawnCount = 0;

      RegistryRegisterCount = 0;

      TargetAcquireCount = 0;

      AttackAttemptCount = 0;

      ProjectileSpawnCount = 0;

      DamageEventCount = 0;

      KillCount = 0;

      XpPickupSpawnCount = 0;

      XpPickupCollectCount = 0;

      FirstSpawnUnscaledTime = -1f;

      FirstTargetAcquireUnscaledTime = -1f;

      FirstAttackAttemptUnscaledTime = -1f;

      FirstProjectileSpawnUnscaledTime = -1f;

      FirstDamageUnscaledTime = -1f;

      FirstKillUnscaledTime = -1f;

      WaveActiveStartUnscaledTime = -1f;

      LastAttackBlockReason = "none";

      _attackBlockCounts.Clear();

    }



    public static void MarkWaveActiveStart()

    {

      if (WaveActiveStartUnscaledTime < 0f)

        WaveActiveStartUnscaledTime = Time.unscaledTime;

    }



    public static void RecordSpawn()

    {

      SpawnCount++;

      if (FirstSpawnUnscaledTime < 0f)

        FirstSpawnUnscaledTime = Time.unscaledTime;

    }



    public static void RecordRegistryRegister() => RegistryRegisterCount++;



    public static void RecordTargetAcquire()

    {

      TargetAcquireCount++;

      if (FirstTargetAcquireUnscaledTime < 0f)

        FirstTargetAcquireUnscaledTime = Time.unscaledTime;

    }



    public static void RecordAttackAttempt()

    {

      AttackAttemptCount++;

      if (FirstAttackAttemptUnscaledTime < 0f)

        FirstAttackAttemptUnscaledTime = Time.unscaledTime;

    }



    public static void RecordAttackBlocked(string reason)

    {

      if (string.IsNullOrEmpty(reason))

        reason = "unknown";

      LastAttackBlockReason = reason;

      _attackBlockCounts.TryGetValue(reason, out var count);

      _attackBlockCounts[reason] = count + 1;

    }



    public static void RecordProjectileSpawn()

    {

      ProjectileSpawnCount++;

      if (FirstProjectileSpawnUnscaledTime < 0f)

        FirstProjectileSpawnUnscaledTime = Time.unscaledTime;

    }



    public static void RecordPlayerDamage()

    {

      DamageEventCount++;

      if (FirstDamageUnscaledTime < 0f)

        FirstDamageUnscaledTime = Time.unscaledTime;

    }



    public static void RecordKill()

    {

      KillCount++;

      if (FirstKillUnscaledTime < 0f)

        FirstKillUnscaledTime = Time.unscaledTime;

    }



    public static void RecordXpPickupSpawn() => XpPickupSpawnCount++;



    public static void RecordXpPickupCollect() => XpPickupCollectCount++;



    public static int GetAttackBlockCount(string reason) =>

      _attackBlockCounts.TryGetValue(reason, out var count) ? count : 0;



    public static string FormatSnapshot(string failedStage = null)

    {

      var sb = new StringBuilder();

      if (!string.IsNullOrEmpty(failedStage))

      {

        sb.Append("FAILED_STAGE=").Append(failedStage).Append('\n');

      }



      sb.Append("chain: spawn=").Append(SpawnCount)

        .Append(" registry=").Append(RegistryRegisterCount)

        .Append(" target=").Append(TargetAcquireCount)

        .Append(" attack=").Append(AttackAttemptCount)

        .Append(" projectile=").Append(ProjectileSpawnCount)

        .Append(" damage=").Append(DamageEventCount)

        .Append(" kill=").Append(KillCount)

        .Append(" xp_spawn=").Append(XpPickupSpawnCount)

        .Append(" xp_collect=").Append(XpPickupCollectCount)

        .Append(" levelup_pause=").Append(RuntimeValidationTelemetry.LevelUpPauseCount)

        .Append('\n');



      sb.Append("timings(unscaled): wave_active=").Append(FormatTime(WaveActiveStartUnscaledTime))

        .Append(" spawn=").Append(FormatTime(FirstSpawnUnscaledTime))

        .Append(" target=").Append(FormatTime(FirstTargetAcquireUnscaledTime))

        .Append(" attack=").Append(FormatTime(FirstAttackAttemptUnscaledTime))

        .Append(" projectile=").Append(FormatTime(FirstProjectileSpawnUnscaledTime))

        .Append(" damage=").Append(FormatTime(FirstDamageUnscaledTime))

        .Append(" kill=").Append(FormatTime(FirstKillUnscaledTime))

        .Append('\n');



      sb.Append("last_attack_block=").Append(LastAttackBlockReason)

        .Append(" blocks{dead=").Append(GetAttackBlockCount("player_dead"))

        .Append(",pause=").Append(GetAttackBlockCount("combat_pause"))

        .Append(",cooldown=").Append(GetAttackBlockCount("cooldown"))

        .Append(",no_input=").Append(GetAttackBlockCount("no_input"))

        .Append(",input_gate=").Append(GetAttackBlockCount("input_gate"))

        .Append(",no_target=").Append(GetAttackBlockCount("no_target"))

        .Append(",reflect=").Append(GetAttackBlockCount("reflect_theme"))

        .Append("}\n");



      AppendPlayerSnapshot(sb);

      AppendWaveSnapshot(sb);

      return sb.ToString();

    }



    static void AppendPlayerSnapshot(StringBuilder sb)

    {

      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");

      if (player == null)

      {

        sb.Append("player: missing\n");

        return;

      }



      var health = player.GetComponent<Game.Shared.Combat.Health.Health>();

      var autoAttack = player.GetComponent<PlayerAutoAttack>();

      var director = player.GetComponent<PlayerAttackDirector>();

      var applier = player.GetComponent<Game.Modes.Roguelike.Build.Apply.RunBuildApplier>();

      var registry = CombatRoot.EnemyRegistry;



      sb.Append("player: dead=").Append(health != null && health.IsDead)

        .Append(" invuln=").Append(health != null && health.IsInvulnerable)

        .Append(" layer=").Append(player.layer)

        .Append(" tag=").Append(player.tag)

        .Append('\n');



      sb.Append("auto_attack: component=").Append(autoAttack != null)

        .Append(" enabled=").Append(autoAttack != null && autoAttack.enabled)

        .Append(" director=").Append(director != null)

        .Append(" profile=").Append(director != null ? director.AttackProfileId : "null")

        .Append(" applier=").Append(applier != null)

        .Append('\n');



      sb.Append("pause: timeScale=").Append(Time.timeScale.ToString("F2"))

        .Append(" combat_pause=").Append(CombatTimePause.IsPaused)

        .Append(" levelup_waiting=").Append(LevelUpController.IsWaiting)

        .Append(" corruption_ui=").Append(Game.Modes.Roguelike.UI.ArenaCorruptionPickUI.IsShowing)

        .Append(" relic_ui=").Append(Game.Modes.Roguelike.UI.ArenaRelicPickUI.IsShowing)

        .Append(" input_gate=").Append(Game.Shared.Gameplay.Input.GameplayInputGateLocator.BlocksPlayerInput)

        .Append('\n');



      sb.Append("starter: build=").Append(ArenaBuildBootstrap.SelectedBuildId)

        .Append(" theme=").Append(Game.Modes.Roguelike.Build.Runtime.RunBuildState.WeaponTheme)

        .Append(" synthetic_input=").Append(RuntimeValidationSettings.UseSyntheticInput)

        .Append('\n');



      sb.Append("registry: exists=").Append(registry != null)

        .Append(" live=").Append(registry != null ? registry.Count : 0)

        .Append(" projectiles=").Append(Object.FindObjectsByType<Game.Shared.Projectile.StraightProjectile>(FindObjectsSortMode.None).Length)

        .Append('\n');

    }



    static void AppendWaveSnapshot(StringBuilder sb)

    {

      var director = WaveDirector.Instance;

      if (director == null)

      {

        sb.Append("wave: director=null\n");

        return;

      }



      sb.Append("wave: phase=").Append(director.CurrentPhase)

        .Append(" current=").Append(director.CurrentWave)

        .Append(" spawned_telemetry=").Append(RuntimeValidationTelemetry.EnemiesSpawned)

        .Append(" killed_telemetry=").Append(RuntimeValidationTelemetry.EnemiesKilled)

        .Append('\n');

    }



    static string FormatTime(float t) => t < 0f ? "-" : (t - WaveActiveStartUnscaledTime).ToString("F2");

  }

}

#endif

