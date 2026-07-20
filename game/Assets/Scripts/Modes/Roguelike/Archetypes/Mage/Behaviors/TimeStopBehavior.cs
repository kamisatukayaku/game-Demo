using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class TimeStopBehavior : IMageBehavior
  {
    public string Id => "time_stop";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) =>
      runner.TickTimeStop(ctx, owner);

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex)
    {
      if (ctx.SkillTimeStopChance > 0f && Random.value < ctx.SkillTimeStopChance)
        runner.TriggerTimeStop();
    }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage) { }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer) { }
  }
}
