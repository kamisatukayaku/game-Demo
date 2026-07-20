using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Archetypes.Ranged;
using Game.Shared.Combat.Buff;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Player;
using Game.Shared.Vfx;

namespace Game.DevTools.Sandbox
{
  /// <summary>开发者触发器：直接调用真?Archetype Runtime，不复制技能逻辑?/summary>
  public static class SandboxTriggerService
  {
    public static void Trigger(string triggerId, SandboxSceneController scene)
    {
      if (scene?.Player == null)
        return;

      switch (triggerId)
      {
        case "time_stop": TriggerMage(scene, "time_stop"); break;
        case "element_burst": TriggerMageElement(scene, "element_burst"); break;
        case "element_melt": TriggerMageElement(scene, "element_melt"); break;
        case "element_overload": TriggerMageElement(scene, "element_overload"); break;
        case "cooldown_reset": TriggerMage(scene, "cooldown_reset"); break;
        case "gravity_well": TriggerGravityWell(scene); break;
        case "pierce":
        case "split":
        case "chain":
        case "homing":
        case "explosion":
        case "attack":
          TriggerRangedAttack(scene, triggerId);
          break;
        default:
          CombatDebugBus.Emit("sandbox", triggerId, $"Unknown trigger: {triggerId}");
          break;
      }
    }

    static void TriggerMage(SandboxSceneController scene, string featureId)
    {
      var ctrl = MageController.Ensure(scene.Player);
      if (featureId == "time_stop")
        ctrl.SandboxTriggerTimeStop();
      else if (featureId == "cooldown_reset")
        ctrl.SandboxTriggerCooldownReset();

      SandboxRuntimeHooks.Mage(featureId, featureId);
    }

    static void TriggerMageElement(SandboxSceneController scene, string featureId)
    {
      var enemy = EnsureDummy(scene, Vector2.right * 3f);
      if (enemy == null)
        return;

      var buffs = enemy.GetComponent<BuffContainer>();
      buffs?.ApplyBuff("buff_burn", new BuffContainer.BuffApplyContext
      {
        sourceEntity = scene.Player,
        sourceKind = "skill",
        abilityId = "sandbox",
        stacks = 1,
        customDps = 4f,
        customDuration = 3f
      });
      buffs?.ApplyBuff("buff_slow_debuff", new BuffContainer.BuffApplyContext
      {
        sourceEntity = scene.Player,
        sourceKind = "skill",
        abilityId = "sandbox",
        stacks = 1,
        customSlowAmount = 0.4f,
        customDuration = 3f
      });

      MageController.Ensure(scene.Player).SandboxSimulateSkillHit(enemy, 12f);
      SandboxRuntimeHooks.Mage(featureId, featureId);
    }

    static void TriggerGravityWell(SandboxSceneController scene)
    {
      EnsureDummy(scene, Vector2.right * 4f);
      SkillGravityWellZone.Spawn(scene.Player.transform, Vector2.right, 2.5f, 2f, 10f);
      SandboxRuntimeHooks.Mage("gravity_well", "Gravity well spawned");
    }

    static void TriggerRangedAttack(SandboxSceneController scene, string featureId)
    {
      EnsureDummy(scene, Vector2.right * 6f);
      var director = scene.Player.GetComponent<PlayerAttackDirector>();
      director?.SandboxExecuteAttack(Vector2.right);
      SandboxRuntimeHooks.Range(featureId, $"Ranged {featureId}");
    }

    static GameObject EnsureDummy(SandboxSceneController scene, Vector2 offset)
    {
      if (scene.Spawner.Spawned.Count == 0)
        return scene.Spawner.SpawnDummy(offset);

      return scene.Spawner.Spawned[scene.Spawner.Spawned.Count - 1];
    }
  }
}
