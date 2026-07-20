using UnityEngine;

using Game.Shared.Combat.Damage;

namespace Game.Modes.Roguelike.Archetypes.Mage
{
  public interface ISkillSystem
  {
    SkillContext Context { get; }
    float GetSkillDamageMult();
    DamageRequest BuildSkillDamageRequest(float baseDamage, GameObject caster);
  }

  public static class SkillSystemLocator
  {
    static ISkillSystem s_system = NullSkillSystem.Instance;

    public static ISkillSystem System => s_system;

    public static void Register(ISkillSystem system) =>
      s_system = system ?? NullSkillSystem.Instance;

    public static void Clear() => s_system = NullSkillSystem.Instance;
  }

  sealed class NullSkillSystem : ISkillSystem
  {
    public static readonly NullSkillSystem Instance = new();
    public SkillContext Context => default;
    public float GetSkillDamageMult() => 1f;
    public DamageRequest BuildSkillDamageRequest(float baseDamage, GameObject caster) =>
      DamageRequest.Direct(baseDamage, "energy", "skill", caster);
  }
}
