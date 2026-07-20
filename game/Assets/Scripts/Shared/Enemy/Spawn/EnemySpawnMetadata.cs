using UnityEngine;

namespace Game.Shared.Enemy.Spawn
{
  /// <summary>生成时写入的敌人元数据（passive_buffs / on_death）?/summary>
  [DisallowMultipleComponent]
  public class EnemySpawnMetadata : MonoBehaviour
  {
    public string enemyId;
    public string[] passiveBuffs;
    public string[] onDeathEffects;
    public string[] onHitBuffs;

    public static bool IsBossId(string id)
    {
      return !string.IsNullOrEmpty(id)
        && id.IndexOf("boss", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsBossEnemy(GameObject enemy)
    {
      if (enemy == null)
        return false;

      var metadata = enemy.GetComponent<EnemySpawnMetadata>();
      return IsBossId(metadata != null ? metadata.enemyId : null)
        || IsBossId(enemy.name);
    }

    public void Configure(EnemySpawner.EnemyDef def)
    {
      if (def == null)
        return;

      enemyId = def.id;
      passiveBuffs = def.passive_buffs;
      onDeathEffects = def.on_death;
      onHitBuffs = def.on_hit_buffs;
    }
  }
}
