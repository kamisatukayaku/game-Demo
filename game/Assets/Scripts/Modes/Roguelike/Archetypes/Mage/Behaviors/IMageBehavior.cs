using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  public interface IMageBehavior
  {
    string Id { get; }

    void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime);

    void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex);

    void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage);

    void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer);
  }
}
