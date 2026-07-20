using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class CooldownResetBehavior : IMageBehavior
  {
    public string Id => "cooldown_reset";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) { }

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex)
    {
      if (ctx.SkillCdResetChance > 0f && Random.value < ctx.SkillCdResetChance)
        runner.ResetCooldown(slotIndex);
    }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage)
    {
      if (attacker != runner.Owner || ctx.SkillTimeRewind <= 0.5f || Random.value >= 0.08f)
        return;

      runner.ResetAllCooldowns();
    }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer)
    {
      if (ctx.SkillCdResetChance > 0f && Random.value < ctx.SkillCdResetChance * 0.35f)
        runner.ResetAllCooldowns();
    }
  }
}
