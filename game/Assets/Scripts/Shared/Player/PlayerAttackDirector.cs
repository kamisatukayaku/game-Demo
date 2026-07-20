using System;
using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Events;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Projectile;
using Game.Shared.UI;
using Game.Shared.Vfx;
using Game.Shared.Laser;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Input;
namespace Game.Shared.Player
{
  /// <summary>
  /// 玩家攻击：由 attack profile ?delivery / targeting 驱动，装备加成由外部注入?
  /// </summary>
  [DisallowMultipleComponent]
  public class PlayerAttackDirector : MonoBehaviour
  {
    [SerializeField] string attackProfileId = "weapon_starter_melee";
    [SerializeField] Color projectileColor = new(1f, 0.92f, 0.35f, 1f);
    [SerializeField] Color laserColor = new(0.4f, 0.95f, 1f, 1f);
    [SerializeField] bool debugLogAttacks;

    float _cooldownTimer;
    float _hybridMeleeRange = 1.6f;
    string _hybridRangedProfileId = "weapon_starter_bolt";

    float _eqAttackMult = 1f;
    float _eqAttackFlatAdd;
    float _eqAttackSpeedMult = 1f;
    float _eqRangeAdd;
    float _eqRangedRange = float.MaxValue; // 远程攻击基础范围，默认无限（向后兼容）
    float _eqCritChance;
    float _eqCritDamage = 1.5f;
    float _eqLifesteal;
    float _eqCooldownMult = 1f;

    bool _autoAttack; // 自动攻击开关

    // v2: Build 构筑效果
    BuildModifiers _buildMods;
    static string s_eventWeaponTheme = "melee";

    Health _playerHealth;
    IEquipmentDamageReporter _equipReporter;
    PlayerAutoAttack _legacyHost;

    /// <summary>?RunBuildApplier 注入的构筑效果参数?/summary>
    public struct BuildModifiers
    {
      // 弹体通用
      public int extraProjectiles;
      public int pierceCount;
      public bool pierceNoFalloff;
      public float explosionRadius;
      public float explosionDamageRatio;
      public float explosionDamageMult;
      public int chainCount;
      public float chainDamageRatio;
      public float chainJumpRange;
      public float chainFalloffPerJump;
      public bool weakHoming;
      public float homingTurnRate;
      public bool sideShed;
      public int trailSprayCount;
      public int splitOnHitCount;
      public bool heavyShot;
      public float projectileSpeedMult;
      public float projectileSizeMult;
      public bool homing;
      public float slowChance;
      public float slowAmount;
      public float burnDps;
      public float burnDuration;
      public float eliteDamageMult;
      public float bossDamageMult;
      public float longRangeDamageMult;
      public float slowTargetDamageMult;
      public bool explosionVacuum;

      // 近战专属
      public float meleeExplosionRadius;
      public float meleeKnockbackChance;
      public float meleeSlowChance;
      public float meleeSlowAmount;
      public float meleeBleedDps;
      public float meleeBleedDuration;
      public float meleeBurnDps;
      public float meleeBurnDuration;
      public int meleeComboCount;
      public float meleeArcHalfAngle;

      // 连发/弹体增强
      public int burstCount;

      // 技能专局"
      public int skillExtraProjectiles;
      public float skillCritChance;
      public float skillCritDamage;
      public float skillSlowChance;
      public float skillSlowAmount;
      public float skillBurnDps;
      public float skillBurnDuration;
      public int skillChainCount;
      public int skillPierce;
      public float skillExplosionRadius;
      public float skillExplosionRatio;
      public float skillChainDamageRatio;
      public int skillEchoCount;
      public float skillEchoGuarantee;
      public bool skillVacuum;
      public int skillMirrorCast;
      public int skillSplitOnHit;
      public bool skillHoming;
      public float skillHomingTurnRate;
      public int skillVolleyOnCast;

      // ?RunBuildApplier 注入的运行时构筑摘要（避?Shared 依赖 Roguelike?
      public string weaponTheme;
      public float healOnHitPct;
      public float skillDamageMult;
      public float skillRangeMult;
      public float skillCooldownReduce;
      public float mageArcaneDamageMult;
      public float mageArcaneProjectileSpeed;
      public float mageArcaneCooldownReduce;

      public float spreadAngleDegrees;
      public float primaryProjectileDamageMult;
      public bool volleyStagger;
      public int auxiliaryExplosiveTier;
      public int auxiliaryLightningTier;
      public float auxiliaryExplosiveInterval;
      public float auxiliaryLightningInterval;
      public int auxiliaryExplosiveProjectileCount;
      public float auxiliaryExplosiveSpreadAngle;
      public int auxiliaryExplosivePierce;
      public float auxiliaryExplosiveProjectileDamageMult;
      public int auxiliaryLightningProjectileCount;
      public float auxiliaryLightningSpreadAngle;
      public int auxiliaryLightningPierce;
      public float auxiliaryLightningProjectileDamageMult;
      public bool explosionSecondaryWave;
      public bool explosionFragmentBurst;
      public bool explosionChainDetonate;
      public bool explosionSaturation;
      public int lightningForkJumps;
      public bool lightningConductMark;
      public bool lightningNetwork;
      public bool pierceTrailFeedback;
    }

    int _volleyStaggerPhase;

    public void SetBuildModifiers(BuildModifiers mods)
    {
      _buildMods = mods;
      if (!string.IsNullOrEmpty(mods.weaponTheme))
        s_eventWeaponTheme = mods.weaponTheme;
    }

    public BuildModifiers GetBuildModifiers() => _buildMods;

    public static event Action<string, string> AttackPerformed;

    public string AttackProfileId => attackProfileId;

    public void BindLegacyHost(PlayerAutoAttack host) => _legacyHost = host;

    void Awake()
    {
      _playerHealth = GetComponent<Health>();
      _equipReporter = GetComponent<IEquipmentDamageReporter>();
      _legacyHost = GetComponent<PlayerAutoAttack>();
    }

    void OnEnable() => CombatEventBus.OnAttackHit += HandleAttackHit;
    void OnDisable() => CombatEventBus.OnAttackHit -= HandleAttackHit;

    void HandleAttackHit(CombatEventBus.AttackHitArgs args)
    {
      if (args.Attacker != gameObject)
        return;
      OnDealDamage(args.Target?.transform, args.Damage);
    }

    public void SetAttackProfile(string profileId)
    {
      if (string.IsNullOrEmpty(profileId))
        return;

      attackProfileId = profileId;
      _cooldownTimer = 0f;
    }

    public void SetHybridFallback(float meleeRange, string rangedProfileId)
    {
      _hybridMeleeRange = meleeRange;
      _hybridRangedProfileId = rangedProfileId;
    }

    public void SetEquipmentModifiers(
      float attackMult,
      float attackSpeedMult,
      float rangeAdd,
      float critChance,
      float critDamage,
      float lifesteal,
      float cooldownMult,
      float attackFlatAdd = 0f,
      float rangedRange = float.MaxValue)
    {
      _eqAttackMult = attackMult;
      _eqAttackFlatAdd = attackFlatAdd;
      _eqAttackSpeedMult = attackSpeedMult;
      _eqRangeAdd = rangeAdd;
      _eqRangedRange = rangedRange;
      _eqCritChance = critChance;
      _eqCritDamage = critDamage;
      _eqLifesteal = lifesteal;
      _eqCooldownMult = cooldownMult;
    }

    /// <summary>设置自动攻击模式（true=范围内有敌人时自动瞄准并开火）。</summary>
    public void SetAutoAttack(bool enabled)
    {
      _autoAttack = enabled;
    }

    public void Tick()
    {
      if (_playerHealth != null && _playerHealth.IsDead)
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("player_dead");
#endif
        return;
      }

      if (CombatTimePause.IsPaused)
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("combat_pause");
#endif
        return;
      }

      if (!string.IsNullOrEmpty(_buildMods.weaponTheme) && _buildMods.weaponTheme == "reflect")
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("reflect_theme");
#endif
        return;
      }

      _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);
      if (_cooldownTimer > 0f)
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("cooldown");
#endif
        return;
      }

      if (!_autoAttack && !GameInputBindings.IsHeld(GameInputBindings.InputAction.Attack))
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("no_input");
#endif
        return;
      }

      if (GameplayInputGateLocator.BlocksPlayerInput)
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("input_gate");
#endif
        return;
      }

      var aimDir = PlayerAimController.GetAimDirectionOrDefault();
      if (_autoAttack)
      {
        if (!TryGetAutoAimDirection(out aimDir))
        {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
          Game.Shared.Diagnostics.CombatValidationHooks.OnAttackBlocked?.Invoke("no_target");
#endif
          return;
        }
      }

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      Game.Shared.Diagnostics.CombatValidationHooks.OnAttackAttempt?.Invoke();
#endif
      ExecuteAttack(aimDir);
    }

    bool TryGetAutoAimDirection(out Vector2 aimDirection)
    {
      aimDirection = Vector2.right;
      var registry = CombatRoot.EnemyRegistry;
      var profile = AttackProfileDatabase.Get(attackProfileId);
      if (profile == null)
        return false;

      var origin = GameplayPlane.Position2D(transform);
      var acquisitionRange = Mathf.Max(1f, profile.range + _eqRangeAdd);
      var bestSqrDistance = float.MaxValue;
      var found = false;

      if (registry != null)
      {
        foreach (var enemy in registry.GetInRange(origin, acquisitionRange))
        {
          if (enemy == null || !TryGetHealth(enemy.transform, out var health) || health.IsDead)
            continue;

          var delta = GameplayPlane.Position2D(enemy.transform) - origin;
          var sqrDistance = delta.sqrMagnitude;
          if (sqrDistance < 0.0001f || sqrDistance >= bestSqrDistance)
            continue;

          bestSqrDistance = sqrDistance;
          aimDirection = delta.normalized;
          found = true;
        }
      }

      if (found)
      {
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
        Game.Shared.Diagnostics.CombatValidationHooks.OnTargetAcquired?.Invoke();
#endif
      }

      if (!found)
      {
        var fallback = FindNearestEnemy(acquisitionRange);
        if (fallback != null)
        {
          var delta = GameplayPlane.Position2D(fallback) - origin;
          if (delta.sqrMagnitude > 0.0001f)
          {
            aimDirection = delta.normalized;
            found = true;
#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
            Game.Shared.Diagnostics.CombatValidationHooks.OnTargetAcquired?.Invoke();
#endif
          }
        }
      }

      return found;
    }

    /// <summary>Developer Sandbox：绕过输入直接攻击?/summary>
    public void SandboxExecuteAttack(Vector2 aimDir)
    {
      if (_playerHealth != null && _playerHealth.IsDead)
        return;

      _cooldownTimer = 0f;
      ExecuteAttack(aimDir);
    }

    public bool TryResolveAutoAim(out Vector2 aimDirection) => TryGetAutoAimDirection(out aimDirection);

    public void FireAuxiliaryExplosive(Vector2 aimDir)
    {
      if (_buildMods.auxiliaryExplosiveTier <= 0)
        return;

      var profile = AttackProfileDatabase.Get(attackProfileId);
      if (profile == null)
        return;

      var request = BuildDamageRequest(profile.base_damage, profile);
      request.Base *= Mathf.Max(0.15f, 1f + _buildMods.auxiliaryExplosiveProjectileDamageMult);
      var mods = new ProjectileBuildModifiers
      {
        pierceCount = Mathf.Max(0, _buildMods.auxiliaryExplosivePierce),
        explosionRadius = _buildMods.explosionRadius > 0.01f
          ? _buildMods.explosionRadius
          : 1.15f + Mathf.Max(0, _buildMods.auxiliaryExplosiveTier - 1) * 0.15f,
        explosionDamageRatio = _buildMods.explosionDamageRatio > 0f ? _buildMods.explosionDamageRatio : 0.55f,
        explosionDamageMult = _buildMods.explosionDamageMult,
        explosionVacuum = _buildMods.explosionVacuum,
        explosionSecondaryWave = _buildMods.explosionSecondaryWave,
        explosionFragmentBurst = _buildMods.explosionFragmentBurst,
        explosionChainDetonate = _buildMods.explosionChainDetonate,
        explosionSaturation = _buildMods.explosionSaturation
      };

      var count = Mathf.Max(1, 1 + _buildMods.auxiliaryExplosiveProjectileCount);
      SpawnAuxiliaryVolley(
        aimDir, profile, request, mods, new Color(1f, 0.78f, 0.32f, 1f), 1.22f,
        count,
        _buildMods.auxiliaryExplosiveSpreadAngle,
        RangedProjectileVisualKind.Explosive);
      RangedMuzzleFlashVfx.Spawn(transform.position + (Vector3)aimDir * 0.35f, 1.1f);
    }

    public void FireAuxiliaryLightning(Vector2 aimDir)
    {
      if (_buildMods.auxiliaryLightningTier <= 0)
        return;

      var profile = AttackProfileDatabase.Get(attackProfileId);
      if (profile == null)
        return;

      var request = BuildDamageRequest(profile.base_damage, profile);
      request.Base *= Mathf.Max(0.15f, 1f + _buildMods.auxiliaryLightningProjectileDamageMult);
      var mods = new ProjectileBuildModifiers
      {
        pierceCount = Mathf.Max(0, _buildMods.auxiliaryLightningPierce),
        chainCount = Mathf.Max(1, _buildMods.chainCount),
        chainDamageRatio = _buildMods.chainDamageRatio > 0f ? _buildMods.chainDamageRatio : 0.55f,
        chainJumpRange = _buildMods.chainJumpRange > 0.01f ? _buildMods.chainJumpRange : 6f,
        chainFalloffPerJump = _buildMods.chainFalloffPerJump,
        lightningForkJumps = _buildMods.lightningForkJumps,
        lightningConductMark = _buildMods.lightningConductMark,
        lightningNetwork = _buildMods.lightningNetwork
      };

      var count = Mathf.Max(1, 1 + _buildMods.auxiliaryLightningProjectileCount);
      SpawnAuxiliaryVolley(
        aimDir, profile, request, mods, new Color(0.42f, 0.86f, 1f, 1f), 1.18f,
        count,
        _buildMods.auxiliaryLightningSpreadAngle,
        RangedProjectileVisualKind.Lightning);
      RangedMuzzleFlashVfx.Spawn(transform.position + (Vector3)aimDir * 0.35f, 1.05f);
    }

    void ExecuteAttack(Vector2 aimDir)
    {
      var profile = AttackProfileDatabase.Get(attackProfileId);
      if (profile == null)
        return;

      var delivery = string.IsNullOrEmpty(profile.delivery) ? "hybrid" : profile.delivery;
      var range = profile.range + _eqRangeAdd;
      var meleeArcAngle = _buildMods.meleeArcHalfAngle > 0f ? _buildMods.meleeArcHalfAngle : 75f;

      switch (delivery)
      {
        case "melee":
          if (TryFindMeleeTarget(aimDir, range, meleeArcAngle, out var meleeHealth))
            FireMelee(profile, meleeHealth);
          else
            StartCooldown(profile.cooldown);
          NotifyAttackPerformed(profile);
          break;

        case "projectile":
          FireProjectileDirectional(profile, aimDir, _eqRangedRange);
          NotifyAttackPerformed(profile);
          break;

        case "beam":
          if (!TryFindMeleeTarget(aimDir, range, 18f, out var beamTarget))
            return;
          FireBeam(profile, beamTarget.transform, range);
          NotifyAttackPerformed(profile);
          break;

        case "hybrid":
          if (TryFindMeleeTarget(aimDir, _hybridMeleeRange + _eqRangeAdd, meleeArcAngle, out var hybridMelee))
          {
            FireMelee(profile, hybridMelee);
            NotifyAttackPerformed(profile);
          }
          else
          {
            var rangedProfile = AttackProfileDatabase.Get(_hybridRangedProfileId) ?? profile;
            FireProjectileDirectional(rangedProfile, aimDir, _eqRangedRange);
            NotifyAttackPerformed(rangedProfile);
          }
          break;

        default:
          if (!TryFindMeleeTarget(aimDir, range, meleeArcAngle, out var fallbackHealth))
            return;
          FireMelee(profile, fallbackHealth);
          NotifyAttackPerformed(profile);
          break;
      }
    }

    void NotifyAttackPerformed(AttackProfileDatabase.AttackProfile profile)
    {
      var theme = string.IsNullOrEmpty(_buildMods.weaponTheme) ? "melee" : _buildMods.weaponTheme;
      var delivery = profile?.delivery ?? "melee";
      AttackPerformed?.Invoke(theme, delivery);
    }

    public static void NotifyReflectParryPerformed()
    {
      var theme = string.IsNullOrEmpty(s_eventWeaponTheme) ? "reflect" : s_eventWeaponTheme;
      AttackPerformed?.Invoke(theme, "reflect");
    }

    public float ComputeCooldown(float baseCooldown)
    {
      var speed = Mathf.Max(0.2f, _eqAttackSpeedMult * GetBuffAttackSpeed());
      return baseCooldown * _eqCooldownMult / speed;
    }

    bool TryFindMeleeTarget(Vector2 aimDir, float range, out Health health)
    {
      return TryFindMeleeTarget(aimDir, range, 75f, out health);
    }

    bool TryFindMeleeTarget(Vector2 aimDir, float range, float arcHalfAngleDeg, out Health health)
    {
      health = null;
      var origin = GameplayPlane.Position2D(transform);
      Transform best = null;
      var bestDist = float.MaxValue;

      var enemyReg = CombatRoot.EnemyRegistry;
      if (enemyReg != null)
      {
        foreach (var enemy in enemyReg.GetInRange(origin, range))
        {
          if (enemy == null)
            continue;

          if (!TryGetHealth(enemy.transform, out var candidate))
            continue;

          var toEnemy = GameplayPlane.Position2D(enemy.transform) - origin;
          if (toEnemy.sqrMagnitude < 0.0001f)
            continue;

          if (Vector2.Angle(aimDir, toEnemy.normalized) > arcHalfAngleDeg)
            continue;

          var dist = toEnemy.magnitude;
          if (dist > range || dist >= bestDist)
            continue;

          bestDist = dist;
          best = enemy.transform;
          health = candidate;
        }
      }

      return best != null;
    }

    void FireMelee(AttackProfileDatabase.AttackProfile profile, Health health)
    {
      var request = BuildDamageRequest(profile.base_damage, profile);
      ApplyMeleeBuildModifiers(ref request, health);
      var result = DamagePipeline.Apply(request, health);

      // 近战命中后处理构筑效果（传入实际伤害）
      HandleMeleeOnHitEffects(health, result.FinalDamage);

      // 近战连击：额外快速连续判定
      var comboCount = _buildMods.meleeComboCount;
      if (comboCount > 1)
      {
        StartCoroutine(MeleeComboRoutine(profile, health, comboCount - 1));
      }

      StartCooldown(profile.cooldown);
      LogAttack($"Melee {health.name} for {result.FinalDamage:F1}" +
        (comboCount > 1 ? $" (combo ×{comboCount})" : ""));
    }

    /// <summary>近战连击协程：每 0.1s 独立判定一次伤害，显示独立伤害数字。</summary>
    System.Collections.IEnumerator MeleeComboRoutine(
      AttackProfileDatabase.AttackProfile profile, Health target, int remainingHits)
    {
      const float comboInterval = 0.1f;

      for (int i = 0; i < remainingHits; i++)
      {
        yield return new WaitForSeconds(comboInterval);

        if (_playerHealth != null && _playerHealth.IsDead)
          yield break;
        if (target == null || target.IsDead)
          yield break;

        // 检查目标是否仍在范围内
        var dist = GameplayPlane.PlanarDistance(target.transform.position, transform.position);
        var range = profile.range + _eqRangeAdd;
        if (dist > range + 1f)
          yield break;

        var comboRequest = BuildDamageRequest(profile.base_damage, profile);
        ApplyMeleeBuildModifiers(ref comboRequest, target);
        var comboResult = DamagePipeline.Apply(comboRequest, target);
        HandleMeleeOnHitEffects(target, comboResult.FinalDamage);

        LogAttack($"Melee combo {i + 2}/{remainingHits + 1} {target.name} for {comboResult.FinalDamage:F1}");
      }
    }

    void ApplyMeleeBuildModifiers(ref DamageRequest request, Health target)
    {
      if (target == null) return;
      var go = target.gameObject;

      // 精英/Boss 伤害加成（通过 tag 判断；用 tag 属性避?CompareTag ?tag 未注册时报错?
      bool isBoss = EnemySpawnMetadata.IsBossEnemy(go);
      bool isEliteOrBoss = go.tag == "Elite" || isBoss;
      if (isEliteOrBoss)
      {
        if (_buildMods.eliteDamageMult > 0f)
          request.Base *= (1f + _buildMods.eliteDamageMult);
        if (_buildMods.bossDamageMult > 0f && isBoss)
          request.Base *= (1f + _buildMods.bossDamageMult);
      }

      // 对减速目标伤害加戀"
      if (_buildMods.slowTargetDamageMult > 0f)
      {
        var buff = target.GetComponent<BuffContainer>();
        if (buff != null && buff.HasSlowEffect())
          request.Base *= (1f + _buildMods.slowTargetDamageMult);
      }
    }

    void HandleMeleeOnHitEffects(Health target, float actualDamage)
    {
      if (target == null || target.IsDead) return;

      // 爆炸
      if (_buildMods.meleeExplosionRadius > 0f)
      {
        var enemyReg = CombatRoot.EnemyRegistry;
        if (enemyReg != null)
        {
          var explosionDmg = actualDamage * 0.5f;
          var hitPos = target.transform.position;
          BulletExplosionEffect.Spawn(hitPos, _buildMods.meleeExplosionRadius);
          var enemies = enemyReg.GetInRange(
            GameplayPlane.Position2D(target.transform), _buildMods.meleeExplosionRadius);
          foreach (var e in enemies)
          {
            if (e == null || e.transform == target.transform) continue;
            var eHealth = e.GetComponent<Health>();
            if (eHealth != null && !eHealth.IsDead)
            {
              var req = DamageRequest.Direct(explosionDmg, "physical", "weapon", gameObject);
              DamagePipeline.Apply(req, eHealth);
            }
          }
        }
      }

      // 击退
      if (_buildMods.meleeKnockbackChance > 0f && UnityEngine.Random.value < _buildMods.meleeKnockbackChance)
      {
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
          var dir = (target.transform.position - transform.position).normalized;
          rb.AddForce(dir * 8f, ForceMode2D.Impulse);
        }
      }

      // 减速"
      if (_buildMods.meleeSlowChance > 0f && UnityEngine.Random.value < _buildMods.meleeSlowChance)
      {
        var buff = target.GetComponent<BuffContainer>();
        buff?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          sourceKind = "weapon",
          abilityId = "melee_slow",
          stacks = 1,
          customSlowAmount = _buildMods.meleeSlowAmount,
          customDuration = 1.5f
        });
      }

      // 流血
      if (_buildMods.meleeBleedDps > 0f)
      {
        var buff = target.GetComponent<BuffContainer>();
        buff?.ApplyBuff("buff_bleed", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          sourceKind = "weapon",
          abilityId = "melee_bleed",
          stacks = 1,
          customDps = _buildMods.meleeBleedDps,
          customDuration = _buildMods.meleeBleedDuration
        });
      }

      // 灼烧
      if (_buildMods.meleeBurnDps > 0f)
      {
        var buff = target.GetComponent<BuffContainer>();
        buff?.ApplyBuff("buff_burn", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          sourceKind = "weapon",
          abilityId = "melee_burn",
          stacks = 1,
          customDps = _buildMods.meleeBurnDps,
          customDuration = _buildMods.meleeBurnDuration
        });
      }
    }

    void FireProjectileDirectional(AttackProfileDatabase.AttackProfile profile, Vector2 aimDir, float maxRange, bool skillCast = false)
    {
      const float playerProjectileSpeedMult = 1.2f;

      var speed = (profile.projectile_speed > 0f ? profile.projectile_speed : 14f) * playerProjectileSpeedMult;
      var scale = (profile.projectile_scale > 0f ? profile.projectile_scale : 0.2f) * 1.55f
        * Mathf.Max(0.2f, _buildMods.projectileSizeMult > 0f ? _buildMods.projectileSizeMult : 1f);
      var isSkill = string.Equals(profile.damage_source, "skill", StringComparison.OrdinalIgnoreCase);
      var forceHeavy = !skillCast && !isSkill && _buildMods.heavyShot;
      var isArcaneMissile = profile.id == "skill_mage_arcane_bolt";
      var speedMult = isSkill
        ? (isArcaneMissile ? Mathf.Max(0.35f, _buildMods.mageArcaneProjectileSpeed) : 1f)
        : Mathf.Max(0.35f, 1f + _buildMods.projectileSpeedMult);
      if (forceHeavy)
        speedMult *= 0.55f;
      speed *= speedMult;

      var request = BuildDamageRequest(profile.base_damage, profile);
      if (forceHeavy)
      {
        request.Base *= 1.45f;
      }

      if (!isSkill && !skillCast && Mathf.Abs(_buildMods.primaryProjectileDamageMult - 1f) > 0.001f)
        request.Base *= Mathf.Max(0.2f, _buildMods.primaryProjectileDamageMult);

      var extraProjectiles = isSkill
        ? _buildMods.skillExtraProjectiles + _buildMods.skillVolleyOnCast
        : _buildMods.extraProjectiles;
      var count = 1 + extraProjectiles;

      // Burst fire: 技能不使用连发
      var burstCount = !skillCast && !isSkill ? _buildMods.burstCount : 0;
      if (burstCount > 1)
      {
        StartCoroutine(BurstFireRoutine(aimDir, maxRange, speed, scale, request, profile, count, burstCount, profile.cooldown));
        return;
      }

      SpawnVolley(aimDir, maxRange, speed, scale, request, profile, count);

      if (!skillCast)
        StartCooldown(profile.cooldown);
      LogAttack($"Projectile×{count} dir={aimDir} base={profile.base_damage:F1}");
    }

    /// <summary>发射一组弹体（1 或 N 个扇形散布）。</summary>
    void SpawnVolley(Vector2 aimDir, float maxRange, float speed, float scale, DamageRequest request, AttackProfileDatabase.AttackProfile profile, int count)
    {
      var spreadDeg = _buildMods.spreadAngleDegrees > 0.01f
        ? _buildMods.spreadAngleDegrees
        : ComputeVolleySpreadDegrees(count);
      var staggerOffset = 0f;
      if (_buildMods.volleyStagger && count > 2)
      {
        staggerOffset = (_volleyStaggerPhase % 2 == 0 ? -1f : 1f) * (spreadDeg / Mathf.Max(1, count - 1)) * 0.5f;
        _volleyStaggerPhase++;
      }

      if (count <= 1)
      {
        SpawnPlayerProjectile(aimDir, maxRange, speed, scale, request, profile);
        return;
      }

      var baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg + staggerOffset;
      for (var i = 0; i < count; i++)
      {
        var t = i / (float)(count - 1) - 0.5f;
        var angle = baseAngle + t * spreadDeg;
        var rad = angle * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        SpawnPlayerProjectile(dir, maxRange, speed, scale, request, profile);
      }
    }

    static float ComputeVolleySpreadDegrees(int projectileCount)
    {
      if (projectileCount <= 1)
        return 0f;
      return Mathf.Clamp(20f + projectileCount * 4.5f, 28f, 78f);
    }

    /// <summary>连发协程：每 0.08s 发射一组弹体。</summary>
    System.Collections.IEnumerator BurstFireRoutine(
      Vector2 aimDir, float maxRange, float speed, float scale,
      DamageRequest request, AttackProfileDatabase.AttackProfile profile,
      int projectileCount, int burstCount, float cooldown)
    {
      const float burstInterval = 0.08f;

      for (int i = 0; i < burstCount; i++)
      {
        if (_playerHealth != null && _playerHealth.IsDead)
          yield break;

        SpawnVolley(aimDir, maxRange, speed, scale, request, profile, projectileCount);

        if (i < burstCount - 1)
          yield return new WaitForSeconds(burstInterval);
      }

      StartCooldown(cooldown);
      LogAttack($"Projectile×{projectileCount} burst×{burstCount} dir={aimDir} base={request.Base:F1}");
    }

    void SpawnPlayerProjectile(
      Vector2 aimDir,
      float maxRange,
      float speed,
      float scale,
      DamageRequest request,
      AttackProfileDatabase.AttackProfile profile)
    {
      var origin = transform.position + new Vector3(aimDir.x, aimDir.y, 0f) * 0.35f;
      var dir = new Vector3(aimDir.x, aimDir.y, 0f);

      // 注入构筑效果到弹佀"
      var isSkill = string.Equals(profile.damage_source, "skill", StringComparison.OrdinalIgnoreCase);
      var projMods = new ProjectileBuildModifiers
      {
        explosionRadius = isSkill && _buildMods.skillExplosionRadius > 0f
          ? _buildMods.skillExplosionRadius
          : (!isSkill && _buildMods.auxiliaryExplosiveTier > 0 ? 0f : _buildMods.explosionRadius),
        explosionDamageRatio = isSkill && _buildMods.skillExplosionRatio > 0f
          ? _buildMods.skillExplosionRatio : _buildMods.explosionDamageRatio,
        explosionDamageMult = _buildMods.explosionDamageMult,
        chainCount = isSkill
          ? _buildMods.skillChainCount
          : (!isSkill && _buildMods.auxiliaryLightningTier > 0 ? 0 : _buildMods.chainCount),
        chainDamageRatio = isSkill ? _buildMods.skillChainDamageRatio : _buildMods.chainDamageRatio,
        chainJumpRange = _buildMods.chainJumpRange,
        chainFalloffPerJump = _buildMods.chainFalloffPerJump,
        sideShed = _buildMods.sideShed,
        trailSprayCount = isSkill ? 0 : _buildMods.trailSprayCount,
        splitOnHitCount = isSkill ? _buildMods.skillSplitOnHit : _buildMods.splitOnHitCount,
        heavyShot = !isSkill && _buildMods.heavyShot,
        projectileSpeedMult = _buildMods.projectileSpeedMult,
        homing = false,
        weakHoming = isSkill
          ? (_buildMods.skillHoming || _buildMods.weakHoming)
          : _buildMods.weakHoming
            && !string.Equals(_buildMods.weaponTheme, "ranged", System.StringComparison.Ordinal),
        homingTurnRate = isSkill && _buildMods.skillHoming
          ? (_buildMods.skillHomingTurnRate > 1f ? _buildMods.skillHomingTurnRate : 90f)
          : _buildMods.homingTurnRate,
        pierceCount = isSkill ? _buildMods.skillPierce : _buildMods.pierceCount,
        pierceNoFalloff = _buildMods.pierceNoFalloff,
        slowChance = isSkill ? _buildMods.skillSlowChance : _buildMods.slowChance,
        slowAmount = isSkill ? _buildMods.skillSlowAmount : _buildMods.slowAmount,
        burnDps = isSkill ? _buildMods.skillBurnDps : _buildMods.burnDps,
        burnDuration = isSkill ? _buildMods.skillBurnDuration : _buildMods.burnDuration,
        eliteDamageMult = _buildMods.eliteDamageMult,
        bossDamageMult = _buildMods.bossDamageMult,
        longRangeDamageMult = _buildMods.longRangeDamageMult,
        slowTargetDamageMult = _buildMods.slowTargetDamageMult,
        explosionVacuum = isSkill
          ? (_buildMods.skillVacuum || _buildMods.explosionVacuum)
          : _buildMods.explosionVacuum,
        pierceTrailFeedback = !isSkill && _buildMods.pierceTrailFeedback
      };

      ProjectileFactory.SpawnDirectional(
        origin,
        dir,
        request,
        speed,
        scale,
        projectileColor,
        maxRange,
        "PlayerProjectile",
        hitRadius: profile.hit_radius,
        buildMods: projMods,
        visualKind: ResolvePrimaryVisualKind(projMods));

#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD
      Game.Shared.Diagnostics.CombatValidationHooks.OnPlayerProjectileSpawned?.Invoke();
#endif

      if (!isSkill)
        RangedMuzzleFlashVfx.Spawn(origin, 0.92f);
    }

    void SpawnAuxiliaryVolley(
      Vector2 aimDir,
      AttackProfileDatabase.AttackProfile profile,
      DamageRequest request,
      ProjectileBuildModifiers mods,
      Color color,
      float scaleMult,
      int count,
      float spreadAngle,
      RangedProjectileVisualKind visualKind)
    {
      count = Mathf.Max(1, count);
      if (count <= 1)
      {
        SpawnAuxiliaryProjectile(aimDir, profile, request, mods, color, scaleMult, visualKind);
        return;
      }

      var totalSpread = spreadAngle > 0.01f ? spreadAngle : ComputeVolleySpreadDegrees(count) * 0.8f;
      var baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
      for (var i = 0; i < count; i++)
      {
        var t = i / (float)(count - 1) - 0.5f;
        var angle = baseAngle + t * totalSpread;
        var rad = angle * Mathf.Deg2Rad;
        SpawnAuxiliaryProjectile(
          new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)),
          profile,
          request,
          mods,
          color,
          scaleMult,
          visualKind);
      }
    }

    void SpawnAuxiliaryProjectile(
      Vector2 aimDir,
      AttackProfileDatabase.AttackProfile profile,
      DamageRequest request,
      ProjectileBuildModifiers mods,
      Color color,
      float scaleMult,
      RangedProjectileVisualKind visualKind)
    {
      const float playerProjectileSpeedMult = 1.2f;
      var speed = (profile.projectile_speed > 0f ? profile.projectile_speed : 14f) * playerProjectileSpeedMult * 0.92f;
      var scale = (profile.projectile_scale > 0f ? profile.projectile_scale : 0.2f) * 1.55f * scaleMult
        * Mathf.Max(0.2f, _buildMods.projectileSizeMult > 0f ? _buildMods.projectileSizeMult : 1f);
      var maxRange = _eqRangedRange;
      var origin = transform.position + new Vector3(aimDir.x, aimDir.y, 0f) * 0.35f;
      var dir = new Vector3(aimDir.x, aimDir.y, 0f);
      ProjectileFactory.SpawnDirectional(
        origin,
        dir,
        request,
        speed,
        scale,
        color,
        maxRange,
        "PlayerAuxiliaryProjectile",
        hitRadius: profile.hit_radius,
        buildMods: mods,
        visualKind: visualKind);
    }

    static RangedProjectileVisualKind ResolvePrimaryVisualKind(in ProjectileBuildModifiers mods)
    {
      if (mods.heavyShot)
        return RangedProjectileVisualKind.Heavy;
      if (mods.pierceCount > 0)
        return RangedProjectileVisualKind.Pierce;
      return RangedProjectileVisualKind.Primary;
    }

    static RangedProjectileVisualKind ResolveAuxiliaryVisualKind(in ProjectileBuildModifiers mods)
    {
      if (mods.chainCount > 0)
        return RangedProjectileVisualKind.Lightning;
      if (mods.explosionRadius > 0.01f)
        return RangedProjectileVisualKind.Explosive;
      return RangedProjectileVisualKind.Primary;
    }

    /// <summary>传递到弹体的构筑效果?/summary>
    public struct ProjectileBuildModifiers
    {
      public int pierceCount;
      public bool pierceNoFalloff;
      public float explosionRadius;
      public float explosionDamageRatio;
      public float explosionDamageMult;
      public int chainCount;
      public float chainDamageRatio;
      public float chainJumpRange;
      public float chainFalloffPerJump;
      public bool weakHoming;
      public float homingTurnRate;
      public bool sideShed;
      public int trailSprayCount;
      public int splitOnHitCount;
      public bool heavyShot;
      public float projectileSpeedMult;
      public int projectileDepth;
      public bool homing;
      public float slowChance;
      public float slowAmount;
      public float burnDps;
      public float burnDuration;
      public float eliteDamageMult;
      public float bossDamageMult;
      public float longRangeDamageMult;
      public float slowTargetDamageMult;
      public bool explosionVacuum;
      public int ignoreHitTargetId;
      public bool explosionSecondaryWave;
      public bool explosionFragmentBurst;
      public bool explosionChainDetonate;
      public bool explosionSaturation;
      public int lightningForkJumps;
      public bool lightningConductMark;
      public bool lightningNetwork;
      public bool pierceTrailFeedback;
    }

    void FireProjectile(AttackProfileDatabase.AttackProfile profile, Transform target)
    {
      var toTarget = GameplayPlane.PlanarDirection(transform.position, target.position);
      FireProjectileDirectional(profile, toTarget, profile.range + _eqRangeAdd);
    }

    void FireBeam(AttackProfileDatabase.AttackProfile profile, Transform target, float range, bool skillCast = false)
    {
      var halfWidth = profile.beam_half_width > 0f ? profile.beam_half_width : 0.35f;
      var request = BuildDamageRequest(profile.base_damage, profile);

      PlayerLaserBeam.Fire(
        transform.position,
        target,
        range,
        request,
        halfWidth,
        laserColor);

      if (!skillCast)
        StartCooldown(profile.cooldown);
      LogAttack($"Beam {target.name} base={profile.base_damage:F1}");
    }

    void StartCooldown(float baseCooldown)
    {
      if (attackProfileId == "skill_mage_arcane_bolt" || attackProfileId == "weapon_theme_mage")
      {
        var skillCd = Mathf.Clamp01(_buildMods.skillCooldownReduce);
        var arcaneCd = Mathf.Clamp01(_buildMods.mageArcaneCooldownReduce);
        _cooldownTimer = Mathf.Max(0.2f,
          baseCooldown * Mathf.Max(0.1f, 1f - skillCd) * Mathf.Max(0.1f, 1f - arcaneCd));
        return;
      }

      _cooldownTimer = Mathf.Max(0.2f, ComputeCooldown(baseCooldown));
    }

    float GetBuffAttackSpeed()
    {
      var buffContainer = GetComponent<BuffContainer>();
      return buffContainer != null ? buffContainer.GetStatModifier("attack_speed") : 1f;
    }

    DamageRequest BuildDamageRequest(float baseDamage, AttackProfileDatabase.AttackProfile profile)
    {
      var damageType = profile.damage_type ?? "physical";
      var source = profile.damage_source ?? "weapon";
      var scaled = baseDamage;
      var isSkill = string.Equals(source, "skill", StringComparison.OrdinalIgnoreCase);

      if (isSkill)
      {
        scaled *= _buildMods.skillDamageMult > 0f ? _buildMods.skillDamageMult : 1f;
        foreach (var provider in GetComponents<ISkillDamageMultiplierProvider>())
          scaled *= Mathf.Max(0f, provider.SkillDamageMultiplier);
      }
      if (profile.id == "skill_mage_arcane_bolt")
        scaled *= _buildMods.mageArcaneDamageMult > 0f ? _buildMods.mageArcaneDamageMult : 1f;

      var critChance = isSkill ? _buildMods.skillCritChance : _eqCritChance;
      var critDamage = isSkill
        ? (_buildMods.skillCritDamage > 0f ? 1.5f + _buildMods.skillCritDamage : 1.5f)
        : _eqCritDamage;

      var req = DamageRequest
        .Direct(scaled, damageType, source, gameObject)
        .WithAttackerBonuses(_eqAttackMult, _eqAttackFlatAdd, critChance, critDamage);
      req.AttackProfileId = profile.id;
      return req;
    }

    void OnDealDamage(Transform target, float damage)
    {
      var buffContainer = GetComponent<BuffContainer>();
      var buffLifesteal = buffContainer != null ? buffContainer.GetStatModifier("lifesteal") - 1f : 0f;
      var totalLifesteal = _eqLifesteal + buffLifesteal;

      if (totalLifesteal > 0f && _playerHealth != null)
        _playerHealth.Heal(damage * totalLifesteal);

      // 击中回复百分比最大生命值"
      var healOnHitPct = _buildMods.healOnHitPct;
      if (healOnHitPct > 0f && _playerHealth != null && !_playerHealth.IsDead)
        _playerHealth.Heal(_playerHealth.MaxHp * healOnHitPct);

      if (_equipReporter != null)
        _equipReporter.OnDealDamage(damage);
    }

    static bool TryGetHealth(Transform target, out Health health)
    {
      health = target.GetComponent<Health>();
      return health != null && !health.IsDead;
    }

    Transform FindNearestEnemy(float range)
    {
      var enemyReg = CombatRoot.EnemyRegistry;
      if (enemyReg != null)
      {
        var nearest = enemyReg.GetNearest(transform.position, range);
        return nearest != null ? nearest.transform : null;
      }

      Transform best = null;
      var bestDist = range;
      var origin = transform.position;
      var enemies = UnityEngine.Object.FindObjectsOfType<EnemyCore>();
      foreach (var enemy in enemies)
      {
        if (enemy == null)
          continue;
        var health = enemy.GetComponent<Health>();
        if (health != null && health.IsDead)
          continue;

        var dist = GameplayPlane.PlanarDistance(enemy.transform.position, origin);
        if (dist > bestDist)
          continue;

        bestDist = dist;
        best = enemy.transform;
      }

      return best;
    }

    void LogAttack(string message)
    {
      if (debugLogAttacks)
        Debug.Log($"[PlayerAttackDirector] {message}");
    }

    /// <summary>主动技能施法（冷却?PlayerActiveSkillController 管理）?/summary>
    public void CastSkillProfile(string profileId, Vector2 aimDir)
    {
      if (_playerHealth != null && _playerHealth.IsDead)
        return;

      var profile = AttackProfileDatabase.Get(profileId);
      if (profile == null)
        return;

      ExecuteSkillCast(profile, aimDir);

      var mirrorCount = _buildMods.skillMirrorCast;
      for (var m = 0; m < mirrorCount; m++)
      {
        var spread = (m + 1) * 14f * (m % 2 == 0 ? 1f : -1f);
        var mirrorDir = Rotate2D(aimDir, spread);
        ExecuteSkillCast(profile, mirrorDir);
      }

      var echoCount = _buildMods.skillEchoCount;
      if (_buildMods.skillEchoGuarantee > 0.5f)
        echoCount = Mathf.Max(echoCount, 1);
      if (echoCount > 0)
        StartCoroutine(SkillEchoRoutine(profile, aimDir, echoCount));
    }

    static Vector2 Rotate2D(Vector2 v, float degrees)
    {
      var rad = degrees * Mathf.Deg2Rad;
      var cos = Mathf.Cos(rad);
      var sin = Mathf.Sin(rad);
      return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void ExecuteSkillCast(AttackProfileDatabase.AttackProfile profile, Vector2 aimDir)
    {
      var rangeMult = _buildMods.skillRangeMult > 0f ? _buildMods.skillRangeMult : 1f;
      var range = (profile.range + _eqRangeAdd) * rangeMult;
      var delivery = string.IsNullOrEmpty(profile.delivery) ? "projectile" : profile.delivery;

      switch (delivery)
      {
        case "projectile":
          FireProjectileDirectional(profile, aimDir, range, skillCast: true);
          break;
        case "beam":
          if (TryFindMeleeTarget(aimDir, range, 18f, out var beamTarget))
            FireBeam(profile, beamTarget.transform, range, skillCast: true);
          break;
        default:
          FireProjectileDirectional(profile, aimDir, range, skillCast: true);
          break;
      }
    }

    System.Collections.IEnumerator SkillEchoRoutine(
      AttackProfileDatabase.AttackProfile profile,
      Vector2 aimDir,
      int echoCount)
    {
      var remaining = echoCount;
      while (remaining > 0)
      {
        yield return new WaitForSeconds(0.18f);
        if (_playerHealth != null && _playerHealth.IsDead)
          yield break;

        ExecuteSkillCast(profile, aimDir);
        remaining--;
      }
    }

#if UNITY_EDITOR
    public float GetGizmoRange()
    {
      var profile = AttackProfileDatabase.Get(attackProfileId);
      return (profile?.range ?? 2f) + _eqRangeAdd;
    }
#endif
  }
}
