using UnityEngine;

using Game.Shared.Combat.Buff;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Modes.Roguelike.Archetypes.Mage;

namespace Game.Modes.Roguelike.Archetypes.Mage.Behaviors
{
  sealed class TimeDilationFieldBehavior : IMageBehavior
  {
    public string Id => "time_dilation_field";

    public void Tick(MageEffectRunner runner, SkillContext ctx, Transform owner, float deltaTime) =>
      runner.TickTimeDilationField(ctx, owner);

    public void OnSkillCast(MageEffectRunner runner, SkillContext ctx, Transform owner, int slotIndex) { }

    public void OnPostSkillDamage(MageEffectRunner runner, SkillContext ctx, GameObject attacker, GameObject target, float damage) { }

    public void OnKill(MageEffectRunner runner, SkillContext ctx, GameObject killer) { }
  }
}
