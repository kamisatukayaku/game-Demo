using UnityEngine;

using Game.Shared.Gameplay.Bridges;
using Game.Shared.Combat.Buff;
using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class ElementBurstBehavior : IMageBehavior
  {
    public string Id => "element_burst";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) { }

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex) { }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage)
    {
      if (ctx.SkillElementBurst <= 0.5f)
        return;

      var buffs = target.GetComponent<BuffContainer>();
      if (buffs == null || !buffs.HasBuff("buff_burn") || !buffs.HasSlowEffect())
        return;

      var radius = 2.2f * (1f + ctx.SkillBurstRadiusBonus);
      runner.ApplyAreaSkillDamage(attacker, target.transform, radius, damage * 1.8f, spawnExplosion: true);
      CombatDebugHookLocator.Mage("element_burst", "Element Burst");
    }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer) { }
  }
}
