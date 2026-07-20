using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Presentation.VFX;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Health;
using Game.Shared.Core;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Gameplay.Events;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  [DisallowMultipleComponent]
  public sealed class MonsterEcosystemAgent : MonoBehaviour
  {
    MonsterRoleProfile _profile;
    EliteAffixProfile _eliteAffix;
    WaveDirector _director;
    WaveSpawnScaling _scaling;
    Health _health;
    float _timer;
    float _eliteTimer;
    float _spawnGrace = 1.05f;
    bool _charging;
    int _generation;

    public string Role => _profile?.role ?? "chaser";
    public bool IsElite => _eliteAffix != null;

    public void Configure(
      MonsterRoleProfile profile,
      WaveDirector director,
      WaveSpawnScaling scaling,
      EliteAffixProfile eliteAffix = null,
      int generation = 0)
    {
      _profile = profile;
      _director = director;
      _scaling = scaling;
      _eliteAffix = eliteAffix;
      _generation = generation;
      _health = GetComponent<Health>();
      if (_health != null)
        _health.Died += OnDeath;

      var movement = GetComponent<EnemyMovement>();
      if (movement != null)
      {
        movement.SetKnockbackMultiplier(Mathf.Clamp01(profile?.knockback_multiplier ?? 1f));
        if (eliteAffix != null && eliteAffix.speed_mult > 1f)
          movement.SetSprintState(true, eliteAffix.speed_mult);
      }

      ApplyEliteStats();
      Publish("MonsterRole_" + Role, Mathf.Max(0.8f, profile?.effect_radius ?? 1f), IsElite ? 1f : 0f);
    }

    void OnDestroy()
    {
      if (_health != null)
        _health.Died -= OnDeath;
    }

    void Update()
    {
      if (_profile == null || _health == null || _health.IsDead)
        return;
      if (_spawnGrace > 0f)
      {
        _spawnGrace -= Time.deltaTime;
        return;
      }

      _timer -= Time.deltaTime;
      switch (_profile.role)
      {
        case "supporter":
          if (_timer <= 0f) SupportPulse();
          break;
        case "bomber":
          TickBomber();
          break;
        case "disruptor":
          if (_timer <= 0f) DisruptPulse();
          break;
      }

      _eliteTimer -= Time.deltaTime;
      if (_eliteAffix != null && _eliteAffix.gravity_radius > 0f && _eliteTimer <= 0f)
        GravityPulse();
    }

    void SupportPulse()
    {
      _timer = Mathf.Max(0.25f, _profile.interval);
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return;

      var synergyActive = IsSynergyWave();
      foreach (var ally in registry.GetInRange(GameplayPlane.Position2D(transform), _profile.effect_radius))
      {
        if (ally == null || ally.gameObject == gameObject) continue;
        var health = ally.GetComponent<Health>();
        health?.Heal(Mathf.Max(1f, health.MaxHp * _profile.heal_percent));
        ally.GetComponent<BuffContainer>()?.ApplyBuff("buff_overheal_shield", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          sourceKind = "monster_support",
          abilityId = "support_pulse",
          stacks = 1,
          customShieldAmount = _profile.shield_amount,
          customDuration = Mathf.Max(0.5f, _profile.interval + 0.2f)
        });

        if (synergyActive)
        {
          var allyAgent = ally.GetComponent<MonsterEcosystemAgent>();
          if (allyAgent != null && allyAgent.Role == "shooter")
            MonsterSynergyTintVfx.Play(ally.gameObject, 2f);
        }
      }

      var pulseDuration = synergyActive ? 2f : 0f;
      Publish("MonsterSupportPulse", _profile.effect_radius, pulseDuration);
    }

    bool IsSynergyWave()
    {
      var director = _director ?? WaveDirector.Instance;
      return director != null && director.CurrentWave >= 8;
    }

    void TickBomber()
    {
      var player = FindPlayer();
      if (player == null) return;
      var distance = GameplayPlane.PlanarDistance(transform.position, player.transform.position);
      if (!_charging && distance <= _profile.trigger_radius)
      {
        _charging = true;
        _timer = Mathf.Max(0.35f, _profile.windup);
        var movement = GetComponent<EnemyMovement>();
        if (movement != null)
        {
          movement.ResetVelocity();
          movement.enabled = false;
        }
        var core = GetComponent<EnemyCore>();
        if (core != null)
          core.enabled = false;
        Publish("MonsterBomberCharge", _profile.trigger_radius, _timer);
      }
      else if (_charging && _timer <= 0f)
      {
        Explode(player);
      }
    }

    void Explode(GameObject player)
    {
      if (_health == null || _health.IsDead) return;
      var radius = Mathf.Max(0.5f, _profile.effect_radius);
      if (GameplayPlane.PlanarDistance(transform.position, player.transform.position) <= radius)
      {
        var playerHealth = player.GetComponent<Health>();
        if (playerHealth != null)
          DamagePipeline.Apply(
            DamageRequest.Direct(_profile.ability_damage * _scaling.damageMult, "energy", "monster_bomber", gameObject),
            playerHealth);
      }
      Publish("MonsterBomberExplode", radius, _profile.ability_damage);
      _health.TakeDamage(_health.CurrentHp + 1f);
    }

    void DisruptPulse()
    {
      _timer = Mathf.Max(0.35f, _profile.interval);
      var player = FindPlayer();
      if (player == null || GameplayPlane.PlanarDistance(transform.position, player.transform.position) > _profile.effect_radius)
        return;
      var buffs = player.GetComponent<BuffContainer>();
      buffs?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
      {
        sourceEntity = gameObject,
        sourceKind = "monster_disruptor",
        abilityId = "disruption_field",
        stacks = 1,
        customSlowAmount = 0.22f,
        customDuration = _profile.interval + 0.35f
      });
      buffs?.ApplyBuff("buff_aura_weaken", new BuffContainer.BuffApplyContext
      {
        sourceEntity = gameObject,
        sourceKind = "monster_disruptor",
        abilityId = "disruption_field",
        stacks = 1,
        customDuration = _profile.interval + 0.35f
      });
      Publish("MonsterDisruptorPulse", _profile.effect_radius);
    }

    void GravityPulse()
    {
      _eliteTimer = 0.55f;
      var player = FindPlayer();
      if (player == null || GameplayPlane.PlanarDistance(transform.position, player.transform.position) > _eliteAffix.gravity_radius)
        return;
      player.GetComponent<BuffContainer>()?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
      {
        sourceEntity = gameObject,
        sourceKind = "elite_gravity",
        abilityId = "elite_gravity",
        stacks = 1,
        customSlowAmount = 0.16f,
        customDuration = 0.7f
      });
      Publish("MonsterEliteGravity", _eliteAffix.gravity_radius);
    }

    void OnDeath()
    {
      if (_profile != null && !string.IsNullOrEmpty(_profile.split_child_id)
          && _generation < _profile.split_generations)
        SpawnChildren(_profile.split_child_id, Mathf.Max(2, _profile.split_count));

      if (_eliteAffix != null && _eliteAffix.split_count > 0)
        SpawnChildren(_profile?.split_child_id ?? "mob_square_01", _eliteAffix.split_count);

      if (_eliteAffix != null && _eliteAffix.death_explosion_radius > 0f)
        EliteDeathExplosion();
    }

    void SpawnChildren(string enemyId, int count)
    {
      if (_director == null || string.IsNullOrEmpty(enemyId)) return;
      for (var i = 0; i < count; i++)
      {
        var angle = Mathf.PI * 2f * i / count;
        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.65f;
        _director.SpawnEcologyEnemy(enemyId, GameplayPlane.Position2D(transform) + offset, _scaling, _generation + 1);
      }
    }

    void EliteDeathExplosion()
    {
      var player = FindPlayer();
      if (player != null && GameplayPlane.PlanarDistance(transform.position, player.transform.position) <= _eliteAffix.death_explosion_radius)
      {
        var health = player.GetComponent<Health>();
        if (health != null)
          DamagePipeline.Apply(DamageRequest.Direct(
            _eliteAffix.death_explosion_damage * _scaling.damageMult,
            "energy", "elite_death_explosion", gameObject), health);
      }
      Publish("MonsterEliteExplosion", _eliteAffix.death_explosion_radius, _eliteAffix.death_explosion_damage);
    }

    void ApplyEliteStats()
    {
      if (_eliteAffix == null || _health == null) return;
      if (_eliteAffix.hp_mult > 1f)
        _health.Configure(_health.MaxHp * _eliteAffix.hp_mult);
      if (_eliteAffix.scale_mult > 1f)
        transform.localScale *= _eliteAffix.scale_mult;
      if (_eliteAffix.shield_amount > 0f)
        GetComponent<BuffContainer>()?.ApplyBuff("buff_overheal_shield", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          sourceKind = "elite",
          abilityId = _eliteAffix.id,
          stacks = 1,
          customShieldAmount = _eliteAffix.shield_amount * _scaling.hpMult,
          customDuration = 999f
        });
      Publish("MonsterElite", 1.4f, 0f, true);
    }

    static GameObject FindPlayer()
    {
      var player = GameObject.FindGameObjectWithTag("Player");
      return player != null ? player : GameObject.Find("Player");
    }

    void Publish(string id, float scale = 1f, float value = 0f, bool alternate = false) =>
      GameEventBus.Publish(new TriggerActivatedEvent(id, transform.position, gameObject, scale, value, alternate));
  }
}
