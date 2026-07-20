using UnityEngine;

using Game.Shared.Gameplay.Bridges;
using Game.Shared.Combat.Buff;
using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class ElementOverloadBehavior : IMageBehavior
  {
    public string Id => "element_overload";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) { }

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex) { }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage)
    {
      if (ctx.SkillElementOverload <= 0.5f)
        return;

      var buffs = target.GetComponent<BuffContainer>();
      if (buffs == null || !buffs.HasBuff("buff_burn"))
        return;

      if (buffs.HasSlowEffect() && ctx.SkillElementMelt > 0.5f)
        return;

      var jumps = ctx.SkillChainCount > 0 ? ctx.SkillChainCount : 3;
      runner.ChainFromTarget(attacker, target, damage * 0.65f, jumps);
      CombatDebugHookLocator.Mage("element_overload", "Element Overload");
    }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer) { }
  }
}
