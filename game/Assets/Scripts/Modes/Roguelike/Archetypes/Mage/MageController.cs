using UnityEngine;

using Game.Modes.Roguelike.Gameplay.Events;
using Game.Modes.Roguelike.Build.Apply;
using Game.Shared.Combat.Events;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Gameplay.Events;
using Game.Shared.Projectile;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  [DisallowMultipleComponent]
  public sealed class MageController : MonoBehaviour
  {
    readonly MageEffectRunner _runner = new();
    PlayerActiveSkillController _skills;
    SkillContext Ctx => MageSystemLocator.Context;

    public static MageController Ensure(GameObject player)
    {
      if (player == null)
        return null;
      var c = player.GetComponent<MageController>();
      return c != null ? c : player.AddComponent<MageController>();
    }

    void Awake()
    {
      _skills = GetComponent<PlayerActiveSkillController>();
      _runner.Bind(gameObject, _skills);
    }

    void OnEnable()
    {
      CombatEventBus.PostDamage += OnPostDamage;
      CombatEventBus.OnKill += OnKill;
      ActiveProjectileRegistry.Spawned += OnProjectileSpawned;
    }

    void OnDisable()
    {
      CombatEventBus.PostDamage -= OnPostDamage;
      CombatEventBus.OnKill -= OnKill;
      ActiveProjectileRegistry.Spawned -= OnProjectileSpawned;
    }

    void Update()
    {
      if (!Ctx.IsMageTheme)
        return;

      _runner.Tick(Ctx, transform, Time.deltaTime);
    }

    public void OnSkillCast(int slotIndex)
    {
      if (!Ctx.IsMageTheme)
        return;

      _runner.OnSkillCast(Ctx, transform, slotIndex);
    }

    void OnProjectileSpawned(StraightProjectile projectile, DamageRequest request)
    {
      if (!Ctx.IsMageTheme || projectile == null)
        return;

      if (request.AttackProfileId != "skill_mage_arcane_bolt")
        return;

      GameEventBus.Publish(new TriggerActivatedEvent(
        "MageArcaneMissileAttach",
        projectile.transform.position,
        projectile.gameObject,
        Mathf.Max(0.2f, projectile.transform.localScale.x)));
    }

    void OnPostDamage(in CombatEventBus.PostDamageArgs args)
    {
      if (!Ctx.IsMageTheme || args.Result.FinalDamage <= 0f || args.Target == null)
        return;

      if ((args.Request.DamageSourceId ?? "") != "skill")
        return;

      if (args.Request.AttackProfileId == "skill_mage_arcane_bolt")
        GameEventBus.Publish(new TriggerActivatedEvent(
          "MageArcaneMissileHit",
          args.Target.transform.position,
          args.Attacker,
          1f,
          alternate: args.Result.WasCritical));

      _runner.OnPostSkillDamage(Ctx, args.Attacker, args.Target, args.Result.FinalDamage);
    }

    void OnKill(CombatEventBus.KillArgs args)
    {
      if (!Ctx.IsMageTheme || args.Killer != gameObject)
        return;

      _runner.OnKill(Ctx, args.Killer);
    }

    public void SandboxTriggerTimeStop() => _runner.TriggerTimeStop();

    public void SandboxTriggerCooldownReset() => _runner.ResetAllCooldowns();

    public void SandboxSimulateSkillHit(GameObject target, float damage) =>
      _runner.OnPostSkillDamage(Ctx, gameObject, target, damage);
  }
}
