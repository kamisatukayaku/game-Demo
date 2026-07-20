using UnityEngine;

using Game.Shared.Gameplay.Bridges;
using Game.Shared.Combat.Buff;
using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class ElementMeltBehavior : IMageBehavior
  {
    public string Id => "element_melt";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) { }

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex) { }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage)
    {
      if (ctx.SkillElementMelt <= 0.5f)
        return;

      var buffs = target.GetComponent<BuffContainer>();
      if (!buffs.HasSlowEffect() || !buffs.HasBuff("buff_burn"))
        return;

      if (ctx.SkillElementBurst > 0.5f)
        return;

      runner.ApplyAreaSkillDamage(attacker, target.transform, 1.6f, damage * 0.9f, spawnExplosion: true);
      CombatDebugHookLocator.Mage("element_melt", "Element Melt");
    }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer) { }
  }
}
