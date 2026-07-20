using System;
using UnityEngine;

namespace Game.Shared.Combat.Damage
{
  /// <summary>
  /// 目标侧护甲与伤害类型抗性（步骤 3?）?
  /// </summary>
  [DisallowMultipleComponent]
  public class DamageReceiver : MonoBehaviour
  {
    [Serializable]
    public struct TypeResistance
    {
      public string damageTypeId;
      [Tooltip("1 = 正常?.8 = 减伤 20%?.2 = 易伤 20%")]
      public float multiplier;
      public float armor;
    }

    [SerializeField] TypeResistance[] resistances =
    {
      new() { damageTypeId = "physical", multiplier = 1f, armor = 0f },
      new() { damageTypeId = "kinetic", multiplier = 1f, armor = 0f },
      new() { damageTypeId = "energy", multiplier = 1f, armor = 0f },
      new() { damageTypeId = "impact", multiplier = 1f, armor = 0f },
      new() { damageTypeId = "true", multiplier = 1f, armor = 0f }
    };

    public static DamageReceiver Ensure(GameObject root)
    {
      if (root == null)
        return null;

      var receiver = root.GetComponent<DamageReceiver>();
      if (receiver == null)
        receiver = root.AddComponent<DamageReceiver>();

      return receiver;
    }

    public float GetResistanceMultiplier(string damageTypeId)
    {
      if (string.IsNullOrEmpty(damageTypeId))
        return 1f;

      foreach (var entry in resistances)
      {
        if (entry.damageTypeId == damageTypeId)
          return Mathf.Max(0f, entry.multiplier);
      }

      return 1f;
    }

    public float GetArmor(string damageTypeId)
    {
      if (string.IsNullOrEmpty(damageTypeId))
        return 0f;

      foreach (var entry in resistances)
      {
        if (entry.damageTypeId == damageTypeId)
          return Mathf.Max(0f, entry.armor);
      }

      return 0f;
    }

    public float ApplyArmorReduction(float damage, string damageTypeId, float armorBonus = 0f)
    {
      var armor = GetArmor(damageTypeId) + armorBonus;
      if (armor <= 0f)
        return damage;

      // 比例减伤：armor / (armor + 50)
      var reduction = armor / (armor + 50f);
      return damage * (1f - Mathf.Clamp01(reduction));
    }
  }
}