using System;

namespace Game.World
{
  /// <summary>
  /// 营地怪物快照 — 存储类型和血量比例（0~1）。
  /// 恢复生成时按当前营地/世界等级的属性重新计算 MaxHp，再乘以比例得到 CurrentHp。
  /// </summary>
  [Serializable]
  public struct CampEnemyRecord
  {
    /// <summary>敌人 archetype ID（对应 enemies.json 中的 id 字段）</summary>
    public string EnemyId;

    /// <summary>被存储时的血量比例（CurrentHp / MaxHp，0~1）。-1 表示满血。</summary>
    public float HpRatio;

    public CampEnemyRecord(string enemyId, float hpRatio)
    {
      EnemyId = enemyId;
      HpRatio = hpRatio;
    }
  }
}
