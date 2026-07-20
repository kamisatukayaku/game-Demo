using UnityEngine;
using Game.Shared.Gameplay.Events;

namespace Game.Shared.Gameplay.Events
{
  public readonly struct DamageDealtEvent : IGameEvent
  {
    public readonly GameObject Attacker;
    public readonly GameObject Target;
    public readonly float Damage;
    public readonly string DamageSourceId;
    public readonly string DamageTypeId;
    public readonly bool WasCritical;

    public DamageDealtEvent(
      GameObject attacker,
      GameObject target,
      float damage,
      string damageSourceId,
      string damageTypeId,
      bool wasCritical)
    {
      Attacker = attacker;
      Target = target;
      Damage = damage;
      DamageSourceId = damageSourceId;
      DamageTypeId = damageTypeId;
      WasCritical = wasCritical;
    }
  }

  public readonly struct CriticalHitEvent : IGameEvent
  {
    public readonly GameObject Attacker;
    public readonly GameObject Target;
    public readonly float Damage;
    public readonly string AttackProfileId;

    public CriticalHitEvent(
      GameObject attacker,
      GameObject target,
      float damage,
      string attackProfileId)
    {
      Attacker = attacker;
      Target = target;
      Damage = damage;
      AttackProfileId = attackProfileId;
    }
  }

  public readonly struct SkillUsedEvent : IGameEvent
  {
    public readonly GameObject Caster;
    public readonly string SkillId;
    public readonly Vector3 Position;

    public SkillUsedEvent(GameObject caster, string skillId, Vector3 position)
    {
      Caster = caster;
      SkillId = skillId;
      Position = position;
    }
  }

}
