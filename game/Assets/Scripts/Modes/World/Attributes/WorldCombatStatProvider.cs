using Game.Shared.Stats;

namespace Game.World
{
  /// <summary>
  /// World 模式专用的 ICombatStatProvider 实现。
  /// 在 DamagePipeline 结算玩家受伤时提供防御值和伤害减免。
  ///
  /// 在 WorldManager.InitWorld 中注册到 CombatStatProviderLocator，
  /// 在 Shutdown 中注销。
  /// </summary>
  public class WorldCombatStatProvider : ICombatStatProvider
  {
    /// <summary>玩家防御值（flat subtraction）：从 AttributeManager 读取 "defense" 和 "defense_mult" 的最终值。</summary>
    public float Defense
    {
      get
      {
        if (!WorldRuntimeContext.IsWorldModeActive) return 0f;
        var attr = WorldManager.Instance?.Attributes;
        if (attr == null) return 0f;
        // defense 已是最终值（defense_mult 在 AttributeManager 中作用于 defense 的乘法链）
        return attr.GetValue("defense");
      }
    }

    /// <summary>百分比伤害减免：从 AttributeManager 读取 "damage_reduction"。</summary>
    public float DamageReduction
    {
      get
      {
        if (!WorldRuntimeContext.IsWorldModeActive) return 0f;
        var attr = WorldManager.Instance?.Attributes;
        if (attr == null) return 0f;
        return attr.GetValue("damage_reduction");
      }
    }

    /// <summary>全抗性：World 模式暂不使用。</summary>
    public float AllResist => 0f;

    /// <summary>过量治疗转护盾比例：World 模式暂不使用。</summary>
    public float OverhealShield => 0f;

    /// <summary>暴击率：由 WorldPlayerAttackBridge 直连 Director，不使用此接口。</summary>
    public float CritChance => 0f;

    /// <summary>暴击伤害倍率：由 WorldPlayerAttackBridge 直连 Director，不使用此接口。</summary>
    public float CritDamageMult => 1.5f;

    /// <summary>吸血：由 WorldPlayerAttackBridge 直连 Director，不使用此接口。</summary>
    public float Lifesteal => 0f;

    /// <summary>击中回复百分比：World 模式暂不使用。</summary>
    public float HealOnHitPct => 0f;

    /// <summary>击退抗性：World 模式暂不使用。</summary>
    public float KnockbackResist => 0f;

    /// <summary>反弹伤害换算：World 模式暂不使用。</summary>
    public float ComputeReflectDamage(float baseDamage) => baseDamage;
  }
}
