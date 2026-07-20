using Health = global::Game.Shared.Combat.Health.Health;
using UnityEngine;
using Game.Shared.Enemy.Spawn;

namespace Game.Shared.Enemy.Death
{
  /// <summary>
  /// 怪物死亡：on_death 效果 + 模式层掉落处理?
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(Health))]
  public class EnemyDeathHandler : MonoBehaviour
  {
    [Header("Loot")]
    [SerializeField] string lootTableId = "common_mob";
    [SerializeField] float deathDelay = 0.15f;

    Health _health;

    public string LootTableId
    {
      get => lootTableId;
      set => lootTableId = value ?? "common_mob";
    }

    void Awake()
    {
      _health = GetComponent<Health>();
      _health.Died += OnDied;
    }

    void OnDestroy()
    {
      if (_health != null)
        _health.Died -= OnDied;
    }

    void OnDied()
    {
      var pos = transform.position;
      var metadata = GetComponent<EnemySpawnMetadata>();

      if (metadata?.onDeathEffects != null)
        EnemyDeathEffects.Execute(metadata.onDeathEffects, pos, gameObject);

      Destroy(gameObject, deathDelay);
    }
  }
}
